using System;

namespace Core.Services
{
    /// <summary>
    /// Specifies the dependencies required by a service implementation to ensure the correct initialization order within the service locator.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class DependsOnAttribute : Attribute
    {
        /// <summary>
        /// Gets the collection of service interface types that this service depends on.
        /// </summary>
        public Type[] Dependencies { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DependsOnAttribute"/> class with the specified dependency types.
        /// </summary>
        /// <param name="dependencies">The interface types of the services required by this implementation.</param>
        public DependsOnAttribute(params Type[] dependencies)
        {
            Dependencies = dependencies ?? Array.Empty<Type>();
        }
    }
}
