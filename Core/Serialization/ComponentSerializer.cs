using System;
using System.Collections.Generic;
using UnityEngine;
using BepInSerializer.Utils;
using BepInSerializer.Core.Models;
using BepInSerializer.Core.Serialization.Converters.Models;

namespace BepInSerializer.Core.Serialization;

internal static class ComponentSerializer
{
    private static readonly Dictionary<UnityEngine.Object, UnityEngine.Object> _referenceMap = [];

    // --- Public API ---

    public static ComponentSerializationState CaptureState(Component component)
    {
        var type = component.GetType();
        bool isTypeRegistered = SerializationRegistry.ComponentFieldMap.TryGetValue(type, out var fieldInfos);

        // If the component implements Unity's serialization interface, 
        // trigger it NOW so it can prepare its fields for the state
        if (component is ISerializationCallbackReceiver receiver)
        {
            try
            {
                receiver.OnBeforeSerialize(); // Suppressor doesn't catch here, so this is fine to be called
            }
            catch (Exception ex)
            {
                BridgeManager.logger.LogWarning($"OnBeforeSerialize failed for {type.Name}: {ex.Message}");
            }
        }

        var state = new ComponentSerializationState(component, type);

        // If the type isn't registered, at least make it a referenceable state
        if (!isTypeRegistered) return state;

        foreach (var fieldInfo in fieldInfos)
        {
            // Fast Getter
            object value = fieldInfo.CreateFieldGetter()(component);
            if (value == null) continue;

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
        if (state == null || state.Fields.Count == 0) return;

        foreach (var fieldData in state.Fields)
        {
            ApplyFieldData(target, fieldData);
        }
    }

    public static void RegisterReference(UnityEngine.Object child, UnityEngine.Object parent)
    {
        if (child && parent) _referenceMap[child] = parent;
    }

    public static void ClearReferences() => _referenceMap.Clear();

    // --- Private Implementation ---

    private static void ApplyFieldData(Component target, SerializedFieldData data)
    {
        // Resolve Type
        Type targetType = target.GetType();

        // Resolve Field
        var fieldInfo = targetType.GetFastField(data.FieldName);
        if (fieldInfo == null) return;

        try
        {
            object valueToSet = data.FieldValue;
            valueToSet = ConversionRegistry.ConvertIfNeeded(
                new FieldContext(
                    fieldInfo,
                    data.FieldValue)); // Gets the parent of this component to serve as reference to the original field values
            fieldInfo.CreateFieldSetter()(target, valueToSet);
        }
        catch (Exception ex)
        {
            if (BridgeManager.enableDebugLogs.Value)
            {
                BridgeManager.logger.LogWarning($"Deserialize Fail [{targetType.Name}.{data.FieldName}]");
                BridgeManager.logger.LogFatal(ex);
            }
        }
    }
}