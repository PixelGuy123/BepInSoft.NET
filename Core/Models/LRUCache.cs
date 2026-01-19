using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace BepInSerializer.Core.Models;

// Will be useful where there's excessive amounts of items to cache
internal class LRUCache<TKey, TValue>(int maxSize) : IEnumerable<KeyValuePair<TKey, TValue>>
{
    private readonly int _maxSize = maxSize;
    private readonly Dictionary<TKey, LinkedListNode<CacheItem>> _cache = [];
    private readonly LinkedList<CacheItem> _history = new();

    // Currently not needed
    public TValue this[TKey key]
    {
        get
        {
            if (TryGetValue(key, out TValue value))
                return value;
            throw new KeyNotFoundException($"{key} not found in LRUCache.");
        }
        set
        {
            if (!_cache.TryGetValue(key, out var node)) throw new KeyNotFoundException($"{key} not found in LRUCache.");
            NotifyNodeUsage(node);
            node.Value = new CacheItem(key, value);
        }
    }

    public TValue GetValue(TKey key, Func<TKey, TValue> createValueCallback)
    {
        if (TryGetValue(key, out var value))
            return value;
        value = createValueCallback(key);
        Add(key, value);
        return value;
    }
    public bool TryGetValue(TKey key, out TValue value)
    {
        if (_cache.TryGetValue(key, out var node))
        {
            // Move to front (Most Recently Used)
            NotifyNodeUsage(node);
            value = node.Value.Value;
            return true;
        }
        value = default;
        return false;
    }

    public bool ContainsKey(TKey key) => _cache.ContainsKey(key);

    public void Add(TKey key, TValue value)
    {
        if (_cache.ContainsKey(key)) throw new ArgumentException($"Key ('{key}') is already contained in cache.");

        // If the maxSize is reached, remove the oldest one
        if (_cache.Count >= _maxSize)
        {
            // Remove the oldest (Last in the list)
            var last = _history.Last;
            _cache.Remove(last.Value.Key);
            _history.RemoveLast();
        }

        var newNode = new LinkedListNode<CacheItem>(new CacheItem(key, value));
        _history.AddFirst(newNode);
        _cache[key] = newNode;
    }

    // Note: not really needed in practice, but UnityExplorer uses it to iterate through, so it's still useful
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _cache.Select(
        kvp => new KeyValuePair<TKey, TValue>(kvp.Value.Value.Key, kvp.Value.Value.Value) // Funny access
        ).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();


    // Helper methods
    private void NotifyNodeUsage(LinkedListNode<CacheItem> node)
    {
        _history.Remove(node);
        _history.AddFirst(node);
    }

    private record struct CacheItem(TKey Key, TValue Value);
}