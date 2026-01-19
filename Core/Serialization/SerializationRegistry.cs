using System.Collections.Generic;
using System;
using HarmonyLib;
using UnityEngine;
using BepInSerializer.Utils;
using BepInSerializer.Core.Models;
using System.Reflection;

namespace BepInSerializer.Core.Serialization;

internal static class SerializationRegistry
{
    // Weak table uses WeakReferences; if one key from Type is nulled out, this cache will release it too
    // The StrongBox is just to hold the boolean (struct) into a reference type wrapper (object)
    internal static LRUCache<Type, bool> _cachedRootTypes;
    internal readonly static Dictionary<Type, List<FieldInfo>> ComponentFieldMap = [];

    // ------ Public API ------
    public static bool Register(Component component)
    {
        var componentType = component.GetType();
        bool cacheIsAvailable = _cachedRootTypes != null; // Doesn't use Nullable extension methods because there are many cases this boolean is used for
        if (cacheIsAvailable && _cachedRootTypes.TryGetValue(componentType, out var worth))
        {
            if (BridgeManager.enableDebugLogs.Value)
                BridgeManager.logger.LogInfo($"===== Cached Root ({componentType}) is Worth? ({worth}) =====");
            return worth;
        }
        if (componentType.IsFromGameAssemblies())
        {
            if (BridgeManager.enableDebugLogs.Value)
                BridgeManager.logger.LogInfo($"===== Refused Game Assembly Root ({componentType}) =====");
            _cachedRootTypes.Add(componentType, false);
            return false;
        }

        bool isWorth = false;

        if (BridgeManager.enableDebugLogs.Value)
            BridgeManager.logger.LogInfo($"===== Registering Root ({componentType}) =====");

        // Start recursive scan
        // Path is initially empty because we are at the component root
        Type currentScanType = componentType;

        // Traverse up the inheritance chain to find all declared fields
        // There are two iterations here: Find base classes of the component AND find base classes of the fields inside these parents.
        while (currentScanType != null &&
               !currentScanType.IsFromGameAssemblies())
        {
            if (BridgeManager.enableDebugLogs.Value && currentScanType != componentType)
                BridgeManager.logger.LogInfo($"===== Checking Sub-Root ({currentScanType.FullName}) =====");
            // currentScanType changes to inspect fields of the base classes.
            isWorth |= ScanComponent(currentScanType);

            // Add to the cache if possible to this branch
            if (cacheIsAvailable && !_cachedRootTypes.ContainsKey(currentScanType))
                _cachedRootTypes.Add(currentScanType, isWorth);

            if (BridgeManager.enableDebugLogs.Value)
            {
                BridgeManager.logger.LogInfo($"===== ATTEMPT TO REGISTER {currentScanType} | Worth: {isWorth} =====");
            }
            currentScanType = currentScanType.BaseType;
        }

        // Then, update the base type to know if it was really worth or not
        if (cacheIsAvailable && currentScanType != componentType)
        {
            if (BridgeManager.enableDebugLogs.Value)
            {
                BridgeManager.logger.LogInfo($"-> Updated ComponentType Cache!");
            }
            _cachedRootTypes[componentType] = isWorth;
        }

        return isWorth;
    }


    // ---- Private Logic ----
    private static bool ScanComponent(Type rootComponentType)
    {
        // Cache fields for this specific type call
        var fields = rootComponentType.GetSerializableFieldInfos();
        bool isDebugEnabled = BridgeManager.enableDebugLogs.Value;
        bool modifiedField = fields.Count != 0;

        for (int i = 0; i < fields.Count; i++)
        {
            var field = fields[i];
            // Register Target
            if (ComponentFieldMap.TryGetValue(rootComponentType, out var infos))
                infos.Add(field);
            else
                ComponentFieldMap[rootComponentType] = [field];

            if (isDebugEnabled)
            {
                BridgeManager.logger.LogInfo($"Registered: {field.DeclaringType.Name}.{field.Name} -> {field.FieldType}");
            }
        }

        return modifiedField;
    }
}