using System.Collections.Generic;
using UnityEngine;

namespace Core.Services
{
    /// <summary>
    /// Serves as the central aggregation root for the application's registry system.
    /// This ScriptableObject manages the configuration and injection lifecycle of all subsystem registries.
    /// </summary>
    [CreateAssetMenu(fileName = "MasterRegistry", menuName = "GameServices/Core/Master Registry")]
    public class MasterRegistrySO : ScriptableObject
    {
        [Tooltip("The core registry responsible for defining service bindings and implementation types.")]
        [SerializeField]
        private ServiceRegistrySO _serviceRegistry;

        [Tooltip("A collection of subsystem registries that require dependency injection binding prior to initialization.")]
        [SerializeField]
        private List<ScriptableObject> _injectableRegistries = new List<ScriptableObject>();

        /// <summary>
        /// Gets the reference to the primary service configuration.
        /// </summary>
        public ServiceRegistrySO ServiceRegistry => _serviceRegistry;

        /// <summary>
        /// Iterates through all registered subsystem registries and triggers their injection logic to bind dependencies.
        /// </summary>
        public void InjectAll()
        {
            foreach (var registry in _injectableRegistries)
            {
                if (registry == null)
                {
                    Debug.LogWarning("[MasterRegistry] Null registry found in list");
                    continue;
                }

                if (registry is IInjectableRegistry injectable)
                {
                    injectable.Inject();
                }
                else
                {
                    Debug.LogWarning($"[MasterRegistry] '{registry.name}' does not implement IInjectableRegistry");
                }
            }
        }

        /// <summary>
        /// Performs a comprehensive integrity check on the registry configuration, returning a list of configuration errors if any exist.
        /// </summary>
        /// <param name="errors">A list populated with error descriptions if validation fails.</param>
        /// <returns>True if the configuration is valid; otherwise, false.</returns>
        public bool ValidateAll(out List<string> errors)
        {
            errors = new List<string>();

            if (_serviceRegistry == null)
            {
                errors.Add("ServiceRegistry is not assigned");
            }

            for (int i = 0; i < _injectableRegistries.Count; i++)
            {
                var registry = _injectableRegistries[i];
                
                if (registry == null)
                {
                    errors.Add($"Injectable Registry [{i}] is null");
                    continue;
                }

                if (!(registry is IInjectableRegistry))
                {
                    errors.Add($"'{registry.name}' does not implement IInjectableRegistry");
                }
            }

            return errors.Count == 0;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Enforces type safety in the editor by automatically removing objects that do not implement <see cref="IInjectableRegistry"/>.
        /// </summary>
        private void OnValidate()
        {
            for (int i = _injectableRegistries.Count - 1; i >= 0; i--)
            {
                var item = _injectableRegistries[i];
                if (item != null && !(item is IInjectableRegistry))
                {
                    Debug.LogWarning($"MasterRegistry: Removed '{item.name}' - does not implement IInjectableRegistry");
                    _injectableRegistries.RemoveAt(i);
                }
            }
        }
#endif
    }
}