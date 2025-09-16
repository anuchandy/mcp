// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Caching.Memory;

/// <inheritdoc />
public sealed class CacheService2 : ICacheService2, IDisposable
{
    // We never dispose the IMemoryCache here unless we own its lifecycle.
    private readonly IMemoryCache _cache;

    // Secondary indexes: group -> { fullCompositeKey }
    // Use Ordinal string comparer throughout for speed and determinism.
    private readonly Dictionary<string, HashSet<string>> _byGroup0 = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _byGroup1 = new(StringComparer.Ordinal);

    // Protects _byGroup0/_byGroup1 only. Always acquired AFTER any group lock(s).
    private readonly ReaderWriterLockSlim _indexLock = new(LockRecursionPolicy.NoRecursion);

    // Per-group locks to serialize writers within the same group and to
    // block writers during RemoveByGroup0/RemoveByGroup1.
    private readonly ConcurrentDictionary<string, object> _g0Locks = new();
    private readonly ConcurrentDictionary<string, object> _g1Locks = new();

    // Hard-wired delimiter: Unit Separator (rare in real keys, avoids parsing ambiguity).
    private const char Sep = '\u001F';
    private static readonly StringComparer Cmp = StringComparer.Ordinal;

    public CacheService2(IMemoryCache cache)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    /// <inheritdoc />
    public string Compose(string group0, string group1, string localKey)
    { 
        return string.Concat(group0, Sep, group1, Sep, localKey);
    }

    /// <inheritdoc />
    public void Set<T>(string group0, string group1, string localKey, T value, MemoryCacheEntryOptions? options = null)
    {
        var full = Compose(group0, group1, localKey);
        var l0 = GetLock(_g0Locks, group0);
        var l1 = GetLock(_g1Locks, group1);

        // Strong semantics: writers must hold the relevant group locks.
        LockGroups(group0, group1, l0, l1, () =>
        {
            var opts = options ?? new MemoryCacheEntryOptions();
            // Ensure that when this entry goes away (expiry/manual/trim) we clean our indexes.
            opts.RegisterPostEvictionCallback(OnEvicted, new IndexState(group0, group1, full));

            _cache.Set(full, value!, opts);

            // Update both indexes while the group locks are still held.
            _indexLock.EnterWriteLock();
            try
            {
                AddToIndex(_byGroup0, group0, full);
                AddToIndex(_byGroup1, group1, full);
            }
            finally { _indexLock.ExitWriteLock(); }
        });
    }

    /// <inheritdoc />
    public T GetOrCreate<T>(string group0, string group1, string localKey, Func<ICacheEntry, T> factory)
    {
        var full = Compose(group0, group1, localKey);

        // Fast path: no locks for a pure cache read.
        if (TryGetTyped(_cache, full, out T existing))
        {
            return existing;
        }

        var l0 = GetLock(_g0Locks, group0);
        var l1 = GetLock(_g1Locks, group1);
        T result = default!;

        // Strong semantics: creation/publish/indexing happens under group locks.
        LockGroups(group0, group1, l0, l1, () =>
        {
            // Re-check under the lock; someone else may have created it meanwhile.
            if (TryGetTyped(_cache, full, out T ex))
            {
                result = ex;
                return;
            }

            using var entry = _cache.CreateEntry(full);
            entry.RegisterPostEvictionCallback(OnEvicted, new IndexState(group0, group1, full));
            var created = factory(entry);
            entry.Value = created;
            entry.Dispose(); // Publish to IMemoryCache

            _indexLock.EnterWriteLock();
            try
            {
                AddToIndex(_byGroup0, group0, full);
                AddToIndex(_byGroup1, group1, full);
            }
            finally { _indexLock.ExitWriteLock(); }

            result = created!;
        });

        return result;
    }

    /// <inheritdoc />
    public bool TryGetValue<T>(string group0, string group1, string localKey, out T? value)
    {
        return _cache.TryGetValue(Compose(group0, group1, localKey), out value);
    }

    /// <inheritdoc />
    public int RemoveByGroup0(string group0)
    {
        var l0 = GetLock(_g0Locks, group0);
        string[] keys;

        // Block writers for this group while we snapshot and drop the index branch.
        lock (l0)
        {
            _indexLock.EnterUpgradeableReadLock();
            try
            {
                if (!_byGroup0.TryGetValue(group0, out var set) || set.Count == 0)
                {
                    return 0;
                }

                keys = set.ToArray(); // snapshot to mutate outside the index lock

                _indexLock.EnterWriteLock();
                try
                {
                    _byGroup0.Remove(group0);
                }
                finally
                {
                    _indexLock.ExitWriteLock();
                }
            }
            finally
            {
                _indexLock.ExitUpgradeableReadLock();
            }
        }

        // Now remove each entry from IMemoryCache and the *other* index (group1).
        int removed = 0;
        foreach (var key in keys)
        {
            TryParseGroup1(key, out var g1);
            RemoveKeyEverywhere(group0, g1, key, removeFromCache: true, ref removed);
        }

        return removed;
    }

