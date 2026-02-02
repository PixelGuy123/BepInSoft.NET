using System;
using System.Collections;
using BepInSerializer.Core.Serialization.Converters.Models;

namespace BepInSerializer.Core.Serialization.Converters;

// ClassConverter (internal)
internal class ClassConverter : FieldConverter
{
    public override bool CanConvert(FieldContext context)
    {
        var type = context.ValueType;
        if (typeof(IDisposable).IsAssignableFrom(type)) return false; // There should NEVER be a disposable serializable object anywhere
        if (!type.IsClass || type.IsValueType) return false;
        if (typeof(string) != type && typeof(IEnumerable).IsAssignableFrom(type)) return false; // IEnumerable collections are not supported
        return true;
    }

    public override object Convert(FieldContext context)
    {
        // If the field has SerializeReference, nothing needs to change; the Serializer can copy the field
        if (context.ContainsSerializeReference)
            return context.OriginalValue;

        // Make convert instance
        object newConvert;

        // If the scope of the current context is available, use it for this check
        if (context.TryBeginDependencyScope(out var objectScope))
        {
            using (objectScope)
            {
                // If the original value was null, this needs to be a new instance
                if (context.OriginalValue == null)
                {
                    // In a A -> B -> null scenario, this would replace 'null' with A
                    // By checking first if the type is already known (that is, if A is known),
                    // we can tell whether B should actually make a new node or not, to prevent
                    // making new object instances indefinitely
                    if (objectScope.DoesScopeContainsType(context.PreviousValueType))
                        return null;

                    // Otherwise, just make a new object (if possible) and return it back
                    return TryConstructNewObject(context, out newConvert) ? newConvert : null;
                }
            }
        }

        // Attempt to create object through a parameterless constructor
        if (TryConstructNewObject(context, out newConvert))
        {
            // Go through each field to convert them as well
            ManageFieldsFromType(context, newConvert.GetType(), (newContext, setValue) =>
            {
                // Check dependency scope first for the field (prevents A->B->A scenarios)
                if (!newContext.TryBeginDependencyScope(out var fieldScope))
                    return;

                using (fieldScope)
                {
                    // Create new context for the field
                    newConvert = setValue(newConvert, ReConvert(newContext));
                }
            });
            return newConvert;
        }

        // If construction failed, just default to the same old value
        return null;
    }
}