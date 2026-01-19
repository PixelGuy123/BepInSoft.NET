using System;
using System.Collections;
using System.Collections.Generic;
using BepInSerializer.Core.Serialization.Converters.Models;
using BepInSerializer.Utils;

namespace BepInSerializer.Core.Serialization.Converters;

/// <summary>
/// Base class for all field converters used in the serialization system.
/// </summary>
public abstract class FieldConverter
{
    // ----- Public API -----
    /// <summary>
    /// Delegate for setting a field's value (using optimized caching).
    /// </summary>
    /// <param name="fieldHolder">The object that holds this field.</param>
    /// <param name="fieldValue">The value for the field.</param>
    public delegate void SetFieldValue(object fieldHolder, object fieldValue);
    /// <summary>
    /// Determines whether this converter can convert the given field context.
    /// </summary>
    /// <param name="context">The context to be evaluated.</param>
    /// <returns><see langword="true"/> if the converter can handle the context; otherwise, <see langword="false"/>.</returns>
    public abstract bool CanConvert(FieldContext context);
    /// <summary>
    /// Converts the field based on the provided context and Converter type.
    /// </summary>
    /// <param name="context">The context to be converted.</param>
    /// <returns>A value of the field in the expected conversion.</returns>
    public abstract object Convert(FieldContext context);
    // ----- Protected API -----
    /// <summary>
    /// Takes the object back to the conversion system to be converted by another <see cref="FieldConverter"/> and returns back the converted value.
    /// </summary>
    /// <param name="context">The active context of this conversion.</param>
    /// <returns>An instance of a converted object.</returns>
    protected object ReConvert(FieldContext context) => ConversionRegistry.ConvertIfNeeded(context);

    /// <summary>
    /// Gets the generic types defined in the object (assuming it is a collection).
    /// </summary>
    /// <param name="toConvert">The object (collection) to be searched.</param>
    /// <returns>If the object passed is a collection, a list of all generic parameters, defined in the object, is returned; otherwise, a null list is returned.</returns>
    protected List<Type> GetTypesFromCollection(object toConvert)
    {
        if (toConvert is not IEnumerable) return null;
        return toConvert.GetType().GetTypesFromArray(1); // Allowed only a single depth. More depth is technically not needed for a simple converter
    }

    /// <summary>
    /// Attempt to construct the object using its default constructor (if there is one).
    /// </summary>
    /// <param name="context">The context to be aware of the type of the object.</param>
    /// <param name="newConvert">The object constructed from the original. May be null if the method returns false.</param>
    /// <returns><see langword="true"/> if the construction was a success; otherwise, <see langword="false"/>.</returns>
    protected bool TryConstructNewObject(FieldContext context, out object newConvert)
    {
        var constructor = context.Field.FieldType.GetParameterlessConstructor();
        if (constructor != null)
        {
            newConvert = constructor();
            return true;
        }
        newConvert = null;
        return false;
    }

    /// <summary>
    /// Manages the fields from a given type by applying a specified action on each field's context.
    /// </summary>
    /// <param name="context">The FieldContext to be used for managing fields.</param>
    /// <param name="type">The type whose fields are to be managed.</param>
    /// <param name="fieldConverterAction">The action to be applied on each field's context.</param>
    protected void ManageFieldsFromType(FieldContext context, Type type, Action<FieldContext, SetFieldValue> fieldConverterAction) =>
        ManageFieldsFromType(context, true, type, fieldConverterAction);
    /// <summary>
    /// Manages the fields from a given type by applying a specified action on each field's context.
    /// </summary>
    /// <param name="context">The context to be used for managing fields.</param>
    /// <param name="acceptUnityTypes">Whether to accept Unity-specific types during field retrieval.</param>
    /// <param name="type">The type whose fields are to be managed.</param>
    /// <param name="fieldConverterAction">The action to be applied on each field's context.</param>
    protected void ManageFieldsFromType(FieldContext context, bool acceptUnityTypes, Type type, Action<FieldContext, SetFieldValue> fieldConverterAction)
    {
        // Scan the fields from the type
        var fields = type.GetSerializableFieldInfos(acceptUnityTypes);
        // Go through each field to convert them as well
        foreach (var field in fields)
        {
            // Create a new context to update dependency tracking
            var newContext = new FieldContext(context, field);

            // Run the custom converter action
            fieldConverterAction(newContext, (fieldHolder, fieldValue) => field.CreateFieldSetter()(fieldHolder, fieldValue));
        }
    }
}