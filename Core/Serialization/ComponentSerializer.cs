using System;
using UnityEngine;
using BepInSerializer.Utils;
using BepInSerializer.Core.Models;
using BepInSerializer.Core.Serialization.Converters.Models;
using System.Collections.Concurrent;

namespace BepInSerializer.Core.Serialization;

internal static class ComponentSerializer
{
    private static readonly ConcurrentDictionary<UnityEngine.Object, UnityEngine.Object> _referenceMap = [];

    // --- Public API ---
    public static ComponentSerializationState CaptureState(Component component)
    {
        var state = new ComponentSerializationState(component);
        var fieldInfos = SerializationRegistry.GetMappedFields(state.ComponentType);

        // If the type isn't registered, at least make it a referenceable state
        if (fieldInfos == null) return state;

        foreach (var fieldInfo in fieldInfos)
        {
            // Fast Getter
            object value = fieldInfo.CreateFieldGetter()(component);

            // Serialize
            state.Fields.Add(new SerializedFieldData(
                fieldInfo.Name,
                value
            ));
        }

        return state;
    }

    public static void RestoreState(Component target, ComponentSerializationState state)
    {
        if (state == null || state.Fields.Count == 0)
        {
            if (BridgeManager.enableDebugLogs.Value) BridgeManager.logger.LogWarning($"Failed to restore state. FieldCount: {state?.Fields.Count.ToString() ?? "Null"}");
            return;
        }

        if (BridgeManager.enableDebugLogs.Value)
            BridgeManager.logger.LogWarning($"Deserializing {target}");

        foreach (var fieldData in state.Fields)
        {
            ApplyFieldData(target, fieldData);
        }
    }

    public static void RegisterReference(UnityEngine.Object child, UnityEngine.Object parent)
    {
        if (child && parent) _referenceMap[parent] = child;
    }

    public static void ClearReferences() => _referenceMap.Clear();

    public static UnityEngine.Object GetLastChildObjectFromHierarchy(UnityEngine.Object parent)
    {
        var lastValidParent = parent;
        while (parent != null && _referenceMap.TryGetValue(parent, out var child))
        {
            parent = child;
            lastValidParent = child ?? lastValidParent;
        }
        return lastValidParent;
    }

    // --- Private Implementation ---
    private static void ApplyFieldData(Component target, SerializedFieldData data)
    {
        // Resolve Type
        Type targetType = target.GetType();

        // Resolve Field
        var fieldInfo = targetType.GetFastField(data.FieldName);
        if (fieldInfo == null)
        {
            if (BridgeManager.enableDebugLogs.Value) BridgeManager.logger.LogWarning($"{target} couldn't find field '{data.FieldName}'");
            return;
        }

        try
        {
            object valueToSet = data.FieldValue;
            valueToSet = ConversionRegistry.ConvertIfNeeded(
                FieldContext.CreatePrimaryContext(
                    fieldInfo,
                    data.FieldValue)); // Gets the parent of this component to serve as reference to the original field values

            // If the value is null, do NOT set it to null; leave the default constructor of the component do the job
            if (valueToSet != null)
                fieldInfo.CreateFieldSetter()(target, valueToSet);
        }
        catch (Exception ex)
        {
            BridgeManager.logger.LogWarning($"Deserialize Fail [{targetType.Name}.{data.FieldName}]");
            BridgeManager.logger.LogError(ex);
        }
    }
}