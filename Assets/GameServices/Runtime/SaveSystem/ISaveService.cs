using Core.Services;

namespace Save
{
    /// <summary>
    /// Defines the abstract contract for the application's data persistence layer.
    /// Decouples high-level game logic from specific storage implementations (e.g., FileSystem, PlayerPrefs, Cloud), facilitating modularity and testing.
    /// </summary>
    public interface ISaveService : IService
    {
        /// <summary>
        /// Writes a strongly typed value to the volatile memory buffer associated with the specified key.
        /// Note: Data is not written to physical storage until <see cref="Flush"/> is invoked.
        /// </summary>
        /// <typeparam name="T">The type of the data to serialize.</typeparam>
        /// <param name="key">The unique string identifier for the data entry.</param>
        /// <param name="value">The data object to be stored.</param>
        void Save<T>(string key, T value);

        /// <summary>
        /// Retrieves a deserialized value associated with the specified key.
        /// Returns the provided fallback value if the key does not exist or deserialization fails.
        /// </summary>
        /// <typeparam name="T">The expected type of the data.</typeparam>
        /// <param name="key">The unique string identifier to lookup.</param>
        /// <param name="defaultValue">The value to return if the key is missing.</param>
        /// <returns>The stored value or the fallback default.</returns>
        T Load<T>(string key, T defaultValue);

        /// <summary>
        /// Retrieves a deserialized value associated with the specified key.
        /// Returns the default value of type <typeparamref name="T"/> (e.g., null for reference types, 0 for integers) if the key is missing.
        /// </summary>
        /// <typeparam name="T">The expected type of the data.</typeparam>
        /// <param name="key">The unique string identifier to lookup.</param>
        /// <returns>The stored value or the type's default value.</returns>
        T Load<T>(string key);

        /// <summary>
        /// Determines whether a specific data entry exists within the persistent store identified by the given key.
        /// </summary>
        /// <param name="key">The unique identifier to check.</param>
        /// <returns>True if the key exists; otherwise, false.</returns>
        bool HasKey(string key);

        /// <summary>
        /// Removes the specific data entry and its associated value from the registry.
        /// </summary>
        /// <param name="key">The unique identifier of the entry to delete.</param>
        void DeleteKey(string key);

        /// <summary>
        /// Purges all persistent data managed by this service.
        /// This operation is destructive and cannot be undone.
        /// </summary>
        void DeleteAll();

        /// <summary>
        /// Forces the immediate synchronization of the in-memory buffer with the physical storage medium.
        /// Crucial for ensuring data integrity during application pauses, backgrounding, or shutdown sequences.
        /// </summary>
        void Flush();
    }
}