    /// <inheritdoc />
    public int RemoveByGroup1(string group1)
    {
        var l1 = GetLock(_g1Locks, group1);
        string[] keys;

        lock (l1)
        {
            _indexLock.EnterUpgradeableReadLock();
            try
            {
                if (!_byGroup1.TryGetValue(group1, out var set) || set.Count == 0)
                {
                    return 0;
                }

                keys = set.ToArray();

                _indexLock.EnterWriteLock();
                try
                {
                    _byGroup1.Remove(group1);
                }
                finally
                {
                    _indexLock.ExitWriteLock();
                }
            }
            finally
            {
                _indexLock.ExitUpgradeableReadLock();
            }
        }

        int removed = 0;
        foreach (var key in keys)
        {
            TryParseGroup0(key, out var g0);
            RemoveKeyEverywhere(g0, group1, key, removeFromCache: true, ref removed);
        }

        return removed;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> KeysByGroup0(string group0)
    {
        _indexLock.EnterReadLock();
        try
        {
            return _byGroup0.TryGetValue(group0, out var set)
                ? set.ToArray()
                : Array.Empty<string>();
        }
        finally
        {
            _indexLock.ExitReadLock();
        }
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> KeysByGroup1(string group1)
    {
        _indexLock.EnterReadLock();
        try
        {
            return _byGroup1.TryGetValue(group1, out var set)
                ? set.ToArray()
                : Array.Empty<string>();
        }
        finally
        {
            _indexLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Disposes internal synchronization primitives.
    /// <para>
    /// Do <b>not</b> dispose <see cref="IMemoryCache"/> it’s managed by DI.
    /// </para>
    /// </summary>
    public void Dispose()
    {
        _indexLock.Dispose();
    }

    #region Internals: locking, indexing, eviction

    private sealed record IndexState(string Group0, string Group1, string FullKey);

    private static object GetLock(ConcurrentDictionary<string, object> dict, string key) =>
        dict.GetOrAdd(key, _ => new object());

    /// <summary>
    /// Locks both groups in a **deterministic order** (by Ordinal string compare) to avoid deadlocks
    /// when callers need to touch two groups (e.g., writes with both group0 and group1).
    /// </summary>
    private static void LockGroups(string g0, string g1, object l0, object l1, Action body)
    {
        if (string.CompareOrdinal(g0, g1) <= 0)
        {
            lock (l0)
            {
                lock (l1)
                {
                    body();
                }
            }
        }
        else
        {
            lock (l1)
            {
                lock (l0)
                {
                    body();
                }
            }
        }
    }

    private static void AddToIndex(Dictionary<string, HashSet<string>> index, string key, string full)
    {
        if (!index.TryGetValue(key, out var set))
        {
            index[key] = set = new HashSet<string>(Cmp);
        }
        set.Add(full);
    }

    /// <summary>
    /// Eviction callback from IMemoryCache: cleans both indexes.
    /// We deliberately **don’t** take any group locks here (to avoid deadlocks with writers);
    /// we only take the index write lock.
    /// </summary>
    private void OnEvicted(object cacheKey, object? value, EvictionReason reason, object? stateObj)
    {
        var state = (IndexState)stateObj!; // contains group0, group1, and the full key
        int ignored = 0;
        RemoveKeyEverywhere(state.Group0, state.Group1, state.FullKey, removeFromCache: false, ref ignored);
    }

    /// <summary>
    /// Removes the given full key from IMemoryCache (optional) and from both group indexes.
    /// Callers should hold the relevant *group* lock(s) if they require strict coordination
    /// with concurrent writers for those groups.
    /// </summary>
    private void RemoveKeyEverywhere(string? group0, string? group1, string fullKey, bool removeFromCache, ref int removedCount)
    {
        if (removeFromCache)
        {
            _cache.Remove(fullKey);
            removedCount++;
        }

        _indexLock.EnterWriteLock();
        try
        {
            if (group0 is not null && _byGroup0.TryGetValue(group0, out var s0))
            {
                s0.Remove(fullKey);
                if (s0.Count == 0)
                {
                    _byGroup0.Remove(group0);
                }
            }
            if (group1 is not null && _byGroup1.TryGetValue(group1, out var s1))
            {
                s1.Remove(fullKey);
                if (s1.Count == 0)
                {
                    _byGroup1.Remove(group1);
                }
            }
        }
        finally
        {
            _indexLock.ExitWriteLock();
        }
    }

    private static bool TryGetTyped<T>(IMemoryCache cache, object key, out T value)
    {
        if (cache.TryGetValue(key, out var boxed) && boxed is T v)
        {
            value = v;
            return true;
        }

        value = default!; // safe: ignored by callers when returning false
        return false;
    }

    private static bool TryParseGroup0(string fullKey, out string? g0)
    {
        var i = fullKey.IndexOf(Sep);
        if (i <= 0)
        {
            g0 = null;
            return false;
        }

        g0 = fullKey[..i];
        return true;
    }

    private static bool TryParseGroup1(string fullKey, out string? g1)
    {
        var first = fullKey.IndexOf(Sep);
        if (first <= 0)
        {
            g1 = null;
            return false;
        }

        var second = fullKey.IndexOf(Sep, first + 1);
        if (second <= first)
        {
            g1 = null;
            return false;
        }

        g1 = fullKey.Substring(first + 1, second - first - 1);
        return true;
    }

    #endregion
}