using System.Collections.Generic;

namespace BepInSerializer.Core.Serialization.Converters.Models;

internal class CircularDependencyDetector
{
    private readonly HashSet<object> _detectedObjects = [];
    private object _lastAddedValue;

    /// <summary>
    /// Checks if the specified type has a circular dependency.
    /// </summary>
    /// <param name="value">The value to check for circular dependency.</param>
    /// <returns><see langword="true"/> if a circular dependency is detected, otherwise <see langword="false"/>.</returns>
    public bool HasCircularDependency(object value)
    {
        if (value == null) return false;

        var type = value.GetType();
        if (type.IsValueType) return false; // Value types cannot have circular dependencies (they aren't referenced)

        if (!_detectedObjects.Add(value))
            return true; // Circular dependency detected
        _lastAddedValue = value;
        return false;
    }

    /// <summary>
    /// Removes the last value added to this detector.
    /// </summary>
    internal void RemoveLastValue() => _detectedObjects.Remove(_lastAddedValue);
}