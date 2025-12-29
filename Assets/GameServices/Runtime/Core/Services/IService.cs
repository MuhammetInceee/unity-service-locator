namespace Core.Services
{
    /// <summary>
    /// Defines the core contract for all managed game services, enforcing a standardized lifecycle for initialization, suspension, and termination.
    /// </summary>
    public interface IService
    {
        /// <summary>
        /// Executes the initialization routine for the service.
        /// This method is invoked by the bootstrapper after the service has been successfully registered and its dependencies resolved.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Performs necessary cleanup operations, such as unsubscribing from events and releasing managed resources, when the service is destroyed.
        /// </summary>
        void Dispose();

        /// <summary>
        /// Handles logic required when the application enters a paused state (e.g., app backgrounding on mobile).
        /// Ideal for triggering immediate data persistence or pausing active timers.
        /// </summary>
        void OnApplicationPause();

        /// <summary>
        /// Executes final teardown procedures immediately before the application terminates.
        /// </summary>
        void OnApplicationQuit();
    }
}
