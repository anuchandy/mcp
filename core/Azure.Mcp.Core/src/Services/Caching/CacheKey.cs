// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

/// <summary>
/// Value object for a cache entry key.  
/// Equality and hashing are based only on <see cref="FullKey"/>.  
/// </summary>
public sealed record CacheKey(string FullKey, string Group0, string Group1, string LocalKey)
{
    public override string ToString() => FullKey;

    public bool Equals(CacheKey? other) =>
        other is not null && StringComparer.Ordinal.Equals(FullKey, other.FullKey);

    public override int GetHashCode() =>
        StringComparer.Ordinal.GetHashCode(FullKey);
}