using System.Collections;
using System.Collections.Generic;
using BepInSerializer.Core.Serialization.Converters.Models;

namespace BepInSerializer.Core.Serialization.Converters;

// StructConverter (internal)
internal class StructConverter : FieldConverter
{
    public override bool CanConvert(FieldContext context)
    {
        var type = context.ValueType;
        if (type.IsClass || !type.IsValueType) return false;
        if (typeof(IEnumerable).IsAssignableFrom(type)) return false; // IEnumerable collections are not supported
        return true;
    }

    public override object Convert(FieldContext context)
    {
        // If the field has SerializeReference, nothing needs to change; the Serializer can copy the field
        if (context.ValueType.IsPrimitive || context.ContainsSerializeReference)
            return context.OriginalValue;

        // If the original value was null, this is a nullable struct; just return null
        if (context.OriginalValue == null)
            return null;

        // Attempt to create object through a parameterless constructor
        if (TryConstructNewObject(context, out var newConvert))
        {
            // Go through each field to convert them as well
            ManageFieldsFromType(context, newConvert.GetType(), (newContext, setFieldValue) =>
            {
                // Create new context for the field
                // Important: set again the newConvert, to update the struct value here
                newConvert = setFieldValue(newConvert, ReConvert(newContext));
            });
            return newConvert;
        }

        // If construction failed, just default to the same old value
        return context.OriginalValue;
    }
}