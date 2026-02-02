using System.Collections.Generic;
using BepInSerializer.Core.Serialization.Converters;
using BepInSerializer.Core.Serialization.Converters.Models;

namespace BepInSerializer.Core.Serialization;

/// <summary>
/// Registry for field converters used during serialization and deserialization.
/// </summary>
public static class ConversionRegistry
{
    // Converters List
    private static readonly List<FieldConverter> _convertersToUse = [
        new ClassConverter(), // Default converter for classes
        new StringConverter(), // Default converter for strings
        new StructConverter(), // Default converter for structs
        new PseudoStructConverter(), // Default converter for Unity "pseudo-struct" classes (AnimationCurve, Gradient...)
        new UnityObjectConverter(), // Default converter for Unity objects (Component, MonoBehaviour, GameObject...)
        new ArrayConverter(), // Default converter for arrays
        new ListConverter(), // Default converter for List<>
    ];

    // ----- PUBLIC API -----

    /// <summary>
    /// Registers a new field converter to the conversion registry.
    /// </summary>
    /// <param name="converter">The converter to be registered.</param>
    /// <remarks>Converters registered later have higher priority during conversion.</remarks>
    public static void RegisterConverter(FieldConverter converter) => _convertersToUse.Add(converter);

    // ----- Private/Internal API -----
    internal static object ConvertIfNeeded(FieldContext context)
    {
        bool debug = BridgeManager.enableDebugLogs.Value;

        // Prioritized conversion loop
        for (int i = 0; i < context.PreAvailableConverters.Count; i++)
        {
            var converter = context.PreAvailableConverters[i];
            if (converter.CanConvert(context))
                return TryConverter(converter);
        }

        // Normal conversion loop
        for (int i = _convertersToUse.Count - 1; i >= 0; i--)
        {
            var converter = _convertersToUse[i];
            if (converter.CanConvert(context))
                return TryConverter(converter);
        }

        return null;

        object TryConverter(FieldConverter converter)
        {
            if (debug) BridgeManager.logger.LogInfo((context.PreviousValueType == null ? "## " : string.Empty) +
                $"Converter {converter.GetType().Name} will convert type {context.ValueType}.");
            var result = converter.Convert(context);
            if (debug) BridgeManager.logger.LogInfo((context.PreviousValueType == null ? "## " : string.Empty) +
            $"Converter {converter.GetType().Name} finished conversion and returned {result}.");
            return result;

        }
    }
}