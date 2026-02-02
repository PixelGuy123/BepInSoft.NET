using System;
using System.Collections;
using System.Collections.Generic;
using BepInSerializer.Core.Serialization.Converters.Models;

namespace BepInSerializer.Core.Serialization.Converters;

// DictionaryConverter
/// <summary>
/// A <see cref="FieldConverter"/> aimed to properly serialize <see cref="Dictionary{TKey, TValue}"/>.
/// </summary>
public class DictionaryConverter : FieldConverter
{
    /// <summary>
    /// Returns whether the <see cref="FieldContext"/> can be converted by this class or not.
    /// </summary>
    /// <param name="context">The context to be analyzed.</param>
    /// <returns><see langword="true"/> if the context can be converted; otherwise, <see langword="false"/>.</returns>
    public override bool CanConvert(FieldContext context)
    {
        var type = context.ValueType;
        // Make sure to get the generic definition first
        if (type.IsGenericType)
            type = type.GetGenericTypeDefinition();

        // Then, check specifically for List<>
        return typeof(Dictionary<,>) == type;
    }
    /// <summary>
    /// Converts the field using its current <see cref="FieldContext"/> available.
    /// </summary>
    /// <param name="context">The context to be used during conversion.</param>
    /// <returns>An instance of <see cref="Dictionary{TKey, TValue}"/> if successfully converted; otherwise, <see langword="null"/>.</returns>
    public override object Convert(FieldContext context)
    {
        if (context.OriginalValue is not IDictionary originalDictionary) return null;

        // Generic argument from Dictionary<TKey, TValue>
        var genericArgs = context.ValueType.GetGenericArguments();
        var genericKeyType = genericArgs[0];
        var genericValueType = genericArgs[1];

        // If the dictionary is not allowed to be populated, return null
        if (!context.ContainsAllowCollectionNesting && !CanDictionaryBeRecursivelyPopulated(genericKeyType, genericValueType))
            return null;

        // Make a new list (object)
        if (TryConstructNewObject(context, out var newObject))
        {
            // Failsafe to be an actual list
            if (newObject is not IDictionary newDictionary) return null;


            // Copy the original items to this new list, by using ReConvert
            foreach (DictionaryEntry kvp in originalDictionary)
            {
                // Convert key
                var newKey = ReConvert(FieldContext.CreateRemoteContext(context, kvp.Key, genericKeyType));
                // If the key is null, don't add it back to the dictionary
                if (newKey == null)
                {
                    if (BridgeManager.enableDebugLogs.Value) BridgeManager.logger.LogWarning($"[DictionaryConverter] Dictionary Key from ('{originalDictionary}') is null. Removing entry.");
                    continue;
                }
                // Convert value
                var newValue = ReConvert(FieldContext.CreateRemoteContext(context, kvp.Value, genericValueType));

                // Make a remote context for each item and copy back to this new list
                newDictionary.Add(newKey, newValue);
            }
            return newDictionary;
        }

        // If no list has been given, return null
        return null;
    }

    /// <summary>
    /// Whether the element types given are supported by the <see cref="Dictionary{TKey, TValue}"/>.
    /// </summary>
    /// <param name="keyElementType">The <see cref="Type"/> of the keys.</param>
    /// <param name="valueElementType">The <see cref="Type"/> of the values.</param>
    /// <returns><see langword="true"/> if both types are accepted by the dictionary; otherwise, <see langword="false"/></returns>
    protected virtual bool CanDictionaryBeRecursivelyPopulated(Type keyElementType, Type valueElementType) =>
        (keyElementType == typeof(string) || !typeof(IEnumerable).IsAssignableFrom(keyElementType)) &&
        (valueElementType == typeof(string) || !typeof(IEnumerable).IsAssignableFrom(valueElementType)); // If the type is not an IEnumerable (except strings), continue
}