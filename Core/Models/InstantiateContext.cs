using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

namespace BepInSerializer.Core.Models;

/// <summary>
/// Context passed between Prefix and Postfix.
/// </summary>
internal class InstantiateContext
{
    public GameObject OriginalRoot;
    // Map: RelativePath (hash) -> List of Component States (ordered by occurrence)
    public readonly Dictionary<uint, List<ComponentSerializationState>> SnapshotData = new(16);
    public readonly Dictionary<Type, int> TypeProgressMap = new(8);
    public readonly List<ComponentStatePair> ComponentStateBuffer = new(16);
    public readonly List<Component> ComponentsRetrievalBuffer = new(16); // Buffer for GetComponents
}

/// <summary>
/// A pool of <see cref="InstantiateContext"/>, so that we don't need to make a new instance every time (it has expensive collections).
/// </summary>
internal static class InstantiateContextPool
{
    // The pool itself
    private readonly static ConcurrentBag<InstantiateContext> ContextPool = [];

    public static InstantiateContext GetContext() => ContextPool.TryTake(out var context) ? context : new();
    public static void ReturnContext(InstantiateContext context)
    {
        // Clean up the context first
        context.TypeProgressMap.Clear();
        context.ComponentsRetrievalBuffer.Clear();
        context.ComponentStateBuffer.Clear();
        context.SnapshotData.Clear();

        context.OriginalRoot = null;

        // Add again to the bag
        ContextPool.Add(context);
    }
}