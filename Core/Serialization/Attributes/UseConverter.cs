using BepInSerializer.Core.Serialization.Converters;
using System;
using System.Collections.Generic;

namespace BepInSerializer.Core.Serialization.Attributes;

/// <summary>
/// Explicitly tells the serializer to use a specific <see cref="FieldConverter"/> type before attempting to use from the global scope.
/// </summary>
[AttributeUsage(
    AttributeTargets.Field,
    AllowMultiple = true, // For allowing multiple Converters
    Inherited = true)]
public class UseConverter : Attribute
{
    // Store all the instances, to save memory.
    private static readonly Dictionary<Type, FieldConverter> _typeToConverterSingletons = [];
    /// <summary>
    /// The instance of <see cref="FieldConverter"/> for conversion.
    /// </summary>
    protected internal FieldConverter ConverterInstance; // Uses internal to be accessible

    /// <summary>
    /// Creates an instance of <see cref="UseConverter"/> for determined type.
    /// </summary>
    /// <param name="ConverterType">The type of the Converter to be checked for.</param>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentException"/>
    public UseConverter(Type ConverterType)
    {
        // Converter Type must NOT be null
        if (ConverterType == null) throw new ArgumentNullException(nameof(ConverterType));

        // The Converter type MUST be a Converter
        if (!typeof(FieldConverter).IsAssignableFrom(ConverterType)) throw new ArgumentException($"Type '{ConverterType.Name}' does not inherit FieldConverter.");

        // Get from cache if it exists
        if (_typeToConverterSingletons.TryGetValue(ConverterType, out ConverterInstance)) return;

        // If the constructor is null, then this is not an acceptable constructor
        if (ConverterType.GetConstructor(Type.EmptyTypes) == null) throw new ArgumentException($"Converter of type '{ConverterType.Name}' does not have a parameterless constructor.");

        // Make an instance and cache it
        ConverterInstance = (FieldConverter)Activator.CreateInstance(ConverterType);
        _typeToConverterSingletons[ConverterType] = ConverterInstance;
    }
}