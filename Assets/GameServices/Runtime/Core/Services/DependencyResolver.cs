using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Core.Services
{
    /// <summary>
    /// Utilities for resolving the initialization order of services based on their dependencies using topological sorting.
    /// </summary>
    public static class DependencyResolver
    {
        /// <summary>
        /// Sorts a list of service definitions to ensure dependencies are initialized before their dependents.
        /// </summary>
        /// <param name="definitions">The collection of service definitions to process.</param>
        /// <returns>A new list of definitions sorted by initialization order.</returns>
        /// <exception cref="InvalidOperationException">Thrown when a circular dependency is detected within the service graph.</exception>
        public static List<ServiceDefinition> ResolveDependencies(IReadOnlyList<ServiceDefinition> definitions)
        {
            var enabledDefinitions = definitions.Where(d => d.IsEnabled).ToList();

            var interfaceToDefinition = new Dictionary<Type, ServiceDefinition>();
            foreach (var def in enabledDefinitions)
            {
                if (def.InterfaceType != null)
                {
                    interfaceToDefinition[def.InterfaceType] = def;
                }
            }

            var dependencies = new Dictionary<ServiceDefinition, List<ServiceDefinition>>();
            foreach (var def in enabledDefinitions)
            {
                dependencies[def] = GetDependencies(def, interfaceToDefinition);
            }

            return TopologicalSort(enabledDefinitions, dependencies);
        }

        /// <summary>
        /// Identifies direct dependencies for a specific service definition by inspecting the <see cref="DependsOnAttribute"/>.
        /// </summary>
        private static List<ServiceDefinition> GetDependencies(
            ServiceDefinition definition,
            Dictionary<Type, ServiceDefinition> interfaceToDefinition)
        {
            var result = new List<ServiceDefinition>();

            if (definition.ImplementationType == null)
                return result;

            var attribute = definition.ImplementationType.GetCustomAttribute<DependsOnAttribute>();
            if (attribute == null)
                return result;

            foreach (var dependencyType in attribute.Dependencies)
            {
                if (interfaceToDefinition.TryGetValue(dependencyType, out var dependencyDef))
                {
                    result.Add(dependencyDef);
                }
                else
                {
                    UnityEngine.Debug.LogWarning(
                        $"Service '{definition.ImplementationType.Name}' depends on '{dependencyType.Name}' " +
                        "which is not registered in ServiceRegistry.");
                }
            }

            return result;
        }

        /// <summary>
        /// Executes the topological sort algorithm on the generated dependency graph.
        /// </summary>
        private static List<ServiceDefinition> TopologicalSort(
            List<ServiceDefinition> definitions,
            Dictionary<ServiceDefinition, List<ServiceDefinition>> dependencies)
        {
            var result = new List<ServiceDefinition>();
            var visited = new HashSet<ServiceDefinition>();
            var visiting = new HashSet<ServiceDefinition>();

            foreach (var def in definitions)
            {
                if (!visited.Contains(def))
                {
                    Visit(def, dependencies, visited, visiting, result);
                }
            }

            return result;
        }

        /// <summary>
        /// Recursively visits nodes in the graph to build the sorted list and checks for circular references.
        /// </summary>
        private static void Visit(
            ServiceDefinition definition,
            Dictionary<ServiceDefinition, List<ServiceDefinition>> dependencies,
            HashSet<ServiceDefinition> visited,
            HashSet<ServiceDefinition> visiting,
            List<ServiceDefinition> result)
        {
            if (visiting.Contains(definition))
            {
                throw new InvalidOperationException(
                    $"Circular dependency detected involving service '{definition.ImplementationType?.Name ?? "Unknown"}'. " +
                    "Check your [DependsOn] attributes for circular references.");
            }

            if (visited.Contains(definition))
                return;

            visiting.Add(definition);

            if (dependencies.TryGetValue(definition, out var deps))
            {
                foreach (var dep in deps)
                {
                    Visit(dep, dependencies, visited, visiting, result);
                }
            }

            visiting.Remove(definition);
            visited.Add(definition);
            result.Add(definition);
        }
    }
}
