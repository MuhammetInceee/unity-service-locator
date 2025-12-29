# Quick Setup Guide
**Game Services Framework - 5 Minute Setup**

## Step 1: Import Package (30 seconds)
Copy the entire `GameServicesPackage` folder into your Unity project's `Assets` folder.

## Step 2: Create ScriptableObject Assets (1 minute)
1. In Unity Project window, Right-click → Create → **Folder** → Name it "Resources/GameServices"
2. Right-click in that folder:
   - Create → **Master Registry** → Save as "MasterRegistry"
   - Create → **Service Registry** → Save as "ServiceRegistry"

## Step 3: Configure Service Registry (2 minutes)
1. Select **ServiceRegistry** asset
2. Click "+ Add Definition"
3. For Save Service:
   - Interface Type Name: `Save.ISaveService`
   - Implementation Type Name: `Save.SaveService`
   - Is Enabled: ✓ (check)

That's it for now! You can add more services later.

## Step 4: Link Master Registry (30 seconds)
1. Select **MasterRegistry** asset
2. Drag **ServiceRegistry** into the "Service Registry" field

## Step 5: Create Bootstrapper GameObject (1 minute)
1. In your first/main scene, GameObject → Create Empty
2. Name it "GameBootstrapper"
3. Add Component → Search "GameBootstrapper"
4. Drag **MasterRegistry** asset into the "Master Registry" field
5. Optional: Check "Debug Logging" to see initialization logs

## Step 6: Test It! (30 seconds)
Create a test script:

```csharp
using UnityEngine;
using Core.Services;
using Save;

public class TestServices : MonoBehaviour
{
    void Start()
    {
        var saveService = ServiceLocator.Get<ISaveService>();

        saveService.Save("test", "Hello World!");
        string result = saveService.Load("test", "");

        Debug.Log($"Loaded: {result}"); // Should print "Hello World!"
        saveService.Flush();
    }
}
```

Attach to any GameObject and press Play. If you see "Loaded: Hello World!" → Success! ✅

---

## Common First-Time Issues

### Issue: "Service of type ISaveService is not registered"
**Fix**: Make sure you added the SaveService definition to ServiceRegistry with the correct type names.

### Issue: "GameBootstrapper: MasterRegistry is not assigned!"
**Fix**: Drag MasterRegistry asset into GameBootstrapper component's field.

### Issue: "Interface type 'ISaveService' not found"
**Fix**: Use full namespace: `Save.ISaveService` and `Save.SaveService`

---

## Next Steps

1. ✅ **Add Your Own Services**
   - Create interface extending `IService`
   - Create implementation class
   - Add to ServiceRegistry
   - Access via `ServiceLocator.Get<YourInterface>()`

2. ✅ **Use Save System**
   - Save game data: `saveService.Save(key, value)`
   - Load game data: `saveService.Load<T>(key, defaultValue)`
   - Don't forget to `Flush()`!

3. ✅ **Explore Attributes**
   - Use `[ID]` for auto-generated GUIDs
   - Use `[ReadOnly]` for display-only fields

---

**Need more help?** Check the full README.md for detailed documentation!
