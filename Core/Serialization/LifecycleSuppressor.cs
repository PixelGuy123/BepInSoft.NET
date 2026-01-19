using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Linq.Expressions;
using MonoMod.RuntimeDetour;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using BepInSerializer.Core.Models;
using System.Collections.Generic;
using UnityEngine;
using BepInSerializer.Utils;

namespace BepInSerializer.Core.Serialization;

internal static class LifecycleSuppressor
{
    // --- State ---
    private static readonly ConcurrentDictionary<Type, int> _suppressionCounts = new();
    private static readonly Dictionary<MethodBase, ILHook> _activeHooks = [];

    // Caches for the delegates we will invoke manually later
    internal static LRUCache<(SuppressMethod, Type), Action<object>> _methodCache;

    // --- API ---

    public static void Suppress(Type type)
    {
        _suppressionCounts.AddOrUpdate(type, 1, (_, count) => count + 1);
        EnsureHooksInstalled(type);
    }

    public static void Release(Type type)
    {
        if (_suppressionCounts.TryGetValue(type, out int count))
        {
            if (count <= 1) _suppressionCounts.TryRemove(type, out _);
            else _suppressionCounts[type] = count - 1;
        }
    }

    public static Action<object> GetMethodInvoker(Type t, SuppressMethod method) => GetCachedDelegate(t, method);

    // --- Internal Logic ---

    private static void EnsureHooksInstalled(Type type)
    {
        // We only care about base Unity lifecycle methods or Interface implementations
        if (typeof(UnityEngine.Object).IsAssignableFrom(type))
        {
            InstallHook(type, "Awake");
            InstallHook(type, "OnEnable");
        }
        if (typeof(ISerializationCallbackReceiver).IsAssignableFrom(type))
        {
            InstallHook(type, nameof(ISerializationCallbackReceiver.OnBeforeSerialize));
            InstallHook(type, nameof(ISerializationCallbackReceiver.OnAfterDeserialize));
        }
    }

    private static void InstallHook(Type type, string methodName)
    {
        // Find method including non-public and instance
        var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (method == null || _activeHooks.ContainsKey(method)) return;

        // Apply IL Hook: Check dictionary at start of method
        var hook = new ILHook(method, il =>
        {
            var c = new ILCursor(il);
            var runOriginal = c.DefineLabel();

            c.Emit(OpCodes.Ldarg_0); // Load 'this'
            c.EmitDelegate<Func<object, bool>>(obj => _suppressionCounts.ContainsKey(obj.GetType()));
            c.Emit(OpCodes.Brfalse, runOriginal); // If not suppressed, jump to original code
            c.Emit(OpCodes.Ret); // Else, return immediately
            c.MarkLabel(runOriginal);
        });

        _activeHooks[method] = hook;
    }

    private static Action<object> GetCachedDelegate(Type type, SuppressMethod supMethod)
    {
        if (supMethod == SuppressMethod.Null) return null;
        var typeSuppress = (supMethod, type);
        if (_methodCache.NullableTryGetValue(typeSuppress, out var action)) return action;

        var method = type.GetMethod(supMethod.GetSuppressMethodName(), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (method != null)
        {
            // Create open delegate (Action<object>)
            var param = Expression.Parameter(typeof(object), "obj");
            var cast = Expression.Convert(param, type);
            var call = Expression.Call(cast, method);
            action = Expression.Lambda<Action<object>>(call, param).Compile();
        }

        _methodCache.NullableAdd(typeSuppress, action); // Cache result (even if null)
        return action;
    }

    // Helper Method
    private static string GetSuppressMethodName(this SuppressMethod method) => method switch
    {
        SuppressMethod.Awake => "Awake",
        SuppressMethod.OnEnable => "OnEnable",
        SuppressMethod.OnAfterDeserialize => "OnAfterDeserialize",
        _ or SuppressMethod.Null => string.Empty
    };
}