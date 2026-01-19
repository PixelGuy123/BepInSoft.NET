using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using BepInSerializer.Core.Serialization;
using BepInSerializer.Utils;
using Object = UnityEngine.Object;
using BepInSerializer.Core.Models;

namespace BepInSerializer.Patches.Serialization;

[HarmonyPatch]
internal static partial class SerializationPatcher
{
    private static readonly List<ComponentStatePair> _componentStateBuffer = new(64);
    private static readonly List<uint> _pathBuffer = new(16); // Reusable buffer to avoid allocations
    internal static int mainThreadId; // Assigned by BridgeManager

    [HarmonyTargetMethods]
    static IEnumerable<MethodInfo> GetInstantiationMethods()
    {
        return AccessTools.GetDeclaredMethods(typeof(Object))
            .Where(m => m.Name == nameof(Object.Instantiate))
            .Select(m => m.IsGenericMethodDefinition ? m.MakeGenericMethod(typeof(Object)) : m);
    }

    [HarmonyPrefix]
    [HarmonyWrapSafe]
    [HarmonyPriority(Priority.First)]
    static void Prefix_Instantiate(Object original, out InstantiateContext __state)
    {
        bool debug = BridgeManager.enableDebugLogs.Value;
        if (debug) BridgeManager.logger.LogInfo($"Prefix_Instantiate: Started for object {original?.GetType()} '{original?.name}'");

        // Thread Safety
        if (mainThreadId != System.Threading.Thread.CurrentThread.ManagedThreadId)
        {
            if (debug) BridgeManager.logger.LogWarning($"Prefix_Instantiate: Skipping - called from non-main thread (Thread ID: {System.Threading.Thread.CurrentThread.ManagedThreadId})");
            __state = null;
            return;
        }

        GameObject rootGo = original as GameObject ?? (original as Component)?.gameObject;
        if (!rootGo)
        {
            if (debug) BridgeManager.logger.LogWarning($"Prefix_Instantiate: Original object is neither GameObject nor Component with GameObject. Type: {original?.GetType()}");
            __state = null;
            return;
        }

        // Initialize Context
        __state = new InstantiateContext(rootGo);
        if (debug) BridgeManager.logger.LogInfo($"Prefix_Instantiate: Processing root GameObject '{rootGo.name}' (Active: {rootGo.activeSelf})");

        // Scan & Capture Source Hierarchy
        var transforms = rootGo.GetComponentsInChildren<Transform>(true);
        if (debug) BridgeManager.logger.LogInfo($"Prefix_Instantiate: Found {transforms.Length} transforms in hierarchy");

        foreach (var t in transforms)
        {
            uint pathHash = GetRelativePathHash(rootGo.transform, t);
            var components = t.GetComponents<Component>();

            if (debug) BridgeManager.logger.LogInfo($"Prefix_Instantiate: Transform '{t.name}' at path '{pathHash}' has {components.Length} components");

            foreach (var comp in components)
            {
                if (!comp)
                {
                    if (debug) BridgeManager.logger.LogWarning($"Prefix_Instantiate: Found null component at index {Array.IndexOf(components, comp)} on '{t.name}'");
                    continue;
                }
                Type type = comp.GetType();

                // Filtering Logic
                if (type.IsFromGameAssemblies())
                {
                    if (debug) BridgeManager.logger.LogInfo($"Prefix_Instantiate: Skipping game assembly component: {type}");
                    continue;
                }
                SerializationRegistry.Register(comp);

                // Capture Data (regardless if it has data or not, it still needs to be mapped)
                var state = ComponentSerializer.CaptureState(comp);
                if (!__state.SnapshotData.TryGetValue(pathHash, out var list))
                {
                    list = [];
                    __state.SnapshotData[pathHash] = list;
                    if (debug) BridgeManager.logger.LogInfo($"Prefix_Instantiate: Created new state list for path '{pathHash}'");
                }
                list.Add(state);
                if (debug) BridgeManager.logger.LogInfo($"Prefix_Instantiate: Captured state for {type}.");

                // Suppress Lifecycle
                LifecycleSuppressor.Suppress(type);
                __state.SuppressedTypes.Add(type);

                if (debug) BridgeManager.logger.LogInfo($"Prefix_Instantiate: Suppressed lifecycle for {type}");
            }
        }
    }

