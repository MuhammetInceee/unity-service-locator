using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core.Services
{
    /// <summary>
    /// Manages the application initialization lifecycle, ensuring all core services are instantiated, 
    /// injected, and initialized in the correct dependency order. 
    /// This component persists across scene loads to maintain global state.
    /// </summary>
    public class GameBootstrapper : MonoBehaviour
    {
        [Tooltip("The primary registry configuration containing references to all subsystem registries required for initialization.")]
        [SerializeField]
        private MasterRegistrySO _masterRegistry;

        [Tooltip("Enables verbose console logging for the bootstrap process and service lifecycle events.")]
        [SerializeField]
        private bool _debugLogging = false;

        private static bool _hasInitialized = false;
        private readonly List<IService> _initializedServices = new List<IService>();

        /// <summary>
        /// Initializes the singleton instance, validates the registry configuration, and triggers the service injection sequence.
        /// </summary>
        private void Awake()
        {
            if (_hasInitialized)
            {
                Log("Bootstrapper already initialized, destroying duplicate.");
                Destroy(gameObject);
                return;
            }

            _hasInitialized = true;
            DontDestroyOnLoad(gameObject);

            if (_masterRegistry == null)
            {
                Debug.LogError("GameBootstrapper: MasterRegistry is not assigned!");
                return;
            }

            if (!_masterRegistry.ValidateAll(out var errors))
            {
                foreach (var error in errors)
                {
                    Debug.LogError($"GameBootstrapper: {error}");
                }
                return;
            }

            _masterRegistry.InjectAll();

            InitializeServices();
            SubscribeToLifecycleEvents();
        }

        /// <summary>
        /// Orchestrates the validation, topological sorting, registration, and initialization of all defined services.
        /// </summary>
        private void InitializeServices()
        {
            var serviceRegistry = _masterRegistry.ServiceRegistry;
            
            if (serviceRegistry == null)
            {
                Debug.LogError("GameBootstrapper: ServiceRegistry is not assigned in MasterRegistry!");
                return;
            }

            if (!serviceRegistry.ValidateAll(out var errors))
            {
                foreach (var error in errors)
                {
                    Debug.LogError($"GameBootstrapper: {error}");
                }
                return;
            }

            List<ServiceDefinition> sortedDefinitions;
            try
            {
                sortedDefinitions = DependencyResolver.ResolveDependencies(serviceRegistry.ServiceDefinitions);
            }
            catch (InvalidOperationException e)
            {
                Debug.LogError($"GameBootstrapper: {e.Message}");
                return;
            }

            foreach (var definition in sortedDefinitions)
            {
                try
                {
                    RegisterService(definition);
                }
                catch (Exception e)
                {
                    Debug.LogError($"GameBootstrapper: Failed to register service '{definition.ImplementationTypeName}': {e.Message}");
                }
            }

            foreach (var service in _initializedServices)
            {
                try
                {
                    Log($"Initializing service: {service.GetType().Name}");
                    service.Initialize();
                }
                catch (Exception e)
                {
                    Debug.LogError($"GameBootstrapper: Failed to initialize service '{service.GetType().Name}': {e.Message}");
                }
            }

            ServiceLocator.MarkInitialized();
            Log($"All services initialized. Total: {_initializedServices.Count}");
        }

        /// <summary>
        /// Instantiates a service implementation and registers it with the global <see cref="ServiceLocator"/> using reflection.
        /// </summary>
        /// <param name="definition">The service definition containing type information.</param>
        private void RegisterService(ServiceDefinition definition)
        {
            var interfaceType = definition.InterfaceType;
            var implementationType = definition.ImplementationType;

            var instance = Activator.CreateInstance(implementationType) as IService;
            if (instance == null)
            {
                throw new InvalidOperationException(
                    $"Failed to create instance of '{implementationType.Name}' as IService");
            }

            var registerMethod = typeof(ServiceLocator)
                .GetMethod("Register", BindingFlags.Public | BindingFlags.Static)
                ?.MakeGenericMethod(interfaceType);

            registerMethod.Invoke(null, new object[] { instance });

            _initializedServices.Add(instance);
            Log($"Registered service: {interfaceType.Name} -> {implementationType.Name}");
        }

        private void SubscribeToLifecycleEvents()
        {
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private void OnSceneUnloaded(Scene scene)
        {
            Log($"Scene unloaded: {scene.name}, triggering lifecycle event.");
            OnBeforeSceneChange();
        }

        /// <summary>
        /// Invoked immediately before a scene change occurs. intended to be overridden for custom cleanup logic.
        /// </summary>
        protected virtual void OnBeforeSceneChange()
        {
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                Log("Application paused, notifying services.");
                NotifyApplicationPause();
            }
        }

        private void OnApplicationQuit()
        {
            Log("Application quitting, disposing services.");
            DisposeServices();
        }

        /// <summary>
        /// Propagates the pause state to all registered services, allowing them to suspend operations or trigger save routines.
        /// </summary>
        protected virtual void NotifyApplicationPause()
        {
            foreach (var service in _initializedServices)
            {
                try
                {
                    service.OnApplicationPause();
                }
                catch (Exception e)
                {
                    Debug.LogError($"GameBootstrapper: Error during OnApplicationPause for {service.GetType().Name}: {e.Message}");
                }
            }
        }

        /// <summary>
        /// Performs a controlled shutdown of all services in reverse initialization order to ensure safe resource cleanup.
        /// </summary>
        private void DisposeServices()
        {
            foreach (var service in _initializedServices)
            {
                try
                {
                    service.OnApplicationQuit();
                }
                catch (Exception e)
                {
                    Debug.LogError($"GameBootstrapper: Error during OnApplicationQuit for {service.GetType().Name}: {e.Message}");
                }
            }

            for (int i = _initializedServices.Count - 1; i >= 0; i--)
            {
                try
                {
                    Log($"Disposing service: {_initializedServices[i].GetType().Name}");
                    _initializedServices[i].Dispose();
                }
                catch (Exception e)
                {
                    Debug.LogError($"GameBootstrapper: Error disposing service: {e.Message}");
                }
            }

            ServiceLocator.Clear();
            _initializedServices.Clear();
        }

        private void Log(string message)
        {
            if (_debugLogging)
            {
                Debug.Log($"[Bootstrapper] {message}");
            }
        }

        private void OnDestroy()
        {
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }
    }
}