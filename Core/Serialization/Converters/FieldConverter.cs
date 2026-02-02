using System;
using System.Runtime.Serialization;
using BepInSerializer.Core.Serialization.Converters.Models;
using BepInSerializer.Utils;
using UnityEngine;

namespace BepInSerializer.Core.Serialization.Converters;

/// <summary>
/// Base class for all field Converters used in the serialization system.
/// </summary>
public abstract class FieldConverter
{
    // ----- Public API -----
    /// <summary>
    /// Delegate for setting a value (using optimized caching) and that returns back the modified value (for the case of structs).
    /// </summary>
    /// <param name="obj">The object to set the data into.</param>
    /// <param name="value">The new value to be inserted.</param>
    public delegate object SetValue(object obj, object value);
    /// <summary>
    /// Determines whether this Converter can convert the given field context.
    /// </summary>
    /// <param name="context">The context to be evaluated.</param>
    /// <returns><see langword="true"/> if the Converter can handle the context; otherwise, <see langword="false"/>.</returns>
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

    // ----- RETRIEVING METHODS -----
    /// <summary>
    /// Tries to get the mapped <see cref="UnityEngine.Object"/> from an internal hierarchy map.
    /// </summary>
    /// <param name="context">The context to be aware of the object.</param>
    /// <param name="mappedObject">The mapped object, if successful.</param>
    /// <returns><see langword="true"/> if the mapping was successful; otherwise, <see langword="false"/>.</returns>
    protected bool TryGetMappedUnityObject(FieldContext context, out UnityEngine.Object mappedObject)
    {
        if (context.OriginalValue is UnityEngine.Object unityObj)
        {
            mappedObject = ComponentSerializer.GetLastChildObjectFromHierarchy(unityObj);
            return true;
        }
        mappedObject = null;
        return false;
    }

    /// <summary>
    /// Attempt to construct the object using its default constructor (if there is one).
    /// </summary>
    /// <param name="context">The context to be aware of the type of the object.</param>
    /// <param name="newConvert">The object constructed from the original, if successful.</param>
    /// <returns><see langword="true"/> if the construction was a success; otherwise, <see langword="false"/>.</returns>
    protected bool TryConstructNewObject(FieldContext context, out object newConvert)
    {
        var constructor = context.ValueType.GetParameterlessConstructor();
        // Try the normal constructor
        if (constructor != null)
        {
            newConvert = constructor();
            return true;
        }
        // If this is not value type, we can try an uninitialized object
        if (!context.ValueType.IsValueType)
        {
            try
            {
                // Make an uninitialized object to be filled up
                newConvert = FormatterServices.GetUninitializedObject(context.ValueType);
                return true;
            }
            catch { }
        }
        newConvert = null;
        return false;
    }

    /// <summary>
    /// Attempts to copy the object using a self-activator (if available).
    /// </summary>
    /// <param name="context">The context to be aware of the type of the object.</param>
    /// <param name="newConvert">The copied object, if successful.</param>
    /// <returns><see langword="true"/> if the copying was a success; otherwise, <see langword="false"/>.</returns>
    /// <remarks>This method does not support <see cref="Component"/> or <see cref="GameObject"/> types.</remarks>
    protected bool TryCopyNewObject(FieldContext context, out object newConvert)
    {
        // If this is a component, return false
        if (context.ValueType.IsUnityComponentType())
        {
            newConvert = null;
            return false;
        }
        var uniObj = context.OriginalValue as UnityEngine.Object;
        // If this is not an UnityEngine.Object, return false
        if (uniObj == null)
        {
            newConvert = null;
            return false;
        }

        // Try to get an instance of this object
        if (!context.ValueType.TryGetSelfActivator(out var constructor))
        {
            // Last effort attempt
            newConvert = UnityEngine.Object.Instantiate(uniObj);
            return newConvert != null;
        }

        newConvert = constructor(context.OriginalValue);
        return true;
    }
    /// <summary>
    /// Attempts to construct a new array based on the provided lengths for each dimension.
    /// </summary>
    /// <param name="context">The context to be aware of the type of the array.</param>
    /// <param name="lengths">The lengths of each dimension of the array to be constructed.</param>
    /// <param name="newArray">The newly constructed array, if successful.</param>
    /// <returns><see langword="true"/> if the construction was a success; otherwise, <see langword="false"/>.</returns>
    protected bool TryConstructNewArray(FieldContext context, int[] lengths, out Array newArray)
    {
        var elementType = context.ValueType.GetElementType();
        if (elementType != null)
        {
            newArray = elementType.GetArrayConstructor(lengths.Length)(lengths);
            return true;
        }
        newArray = null;
        return false;
    }
    // ----- CALL BACK METHODS -----
    /// <summary>
    /// Manages the fields from a given type by applying a specified action on each field's context.
    /// </summary>
    /// <param name="context">The context to be used for managing fields.</param>
    /// <param name="type">The type whose fields are to be managed.</param>
    /// <param name="fieldConverterAction">The action to be applied on each field's context.</param>
    protected void ManageFieldsFromType(FieldContext context, Type type, Action<FieldContext, SetValue> fieldConverterAction) =>
        // Go through each field to convert them as well
        type.GetUnserializableFieldInfos().ForEach(
            (field) => fieldConverterAction(
                FieldContext.CreateSubContext(context, field),
                (fieldHolder, fieldValue) => field.CreateFieldSetter()(fieldHolder, fieldValue)
            ));

    /// <summary>
    /// Manages the properties from a given type by applying a specified action on each property's context.
    /// </summary>
    /// <param name="context">The context to be used for managing properties.</param>
    /// <param name="type">The type whose properties are to be managed.</param>
    /// <param name="propertyConverterAction">The action to be applied on each property's context.</param>
    protected void ManagePropertiesFromType(FieldContext context, Type type, Action<FieldContext, SetValue> propertyConverterAction) =>
        // Go through each field to convert them as well
        type.GetUnserializablePropertyInfos().ForEach(
            (property) => propertyConverterAction(
                FieldContext.CreateSubContext(context, property),
                (obj, value) => property.CreatePropertySetter()(obj, value)
            ));
    // ----- HELPER CHECKS -----
    /// <summary>
    /// Determines whether the specified type belongs to a Unity assembly.
    /// </summary>
    /// <param name="type">The type to be checked.</param>
    /// <returns><see langword="true"/> if the type belongs to a Unity assembly; otherwise, <see langword="false"/>.</returns>
    protected bool IsUnityAssembly(Type type) => type.Assembly.IsUnityAssembly();
}