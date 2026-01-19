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
        new ClassConverter(), // The first means the default converter for classes
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
        // Reverse for statement for going from user-defined to default converters
        for (int i = _convertersToUse.Count - 1; i >= 0; i--)
        {
            var converter = _convertersToUse[i];
            if (converter.CanConvert(context))
            {
                if (debug) BridgeManager.logger.LogInfo($"[CONVERSION] Converting object ({context.Field.Name}) with ({converter.GetType()})");
                return converter.Convert(context);
            }
        }


        if (debug) BridgeManager.logger.LogInfo($"[CONVERSION] No converter found for field {context.Field.Name} of type {context.Field.FieldType}. Owner: {context.Field.DeclaringType}");
        return context.OriginalValue;
    }
}