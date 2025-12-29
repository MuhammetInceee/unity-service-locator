using System.Collections.Generic;
using UnityEngine;

namespace Core.Services
{
    /// <summary>
    /// Acts as the configuration manifest for the application's service layer.
    /// This ScriptableObject defines the catalog of services to be instantiated, validated, and registered by the bootstrapper.
    /// </summary>
    [CreateAssetMenu(fileName = "ServiceRegistry", menuName = "GameServices/Core/Service Registry")]
    public class ServiceRegistrySO : ScriptableObject
    {
        [Tooltip("The collection of service definitions mapping interfaces to their concrete implementations for game startup.")]
        [SerializeField]
        private List<ServiceDefinition> _serviceDefinitions = new List<ServiceDefinition>();

        /// <summary>
        /// Gets the read-only collection of configured service definitions available for registration.
        /// </summary>
        public IReadOnlyList<ServiceDefinition> ServiceDefinitions => _serviceDefinitions;

        /// <summary>
        /// Performs a comprehensive integrity check on all enabled service definitions to ensure type validity and configuration correctness.
        /// </summary>
        /// <param name="errors">A list populated with descriptive error messages for any invalid definitions found.</param>
        /// <returns>True if all enabled definitions are valid; otherwise, false.</returns>
        public bool ValidateAll(out List<string> errors)
        {
            errors = new List<string>();

            for (int i = 0; i < _serviceDefinitions.Count; i++)
            {
                var definition = _serviceDefinitions[i];
                
                if (!definition.IsEnabled)
                    continue;

                if (!definition.Validate(out string error))
                {
                    errors.Add($"Service [{i}]: {error}");
                }
            }

            return errors.Count == 0;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Programmatically appends a new service definition to the registry.
        /// Intended for use by custom editors or automation tools.
        /// </summary>
        /// <param name="definition">The service definition to add.</param>
        public void AddDefinition(ServiceDefinition definition)
        {
            _serviceDefinitions.Add(definition);
            UnityEditor.EditorUtility.SetDirty(this);
        }

        /// <summary>
        /// Removes all entries from the registry, effectively resetting the service configuration.
        /// </summary>
        public void ClearDefinitions()
        {
            _serviceDefinitions.Clear();
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}