// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Caching.Memory;

/// <summary>
/// Defines a cache contract for entries whose **full cache key** is composed as
/// <c>group0 spr group1 spr localKey</c>, where spr is the Unit Separator (<c>\u001F</c>).
/// <para>
/// The cache lets you:
///  - Set/get entries by (group0, group1, localKey)
///  - Bulk-remove by group0 (tenant, for example) or by group1 (user, etc.)
/// </para>
/// <para>
/// Concurrency model:
///  - We enforce **strong remove-by-group semantics** using *group-level locks*:
///    while <see cref="RemoveByGroup0"/> (or <see cref="RemoveByGroup1"/>) runs,
///    no concurrent <see cref="Set"/> / <see cref="GetOrCreate"/> for that group can publish.
///  - A separate RW lock protects the **secondary indexes** (group→fullKey sets).
///    We always acquire group locks *before* the index lock (fixed order) to avoid deadlocks.
///  - Index updates occur while holding the relevant group lock(s) and before releasing them,
///    ensuring removers cannot miss in-flight adds for that group.
///  - Eviction callbacks (expiry/manual remove/size trimming) clean the indexes so they don’t leak.
/// </para>
/// <para>
/// Performance notes:
///  - Reads by full key (e.g., <see cref="TryGetValue"/>) do not take locks.
///  - Writes for different groups proceed independently (contention is per-group).
/// </para>
/// </summary>
public interface ICacheService2
{
    /// <summary>Composes the full cache key: <c>group0 spr group1 spr localKey</c>.</summary>
    string Compose(string group0, string group1, string localKey);

    /// <summary>Stores a value and wires eviction cleanup; overwrites existing entry for the same full key.</summary>
    void Set<T>(string group0, string group1, string localKey, T value, MemoryCacheEntryOptions? options = null);

    /// <summary>
    /// Retrieves an existing value or creates it atomically for the given full key.
    /// The factory runs under the appropriate group locks; publishing and indexing are synchronized.
    /// </summary>
    T GetOrCreate<T>(string group0, string group1, string localKey, Func<ICacheEntry, T> factory);

    /// <summary>Tries to get a value by full key without taking any locks.</summary>
    bool TryGetValue<T>(string group0, string group1, string localKey, out T? value);

    /// <summary>Bulk-removes all entries for <paramref name="group0"/> (e.g., tenant-level purge).</summary>
    int RemoveByGroup0(string group0);

    /// <summary>Bulk-removes all entries for <paramref name="group1"/> (e.g., user-level purge across tenants).</summary>
    int RemoveByGroup1(string group1);

    /// <summary>Returns a snapshot of composed full keys currently indexed under <paramref name="group0"/>.</summary>
    IReadOnlyCollection<string> KeysByGroup0(string group0);

    /// <summary>Returns a snapshot of composed full keys currently indexed under <paramref name="group1"/>.</summary>
    IReadOnlyCollection<string> KeysByGroup1(string group1);

    /// <summary>
    /// Returns a snapshot of the current VALUES for all entries under the given group0.
    /// Only entries that are still present in IMemoryCache at read time are returned.
    /// </summary>
    IReadOnlyDictionary<CacheKey, T> GetValuesByGroup0<T>(string group0);

    /// <summary>
    /// Returns a snapshot of the current VALUES for all entries under the given group1.
    /// Only entries that are still present in IMemoryCache at read time are returned.
    /// </summary>
    IReadOnlyDictionary<CacheKey, T> GetValuesByGroup1<T>(string group1);
}