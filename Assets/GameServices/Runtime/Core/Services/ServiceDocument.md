# Game Services Framework - Architecture Documentation

## Table of Contents

1. [Overview](#overview)
2. [Core Concepts](#core-concepts)
3. [System Components](#system-components)
4. [Lifecycle Management](#lifecycle-management)
5. [Dependency Resolution](#dependency-resolution)
6. [Data Flow](#data-flow)
7. [Extensibility](#extensibility)

---

## Overview

The Game Services Framework is a lightweight **Service Locator** pattern implementation designed for Unity applications. Its primary purpose is to provide centralized service management, automatic dependency resolution, and coordinated lifecycle handling across all game services.

### Problem Statement

| Challenge | Framework Solution |
|-----------|-------------------|
| Uncontrolled singleton proliferation | Centralized ServiceLocator provides single access point |
| Tight coupling between services | Interface-based abstraction decouples consumers from implementations |
| Initialization order issues | Topological sort ensures correct dependency ordering |
| Lifecycle management complexity | IService interface enforces standardized lifecycle hooks |
| Editor configuration difficulties | ScriptableObject-based registry system enables visual configuration |

### Architectural Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              UNITY RUNTIME                                  │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │                        GameBootstrapper                              │   │
│  │  MonoBehaviour - DontDestroyOnLoad                                   │   │
│  │                                                                      │   │
│  │  Responsibilities:                                                   │   │
│  │  • Reads MasterRegistry configuration                                │   │
│  │  • Performs validation checks                                        │   │
│  │  • Injects injectable registries                                     │   │
│  │  • Initializes services in dependency order                          │   │
│  │  • Manages lifecycle events (pause, quit)                            │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
│                                    │                                        │
│                                    ▼                                        │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │                    ScriptableObject Layer                            │   │
│  │                                                                      │   │
│  │  ┌─────────────────────┐      ┌─────────────────────┐                │   │
│  │  │   MasterRegistrySO  │─────▶│  ServiceRegistrySO  │                │   │
│  │  │                     │      │                     │                │   │
│  │  │ • ServiceRegistry   │      │ • ServiceDefinition │                │   │
│  │  │ • InjectableRegs[]  │      │ • ServiceDefinition │                │   │
│  │  └─────────────────────┘      │ • ...               │                │   │
│  │            │                  └─────────────────────┘                │   │
│  │            │                                                         │   │
│  │            ▼                                                         │   │
│  │  ┌─────────────────────┐                                             │   │
│  │  │ IInjectableRegistry │ (Optional subsystem registries)             │   │
│  │  └─────────────────────┘                                             │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
│                                    │                                        │
│                                    ▼                                        │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │                      DependencyResolver                              │   │
│  │                                                                      │   │
│  │  • Inspects [DependsOn] attributes                                   │   │
│  │  • Constructs dependency graph                                       │   │
│  │  • Performs topological sort                                         │   │
│  │  • Detects circular dependencies                                     │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
│                                    │                                        │
│                                    ▼                                        │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │                        ServiceLocator                                │   │
│  │  Static Class - Global Access Point                                  │   │
│  │                                                                      │   │
│  │  Dictionary<Type, IService>                                          │   │
│  │  ┌─────────────────┬─────────────────┬─────────────────┐             │   │
│  │  │ ISaveService    │ IAudioService   │ IScoreService   │             │   │
│  │  │       │         │       │         │       │         │             │   │
│  │  │       ▼         │       ▼         │       ▼         │             │   │
│  │  │ SaveService     │ AudioService    │ ScoreService    │             │   │
│  │  └─────────────────┴─────────────────┴─────────────────┘             │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Core Concepts

### Service Locator Pattern

The Service Locator pattern provides global access to services without requiring direct dependencies on concrete implementations. In Unity's MonoBehaviour-centric architecture, this approach often proves more practical than traditional Dependency Injection.

```csharp
// Consumer code
var saveService = ServiceLocator.Get<ISaveService>();
saveService.Save("player_score", currentScore);
```

### Interface-Based Abstraction

Every service is defined through an interface contract. This architectural decision provides:

- **Testability**: Mock implementations can be substituted for unit testing
- **Flexibility**: Implementations can be swapped without modifying consumers
- **Explicit Dependencies**: Service relationships become visible and documented

```csharp
// Contract definition
public interface ISaveService : IService
{
    void Save<T>(string key, T value);
    T Load<T>(string key, T defaultValue);
}

// Production implementation
public class SaveService : ISaveService { /* PlayerPrefs backend */ }

// Alternative implementation
public class CloudSaveService : ISaveService { /* Remote storage backend */ }
```

### ScriptableObject Configuration

Registries are implemented as ScriptableObjects, providing several advantages:

- Visual configuration through Unity's Inspector
- Version control compatibility (YAML serialization)
- Runtime immutability (configuration cannot be accidentally modified)
- Independence from scene hierarchy and prefabs

---

## System Components

### 1. IService Interface

The foundational contract that all managed services must implement.

```csharp
public interface IService
{
    void Initialize();          // Invoked after registration, dependencies available
    void Dispose();             // Cleanup when service is destroyed
    void OnApplicationPause();  // Application backgrounded (mobile platforms)
    void OnApplicationQuit();   // Application terminating
}
```

**Lifecycle Guarantees:**

| Method | Guarantee |
|--------|-----------|
| `Initialize()` | Called after all dependencies are registered in ServiceLocator |
| `Dispose()` | Called in reverse initialization order (LIFO) |
| `OnApplicationPause()` | Called when app enters background; critical for mobile data persistence |
| `OnApplicationQuit()` | Final opportunity for cleanup before process termination |

### 2. ServiceDefinition

A serializable class that maps interface contracts to their concrete implementations.

```csharp
[Serializable]
public class ServiceDefinition
{
    [SerializeField] private string _interfaceTypeName;      // "Save.ISaveService"
    [SerializeField] private string _implementationTypeName; // "Save.SaveService"
    [SerializeField] private bool _isEnabled;                // Runtime toggle
    
    public Type InterfaceType { get; }       // Resolved and cached
    public Type ImplementationType { get; }  // Resolved and cached
}
```

**Validation Checks:**

The `Validate()` method performs comprehensive integrity verification:

1. Type names are not null or empty
2. Types can be resolved from the current assembly
3. Interface type is actually an interface
4. Implementation type implements the specified interface
5. Implementation type implements `IService`

### 3. ServiceRegistrySO

A ScriptableObject that maintains the catalog of service definitions.

```csharp
[CreateAssetMenu(menuName = "GameServices/Core/Service Registry")]
public class ServiceRegistrySO : ScriptableObject
{
    [SerializeField] private List<ServiceDefinition> _serviceDefinitions;
    
    public IReadOnlyList<ServiceDefinition> ServiceDefinitions { get; }
    public bool ValidateAll(out List<string> errors);
}
```

**Editor Integration:**

- `AddDefinition()`: Programmatically add entries (for tooling)
- `ClearDefinitions()`: Reset the registry
- Both methods invoke `EditorUtility.SetDirty()` to ensure persistence

### 4. MasterRegistrySO

The root configuration object that aggregates all registries.

```csharp
[CreateAssetMenu(menuName = "GameServices/Core/Master Registry")]
public class MasterRegistrySO : ScriptableObject
{
    [SerializeField] private ServiceRegistrySO _serviceRegistry;
    [SerializeField] private List<ScriptableObject> _injectableRegistries;
    
    public void InjectAll();
    public bool ValidateAll(out List<string> errors);
}
```

**Injectable Registry System:**

Certain subsystems (e.g., item databases, audio configurations) may require injection into static managers before service initialization. The `IInjectableRegistry` interface facilitates this:

```csharp
public interface IInjectableRegistry
{
    void Inject();
    string RegistryName { get; }
}

// Example implementation
[CreateAssetMenu(menuName = "Game/Item Database")]
public class ItemDatabaseSO : ScriptableObject, IInjectableRegistry
{
    [SerializeField] private List<ItemData> _items;
    
    public string RegistryName => "ItemDatabase";
    
    public void Inject()
    {
        ItemManager.Initialize(this);
    }
}
```

### 5. DependencyResolver

A static utility class that performs topological sorting of service definitions.

```csharp
public static class DependencyResolver
{
    public static List<ServiceDefinition> ResolveDependencies(
        IReadOnlyList<ServiceDefinition> definitions);
}
```

**Algorithm Overview:**

1. Filter to only enabled definitions
2. Build interface-to-definition mapping
3. Extract dependencies from `[DependsOn]` attributes
4. Construct dependency graph
5. Execute depth-first topological sort
6. Detect and report circular dependencies

**Visualization:**

```
Input Definitions:              Sorted Output:
┌─────────────────┐             
│  ScoreService   │             1. ISaveService    (no dependencies)
│  [DependsOn:    │             2. IConfigService  (no dependencies)
│   ISaveService] │             3. IScoreService   (depends on 1)
└─────────────────┘             
        │                       
        ▼                       
┌─────────────────┐             
│  SaveService    │             
│  (no deps)      │             
└─────────────────┘             
```

### 6. ServiceLocator

The central static registry for service instances.

```csharp
public static class ServiceLocator
{
    // Internal state
    private static Dictionary<Type, IService> _services;
    private static bool _isInitialized;
    
    // Registration
    public static void Register<TInterface>(IService service);
    public static bool Unregister<TInterface>();
    
    // Resolution
    public static TInterface Get<TInterface>();
    public static bool TryGet<TInterface>(out TInterface service);
    public static bool HasService<TInterface>();
    
    // Lifecycle
    internal static void MarkInitialized();
    public static void Clear();
}
```

**Thread Safety Note:**

The current implementation is not thread-safe. Given Unity's predominantly single-threaded execution model, this is typically acceptable. However, caution is advised when performing service access from background threads or async contexts.

### 7. GameBootstrapper

The MonoBehaviour responsible for orchestrating the initialization sequence.

```csharp
public class GameBootstrapper : MonoBehaviour
{
    [SerializeField] private MasterRegistrySO _masterRegistry;
    [SerializeField] private bool _debugLogging;
    
    private static bool _hasInitialized;
    private readonly List<IService> _initializedServices;
}
```

**Initialization Sequence:**

```
Awake()
├── Check for duplicate bootstrapper (destroy if exists)
├── Mark as initialized, DontDestroyOnLoad
├── Validate MasterRegistry assignment
├── MasterRegistry.ValidateAll()
├── MasterRegistry.InjectAll()
│   └── For each IInjectableRegistry: Inject()
├── InitializeServices()
│   ├── Validate ServiceRegistry assignment
│   ├── ServiceRegistry.ValidateAll()
│   ├── DependencyResolver.ResolveDependencies()
│   ├── For each definition: RegisterService()
│   │   ├── Activator.CreateInstance()
│   │   └── ServiceLocator.Register<T>()
│   ├── For each service: Initialize()
│   └── ServiceLocator.MarkInitialized()
└── SubscribeToLifecycleEvents()
```

---

## Lifecycle Management

### Initialization Phase

```
┌─────────────────────────────────────────────────────────────────┐
│                     INITIALIZATION SEQUENCE                     │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  [1] GameBootstrapper.Awake()                                   │
│       │                                                         │
│       ▼                                                         │
│  [2] MasterRegistry.InjectAll()                                 │
│       │  ┌─────────────────────────────────┐                    │
│       │  │ IInjectableRegistry.Inject()    │                    │
│       │  │ IInjectableRegistry.Inject()    │                    │
│       │  │ ...                             │                    │
│       │  └─────────────────────────────────┘                    │
│       ▼                                                         │
│  [3] DependencyResolver.ResolveDependencies()                   │
│       │  ┌─────────────────────────────────┐                    │
│       │  │ Topological Sort                │                    │
│       │  │ A → B → C (dependency order)    │                    │
│       │  └─────────────────────────────────┘                    │
│       ▼                                                         │
│  [4] ServiceLocator.Register<T>() (for each service)            │
│       │  ┌─────────────────────────────────┐                    │
│       │  │ Activator.CreateInstance()      │                    │
│       │  │ Add to Dictionary               │                    │
│       │  └─────────────────────────────────┘                    │
│       ▼                                                         │
│  [5] IService.Initialize() (for each service)                   │
│       │  ┌─────────────────────────────────┐                    │
│       │  │ Dependencies now accessible     │                    │
│       │  │ ServiceLocator.Get<T>() safe    │                    │
│       │  └─────────────────────────────────┘                    │
│       ▼                                                         │
│  [6] ServiceLocator.MarkInitialized()                           │
│       │                                                         │
│       ▼                                                         │
│  ✅ SYSTEM READY                                                │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### Runtime Events

```
┌─────────────────────────────────────────────────────────────────┐
│                        RUNTIME EVENTS                           │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌─────────────────────┐                                        │
│  │ OnApplicationPause  │ ◄─── iOS: Home button pressed          │
│  │ (pauseStatus: true) │      Android: Home/Back pressed        │
│  └──────────┬──────────┘      Desktop: Window lost focus        │
│             │                                                   │
│             ▼                                                   │
│  ┌────────────────────────┐                                     │
│  │ foreach (service)      │                                     │
│  │   service              │                                     │
│  │   .OnApplicationPause()│                                     │
│  └────────────────────────┘                                     │
│                                                                 │
│  ┌─────────────────────┐                                        │
│  │ SceneManager        │                                        │
│  │ .sceneUnloaded      │ ◄─── Scene transition occurring        │
│  └──────────┬──────────┘                                        │
│             │                                                   │
│             ▼                                                   │
│  ┌─────────────────────┐                                        │
│  │ OnBeforeSceneChange │ (virtual - override for custom logic)  │
│  └─────────────────────┘                                        │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### Shutdown Sequence

```
┌─────────────────────────────────────────────────────────────────┐
│                      SHUTDOWN SEQUENCE                          │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  [1] OnApplicationQuit()                                        │
│       │                                                         │
│       ▼                                                         │
│  [2] foreach (service) → service.OnApplicationQuit()            │
│       │  ┌─────────────────────────────────┐                    │
│       │  │ Final persistence operations    │                    │
│       │  │ Analytics flush                 │                    │
│       │  │ Network connection cleanup      │                    │
│       │  └─────────────────────────────────┘                    │
│       ▼                                                         │
│  [3] for (i = Count-1; i >= 0; i--) → service.Dispose()         │
│       │  ┌─────────────────────────────────┐                    │
│       │  │ REVERSE ORDER (LIFO)            │                    │
│       │  │ C.Dispose() → B.Dispose() → A   │                    │
│       │  └─────────────────────────────────┘                    │
│       ▼                                                         │
│  [4] ServiceLocator.Clear()                                     │
│       │                                                         │
│       ▼                                                         │
│  ✅ CLEAN SHUTDOWN                                              │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## Dependency Resolution

### DependsOn Attribute

Declaratively specifies inter-service dependencies.

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class DependsOnAttribute : Attribute
{
    public Type[] Dependencies { get; }
    
    public DependsOnAttribute(params Type[] dependencies)
    {
        Dependencies = dependencies ?? Array.Empty<Type>();
    }
}
```

**Usage Examples:**

```csharp
// Single dependency
[DependsOn(typeof(ISaveService))]
public class ScoreService : IScoreService { }

// Multiple dependencies
[DependsOn(typeof(ISaveService), typeof(IConfigService), typeof(IAnalyticsService))]
public class GameProgressService : IGameProgressService { }

// No dependencies (attribute optional)
public class SaveService : ISaveService { }
```

### Topological Sort Algorithm

The framework employs a depth-first search variant of topological sorting:

```
Algorithm: TopologicalSort(G)
Input:  G = (V, E) where V = services, E = dependency edges
Output: Linear ordering of V respecting all dependencies

1. visited ← ∅
2. visiting ← ∅     // For cycle detection
3. result ← []

4. for each v ∈ V:
      if v ∉ visited:
          Visit(v)

Visit(v):
    if v ∈ visiting:
        throw CircularDependencyException(v)
    if v ∈ visited:
        return
    
    visiting ← visiting ∪ {v}
    
    for each dependency d of v:
        Visit(d)
    
    visiting ← visiting \ {v}
    visited ← visited ∪ {v}
    result.append(v)
```

### Circular Dependency Detection

```csharp
// This configuration will throw at runtime:
[DependsOn(typeof(IServiceB))]
public class ServiceA : IServiceA { }

[DependsOn(typeof(IServiceA))]
public class ServiceB : IServiceB { }

// Error message:
// "Circular dependency detected involving service 'ServiceA'. 
//  Check your [DependsOn] attributes for circular references."
```

**Resolution Strategies:**

| Approach | Description |
|----------|-------------|
| Event-based communication | Services communicate through events rather than direct calls |
| Mediator pattern | Introduce an intermediary to coordinate interactions |
| Lazy initialization | Defer dependency resolution until first access |
| Interface segregation | Split interfaces to break dependency cycles |

---

## Data Flow

### Registration Flow

```
ServiceDefinition              ServiceLocator
      │                              │
      │  1. Validate()               │
      │─────────────────────────────▶│
      │                              │
      │  2. Get ImplementationType   │
      │─────────────────────────────▶│
      │                              │
      │  3. Activator.CreateInstance │
      │                         ┌────┴────┐
      │                         │ IService│
      │                         │ instance│
      │                         └────┬────┘
      │                              │
      │  4. Register<TInterface>()   │
      │◀─────────────────────────────│
      │                              │
      │  5. _services[Type]=instance │
      │                              │
```

### Resolution Flow

```
Consumer Code                ServiceLocator              Dictionary
     │                            │                          │
     │ Get<ISaveService>()        │                          │
     │───────────────────────────▶│                          │
     │                            │                          │
     │                            │ TryGetValue(typeof(T))   │
     │                            │─────────────────────────▶│
     │                            │                          │
     │                            │◀─────────────────────────│
     │                            │    IService instance     │
     │                            │                          │
     │◀───────────────────────────│                          │
     │   (ISaveService)instance   │                          │
     │                            │                          │
```

---

## Extensibility

### Creating a New Service

```csharp
// Step 1: Define the interface
public interface ILeaderboardService : IService
{
    void SubmitScore(string playerId, int score);
    Task<List<LeaderboardEntry>> GetTopScoresAsync(int count);
    event Action<LeaderboardEntry> OnNewHighScore;
}

// Step 2: Implement the service
[DependsOn(typeof(ISaveService), typeof(INetworkService))]
public class LeaderboardService : ILeaderboardService
{
    private ISaveService _saveService;
    private INetworkService _networkService;
    
    public event Action<LeaderboardEntry> OnNewHighScore;
    
    public void Initialize()
    {
        _saveService = ServiceLocator.Get<ISaveService>();
        _networkService = ServiceLocator.Get<INetworkService>();
    }
    
    public void SubmitScore(string playerId, int score)
    {
        // Implementation
    }
    
    public async Task<List<LeaderboardEntry>> GetTopScoresAsync(int count)
    {
        // Implementation
    }
    
    public void Dispose() { }
    public void OnApplicationPause() { }
    public void OnApplicationQuit() { }
}

// Step 3: Register in ServiceRegistry (via Inspector or code)
// Interface: YourNamespace.ILeaderboardService
// Implementation: YourNamespace.LeaderboardService
```

### Custom Injectable Registry

```csharp
[CreateAssetMenu(menuName = "Game/Level Database")]
public class LevelDatabaseSO : ScriptableObject, IInjectableRegistry
{
    [SerializeField] private List<LevelData> _levels;
    
    public string RegistryName => "LevelDatabase";
    public IReadOnlyList<LevelData> Levels => _levels;
    
    public void Inject()
    {
        LevelManager.SetDatabase(this);
    }
    
    public LevelData GetLevel(int index)
    {
        if (index < 0 || index >= _levels.Count)
            return null;
        return _levels[index];
    }
}
```

### Extending GameBootstrapper

```csharp
public class CustomBootstrapper : GameBootstrapper
{
    [SerializeField] private bool _enableProfiling;
    
    protected override void OnBeforeSceneChange()
    {
        base.OnBeforeSceneChange();
        
        // Custom cleanup logic
        ObjectPoolManager.ReturnAllToPool();
        UIManager.CloseAllModals();
    }
    
    protected override void NotifyApplicationPause()
    {
        if (_enableProfiling)
        {
            Profiler.BeginSample("Service Pause Notification");
        }
        
        base.NotifyApplicationPause();
        
        // Additional pause handling
        TimeManager.PauseAllTimers();
        AudioManager.PauseAllSources();
        
        if (_enableProfiling)
        {
            Profiler.EndSample();
        }
    }
}
```

### Unit Testing Support

```csharp
[TestFixture]
public class ScoreServiceTests
{
    private MockSaveService _mockSave;
    
    [SetUp]
    public void SetUp()
    {
        // Clear any existing registrations
        ServiceLocator.Clear();
        
        // Register mock dependencies
        _mockSave = new MockSaveService();
        ServiceLocator.Register<ISaveService>(_mockSave);
        ServiceLocator.MarkInitialized();
    }
    
    [TearDown]
    public void TearDown()
    {
        ServiceLocator.Clear();
    }
    
    [Test]
    public void AddScore_IncreasesCurrentScore()
    {
        // Arrange
        var scoreService = new ScoreService();
        scoreService.Initialize();
        
        // Act
        scoreService.AddScore(100);
        
        // Assert
        Assert.AreEqual(100, scoreService.CurrentScore);
    }
    
    [Test]
    public void AddScore_PersistsToSaveService()
    {
        // Arrange
        var scoreService = new ScoreService();
        scoreService.Initialize();
        
        // Act
        scoreService.AddScore(500);
        
        // Assert
        Assert.IsTrue(_mockSave.SavedKeys.Contains("score"));
    }
}

public class MockSaveService : ISaveService
{
    public HashSet<string> SavedKeys { get; } = new HashSet<string>();
    public Dictionary<string, object> Storage { get; } = new Dictionary<string, object>();
    
    public void Save<T>(string key, T value)
    {
        SavedKeys.Add(key);
        Storage[key] = value;
    }
    
    public T Load<T>(string key, T defaultValue)
    {
        return Storage.TryGetValue(key, out var value) ? (T)value : defaultValue;
    }
    
    // Implement remaining interface methods...
}
```

---

## Performance Considerations

| Operation | Time Complexity | Notes |
|-----------|-----------------|-------|
| `ServiceLocator.Get<T>()` | O(1) | Dictionary lookup |
| `ServiceLocator.Register<T>()` | O(1) | Dictionary insertion |
| `DependencyResolver.ResolveDependencies()` | O(V + E) | V = service count, E = dependency count |
| `Type.GetType()` | Expensive | Results are cached in ServiceDefinition |

**Recommendations:**

1. **Cache service references** - Avoid repeated `Get<T>()` calls, especially in Update loops
2. **Minimize inter-service dependencies** - Excessive dependencies increase initialization complexity
3. **Consider `TryGet<T>()`** - For optional dependencies in performance-critical paths
4. **Profile initialization** - Enable debug logging to identify slow-initializing services

---

## Summary

This framework provides a structured approach to service management in Unity applications. By combining the Service Locator pattern with automatic dependency resolution and lifecycle management, it enables modular, testable, and maintainable game architecture.

**Recommended Use Cases:**

- Medium to large-scale Unity projects
- Applications requiring modular architecture
- Projects prioritizing testability
- Multi-developer team environments

**Key Takeaways:**

1. All services implement `IService` for standardized lifecycle
2. Dependencies are declared via `[DependsOn]` attributes
3. Configuration lives in ScriptableObjects for editor-friendly setup
4. The framework handles initialization order automatically
5. Disposal occurs in reverse order to respect dependencies