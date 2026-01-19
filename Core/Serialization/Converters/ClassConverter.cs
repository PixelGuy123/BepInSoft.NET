using System.Collections;
using BepInSerializer.Core.Serialization.Converters.Models;

namespace BepInSerializer.Core.Serialization.Converters;

// ClassConverter (internal)
internal class ClassConverter : FieldConverter
{
    public override bool CanConvert(FieldContext context)
    {
        var type = context.Field.FieldType;
        if (!type.IsClass || type.IsValueType) return false;
        if (typeof(IEnumerable).IsAssignableFrom(type)) return false; // IEnumerable collections are not supported
        return true;
    }

    public override object Convert(FieldContext context)
    {
        bool debug = BridgeManager.enableDebugLogs.Value;
        // If the field has SerializeReference, nothing needs to change; the Serializer can copy the field
        if (context.ContainsSerializeReference)
        {
            if (debug) BridgeManager.logger.LogInfo($"[SERIALIZE REFERENCE] Getting original value for field ({context.Field.Name}) of type {context.Field.FieldType}. Owner: {context.Field.DeclaringType}");
            return context.OriginalValue;
        }

        // Attempt to create object through a parameterless constructor
        if (TryConstructNewObject(context, out var newConvert))
        {
            // If the original value was null, this needs to be a new instance
            if (context.OriginalValue == null)
            {
                if (debug) BridgeManager.logger.LogInfo($"Getting NULLABLE original value for field ({context.Field.Name}) of type {context.Field.FieldType}. Owner: {context.Field.DeclaringType}");
                // According to Unity serialization rules, since it doesn't have serialize reference, this should be a default class created
                return newConvert;
            }

            // Go through each field to convert them as well
            ManageFieldsFromType(context, newConvert.GetType(), (newContext, setFieldValue) =>
            {
                // Check for circular dependency
                if (newContext.HasCircularDependency)
                    return;

                // Create new context for the field
                setFieldValue(newConvert, ReConvert(newContext));
            });

            return newConvert;
        }

        // If construction failed, just default to the same old value
        return context.OriginalValue;
    }
}