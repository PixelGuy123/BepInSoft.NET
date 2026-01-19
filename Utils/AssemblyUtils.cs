using System.Reflection;
using System.IO;
using System;
using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using BepInSerializer.Core.Models;

namespace BepInSerializer.Utils;

internal static class AssemblyUtils
{
    internal static bool _cacheIsAvailable = false;
    // Structs for the cache
    internal record struct TypeDepthItem(Type Type, int Depth);
    // Cache itself
    internal static LRUCache<Assembly, bool> TypeIsManagedCache;
    internal static LRUCache<Assembly, bool> TypeIsUnityManagedCache;
    internal static LRUCache<TypeDepthItem, List<Type>> CollectionNestedElementTypesCache;

    public static bool IsFromGameAssemblies(this Type type)
    {
        // Obviously the handler shouldn't be accounted at all
        var assembly = type.Assembly;

        if (typeof(BepInEx.BaseUnityPlugin).IsAssignableFrom(type)) // Never a plugin
            return true;

        return assembly.IsGameAssembly();
    }

    public static bool IsGameAssembly(this Assembly assembly)
    {
        // if the assembly is already known, return the value
        if (TypeIsManagedCache.NullableTryGetValue(assembly, out var box))
            return box;

        // If the dll is not from BepInEx/Plugins, this gotta be a managed assembly file
        bool isManaged = !assembly.TryGetAssemblyDirectoryName(out string dirName) ||
                 !dirName.Contains($"BepInEx{Path.DirectorySeparatorChar}plugins");

        if (_cacheIsAvailable)
            TypeIsManagedCache.NullableAdd(assembly, isManaged);
        return isManaged;
    }

    public static bool IsUnityAssembly(this Assembly assembly)
    {
        // if the assembly is already known, return the value
        if (TypeIsUnityManagedCache.NullableTryGetValue(assembly, out var box))
            return box;

        // If the dll is not from BepInEx/Plugins, this gotta be a managed assembly file
        bool isInManagedFolder = !assembly.TryGetAssemblyDirectoryName(out string dirName) || !dirName.Contains($"BepInEx{Path.DirectorySeparatorChar}plugins");
        if (!isInManagedFolder)
        {
            TypeIsUnityManagedCache.NullableAdd(assembly, false);
            return false;
        }

        // If this is a non assembly-csharp, it must be an Unity dll
        bool isNotAssemblyCsharp = !Path.GetFileName(assembly.Location).StartsWith("Assembly-CSharp");
        if (isNotAssemblyCsharp)
        {
            TypeIsUnityManagedCache.NullableAdd(assembly, true);
            return true;
        }

        // Otherwise, this is inside Csharp, so return false
        TypeIsUnityManagedCache.NullableAdd(assembly, false);
        return false;
    }

    public static bool CanUnitySerialize(this Type type)
    {
        // First, what it CAN serialize by default
        if (type.IsPrimitive || type == typeof(string) || type.IsEnum) return true;

        // If it's assignable here, then it can go
        if (typeof(UnityEngine.Object).IsAssignableFrom(type)) return true;

        // Exactly what the plugin aims to fix: classes and value types that aren't from the assembly not being serialized
        if (!type.IsStandardCollection())
        {
            if (typeof(IEnumerable).IsAssignableFrom(type)) return false; // Other IEnumerable collections are not supported directly by Unity
            return (type.IsClass || type.IsValueType) && type.IsFromGameAssemblies();
        }

        // Assumes it's a standard collection that Unity could serialize
        var elementTypes = type.GetTypesFromArray(1); // Check only a single depth
        return elementTypes[0].CanUnitySerialize(); // Check the single element type
    }

    public static bool IsUnityComponentType(this Type type)
    {
        var elementTypes = type.GetTypesFromArray();
        return elementTypes.Contains(typeof(GameObject)) || elementTypes.Exists(typeof(Component).IsAssignableFrom);
    }

    // Expect the most basic collection types to be checked, not IEnumerable in general
    public static bool IsStandardCollection(this Type t, bool includeDictionaries = false) =>
    t.IsArray ||
    typeof(IList).IsAssignableFrom(t) ||
    (includeDictionaries && typeof(IDictionary).IsAssignableFrom(t));

    public static List<Type> GetTypesFromArray(this Type collectionType, int layersToCheck = -1)
    {
        // Normalize the minimum layer boundaries
        if (layersToCheck <= 0)
            layersToCheck = -1;

        var typeDepthItem = new TypeDepthItem(collectionType, layersToCheck);
        if (CollectionNestedElementTypesCache.NullableTryGetValue(typeDepthItem, out var results)) return results;

        // Initial capacity guess to reduce resizing
        results = [];
        GetTypesFromArrayInternal(collectionType, layersToCheck, 0, results);

        // Cache
        CollectionNestedElementTypesCache.NullableAdd(typeDepthItem, results);
        return results;
    }

    private static void GetTypesFromArrayInternal(this Type collectionType, int layersToCheck, int currentLayer, List<Type> results)
    {
        // Depth Guard
        if (layersToCheck > 0 && currentLayer >= layersToCheck)
        {
            results.Add(collectionType);
            return;
        }

        // Is it a collection?
        if (!collectionType.IsStandardCollection(includeDictionaries: true))
        {
            results.Add(collectionType);
            return;
        }

        // Handle Generics (List<T>, Dictionary<TKey, TValue>)
        if (collectionType.IsGenericType)
        {
            Type[] genericArguments = collectionType.GetGenericArguments();
            for (int i = 0; i < genericArguments.Length; i++)
            {
                GetTypesFromArrayInternal(genericArguments[i], layersToCheck, currentLayer + 1, results);
            }
            return;
        }

        // Handle Arrays
        if (collectionType.IsArray)
        {
            Type elementType = collectionType.GetElementType();
            if (elementType != null)
            {
                GetTypesFromArrayInternal(elementType, layersToCheck, currentLayer + 1, results);
            }
            return;
        }

        results.Add(collectionType);
    }
}