using System;
using System.Collections.Generic;

namespace Core.Services
{
    /// <summary>
    /// Provides a centralized, static access point for retrieving registered game services globally.
    /// Implements the Service Locator pattern to decouple clients from concrete service implementations, facilitating easier testing and modularity.
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, IService> _services = new Dictionary<Type, IService>();
        private static bool _isInitialized;

        /// <summary>
        /// Gets a value indicating whether the service bootstrap sequence has successfully completed and services are ready for consumption.
        /// </summary>
        public static bool IsInitialized => _isInitialized;

        /// <summary>
        /// Registers a concrete service instance against a specific interface contract.
        /// Enforces a strict single-instance policy per interface type to maintain singleton-like behavior.
        /// </summary>
        /// <typeparam name="TInterface">The interface type that defines the service contract.</typeparam>
        /// <param name="service">The concrete implementation instance to register.</param>
        /// <exception cref="InvalidOperationException">Thrown if a service is already registered for the specified interface type.</exception>
        public static void Register<TInterface>(IService service) where TInterface : class, IService
        {
            var type = typeof(TInterface);

            if (!_services.TryAdd(type, service))
            {
                throw new InvalidOperationException(
                    $"Service of type {type.Name} is already registered. " +
                    "Unregister it first before registering a new instance.");
            }
        }

        /// <summary>
        /// Retrieves the registered service instance for the specified interface type.
        /// This is the primary access method for inter-service communication.
        /// </summary>
        /// <typeparam name="TInterface">The interface type to resolve.</typeparam>
        /// <returns>The registered service instance.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the requested service has not been registered, indicating a configuration error or initialization order issue.</exception>
        public static TInterface Get<TInterface>() where TInterface : class, IService
        {
            var type = typeof(TInterface);

            if (!_services.TryGetValue(type, out var service))
            {
                throw new InvalidOperationException(
                    $"Service of type {type.Name} is not registered. " +
                    "Make sure it's added to ServiceRegistrySO and the Bootstrapper has run.");
            }

            return (TInterface)service;
        }

        /// <summary>
        /// Attempts to retrieve a registered service without throwing an exception if the service is missing.
        /// Useful for handling optional dependencies safely.
        /// </summary>
        /// <typeparam name="TInterface">The interface type to resolve.</typeparam>
        /// <param name="service">Output parameter containing the service instance if found; otherwise, null.</param>
        /// <returns>True if the service was successfully found; otherwise, false.</returns>
        public static bool TryGet<TInterface>(out TInterface service) where TInterface : class, IService
        {
            var type = typeof(TInterface);

            if (_services.TryGetValue(type, out var foundService))
            {
                service = (TInterface)foundService;
                return true;
            }

            service = null;
            return false;
        }

        /// <summary>
        /// Removes the service associated with the specified interface type from the registry.
        /// </summary>
        /// <typeparam name="TInterface">The interface type to unregister.</typeparam>
        /// <returns>True if the service was found and removed; otherwise, false.</returns>
        public static bool Unregister<TInterface>() where TInterface : class, IService
        {
            var type = typeof(TInterface);
            return _services.Remove(type);
        }

        /// <summary>
        /// Determines whether a service implementing the specified interface is currently registered.
        /// </summary>
        /// <typeparam name="TInterface">The interface type to check.</typeparam>
        /// <returns>True if the service exists in the registry; otherwise, false.</returns>
        public static bool HasService<TInterface>() where TInterface : class, IService
        {
            return _services.ContainsKey(typeof(TInterface));
        }

        /// <summary>
        /// Flags the locator as fully initialized.
        /// Intended to be called exclusively by the GameBootstrapper upon completion of the startup sequence.
        /// </summary>
        internal static void MarkInitialized()
        {
            _isInitialized = true;
        }

        /// <summary>
        /// Disposes all registered services and clears the internal registry.
        /// Critical for resetting the game state or cleaning up resources between test runs.
        /// </summary>
        public static void Clear()
        {
            foreach (var service in _services.Values)
            {
                try
                {
                    service.Dispose();
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"Error disposing service: {e.Message}");
                }
            }

            _services.Clear();
            _isInitialized = false;
        }
    }
}
