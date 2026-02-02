using System;
using System.Collections.Generic;

namespace BepInSerializer.Core.Serialization.Converters.Models;

internal class CircularDependencyDetector()
{
    private readonly HashSet<object> _detectedObjects = [];
    private readonly HashSet<Type> _detectedTypes = [];

    /// <summary>
    /// Checks if the specified type has a circular dependency.
    /// If not, it registers the object as currently being processed.
    /// </summary>
    /// <param name="value">The value to check for circular dependency.</param>
    /// <param name="valueType">The type of the value to check for circular dependency.</param>
    /// <returns><see langword="true"/> if a circular dependency is detected (value already exists), otherwise <see langword="false"/>.</returns>
    public bool HasCircularDependency(object value, Type valueType)
    {
        _detectedTypes.Add(valueType);
        if (value == null) return false;
        if (valueType.IsValueType) return false; // Value types cannot have circular dependencies (they aren't referenced)

        // If Add returns false, it means the object is already in the set -> Circular Dependency
        return !_detectedObjects.Add(value);
    }

    // Whether the dependency detector has type or not
    internal bool DependencyContainsType(Type type) => _detectedTypes.Contains(type);

    // Internally unregister the object through scope manipulation
    internal void Unregister(object value, Type type)
    {
        if (value != null)
            _detectedObjects.Remove(value);
        if (type != null)
            _detectedTypes.Remove(type);
    }
}