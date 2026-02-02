using System;
using System.Collections;
using System.Collections.Generic;
using BepInSerializer.Core.Serialization.Converters.Models;

namespace BepInSerializer.Core.Serialization.Converters;

// ListConverter (internal)
internal class ListConverter : FieldConverter
{
    public override bool CanConvert(FieldContext context)
    {
        var type = context.ValueType;
        // Make sure to get the generic definition first
        if (type.IsGenericType)
            type = type.GetGenericTypeDefinition();

        // Then, check specifically for List<>
        return typeof(List<>) == type;
    }

    public override object Convert(FieldContext context)
    {
        if (context.OriginalValue is not IList originalList) return null;

        // Generic argument from List<T>
        var genericType = context.ValueType.GetGenericArguments()[0];

        // If the array is not allowed to be populated, return null
        if (!context.ContainsAllowCollectionNesting && !CanListBeRecursivelyPopulated(genericType))
            return null;

        // Make a new list (object)
        if (TryConstructNewObject(context, out var newObject))
        {
            // Failsafe to be an actual list
            if (newObject is not IList newList) return null;


            // Copy the original items to this new list, by using ReConvert
            for (int i = 0; i < originalList.Count; i++)
            {
                // Make a remote context for each item and copy back to this new list
                newList.Add(ReConvert(FieldContext.CreateRemoteContext(context, originalList[i], genericType)));
            }
            return newList;
        }

        // If no list has been given, return null
        return null;
    }

    // Whether it can be converted or not based on elementType
    protected virtual bool CanListBeRecursivelyPopulated(Type elementType) =>
        elementType == typeof(string) || !typeof(IEnumerable).IsAssignableFrom(elementType); // If the type is not an IEnumerable (except strings), continue
}