    [HarmonyWrapSafe]
    [HarmonyPostfix]
    static void Postfix_Instantiate(Object original, object __result, InstantiateContext __state)
    {
        if (__state == null || !__state.IsActive || __state.SnapshotData.Count == 0)
        {
            string reason = __state == null ? "null context" :
                       (!__state.IsActive ? "inactive context" : "empty snapshot data");
            BridgeManager.logger.LogWarning($"Postfix_Instantiate: Skipping {original?.name ?? "Unknown"} - {reason}");
            return;
        }

        GameObject resultGo = __result as GameObject ?? (__result as Component)?.gameObject;
        if (!resultGo)
        {
            BridgeManager.logger.LogError($"Postfix_Instantiate: Result is not a GameObject or Component. Type: {__result?.GetType()?.FullName}");
            CleanupContext(__state);
            return;
        }

        bool debug = BridgeManager.enableDebugLogs.Value;
        if (debug) BridgeManager.logger.LogInfo($"Postfix_Instantiate: Processing cloned GameObject '{resultGo.name}' (Active: {resultGo.activeInHierarchy})");
        // Register Root Reference
        ComponentSerializer.ClearReferences();
        ComponentSerializer.RegisterReference(resultGo, __state.OriginalRoot);
        ComponentSerializer.RegisterReference(resultGo.transform, __state.OriginalRoot.transform);

        // Clear transforms buffer
        _componentStateBuffer.Clear();

        var resultTransforms = resultGo.GetComponentsInChildren<Transform>(true);

        if (debug) BridgeManager.logger.LogInfo($"Postfix_Instantiate: Registered root references for '{resultGo.name}' -> '{__state.OriginalRoot.name}'");

        // Map & Deserialize
        foreach (var targetTrans in resultTransforms)
        {
            uint pathHash = GetRelativePathHash(resultGo.transform, targetTrans);

            // Skip if source didn't have this node or any serialized data for it
            if (!__state.SnapshotData.TryGetValue(pathHash, out var stateList))
            {
                if (debug) BridgeManager.logger.LogInfo($"Postfix_Instantiate: No snapshot data for path '{pathHash}' (Transform: '{targetTrans.name}')");
                continue;
            }

            if (debug) BridgeManager.logger.LogInfo($"Postfix_Instantiate: Found snapshot data for path '{pathHash}' with {stateList.Count} states");

            // Get the components from the cloned object
            var targetComps = targetTrans.GetComponents<Component>();

            if (debug) BridgeManager.logger.LogInfo($"Postfix_Instantiate: Transform '{targetTrans.name}' has {targetComps.Length} components");

            // Create a map to account with duplicates in the body
            var typeProgressMap = new Dictionary<Type, int>();

            for (int i = 0; i < targetComps.Length; i++)
            {
                var targetComp = targetComps[i];
                if (!targetComp)
                {
                    if (debug) BridgeManager.logger.LogWarning($"Postfix_Instantiate: Null component at index {i} on '{targetTrans.name}'");
                    continue;
                }
                Type t = targetComp.GetType();

                // Retrieve the index (if it is not found in the dictionary; we get default(int), which is always 0)
                int occurrenceIndex = typeProgressMap.GetValueSafe(t);

                // Find the Nth occurrence of this type in the captured state list
                // Because both lists are generated by GetComponents(), the order is identical.
                ComponentSerializationState matchingState = null;
                int foundCount = 0;

                for (int j = 0; j < stateList.Count; j++)
                {
                    if (stateList[j].ComponentType == t)
                    {
                        if (foundCount == occurrenceIndex)
                        {
                            matchingState = stateList[j];
                            break;
                        }
                        foundCount++;
                    }
                }

                if (matchingState != null)
                {
                    if (debug) BridgeManager.logger.LogInfo($"Postfix_Instantiate: Matched component [{matchingState.Component} -> {targetComp}]");
                    ComponentSerializer.RegisterReference(targetComp, matchingState.Component);
                    _componentStateBuffer.Add(new(targetComp, matchingState));
                    typeProgressMap[t] = occurrenceIndex + 1;
                }
                else if (debug) BridgeManager.logger.LogInfo($"Postfix_Instantiate: No matching state found for component {t} at occurrence {occurrenceIndex}");
            }
        }

        if (debug) BridgeManager.logger.LogInfo($"Postfix_Instantiate: Restoring {_componentStateBuffer.Count} component states");

        // Restore all the states after remapping everything
        for (int i = 0; i < _componentStateBuffer.Count; i++)
        {
            var entry = _componentStateBuffer[i];
            if (debug) BridgeManager.logger.LogInfo($"Postfix_Instantiate: Restoring state for {entry.Component.GetType()}");
            ComponentSerializer.RestoreState(entry.Component, entry.State);
        }

        if (debug) BridgeManager.logger.LogInfo($"Postfix_Instantiate: Cleaning up context before waking components");

        // Cleanup Suppression (Before waking)
        CleanupContext(__state);

        // Manually Wake Components (Restoring Lifecycle)
        if (debug) BridgeManager.logger.LogInfo($"Postfix_Instantiate: Waking {_componentStateBuffer.Count} components");
        for (int i = 0; i < _componentStateBuffer.Count; i++)
        {
            var comp = _componentStateBuffer[i].Component;
            Type t = comp.GetType();

            // ISerializationCallbackReceiver.OnAfterDeserialize
            if (comp is ISerializationCallbackReceiver)
            {
                if (debug) BridgeManager.logger.LogInfo($"[{t}] Manual OnAfterDeserialize");
                LifecycleSuppressor.GetMethodInvoker(t, SuppressMethod.OnAfterDeserialize)?.Invoke(comp);
            }

            // Awake & OnEnable
            if (comp is Behaviour b && resultGo.activeInHierarchy)
            {
                if (debug) BridgeManager.logger.LogInfo($"[{t}] Manual Awake");
                LifecycleSuppressor.GetMethodInvoker(t, SuppressMethod.Awake)?.Invoke(comp);

                if (b.enabled)
                {
                    if (debug) BridgeManager.logger.LogInfo($"[{t}] Manual OnEnable");
                    LifecycleSuppressor.GetMethodInvoker(t, SuppressMethod.OnEnable)?.Invoke(comp);
                }
            }
        }

        ComponentSerializer.ClearReferences();
        _componentStateBuffer.Clear();
        if (debug) BridgeManager.logger.LogInfo($"Postfix_Instantiate: Completed successfully for '{resultGo.name}'");
    }

    private static void CleanupContext(InstantiateContext context)
    {
        context.IsActive = false;
        // Remove all suppressed types
        for (int i = 0; i < context.SuppressedTypes.Count; i++) LifecycleSuppressor.Release(context.SuppressedTypes[i]);
    }

    private static uint GetRelativePathHash(Transform root, Transform target)
    {
        if (root == target) return 0; // Short token for root

        _pathBuffer.Clear();
        var current = target;

        // Traverse up and collect sibling indices
        while (current && current != root)
        {
            _pathBuffer.Add((uint)current.GetSiblingIndex());
            current = current.parent;
        }

        // Create a hash that theoretically should never have collision (Root -> Target)
        uint hash = 0;
        for (int i = _pathBuffer.Count - 1; i >= 0; i--) // Reverses here
        {
            unchecked // Allows integer overflow, which is fine for hashing
            {
                hash = (hash * 31) ^ _pathBuffer[i]; // Basic hashing algorithm
            }
        }
        return hash;
    }
}