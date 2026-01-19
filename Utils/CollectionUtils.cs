using System;
using System.Collections;
using System.Collections.Generic;

namespace BepInSerializer.Utils;

internal static class CollectionUtils
{
    public static bool HasOfType<T>(this IList<T> list, Type t)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].GetType() == t)
                return true;
        }
        return false;
    }

    public static void LogCollection<T>(this IList<T> list, string collectionName = null)
    {
        BridgeManager.logger.LogInfo($"==== {(string.IsNullOrEmpty(collectionName) ? typeof(T).Name : collectionName)} COLLECTION OVERVIEW ====");
        for (int i = 0; i < list.Count; i++)
            BridgeManager.logger.LogInfo($"[{i}] -- ({list[i]})");
    }
}