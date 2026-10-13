// <copyright file="FlagCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;

namespace Datadog.Trace.FeatureFlags.Rcm.Model;

internal enum FlagLookupResult
{
    Found,
    Invalid,
    NotFound,
}

internal sealed class FlagCollection : IEnumerable<KeyValuePair<string, Flag>>
{
    private readonly Dictionary<string, Flag> _validFlags = new();
    private readonly HashSet<string> _invalidFlagKeys = new();

    public int Count => _validFlags.Count + _invalidFlagKeys.Count;

    internal IEnumerable<KeyValuePair<string, Flag>> ValidFlags => _validFlags;

    internal IEnumerable<string> InvalidFlagKeys => _invalidFlagKeys;

    public Flag this[string key]
    {
        get => _validFlags[key];
        set => Add(key, value);
    }

    public void Add(string key, Flag flag)
    {
        if (flag is null)
        {
            throw new ArgumentNullException(nameof(flag));
        }

        _invalidFlagKeys.Remove(key);
        _validFlags[key] = flag;
    }

    public IEnumerator<KeyValuePair<string, Flag>> GetEnumerator() => _validFlags.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    internal FlagLookupResult Find(string key, out Flag? flag)
    {
        if (_validFlags.TryGetValue(key, out flag))
        {
            return FlagLookupResult.Found;
        }

        return _invalidFlagKeys.Contains(key) ? FlagLookupResult.Invalid : FlagLookupResult.NotFound;
    }

    internal void MarkInvalid(string key)
    {
        _validFlags.Remove(key);
        _invalidFlagKeys.Add(key);
    }

    internal void Merge(FlagCollection other)
    {
        foreach (var pair in other.ValidFlags)
        {
            Add(pair.Key, pair.Value);
        }

        foreach (var key in other.InvalidFlagKeys)
        {
            MarkInvalid(key);
        }
    }
}
