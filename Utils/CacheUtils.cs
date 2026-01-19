using BepInSerializer.Core.Models;

namespace BepInSerializer.Utils;

internal static class CacheUtils
{
    public static void NullableAdd<TKey, TValue>(this LRUCache<TKey, TValue> cache, TKey key, TValue value) => cache?.Add(key, value);
    public static bool NullableTryGetValue<TKey, TValue>(this LRUCache<TKey, TValue> cache, TKey key, out TValue value)
    {
        if (cache?.TryGetValue(key, out value) ?? false)
            return true;
        value = default;
        return false;
    }
}