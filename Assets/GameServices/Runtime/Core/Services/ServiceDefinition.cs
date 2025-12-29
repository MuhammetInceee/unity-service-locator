using System;
using UnityEngine;

namespace Core.Services
{
    /// <summary>
    /// Represents a serializable configuration entry used to bind service interfaces to their concrete implementations.
    /// Utilizes reflection to dynamically resolve types at runtime based on their fully qualified names.
    /// </summary>
    [Serializable]
    public class ServiceDefinition
    {
        [Tooltip("The fully qualified assembly name of the service interface (e.g., 'YourNamespace.ISaveService').")]
        [SerializeField]
        private string _interfaceTypeName;

        [Tooltip("The fully qualified assembly name of the concrete class implementing the interface.")]
        [SerializeField]
        private string _implementationTypeName;

        [Tooltip("Determines whether this service definition is active and should be processed during initialization.")]
        [SerializeField]
        private bool _isEnabled = true;

        /// <summary>
        /// Gets the raw string representation of the interface type name.
        /// </summary>
        public string InterfaceTypeName => _interfaceTypeName;

        /// <summary>
        /// Gets the raw string representation of the implementation type name.
        /// </summary>
        public string ImplementationTypeName => _implementationTypeName;

        /// <summary>
        /// Gets a value indicating whether this service is currently enabled in the registry.
        /// </summary>
        public bool IsEnabled => _isEnabled;

        private Type _cachedInterfaceType;
        private Type _cachedImplementationType;

        /// <summary>
        /// Resolves and returns the runtime <see cref="Type"/> for the interface, caching the result to minimize reflection overhead.
        /// </summary>
        public Type InterfaceType
        {
            get
            {
                if (_cachedInterfaceType == null && !string.IsNullOrEmpty(_interfaceTypeName))
                {
                    _cachedInterfaceType = Type.GetType(_interfaceTypeName);
                }
                return _cachedInterfaceType;
            }
        }

        /// <summary>
        /// Resolves and returns the runtime <see cref="Type"/> for the implementation class, caching the result to minimize reflection overhead.
        /// </summary>
        public Type ImplementationType
        {
            get
            {
                if (_cachedImplementationType == null && !string.IsNullOrEmpty(_implementationTypeName))
                {
                    _cachedImplementationType = Type.GetType(_implementationTypeName);
                }
                return _cachedImplementationType;
            }
        }

        public ServiceDefinition() { }

        public ServiceDefinition(string interfaceTypeName, string implementationTypeName, bool isEnabled = true)
        {
            _interfaceTypeName = interfaceTypeName;
            _implementationTypeName = implementationTypeName;
            _isEnabled = isEnabled;
        }

        /// <summary>
        /// Performs a comprehensive integrity check to ensure type names are valid, types exist in the assembly, and inheritance constraints are met.
        /// </summary>
        /// <param name="error">Output parameter containing a descriptive error message if validation fails.</param>
        /// <returns>True if the definition is valid and safe to instantiate; otherwise, false.</returns>
        public bool Validate(out string error)
        {
            if (string.IsNullOrEmpty(_interfaceTypeName))
            {
                error = "Interface type name is empty";
                return false;
            }

            if (string.IsNullOrEmpty(_implementationTypeName))
            {
                error = "Implementation type name is empty";
                return false;
            }

            if (InterfaceType == null)
            {
                error = $"Interface type '{_interfaceTypeName}' not found";
                return false;
            }

            if (ImplementationType == null)
            {
                error = $"Implementation type '{_implementationTypeName}' not found";
                return false;
            }

            if (!InterfaceType.IsInterface)
            {
                error = $"'{_interfaceTypeName}' is not an interface";
                return false;
            }

            if (!InterfaceType.IsAssignableFrom(ImplementationType))
            {
                error = $"'{_implementationTypeName}' does not implement '{_interfaceTypeName}'";
                return false;
            }

            if (!typeof(IService).IsAssignableFrom(ImplementationType))
            {
                error = $"'{_implementationTypeName}' does not implement IService";
                return false;
            }

            error = null;
            return true;
        }
    }
}
