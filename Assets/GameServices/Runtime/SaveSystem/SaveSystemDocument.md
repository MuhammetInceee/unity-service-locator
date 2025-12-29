# Save System - Technical Documentation

## Table of Contents

1. [Overview](#overview)
2. [Architecture](#architecture)
3. [API Reference](#api-reference)
4. [Usage Examples](#usage-examples)
5. [Buffer Mechanism](#buffer-mechanism)
6. [Serialization Strategy](#serialization-strategy)
7. [Best Practices](#best-practices)
8. [Extending the System](#extending-the-system)

---

## Overview

The Save System provides a robust persistence layer for Unity applications, abstracting the underlying storage mechanism behind a clean, type-safe interface. The default implementation leverages Unity's `PlayerPrefs` as its backend while introducing a **write-through buffer strategy** to minimize expensive disk I/O operations.

### Key Features

| Feature | Description |
|---------|-------------|
| **Buffered Writes** | Data is held in memory until explicitly flushed to disk |
| **Type-Safe API** | Generic methods ensure compile-time type safety |
| **Automatic Persistence** | Auto-flush on application pause and quit events |
| **Lazy Deletion** | Delete operations are buffered alongside writes |
| **Primitive Optimization** | Fast-path serialization for int, float, bool, and string |
| **JSON Fallback** | Complex objects serialize via Unity's JsonUtility |

### The Case for Buffering

```
WITHOUT BUFFER:                    WITH BUFFER:
                                   
Save("a", 1)  → Disk Write         Save("a", 1)  → Memory
Save("b", 2)  → Disk Write         Save("b", 2)  → Memory
Save("c", 3)  → Disk Write         Save("c", 3)  → Memory
                                   Flush()       → Single Disk Write
─────────────────────────          ─────────────────────────
3 Disk I/O Operations (SLOW)       1 Disk I/O Operation (FAST)
```

This approach is particularly beneficial in scenarios where multiple values are saved in quick succession, such as end-of-level persistence or batch settings updates.

---

## Architecture

### Class Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                         ISaveService                            │
│                        <<interface>>                            │
├─────────────────────────────────────────────────────────────────┤
│ + Save<T>(key: string, value: T): void                          │
│ + Load<T>(key: string, defaultValue: T): T                      │
│ + Load<T>(key: string): T                                       │
│ + HasKey(key: string): bool                                     │
│ + DeleteKey(key: string): void                                  │
│ + DeleteAll(): void                                             │
│ + Flush(): void                                                 │
├─────────────────────────────────────────────────────────────────┤
│ <<extends>> IService                                            │
│ + Initialize(): void                                            │
│ + Dispose(): void                                               │
│ + OnApplicationPause(): void                                    │
│ + OnApplicationQuit(): void                                     │
└─────────────────────────────────────────────────────────────────┘
                              ▲
                              │ implements
                              │
┌─────────────────────────────────────────────────────────────────┐
│                         SaveService                             │
├─────────────────────────────────────────────────────────────────┤
│ - _buffer: Dictionary<string, string>                           │
│ - _deletedKeys: HashSet<string>                                 │
│ - _isDirty: bool                                                │
├─────────────────────────────────────────────────────────────────┤
│ - SerializeValue<T>(value: T): string                           │
│ - DeserializeValue<T>(data: string, fallback: T): T             │
└─────────────────────────────────────────────────────────────────┘
                              │
                              │ delegates to
                              ▼
                    ┌─────────────────┐
                    │   PlayerPrefs   │
                    │   (Unity API)   │
                    └─────────────────┘
```

### Internal State Management

The service maintains three pieces of internal state:

| Field | Type | Purpose |
|-------|------|---------|
| `_buffer` | `Dictionary<string, string>` | Holds serialized values pending write |
| `_deletedKeys` | `HashSet<string>` | Tracks keys marked for deletion |
| `_isDirty` | `bool` | Indicates whether uncommitted changes exist |

### State Transitions

```
┌──────────────────────────────────────────────────────────────────┐
│                     SaveService State Machine                    │
├──────────────────────────────────────────────────────────────────┤
│                                                                  │
│    ┌─────────┐     Save() / DeleteKey()        ┌─────────┐       │
│    │  CLEAN  │ ──────────────────────────────  │  DIRTY  │       │
│    │         │                                 │         │       │
│    │_isDirty │                                 │_isDirty │       │
│    │= false  │                                 │= true   │       │
│    └─────────┘                                 └─────────┘       │
│         ▲                                           │            │
│         │                                           │            │
│         │                Flush()                    │            │
│         └───────────────────────────────────────────┘            │
│                                                                  │
│    Flush() executes:                                             │
│    1. Apply pending deletions via PlayerPrefs.DeleteKey()        │
│    2. Persist buffer entries via PlayerPrefs.SetString()         │
│    3. Commit to disk via PlayerPrefs.Save()                      │
│    4. Reset _isDirty to false                                    │
│                                                                  │
└──────────────────────────────────────────────────────────────────┘
```

---

## API Reference

### Save\<T\>

Serializes and stores a value in the in-memory write buffer.

```csharp
void Save<T>(string key, T value)
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `key` | `string` | Unique identifier for the data entry (must not be null or empty) |
| `value` | `T` | The value to serialize and store |

**Behavior:**
- The value is serialized to a string representation and added to `_buffer`
- If the key already exists, the previous value is overwritten
- If the key was previously marked for deletion, it is removed from `_deletedKeys`
- The `_isDirty` flag is set to `true`
- No disk I/O occurs until `Flush()` is invoked

**Example:**
```csharp
saveService.Save("player_name", "Marcus");
saveService.Save("player_score", 15000);
saveService.Save("player_stats", new PlayerStats { health = 100, armor = 50 });
```

---

### Load\<T\> (with default)

Retrieves a value associated with the specified key, returning a fallback if not found.

```csharp
T Load<T>(string key, T defaultValue)
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `key` | `string` | The unique identifier to look up |
| `defaultValue` | `T` | The value to return if the key does not exist |
| **Returns** | `T` | The deserialized value or the provided default |

**Resolution Order:**
```
1. If key ∈ _deletedKeys        → return defaultValue
2. If key ∈ _buffer             → return Deserialize(_buffer[key])
3. If PlayerPrefs.HasKey(key)   → return Deserialize(PlayerPrefs.GetString(key))
4. Otherwise                    → return defaultValue
```

**Example:**
```csharp
string name = saveService.Load("player_name", "Guest");
int score = saveService.Load("player_score", 0);
PlayerStats stats = saveService.Load("player_stats", PlayerStats.Default);
```

---

### Load\<T\> (without default)

Retrieves a value using the type's intrinsic default as the fallback.

```csharp
T Load<T>(string key)
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `key` | `string` | The unique identifier to look up |
| **Returns** | `T` | The deserialized value or `default(T)` |

**Example:**
```csharp
int score = saveService.Load<int>("score");           // Returns 0 if not found
string name = saveService.Load<string>("name");       // Returns null if not found
bool tutorial = saveService.Load<bool>("completed");  // Returns false if not found
```

---

### HasKey

Determines whether a key exists in the persistence layer.

```csharp
bool HasKey(string key)
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `key` | `string` | The unique identifier to check |
| **Returns** | `bool` | `true` if the key exists; otherwise, `false` |

**Resolution Logic:**
```csharp
if (key ∈ _deletedKeys)          return false;  // Pending deletion
if (key ∈ _buffer)               return true;   // In write buffer
if (PlayerPrefs.HasKey(key))     return true;   // On disk
return false;                                    // Does not exist
```

**Example:**
```csharp
if (!saveService.HasKey("onboarding_complete"))
{
    ShowOnboardingFlow();
}
```

---

### DeleteKey

Marks a key for deletion in the next flush cycle.

```csharp
void DeleteKey(string key)
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `key` | `string` | The unique identifier to delete |

**Behavior:**
- The key is removed from `_buffer` if present
- The key is added to `_deletedKeys`
- The `_isDirty` flag is set to `true`
- The key is not removed from disk until `Flush()` is invoked

**Example:**
```csharp
saveService.DeleteKey("cached_response");
saveService.Flush();  // Now physically deleted
```

---

### DeleteAll

Immediately purges all persisted data.

```csharp
void DeleteAll()
```

**Behavior:**
- Clears `_buffer`
- Clears `_deletedKeys`
- Invokes `PlayerPrefs.DeleteAll()`
- Invokes `PlayerPrefs.Save()` (synchronous disk write)
- Sets `_isDirty` to `false`

> ⚠️ **Warning:** This operation is destructive and irreversible.

**Example:**
```csharp
public void FactoryReset()
{
    saveService.DeleteAll();
    SceneManager.LoadScene("MainMenu");
}
```

---

### Flush

Commits all pending changes to persistent storage.

```csharp
void Flush()
```

**Behavior:**
```csharp
if (!_isDirty) return;  // No-op if nothing changed

// Step 1: Apply pending deletions
foreach (var key in _deletedKeys)
    PlayerPrefs.DeleteKey(key);
_deletedKeys.Clear();

// Step 2: Apply pending writes
foreach (var entry in _buffer)
    PlayerPrefs.SetString(entry.Key, entry.Value);

// Step 3: Commit to disk
PlayerPrefs.Save();

// Step 4: Reset dirty flag
_isDirty = false;
```

**Automatic Flush Triggers:**
| Event | Trigger |
|-------|---------|
| `OnApplicationPause()` | Application enters background (iOS/Android) |
| `OnApplicationQuit()` | Application is terminating |
| `Dispose()` | Service is being destroyed |

---

## Usage Examples

### Basic Operations

```csharp
public class ScoreManager : MonoBehaviour
{
    private ISaveService _saveService;
    private int _highScore;
    
    void Start()
    {
        _saveService = ServiceLocator.Get<ISaveService>();
        _highScore = _saveService.Load("high_score", 0);
    }
    
    public void SubmitScore(int score)
    {
        if (score > _highScore)
        {
            _highScore = score;
            _saveService.Save("high_score", _highScore);
            _saveService.Flush();  // Critical data: flush immediately
        }
    }
}
```

### Complex Object Persistence

```csharp
[Serializable]
public class PlayerProgress
{
    public int level;
    public int experience;
    public float playTime;
    public List<string> achievements;
    public Vector3 checkpointPosition;
}

public class ProgressManager : MonoBehaviour
{
    private const string PROGRESS_KEY = "player_progress";
    private ISaveService _saveService;
    private PlayerProgress _progress;
    
    void Start()
    {
        _saveService = ServiceLocator.Get<ISaveService>();
        LoadProgress();
    }
    
    void LoadProgress()
    {
        _progress = _saveService.Load<PlayerProgress>(PROGRESS_KEY);
        
        if (_progress == null)
        {
            _progress = new PlayerProgress
            {
                level = 1,
                experience = 0,
                playTime = 0f,
                achievements = new List<string>()
            };
        }
    }
    
    public void SaveCheckpoint(Vector3 position)
    {
        _progress.checkpointPosition = position;
        _saveService.Save(PROGRESS_KEY, _progress);
        // Flush is handled automatically on pause/quit
    }
    
    public void UnlockAchievement(string achievementId)
    {
        if (!_progress.achievements.Contains(achievementId))
        {
            _progress.achievements.Add(achievementId);
            _saveService.Save(PROGRESS_KEY, _progress);
            _saveService.Flush();  // Achievements should persist immediately
        }
    }
}
```

### Settings Management

```csharp
[Serializable]
public class GameSettings
{
    public float masterVolume = 1.0f;
    public float musicVolume = 0.8f;
    public float sfxVolume = 1.0f;
    public int graphicsQuality = 2;
    public bool fullscreen = true;
    public string locale = "en";
    
    public static GameSettings Default => new GameSettings();
}

public class SettingsController : MonoBehaviour
{
    private const string SETTINGS_KEY = "app_settings";
    
    private ISaveService _saveService;
    private GameSettings _settings;
    
    public GameSettings Current => _settings;
    
    void Awake()
    {
        _saveService = ServiceLocator.Get<ISaveService>();
        _settings = _saveService.Load(SETTINGS_KEY, GameSettings.Default);
        ApplySettings();
    }
    
    public void UpdateSettings(GameSettings newSettings)
    {
        _settings = newSettings;
        ApplySettings();
        
        _saveService.Save(SETTINGS_KEY, _settings);
        _saveService.Flush();  // User expects immediate persistence
    }
    
    private void ApplySettings()
    {
        AudioListener.volume = _settings.masterVolume;
        QualitySettings.SetQualityLevel(_settings.graphicsQuality);
        Screen.fullScreen = _settings.fullscreen;
    }
}
```

### Key Naming Conventions

```csharp
public static class SaveKeys
{
    // Static keys
    public const string HighScore = "high_score";
    public const string Settings = "app_settings";
    public const string PlayerProfile = "player_profile";
    
    // Parameterized keys
    public static string LevelStars(int levelId) => $"level_{levelId}_stars";
    public static string ItemQuantity(string itemId) => $"inventory_{itemId}_qty";
    public static string QuestState(string questId) => $"quest_{questId}_state";
}

// Usage
int stars = saveService.Load(SaveKeys.LevelStars(currentLevel), 0);
saveService.Save(SaveKeys.ItemQuantity("health_potion"), 5);
```

---

## Buffer Mechanism

### Write Buffer Flow

```
┌────────────────────────────────────────────────────────────────────┐
│                        WRITE OPERATION FLOW                        │
├────────────────────────────────────────────────────────────────────┤
│                                                                    │
│   Client Code              SaveService                 PlayerPrefs │
│       │                        │                            │      │
│       │  Save("key", value)    │                            │      │
│       │───────────────────────▶│                            │      │
│       │                        │                            │      │
│       │                   ┌────┴────┐                       │      │
│       │                   │Serialize│                       │      │
│       │                   │  value  │                       │      │
│       │                   └────┬────┘                       │      │
│       │                        │                            │      │
│       │                   ┌────┴────┐                       │      │
│       │                   │ _buffer │                       │      │
│       │                   │["key"]= │                       │      │
│       │                   │ string  │                       │      │
│       │                   └────┬────┘                       │      │
│       │                        │                            │      │
│       │                   _isDirty = true                   │      │
│       │                        │                            │      │
│       │◀───────────────────────│                            │      │
│       │      (returns)         │                            │      │
│       │                        │                            │      │
│       │  Flush()               │                            │      │
│       │───────────────────────▶│                            │      │
│       │                        │                            │      │
│       │                        │  SetString("key", value)   │      │
│       │                        │───────────────────────────▶│      │
│       │                        │                            │      │
│       │                        │  Save()                    │      │
│       │                        │───────────────────────────▶│      │
│       │                        │                       [DISK I/O]  │
│       │                        │                            │      │
│       │                   _isDirty = false                  │      │
│       │◀───────────────────────│                           │      │
│                                                                    │
└────────────────────────────────────────────────────────────────────┘
```

### Read Buffer Flow

```
┌────────────────────────────────────────────────────────────────────┐
│                         READ OPERATION FLOW                        │
├────────────────────────────────────────────────────────────────────┤
│                                                                    │
│   Client Code              SaveService                 PlayerPrefs │
│       │                        │                            │      │
│       │  Load<T>("key", def)   │                            │      │
│       │───────────────────────▶│                           │      │
│       │                        │                            │      │
│       │                   ┌────┴────┐                       │      │
│       │                   │ Check   │                       │      │
│       │                   │_deleted │                       │      │
│       │                   │  Keys   │                       │      │
│       │                   └────┬────┘                       │      │
│       │                        │                            │      │
│       │                   ┌────┴────┐                       │      │
│       │                   │ Check   │  (if not in buffer)   │      │
│       │                   │ _buffer │─────────────────────▶ │      │
│       │                   └────┬────┘    GetString("key")   │      │
│       │                        │                            │      │
│       │                   ┌────┴────┐                       │      │
│       │                   │Deserial-│                       │      │
│       │                   │  ize    │                       │      │
│       │                   └────┬────┘                       │      │
│       │                        │                            │      │
│       │◀───────────────────────│                           │      │
│       │    returns T value     │                            │      │
│                                                                    │
└────────────────────────────────────────────────────────────────────┘
```

### Deletion Tracking

```
┌────────────────────────────────────────────────────────────────────┐
│                      DELETION TRACKING FLOW                        │
├────────────────────────────────────────────────────────────────────┤
│                                                                    │
│  Initial State:                                                    │
│  ┌─────────────────┐    ┌─────────────────┐                        │
│  │     _buffer     │    │  _deletedKeys   │                        │
│  │  {"a": "1"}     │    │     { }         │                        │
│  │  {"b": "2"}     │    │                 │                        │
│  └─────────────────┘    └─────────────────┘                        │
│                                                                    │
│  After DeleteKey("a"):                                             │
│  ┌─────────────────┐    ┌─────────────────┐                        │
│  │     _buffer     │    │  _deletedKeys   │                        │
│  │  {"b": "2"}     │    │    { "a" }      │                        │
│  └─────────────────┘    └─────────────────┘                        │
│                                                                    │
│  After Save("a", "3"):  (resurrection)                             │
│  ┌─────────────────┐    ┌─────────────────┐                        │
│  │     _buffer     │    │  _deletedKeys   │                        │
│  │  {"a": "3"}     │    │     { }         │   ← "a" removed        │
│  │  {"b": "2"}     │    │                 │                        │
│  └─────────────────┘    └─────────────────┘                        │
│                                                                    │
└────────────────────────────────────────────────────────────────────┘
```

---

## Serialization Strategy

### Type-Specific Handling

The service employs a two-tier serialization strategy to optimize performance:

| Type | Serialization Method | Performance |
|------|---------------------|-------------|
| `string` | Direct passthrough | ⚡ Fastest |
| `int` | `ToString()` / `int.TryParse()` | ⚡ Very Fast |
| `long` | `ToString()` / `long.TryParse()` | ⚡ Very Fast |
| `float` | `ToString()` / `float.TryParse()` | ⚡ Very Fast |
| `bool` | `ToString()` / `bool.TryParse()` | ⚡ Very Fast |
| Complex Objects | `JsonUtility.ToJson()` / `FromJson()` | 🔄 Moderate |

### Serialization Flow

```csharp
private string SerializeValue<T>(T value)
{
    if (value == null) return string.Empty;
    
    var type = typeof(T);
    
    // Fast path for primitives
    if (type == typeof(string)) return value as string;
    if (type == typeof(int))    return value.ToString();
    if (type == typeof(long))   return value.ToString();
    if (type == typeof(float))  return value.ToString();
    if (type == typeof(bool))   return value.ToString();
    
    // Fallback for complex objects
    return JsonUtility.ToJson(value);
}
```

### JsonUtility Limitations

When using complex objects, be aware of `JsonUtility` constraints:

| Supported | Not Supported |
|-----------|---------------|
| `[Serializable]` classes | Properties (only fields) |
| Public fields | Private fields (unless `[SerializeField]`) |
| Arrays and Lists | Dictionaries |
| Unity types (Vector3, Color, etc.) | Polymorphic types |
| Nested serializable objects | Abstract classes / interfaces |

**Example of a properly structured data class:**

```csharp
[Serializable]
public class SaveData
{
    // ✅ Supported
    public int score;
    public string playerName;
    public float[] highScores;
    public List<string> achievements;
    public Vector3 position;
    public NestedData nested;
    
    // ❌ Not serialized
    public int PropertyValue { get; set; }
    private int _privateField;
    public Dictionary<string, int> inventory;
}

[Serializable]
public class NestedData
{
    public int value;
    public string name;
}
```

---

## Best Practices

### 1. Cache Service References

```csharp
// ❌ Avoid: Repeated lookups in hot paths
void Update()
{
    var save = ServiceLocator.Get<ISaveService>();
    // ...
}

// ✅ Preferred: Cache the reference
private ISaveService _saveService;

void Start()
{
    _saveService = ServiceLocator.Get<ISaveService>();
}
```

### 2. Batch Related Saves

```csharp
// ❌ Avoid: Multiple flushes
saveService.Save("health", health);
saveService.Flush();
saveService.Save("mana", mana);
saveService.Flush();
saveService.Save("stamina", stamina);
saveService.Flush();

// ✅ Preferred: Single flush
saveService.Save("health", health);
saveService.Save("mana", mana);
saveService.Save("stamina", stamina);
saveService.Flush();
```

### 3. Use Data Objects for Related Values

```csharp
// ❌ Avoid: Scattered keys
saveService.Save("player_x", position.x);
saveService.Save("player_y", position.y);
saveService.Save("player_z", position.z);
saveService.Save("player_health", health);
saveService.Save("player_mana", mana);

// ✅ Preferred: Single cohesive object
[Serializable]
public class PlayerState
{
    public Vector3 position;
    public int health;
    public int mana;
}

saveService.Save("player_state", playerState);
```

### 4. Immediate Flush for Critical Data

```csharp
public void CompletePurchase(string itemId, int cost)
{
    _currency -= cost;
    _inventory.Add(itemId);
    
    // Critical: Flush immediately to prevent item/currency desync
    saveService.Save("currency", _currency);
    saveService.Save("inventory", _inventory);
    saveService.Flush();
}
```

### 5. Establish Key Naming Conventions

```csharp
public static class SaveKeys
{
    // Use prefixes for organization
    public const string Prefix_Player = "player_";
    public const string Prefix_Settings = "settings_";
    public const string Prefix_Analytics = "analytics_";
    
    // Avoid magic strings throughout codebase
    public static string PlayerStat(string stat) => $"{Prefix_Player}{stat}";
}
```

### 6. Handle Missing Data Gracefully

```csharp
// ❌ Avoid: Assuming data exists
var progress = saveService.Load<Progress>("progress");
int level = progress.level;  // NullReferenceException if new player

// ✅ Preferred: Provide sensible defaults
var progress = saveService.Load("progress", Progress.CreateNew());
```

---

## Extending the System

### Alternative Backend Implementation

The interface-based design allows seamless substitution of the storage backend:

```csharp
/// <summary>
/// Cloud-based implementation using a remote persistence service.
/// </summary>
public class CloudSaveService : ISaveService
{
    private readonly ICloudProvider _provider;
    private readonly Dictionary<string, string> _cache;
    private readonly HashSet<string> _dirtyKeys;
    
    public CloudSaveService(ICloudProvider provider)
    {
        _provider = provider;
        _cache = new Dictionary<string, string>();
        _dirtyKeys = new HashSet<string>();
    }
    
    public void Initialize()
    {
        // Fetch initial state from cloud
        _provider.FetchAllAsync().ContinueWith(task =>
        {
            foreach (var kvp in task.Result)
            {
                _cache[kvp.Key] = kvp.Value;
            }
        });
    }
    
    public void Save<T>(string key, T value)
    {
        _cache[key] = JsonUtility.ToJson(value);
        _dirtyKeys.Add(key);
    }
    
    public T Load<T>(string key, T defaultValue)
    {
        if (_cache.TryGetValue(key, out var json))
        {
            return JsonUtility.FromJson<T>(json);
        }
        return defaultValue;
    }
    
    public void Flush()
    {
        var updates = _dirtyKeys
            .Where(k => _cache.ContainsKey(k))
            .ToDictionary(k => k, k => _cache[k]);
            
        _provider.BatchUpdateAsync(updates);
        _dirtyKeys.Clear();
    }
    
    // ... remaining interface methods
}
```

### Encrypted Storage

```csharp
/// <summary>
/// Decorator that adds encryption to any ISaveService implementation.
/// </summary>
public class EncryptedSaveService : ISaveService
{
    private readonly ISaveService _inner;
    private readonly IEncryptionProvider _crypto;
    
    public EncryptedSaveService(ISaveService inner, IEncryptionProvider crypto)
    {
        _inner = inner;
        _crypto = crypto;
    }
    
    public void Save<T>(string key, T value)
    {
        string json = JsonUtility.ToJson(value);
        string encrypted = _crypto.Encrypt(json);
        _inner.Save(key, encrypted);
    }
    
    public T Load<T>(string key, T defaultValue)
    {
        string encrypted = _inner.Load<string>(key);
        if (string.IsNullOrEmpty(encrypted))
        {
            return defaultValue;
        }
        
        string json = _crypto.Decrypt(encrypted);
        return JsonUtility.FromJson<T>(json);
    }
    
    // Delegate remaining methods to _inner
    public void Flush() => _inner.Flush();
    public bool HasKey(string key) => _inner.HasKey(key);
    public void DeleteKey(string key) => _inner.DeleteKey(key);
    public void DeleteAll() => _inner.DeleteAll();
    public void Initialize() => _inner.Initialize();
    public void Dispose() => _inner.Dispose();
    public void OnApplicationPause() => _inner.OnApplicationPause();
    public void OnApplicationQuit() => _inner.OnApplicationQuit();
}
```

### Migration Support

```csharp
public class MigratingSaveService : ISaveService
{
    private readonly ISaveService _inner;
    private const string VERSION_KEY = "__save_version";
    private const int CURRENT_VERSION = 2;
    
    public void Initialize()
    {
        _inner.Initialize();
        
        int savedVersion = _inner.Load(VERSION_KEY, 0);
        
        if (savedVersion < CURRENT_VERSION)
        {
            MigrateData(savedVersion, CURRENT_VERSION);
            _inner.Save(VERSION_KEY, CURRENT_VERSION);
            _inner.Flush();
        }
    }
    
    private void MigrateData(int from, int to)
    {
        // Version 0 → 1: Rename keys
        if (from < 1)
        {
            var oldScore = _inner.Load<int>("score");
            _inner.Save("player_score", oldScore);
            _inner.DeleteKey("score");
        }
        
        // Version 1 → 2: Restructure data
        if (from < 2)
        {
            var health = _inner.Load<int>("health");
            var mana = _inner.Load<int>("mana");
            
            var stats = new PlayerStats { health = health, mana = mana };
            _inner.Save("player_stats", stats);
            
            _inner.DeleteKey("health");
            _inner.DeleteKey("mana");
        }
    }
    
    // Delegate all other methods to _inner...
}
```

---

## Performance Considerations

| Operation | Time Complexity | Notes |
|-----------|-----------------|-------|
| `Save<T>()` | O(1) + serialization | Dictionary insertion |
| `Load<T>()` | O(1) + deserialization | Dictionary/PlayerPrefs lookup |
| `HasKey()` | O(1) | HashSet + Dictionary check |
| `DeleteKey()` | O(1) | HashSet insertion |
| `Flush()` | O(n) | n = number of buffered entries |
| `DeleteAll()` | O(n) | Clears all storage |

**Recommendations:**

1. **Avoid frequent flushes** in performance-critical sections
2. **Batch related data** into single objects to reduce key count
3. **Cache Load results** when accessed repeatedly
4. **Use primitive types** when possible for faster serialization
5. **Profile JsonUtility** usage with complex object graphs
