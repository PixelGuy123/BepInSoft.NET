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
        bool isManaged = IsTypeFromManagedSource(assembly);

        if (_cacheIsAvailable)
            TypeIsManagedCache.NullableAdd(assembly, isManaged);
        return isManaged;
    }

    public static bool IsUnityAssembly(this Assembly assembly)
    {
        if (assembly == null) return false;
        if (TypeIsUnityManagedCache.NullableTryGetValue(assembly, out var box))
            return box;

        string fullName = assembly.FullName;

        // Use some known .NET types to prevent System types first
        if (fullName.StartsWith("mscorlib") ||
            fullName.StartsWith("System") ||
            fullName.StartsWith("Microsoft") ||
            fullName.StartsWith("netstandard"))
        {
            TypeIsUnityManagedCache.NullableAdd(assembly, false);
            return false;
        }

        // Check if it's a UnityEngine core assembly
        bool isUnity = fullName.StartsWith("UnityEngine") || fullName.StartsWith("UnityEditor");

        // If it's not a Unity DLL and not Assembly-CSharp, it's likely a 3rd party lib or plugin
        if (!isUnity)
        {
            // BepInEx check to exclude mod types
            if (!IsTypeFromManagedSource(assembly))
            {
                TypeIsUnityManagedCache.NullableAdd(assembly, false);
                return false;
            }
        }

        TypeIsUnityManagedCache.NullableAdd(assembly, isUnity);
        return isUnity;
    }

    public static bool CanUnitySerialize(this Type type)
    {
        // Primitive types Unity always supports
        if (type.IsPrimitive || type == typeof(string) || type.IsEnum)
            return true;

        // Check if it's a standard collection, check its members (first depth). The first depth should NOT be a secondary collection (nesting is not supported)
        if (type.IsStandardCollection())
            return type.GetTypesFromArray(1).TrueForAll(t => !typeof(ICollection).IsAssignableFrom(t) && t.CanUnitySerialize());

        // Types from game assemblies or Unity types
        if (type.IsFromGameAssemblies() || typeof(UnityEngine.Object).IsAssignableFrom(type))
            return true;

        // Component types
        return type.IsUnityComponentType();
    }

    public static bool IsUnityComponentType(this Type type)
    {
        // If it's a collection, try to get the element types
        if (type.IsStandardCollection())
        {
            var elementTypes = type.GetTypesFromArray();
            return elementTypes.TrueForAll(t => t == typeof(GameObject) || typeof(Component).IsAssignableFrom(t));
        }
        return type == typeof(GameObject) || typeof(Component).IsAssignableFrom(type);
    }

    // Expect the most basic collection types to be checked, not IEnumerable in general
    public static bool IsStandardCollection(this Type t, bool includeDictionaries = false) =>
    t.IsArray ||
    (t.IsGenericType && (t.GetGenericTypeDefinition() == typeof(List<>) ||
    (includeDictionaries && t.GetGenericTypeDefinition() == typeof(Dictionary<,>))));

    public static List<Type> GetTypesFromArray(this Type collectionType, int layersToCheck = -1)
    {
        // Normalize the minimum layer boundaries
        layersToCheck = layersToCheck <= 0 ? -1 : layersToCheck;

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

    private static bool IsTypeFromManagedSource(Assembly assembly)
    {
        // Shared logic for determining if assembly is from managed (non-plugin) source
        return !assembly.TryGetAssemblyDirectoryName(out string dirName) ||
               !dirName.Contains($"BepInEx{Path.DirectorySeparatorChar}plugins");
    }
}