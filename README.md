# Unity Game Services Framework

A lightweight, dependency-aware service locator pattern implementation for Unity. This framework provides a clean architecture for managing game services with automatic dependency resolution, lifecycle management, and ScriptableObject-based configuration.

![Unity](https://img.shields.io/badge/Unity-2021.3%2B-black?logo=unity)
![License](https://img.shields.io/badge/License-MIT-green)
![C#](https://img.shields.io/badge/C%23-10.0-blue?logo=csharp)

## Features

- **Service Locator Pattern** — Centralized access to game services without tight coupling
- **Automatic Dependency Resolution** — Topological sorting ensures correct initialization order
- **ScriptableObject Configuration** — Editor-friendly setup with built-in validation
- **Lifecycle Management** — Standardized handling of initialization, pause, and shutdown events
- **Built-in Save System** — Buffered persistence layer with type-safe serialization
- **Editor Utilities** — Custom attributes for GUID generation and inspector enhancements

## Table of Contents

- [Installation](#installation)
- [Quick Start](#quick-start)
- [Architecture Overview](#architecture-overview)
- [Save System](#save-system)
- [Creating Custom Services](#creating-custom-services)
- [Documentation](#documentation)
- [License](#license)

## Installation

### Unity Package Manager (Git URL)

1. Open **Window → Package Manager**
2. Click **"+"** → **"Add package from git URL..."**
3. Enter: `https://github.com/YOUR_USERNAME/unity-game-services.git`

### Manual Installation

1. Download or clone this repository
2. Copy the `GameServices` folder into your project's `Assets` directory

## Quick Start

### 1. Create Configuration Assets

Create the following folder structure and assets:

```
Assets/
└── Resources/
    └── GameServices/
        ├── MasterRegistry.asset    (Create → GameServices/Core/Master Registry)
        └── ServiceRegistry.asset   (Create → GameServices/Core/Service Registry)
```

### 2. Configure Services

Select `ServiceRegistry.asset` and add your service definitions:

| Field | Example |
|-------|---------|
| Interface Type Name | `Save.ISaveService` |
| Implementation Type Name | `Save.SaveService` |
| Is Enabled | ✓ |

Link the `ServiceRegistry` to `MasterRegistry` via the Inspector.

### 3. Setup Bootstrapper

1. Create an empty GameObject in your startup scene
2. Add the `GameBootstrapper` component
3. Assign the `MasterRegistry` asset
4. Optionally enable **Debug Logging**

### 4. Access Services

```csharp
using Core.Services;
using Save;

public class GameManager : MonoBehaviour
{
    void Start()
    {
        var saveService = ServiceLocator.Get<ISaveService>();
        
        // Save data
        saveService.Save("player_name", "John");
        saveService.Save("high_score", 15000);
        
        // Load data
        string name = saveService.Load("player_name", "Guest");
        int score = saveService.Load("high_score", 0);
        
        // Persist to disk
        saveService.Flush();
    }
}
```

## Architecture Overview

The framework consists of several interconnected components:

```
┌─────────────────────────────────────────────────────────────────┐
│                       GameBootstrapper                           │
│  • Reads configuration from MasterRegistry                      │
│  • Resolves dependencies via topological sort                   │
│  • Initializes services in correct order                        │
│  • Manages lifecycle events                                     │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                       ServiceLocator                             │
│                                                                  │
│   Register<T>()    Get<T>()    TryGet<T>()    HasService<T>()   │
│                                                                  │
│   ┌─────────────┐  ┌─────────────┐  ┌─────────────┐            │
│   │ISaveService │  │IAudioService│  │IScoreService│   ...      │
│   └──────┬──────┘  └──────┬──────┘  └──────┬──────┘            │
│          │                │                │                    │
│          ▼                ▼                ▼                    │
│   ┌─────────────┐  ┌─────────────┐  ┌─────────────┐            │
│   │ SaveService │  │AudioService │  │ScoreService │   ...      │
│   └─────────────┘  └─────────────┘  └─────────────┘            │
└─────────────────────────────────────────────────────────────────┘
```

### Core Components

| Component | Purpose |
|-----------|---------|
| `IService` | Base interface defining the service lifecycle contract |
| `ServiceLocator` | Static registry providing global access to services |
| `ServiceDefinition` | Serializable binding between interface and implementation |
| `ServiceRegistrySO` | ScriptableObject containing service definitions |
| `MasterRegistrySO` | Root configuration aggregating all registries |
| `DependencyResolver` | Resolves initialization order via topological sort |
| `GameBootstrapper` | MonoBehaviour orchestrating the startup sequence |

### Service Lifecycle

```
┌──────────────┐     ┌──────────────┐     ┌──────────────┐
│   Register   │ ──▶ │  Initialize  │ ──▶ │    Ready     │
└──────────────┘     └──────────────┘     └──────────────┘
                                                 │
                     ┌───────────────────────────┤
                     ▼                           ▼
              ┌──────────────┐           ┌──────────────┐
              │    Pause     │           │     Quit     │
              └──────────────┘           └──────────────┘
                                                 │
                                                 ▼
                                         ┌──────────────┐
                                         │   Dispose    │
                                         │  (reverse)   │
                                         └──────────────┘
```

### Dependency Management

Declare dependencies using the `[DependsOn]` attribute:

```csharp
[DependsOn(typeof(ISaveService), typeof(IConfigService))]
public class ScoreService : IScoreService
{
    private ISaveService _saveService;
    
    public void Initialize()
    {
        // Dependencies are guaranteed to be available
        _saveService = ServiceLocator.Get<ISaveService>();
    }
}
```

The framework automatically resolves the initialization order. Circular dependencies are detected and reported at startup.

## Save System

The built-in Save System provides a buffered persistence layer over Unity's PlayerPrefs.

### Key Features

- **Write Buffer** — Minimizes disk I/O by batching writes
- **Type-Safe API** — Generic methods with compile-time safety
- **Auto-Flush** — Automatic persistence on pause and quit
- **Primitive Optimization** — Fast-path for int, float, bool, string

### API Reference

```csharp
var save = ServiceLocator.Get<ISaveService>();

// Store values (buffered in memory)
save.Save("key", value);

// Retrieve values
T value = save.Load<T>("key", defaultValue);
T value = save.Load<T>("key");  // Uses default(T)

// Check existence
bool exists = save.HasKey("key");

// Delete
save.DeleteKey("key");
save.DeleteAll();

// Persist to disk
save.Flush();
```

### Buffer Behavior

```
Save("a", 1)  ──▶  Memory Buffer
Save("b", 2)  ──▶  Memory Buffer
Save("c", 3)  ──▶  Memory Buffer
Flush()       ──▶  Single Disk Write (PlayerPrefs.Save)
```

### Complex Objects

```csharp
[Serializable]
public class PlayerData
{
    public string name;
    public int level;
    public List<string> inventory;
    public Vector3 position;
}

// Save
var data = new PlayerData { name = "Hero", level = 10 };
saveService.Save("player", data);

// Load
var loaded = saveService.Load<PlayerData>("player");
```

## Creating Custom Services

### Step 1: Define the Interface

```csharp
using Core.Services;

public interface ILeaderboardService : IService
{
    void SubmitScore(int score);
    int GetHighScore();
}
```

### Step 2: Implement the Service

```csharp
using Core.Services;

[DependsOn(typeof(ISaveService))]  // Declare dependencies
public class LeaderboardService : ILeaderboardService
{
    private ISaveService _save;
    private int _highScore;
    
    public void Initialize()
    {
        _save = ServiceLocator.Get<ISaveService>();
        _highScore = _save.Load("high_score", 0);
    }
    
    public void SubmitScore(int score)
    {
        if (score > _highScore)
        {
            _highScore = score;
            _save.Save("high_score", _highScore);
            _save.Flush();
        }
    }
    
    public int GetHighScore() => _highScore;
    
    public void Dispose() { }
    public void OnApplicationPause() { }
    public void OnApplicationQuit() { }
}
```

### Step 3: Register the Service

Add to `ServiceRegistry.asset`:

| Field | Value |
|-------|-------|
| Interface Type Name | `YourNamespace.ILeaderboardService` |
| Implementation Type Name | `YourNamespace.LeaderboardService` |
| Is Enabled | ✓ |

### Step 4: Use the Service

```csharp
var leaderboard = ServiceLocator.Get<ILeaderboardService>();
leaderboard.SubmitScore(1000);
```

## Project Structure

```
GameServices/
├── Editor/
│   └── Utilities/
│       ├── IDAttribute.cs          # Auto-generate GUIDs
│       ├── IDDrawer.cs
│       ├── ReadOnlyAttribute.cs    # Inspector read-only fields
│       └── ReadOnlyDrawer.cs
├── Runtime/
│   ├── Core/
│   │   └── Services/
│   │       ├── IService.cs              # Service lifecycle contract
│   │       ├── ServiceLocator.cs        # Global service registry
│   │       ├── ServiceDefinition.cs     # Interface-to-impl binding
│   │       ├── ServiceRegistrySO.cs     # Service configuration
│   │       ├── MasterRegistrySO.cs      # Root configuration
│   │       ├── GameBootstrapper.cs      # Initialization orchestrator
│   │       ├── DependencyResolver.cs    # Topological sort
│   │       ├── DependsOnAttribute.cs    # Dependency declaration
│   │       └── IInjectableRegistry.cs   # Subsystem injection
│   ├── SaveSystem/
│   │   ├── ISaveService.cs              # Persistence contract
│   │   └── SaveService.cs               # PlayerPrefs implementation
│   └── Utilities/
│       └── Attributes/
└── SETUP_GUIDE.md
```

## Documentation

For detailed technical documentation, see:

- **[Architecture Documentation](docs/ARCHITECTURE.md)** — System design, component details, lifecycle management, dependency resolution
- **[Save System Documentation](docs/SAVE_SYSTEM.md)** — Buffer mechanism, API reference, serialization, best practices

## Debugging

Enable **Debug Logging** on the `GameBootstrapper` component to trace:

```
[Bootstrapper] Registered service: ISaveService -> SaveService
[Bootstrapper] Registered service: IScoreService -> ScoreService
[Bootstrapper] Initializing service: SaveService
[Bootstrapper] Initializing service: ScoreService
[Bootstrapper] All services initialized. Total: 2
```

## Troubleshooting

| Issue | Solution |
|-------|----------|
| "Service of type X is not registered" | Verify service is added to ServiceRegistry with correct type names |
| "MasterRegistry is not assigned" | Assign MasterRegistry asset to GameBootstrapper |
| "Interface type not found" | Use fully qualified names (e.g., `Save.ISaveService`) |
| "Circular dependency detected" | Review `[DependsOn]` attributes; consider event-based decoupling |

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

---

**Questions or issues?** Feel free to open an issue on GitHub.
