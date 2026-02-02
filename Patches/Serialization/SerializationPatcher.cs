using BepInSerializer.Core.Models;
using BepInSerializer.Core.Serialization;
using BepInSerializer.Core.Serialization.Interfaces;
using BepInSerializer.Utils;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

using Object = UnityEngine.Object;

namespace BepInSerializer.Patches.Serialization;

[HarmonyPatch]
internal static partial class SerializationPatcher
{
    private record struct ObjectActivity(bool ActiveSelf, bool ActiveInHierarchy, InstantiateContext Context);
    private static readonly LRUCache<GameObject, PrecomputedHierarchyTransformOrder> PrecomputedGOHierarchy = new(128); // It doesn't need to store so many precomputed prefabs
    internal static int mainThreadId; // Assigned by BridgeManager

    [HarmonyTargetMethods]
    static IEnumerable<MethodInfo> GetInstantiationMethods()
    {
        return AccessTools.GetDeclaredMethods(typeof(Object))
            .Where(m => m.Name == nameof(Object.Internal_CloneSingle) || m.Name == nameof(Object.Internal_CloneSingleWithParent));
        //.Select(m => m.IsGenericMethodDefinition ? m.MakeGenericMethod(typeof(Object)) : m);
    }
    #region Pre Instantiation
    [HarmonyPrefix]
    [HarmonyWrapSafe]
    [HarmonyPriority(Priority.Last)] // Runs after any other instantiation patch, to guarantee it gets whatever others changed
    static void Prefix_Instantiate(Object __0, out ObjectActivity __state)
    {
        Object original = __0;
        bool debug = BridgeManager.enableDebugLogs.Value;
        if (debug) BridgeManager.logger.LogInfo($"Prefix_Instantiate: Started for object {original?.GetType()} '{original?.name}'");

        // Thread Safety
        if (mainThreadId != System.Threading.Thread.CurrentThread.ManagedThreadId)
        {
            if (debug) BridgeManager.logger.LogWarning($"Prefix_Instantiate: Skipping - called from non-main thread (Thread ID: {System.Threading.Thread.CurrentThread.ManagedThreadId})");
            __state = default;
            return;
        }

        GameObject rootGo = original as GameObject ?? (original as Component)?.gameObject;
        if (!rootGo)
        {
            if (debug) BridgeManager.logger.LogWarning($"Prefix_Instantiate: Original object is neither GameObject nor Component with GameObject. Type: {original?.GetType()}");
            __state = default;
            return;
        }

        // Initialize Context
        __state = new(rootGo.activeSelf, rootGo.activeInHierarchy, InstantiateContextPool.GetContext());
        var context = __state.Context;
        context.OriginalRoot = rootGo;


        if (debug) BridgeManager.logger.LogInfo($"Prefix_Instantiate: Processing root GameObject '{rootGo.name}' (Active: {rootGo.activeSelf})");

        // Scan & Capture Source Hierarchy
        var transforms = GetGOHierarchy(rootGo);
        if (debug)
            BridgeManager.logger.LogInfo($"Prefix_Instantiate: Found {transforms.Length} transforms in hierarchy");

        foreach (var t in transforms)
        {
            uint pathHash = GetRelativePathHash(rootGo.transform, t);
            var componentsBuffer = context.ComponentsRetrievalBuffer;
            componentsBuffer.Clear();
            t.GetComponents(componentsBuffer);

            if (debug)
                BridgeManager.logger.LogInfo($"Prefix_Instantiate: Transform '{t.name}' at path '{pathHash}' has {componentsBuffer.Count} components");

            foreach (var comp in componentsBuffer)
            {
                if (!comp)
                {
                    if (debug) BridgeManager.logger.LogWarning($"Prefix_Instantiate: Found null component at index {componentsBuffer.IndexOf(comp)} on '{t.name}'");
                    continue;
                }
                Type type = comp.GetType();

                // Filtering Logic
                if (type.IsFromGameAssemblies())
                {
                    if (debug) BridgeManager.logger.LogInfo($"Prefix_Instantiate: Skipping Unity Assembly component: {type}");
                    continue;
                }

                // Register the fields this component can offer
                SerializationRegistry.Register(comp);

                // Call beforehand the OnBeforeSerialize to capture the cleaned state
                if (comp is ISafeSerializationCallbackReceiver receiver)
                {
                    if (debug)
                        BridgeManager.logger.LogInfo($"[{comp}] Manual OnBeforeSerialize");
                    receiver.OnBeforeSerialize();
                }
                // Fall back into the blockage field approach, if there's one to begin with
                else if (comp is ISerializationCallbackReceiver unityReceiver)
                {
                    var blockageField = SerializationRegistry.TryGetReceiverBlockageField(type);
                    if (blockageField != null)
                    {
                        // Create the setter
                        var setField = blockageField.CreateFieldSetter();

                        // Set to false before triggering the BeforeSerialize
                        setField(comp, false);

                        // Call the receiver's method
                        if (debug)
                            BridgeManager.logger.LogInfo($"[{comp} ({comp.GetInstanceID()})] Manual OnBeforeSerialize (Blockage approach)");
                        unityReceiver.OnBeforeSerialize();

                        // Block the call after calling it
                        setField(comp, true);
                    }
                }

                // Capture Data (regardless if it has data or not, it still needs to be mapped)
                var state = ComponentSerializer.CaptureState(comp);
                if (!context.SnapshotData.TryGetValue(pathHash, out var list))
                {
                    list = [];
                    context.SnapshotData[pathHash] = list;
                    if (debug)
                        BridgeManager.logger.LogInfo($"Prefix_Instantiate: Created new state list for path '{pathHash}'");
                }
                list.Add(state);
                if (debug) BridgeManager.logger.LogInfo($"Prefix_Instantiate: Captured state for {type}.");
            }
        }
        // Put rootGo to sleep
        rootGo.SetActive(false);
        if (debug) BridgeManager.logger.LogInfo($"Prefix_Instantiate: Finished prefix for {rootGo}");
    }
    #endregion
    #region Post Instantiation
    [HarmonyWrapSafe]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    static void Postfix_Instantiate(Object __0, object __result, ref ObjectActivity __state)
    {
        Object original = __0;
        bool debug = BridgeManager.enableDebugLogs.Value;
        if (__state == default)
        {
            if (debug) BridgeManager.logger.LogWarning($"Postfix_Instantiate: Skipping {original?.name ?? "Unknown"} - Null Context");
            return;
        }

        // Get the context early here
        var context = __state.Context;
        if (__result == null)
        {
            BridgeManager.logger.LogWarning($"Postfix_Instantiate: Result of instantiation is null for original '{original?.name ?? "Unknown"}'. Skipping deserialization.");
            InstantiateContextPool.ReturnContext(context);
            return;
        }

        GameObject resultGo = __result as GameObject ?? (__result as Component)?.gameObject;
        if (!resultGo)
        {
            BridgeManager.logger.LogError($"Postfix_Instantiate: Result is not a GameObject or Component. Type: {__result?.GetType()?.FullName}");
            InstantiateContextPool.ReturnContext(context);
            return;
        }

        GameObject rootGo = original as GameObject ?? (original as Component)?.gameObject;
        if (!rootGo)
        {
            if (debug) BridgeManager.logger.LogWarning($"Postfix_Instantiate: Original object is neither GameObject nor Component with GameObject. Type: {original?.GetType()}");
            InstantiateContextPool.ReturnContext(context);
            return;
        }

        if (debug) BridgeManager.logger.LogInfo($"Postfix_Instantiate: Processing cloned GameObject '{resultGo.name}' (Active: {resultGo.activeInHierarchy})");
        // Register Root Reference
        ComponentSerializer.ClearReferences();
        ComponentSerializer.RegisterReference(resultGo, context.OriginalRoot);
        ComponentSerializer.RegisterReference(resultGo.transform, context.OriginalRoot.transform);

        // Clear transforms buffer
        var componentsStateBuffer = context.ComponentStateBuffer;
        componentsStateBuffer.Clear();

        var resultTransforms = GetGOHierarchy(resultGo);

        if (debug) BridgeManager.logger.LogInfo($"Postfix_Instantiate: Registered root references for '{resultGo.name}' -> '{context.OriginalRoot.name}'");

        // Map & Deserialize
        foreach (var targetTrans in resultTransforms)
        {
            uint pathHash = GetRelativePathHash(resultGo.transform, targetTrans);

            // Skip if source didn't have this node or any serialized data for it
            bool stateListAvailable = context.SnapshotData.TryGetValue(pathHash, out var stateList);

            if (debug)
            {
                if (stateListAvailable)
                    BridgeManager.logger.LogInfo($"Postfix_Instantiate: Found snapshot data for path '{pathHash}' with {stateList.Count} states");
            }

            // Get the components from the cloned object
            var componentsBuffer = context.ComponentsRetrievalBuffer;
            componentsBuffer.Clear();
            targetTrans.GetComponents(componentsBuffer);

            if (debug)
                BridgeManager.logger.LogInfo($"Postfix_Instantiate: Transform '{targetTrans.name}' has {componentsBuffer.Count} components");

            // Use the map to account with duplicates in the body
            var typeProgressMap = context.TypeProgressMap;

            foreach (var targetComp in componentsBuffer)
            {
                if (!targetComp)
                {
                    if (debug) BridgeManager.logger.LogWarning($"Postfix_Instantiate: Null component at '{targetTrans.name}'");
                    continue;
                }
                Type t = targetComp.GetType();

                // Create a state pair
                ComponentStatePair statePair = new()
                {
                    Component = targetComp
                };

                // Retrieve the index (if it is not found in the dictionary; we get default(int), which is always 0)
                int occurrenceIndex = typeProgressMap.GetValueSafe(t);

                // Find the Nth occurrence of this type in the captured state list
                // Because both lists are generated by GetComponents(), the order is identical.
                ComponentSerializationState matchingState = null;
                int foundCount = 0;

                // Search the map if it is available to find matches
                if (stateListAvailable)
                {
                    for (int i = 0; i < stateList.Count; i++)
                    {
                        if (stateList[i].ComponentType == t)
                        {
                            if (foundCount == occurrenceIndex)
                            {
                                matchingState = stateList[i];
                                break;
                            }
                            foundCount++;
                        }
                    }
                }

                // If a match is found, register it
                if (matchingState != null)
                {
                    // Register the component match
                    if (debug) BridgeManager.logger.LogInfo($"Postfix_Instantiate: Matched component [{matchingState.Component} -> {targetComp}]");
                    ComponentSerializer.RegisterReference(targetComp, matchingState.Component);

                    // Update the state
                    statePair.State = matchingState;

                    // Update the progress map
                    typeProgressMap[t] = occurrenceIndex + 1;
                }
                else if (debug) BridgeManager.logger.LogInfo($"Postfix_Instantiate: No matching state found for component {t} at occurrence {occurrenceIndex}");

                // Add the pair to the collection
                componentsStateBuffer.Add(statePair);
            }
        }

        if (debug) BridgeManager.logger.LogInfo($"Postfix_Instantiate: Restoring {componentsStateBuffer.Count} component states");

        // Restore all the states after remapping everything
        foreach (var entry in componentsStateBuffer)
        {
            if (!entry.HasStateDefined) continue; // Only go through defined states

            if (debug)
                BridgeManager.logger.LogInfo($"Postfix_Instantiate: Restoring state for {entry.State.Component}");
            ComponentSerializer.RestoreState(entry.Component, entry.State);
        }


        // Manually Wake Components (Restoring Lifecycle)
        if (debug)
            BridgeManager.logger.LogInfo($"Postfix_Instantiate: Waking {componentsStateBuffer.Count} components");
        foreach (var entry in componentsStateBuffer)
        {
            Type t = entry.Component.GetType();

            // No Unity Assembly should be affected
            if (t.IsFromGameAssemblies()) continue;

            var component = entry.Component;
            Action<object> invoker;

            // ISafeSerializationCallbackReceiver.OnAfterDeserialize
            if (component is ISafeSerializationCallbackReceiver receiver)
            {
                if (debug)
                    BridgeManager.logger.LogInfo($"[{component}] Manual OnAfterDeserialize");
                receiver.OnAfterDeserialize();
            }
            // Fallback to the blockage approach if there's one
            else if (component is ISerializationCallbackReceiver unityReceiver)
            {
                var blockageField = SerializationRegistry.TryGetReceiverBlockageField(t);
                if (blockageField != null)
                {
                    var setField = blockageField.CreateFieldSetter();

                    // Try to disable the block flag
                    setField(component, false);

                    if (debug)
                        BridgeManager.logger.LogInfo($"[{component} ({component.GetInstanceID()})] Manual OnAfterDeserialize (Blockage Approach)");
                    // Then, call the deserialize method
                    unityReceiver.OnAfterDeserialize();

                    // Then, block it again for the future
                    setField(component, true);
                }
            }

            // Awake & OnEnable
            if (component is Behaviour b && __state.ActiveInHierarchy)
            {
                if (component.gameObject != resultGo)
                {
                    invoker = DelegateProvider.GetMethodInvoker(t, DelegateMethod.Awake);
                    if (invoker != null)
                    {
                        if (debug) BridgeManager.logger.LogInfo($"[{component}] Manual Awake");
                        invoker(component);
                    }
                }

                // Since resultGo below will be set active, we don't need to call it here twice
                if ((!__state.ActiveSelf || component.gameObject != resultGo)
                 && b.enabled)
                {
                    invoker = DelegateProvider.GetMethodInvoker(t, DelegateMethod.OnEnable);
                    if (invoker != null)
                    {
                        if (debug) BridgeManager.logger.LogInfo($"[{component}] Manual OnEnable");
                        invoker(component);
                    }
                }
            }
        }

        // Reset back the SetActive state again
        if (debug) BridgeManager.logger.LogInfo($"Postfix_Instantiate: Activating object states.");
        rootGo.SetActive(__state.ActiveSelf);
        resultGo.SetActive(__state.ActiveSelf);
        ComponentSerializer.ClearReferences();
        InstantiateContextPool.ReturnContext(context);
        if (debug) BridgeManager.logger.LogInfo($"Postfix_Instantiate: Completed successfully for '{resultGo.name}'");
    }
    #endregion

    private static Transform[] GetGOHierarchy(GameObject go)
    {
        if (PrecomputedGOHierarchy.TryGetValue(go, out var precomputedHierarchy)) return precomputedHierarchy.GetOrderedTransforms();

        precomputedHierarchy = new PrecomputedHierarchyTransformOrder(go.transform);
        PrecomputedGOHierarchy.Add(go, precomputedHierarchy);
        return precomputedHierarchy.GetOrderedTransforms();
    }

    private static uint GetRelativePathHash(Transform root, Transform target)
    {
        if (root == target) return 0; // Short token for root

        // _pathBuffer.Clear();
        var current = target;
        uint hash = 0;

        // Traverse up and collect sibling indices
        while (current && current != root)
        {
            // _pathBuffer.Add((uint)current.GetSiblingIndex());
            unchecked // Allows integer overflow, which is fine for hashing
            {
                hash = (hash * 31) ^ (uint)(current.GetSiblingIndex() + 1); // Basic hashing algorithm
            }
            current = current.parent;
        }

        // Create a hash that theoretically should never have collision (Root -> Target)
        // for (int i = _pathBuffer.Count - 1; i >= 0; i--) // Reverses here
        // {
        // }
        return hash;
    }
}