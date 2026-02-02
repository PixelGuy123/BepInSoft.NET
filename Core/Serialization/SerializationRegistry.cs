using System.Collections.Generic;
using System;
using UnityEngine;
using BepInSerializer.Utils;
using BepInSerializer.Core.Models;
using System.Reflection;
using HarmonyLib;

namespace BepInSerializer.Core.Serialization;

internal static class SerializationRegistry
{
    private readonly static HashSet<Type> _cachedRootTypes = [];
    private readonly static Dictionary<Type, List<FieldInfo>> ComponentFieldMap = [];
    private readonly static Dictionary<Type, FieldInfo> ComponentCallbackReceiverBlockageMap = [];
    internal static LRUCache<Type, List<FieldInfo>> LongHierarchyComponentFieldMap; // Uses LRUCache to be a temporary cache for long-hierarchy components

    // ------ Public API ------
    public static void Register(Component component)
    {
        var componentType = component.GetType();
        if (_cachedRootTypes.Contains(componentType))
        {
            if (BridgeManager.enableDebugLogs.Value)
                BridgeManager.logger.LogInfo($"===== Cached Root ({componentType}) =====");
            return;
        }
        if (componentType.IsFromGameAssemblies())
        {
            if (BridgeManager.enableDebugLogs.Value)
                BridgeManager.logger.LogInfo($"===== Refused Game Assembly Root ({componentType}) =====");
            _cachedRootTypes.Add(componentType);
            return;
        }

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
            ScanComponent(currentScanType);

            // Add to the cache if possible to this branch
            _cachedRootTypes.Add(currentScanType);

            if (BridgeManager.enableDebugLogs.Value)
            {
                BridgeManager.logger.LogInfo($"===== ATTEMPT TO REGISTER {currentScanType} =====");
            }
            currentScanType = currentScanType.BaseType;
        }

        // Then, update the base type to know if it was really worth or not
        if (currentScanType != componentType)
        {
            if (BridgeManager.enableDebugLogs.Value)
            {
                BridgeManager.logger.LogInfo($"-> Updated ComponentType Cache!");
            }
            _cachedRootTypes.Add(componentType);
        }
    }

    public static List<FieldInfo> GetMappedFields(Type componentType)
    {
        if (LongHierarchyComponentFieldMap.NullableTryGetValue(componentType, out var cachedLongFieldInfos))
            return cachedLongFieldInfos;

        List<FieldInfo> fieldInfos = new(8);
        // Try to get mapped fields from the first ever type found in the hierarchy
        Type currentScanType = componentType;
        while (currentScanType != null &&
               !currentScanType.IsFromGameAssemblies())
        {
            if (ComponentFieldMap.TryGetValue(currentScanType, out var cachedFieldInfos))
                fieldInfos.AddRange(cachedFieldInfos);
            currentScanType = currentScanType.BaseType;
        }

        LongHierarchyComponentFieldMap.NullableAdd(componentType, fieldInfos);
        return fieldInfos.Count != 0 ? fieldInfos : null;
    }

    public static FieldInfo TryGetReceiverBlockageField(Type componentType) => ComponentCallbackReceiverBlockageMap.GetValueSafe(componentType);


    // ---- Private Logic ----
    private static void ScanComponent(Type rootComponentType)
    {
        // Cache fields for this specific type call
        var fields = rootComponentType.GetUnserializableFieldInfos();
        bool isDebugEnabled = BridgeManager.enableDebugLogs.Value;

        for (int i = 0; i < fields.Count; i++)
        {
            var field = fields[i];
            // === General Field Mapping ===
            if (ComponentFieldMap.TryGetValue(rootComponentType, out var infos))
                infos.Add(field);
            else
                ComponentFieldMap[rootComponentType] = new List<FieldInfo>(fields.Count) { field }; // Make the capacity of the whole collection

            if (isDebugEnabled) BridgeManager.logger.LogInfo($"Registered: {field.DeclaringType.Name}.{field.Name} -> {field.FieldType}");
        }

        // === Specific Field Mapping ===
        // -- ISerializationCallbackReceiver blockage flag --
        RegisterSpecialField(ConstantStorage.CALLBACK_RECEIVER_BLOCKAGE_FIELD_NAME, (f) => f.FieldType == typeof(bool), ComponentCallbackReceiverBlockageMap);
        return;

        void RegisterSpecialField(string fieldName, Func<FieldInfo, bool> fieldPredicate, Dictionary<Type, FieldInfo> toMapField)
        {
            var field = rootComponentType.GetFastField(fieldName);
            if (field != null && fieldPredicate(field))
                toMapField[rootComponentType] = field;
        }
    }
}