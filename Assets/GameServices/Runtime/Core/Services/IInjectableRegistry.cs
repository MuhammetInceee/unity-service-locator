namespace Core.Services
{
    /// <summary>
    /// Defines a contract for ScriptableObject registries that require dependency injection prior to the initialization of the service layer.
    /// Implementing this interface ensures the registry is bound to its corresponding static service manager during the bootstrap phase.
    /// </summary>
    public interface IInjectableRegistry
    {
        /// <summary>
        /// Executes the injection logic to bind this registry instance to its target service or manager class.
        /// </summary>
        void Inject();

        /// <summary>
        /// Gets the human-readable identifier of the registry, primarily used for debugging and logging operations.
        /// </summary>
        string RegistryName { get; }
    }
}
