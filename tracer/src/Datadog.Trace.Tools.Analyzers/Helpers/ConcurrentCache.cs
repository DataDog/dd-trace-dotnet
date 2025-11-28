// <copyright file="ConcurrentCache.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Datadog.Trace.Tools.Analyzers.Helpers;

// very simple cache with a specified size.
// expiration policy is "new entry wins over old entry if hashed into the same bucket"
internal sealed class ConcurrentCache<TKey, TValue> : CachingBase<ConcurrentCache<TKey, TValue>.Entry>
    where TKey : notnull
{
    private readonly IEqualityComparer<TKey> _keyComparer;

    public ConcurrentCache(int size, IEqualityComparer<TKey> keyComparer)
        // Defer creating the backing array until it is actually needed.  This saves on expensive allocations for
        // short-lived compilations that do not end up using the cache.  As the cache is simple best-effort, it's
        // fine if multiple threads end up creating the backing array at the same time.  One thread will be last and
        // will win, and the others will just end up creating a small piece of garbage that will be collected.
        : base(size, createBackingArray: false)
    {
        _keyComparer = keyComparer;
    }

    public ConcurrentCache(int size)
        : this(size, EqualityComparer<TKey>.Default)
    {
    }

    public bool TryAdd(TKey key, TValue value)
    {
        var hash = _keyComparer.GetHashCode(key);
        var idx = hash & Mask;

        var entry = this.Entries[idx];
        if (entry != null && entry.Hash == hash && _keyComparer.Equals(entry.Key, key))
        {
            return false;
        }

        Entries[idx] = new Entry(hash, key, value);
        return true;
    }

    public bool TryGetValue(TKey key, [MaybeNullWhen(returnValue: false)] out TValue value)
    {
        int hash = _keyComparer.GetHashCode(key);
        int idx = hash & Mask;

        var entry = this.Entries[idx];
        if (entry != null && entry.Hash == hash && _keyComparer.Equals(entry.Key, key))
        {
            value = entry.Value;
            return true;
        }

        value = default!;
        return false;
    }

    // class, to ensure atomic updates.
    internal class Entry
    {
#pragma warning disable SA1401 //field should be private
        internal readonly int Hash;
        internal readonly TKey Key;
        internal readonly TValue Value;
#pragma warning restore SA1401

        internal Entry(int hash, TKey key, TValue value)
        {
            this.Hash = hash;
            this.Key = key;
            this.Value = value;
        }
    }
}
