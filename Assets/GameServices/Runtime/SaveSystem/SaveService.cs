using System;
using System.Collections.Generic;
using Core.Services;
using UnityEngine;

namespace Save
{
    /// <summary>
    /// Concrete implementation of the persistence layer utilizing Unity's <see cref="PlayerPrefs"/> backend.
    /// Implements a write-through buffer strategy to minimize expensive disk I/O operations, requiring explicit <see cref="Flush"/> calls to persist data.
    /// </summary>
    public class SaveService : ISaveService
    {
        private readonly Dictionary<string, string> _buffer = new Dictionary<string, string>();
        private readonly HashSet<string> _deletedKeys = new HashSet<string>();
        private bool _isDirty = false;

        /// <summary>
        /// Prepares the service for operation.
        /// No explicit initialization is required for the PlayerPrefs backend, but this method fulfills the <see cref="IService"/> contract.
        /// </summary>
        public void Initialize()
        {
            // No initialization needed for PlayerPrefs-based implementation
        }

        /// <summary>
        /// Performs a final synchronization of buffered data to disk and clears internal memory buffers upon service destruction.
        /// </summary>
        public void Dispose()
        {
            Flush();
            _buffer.Clear();
            _deletedKeys.Clear();
        }

        /// <summary>
        /// Triggered when the application pauses; forces an immediate flush to ensure data integrity during background transitions.
        /// </summary>
        public void OnApplicationPause()
        {
            Flush();
        }

        /// <summary>
        /// Triggered when the application quits; forces a final commit of all pending changes to the physical storage.
        /// </summary>
        public void OnApplicationQuit()
        {
            Flush();
        }

        /// <summary>
        /// Serializes and stores a value in the in-memory write buffer.
        /// Marks the service as 'dirty' but does not trigger a disk write until <see cref="Flush"/> is called.
        /// </summary>
        /// <typeparam name="T">The type of the value to store.</typeparam>
        /// <param name="key">The unique identifier for the data.</param>
        /// <param name="value">The data to serialize.</param>
        public void Save<T>(string key, T value)
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogError("SaveService: Cannot save with null or empty key");
                return;
            }

            string serializedValue = SerializeValue(value);
            _buffer[key] = serializedValue;
            _deletedKeys.Remove(key);
            _isDirty = true;
        }

        /// <summary>
        /// Retrieves a value by checking the memory buffer first, then falling back to persistent storage.
        /// Returns the specified default value if the key is missing or marked for deletion.
        /// </summary>
        /// <typeparam name="T">The expected type of the data.</typeparam>
        /// <param name="key">The identifier to lookup.</param>
        /// <param name="defaultValue">The fallback value.</param>
        /// <returns>The deserialized value or default.</returns>
        public T Load<T>(string key, T defaultValue)
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogError("SaveService: Cannot load with null or empty key");
                return defaultValue;
            }

            if (_deletedKeys.Contains(key))
            {
                return defaultValue;
            }

            if (_buffer.TryGetValue(key, out string bufferedValue))
            {
                return DeserializeValue<T>(bufferedValue, defaultValue);
            }

            if (!PlayerPrefs.HasKey(key))
            {
                return defaultValue;
            }

            string storedValue = PlayerPrefs.GetString(key);
            return DeserializeValue<T>(storedValue, defaultValue);
        }

        /// <summary>
        /// Retrieves a value using the type's default (e.g., null, 0, false) as the fallback.
        /// </summary>
        public T Load<T>(string key)
        {
            return Load<T>(key, default);
        }

        /// <summary>
        /// Determines if a key exists in either the active write buffer or the persistent storage, accounting for pending deletions.
        /// </summary>
        public bool HasKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            if (_deletedKeys.Contains(key))
            {
                return false;
            }

            return _buffer.ContainsKey(key) || PlayerPrefs.HasKey(key);
        }

        /// <summary>
        /// Marks a specific key for deletion in the next flush cycle and removes it from the active buffer immediately.
        /// </summary>
        public void DeleteKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogError("SaveService: Cannot delete with null or empty key");
                return;
            }

            _buffer.Remove(key);
            _deletedKeys.Add(key);
            _isDirty = true;
        }

        /// <summary>
        /// Wipes all data from both the internal memory buffer and the underlying PlayerPrefs storage.
        /// </summary>
        public void DeleteAll()
        {
            _buffer.Clear();
            _deletedKeys.Clear();
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            _isDirty = false;
        }

        /// <summary>
        /// Commits all buffered changes (writes and deletions) to the disk and executes <see cref="PlayerPrefs.Save"/>.
        /// Operation is skipped if no changes have been made since the last flush.
        /// </summary>
        public void Flush()
        {
            if (!_isDirty)
            {
                return;
            }

            foreach (var key in _deletedKeys)
            {
                PlayerPrefs.DeleteKey(key);
            }

            _deletedKeys.Clear();

            foreach (var kvp in _buffer)
            {
                PlayerPrefs.SetString(kvp.Key, kvp.Value);
            }

            PlayerPrefs.Save();
            _isDirty = false;
        }

        /// <summary>
        /// Converts a strongly-typed value into a string representation.
        /// Uses optimized `ToString()` for primitives and `JsonUtility` for complex objects.
        /// </summary>
        private string SerializeValue<T>(T value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            var type = typeof(T);

            if (type == typeof(string))
            {
                return value as string;
            }

            if (type == typeof(int))
            {
                return value.ToString();
            }

            if (type == typeof(long))
            {
                return value.ToString();
            }

            if (type == typeof(float))
            {
                return value.ToString();
            }

            if (type == typeof(bool))
            {
                return value.ToString();
            }

            return JsonUtility.ToJson(value);
        }

        /// <summary>
        /// Reconstructs the original type from a string representation.
        /// Handles parsing for primitives and JSON deserialization for complex objects.
        /// </summary>
        private T DeserializeValue<T>(string serializedValue, T defaultValue)
        {
            if (string.IsNullOrEmpty(serializedValue))
            {
                return defaultValue;
            }

            try
            {
                var type = typeof(T);

                if (type == typeof(string))
                {
                    return (T)(object)serializedValue;
                }

                if (type == typeof(int))
                {
                    if (int.TryParse(serializedValue, out int intResult))
                    {
                        return (T)(object)intResult;
                    }

                    return defaultValue;
                }

                if (type == typeof(long))
                {
                    if (long.TryParse(serializedValue, out long longResult))
                    {
                        return (T)(object)longResult;
                    }

                    return defaultValue;
                }

                if (type == typeof(float))
                {
                    if (float.TryParse(serializedValue, out float floatResult))
                    {
                        return (T)(object)floatResult;
                    }

                    return defaultValue;
                }

                if (type == typeof(bool))
                {
                    if (bool.TryParse(serializedValue, out bool boolResult))
                    {
                        return (T)(object)boolResult;
                    }

                    return defaultValue;
                }

                return JsonUtility.FromJson<T>(serializedValue);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"SaveService: Failed to deserialize value for type {typeof(T).Name}: {e.Message}");
                return defaultValue;
            }
        }
    }
}
