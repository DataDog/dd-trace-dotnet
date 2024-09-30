// <copyright file="OrderedKeyValuePairList.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace Datadog.Trace.Util;

internal class OrderedKeyValuePairList<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
{
    private static readonly EqualityComparer<TKey> EqualityComparer = EqualityComparer<TKey>.Default;

    private static readonly List<KeyValuePair<TKey, TValue>> EmptyList = [];

    private readonly ReaderWriterLockSlim _lock = new();

    private List<KeyValuePair<TKey, TValue>>? _list;

    public OrderedKeyValuePairList()
    {
    }

    public OrderedKeyValuePairList(IEnumerable<KeyValuePair<TKey, TValue>> items)
    {
        _list = [..items];
    }

    public int Count => _list?.Count ?? 0;

    public TValue this[TKey key]
    {
        get
        {
            _lock.EnterReadLock();

            try
            {
                if (TryGetValue(key, out var value))
                {
                    return value;
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            ThrowHelper.ThrowKeyNotFoundException($"The key was not found: {key}");
            return default!; // unreachable
        }

        set
        {
            _lock.EnterWriteLock();

            try
            {
                Set(key, value);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
    }

    private List<KeyValuePair<TKey, TValue>> EnsureListInitialized()
    {
        if (_list == null)
        {
            Interlocked.CompareExchange(ref _list, [], null);
        }

        return _list;
    }

    public void Set(TKey key, TValue value)
    {
        _lock.EnterWriteLock();

        try
        {
            var list = EnsureListInitialized();

            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];

                if (EqualityComparer.Equals(item.Key, key))
                {
                    list[i] = new KeyValuePair<TKey, TValue>(key, value);
                    return;
                }
            }

            // key is found, add new entry
            list.Add(new KeyValuePair<TKey, TValue>(key, value));
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public bool TryGetValue(TKey key, [NotNullWhen(true)] out TValue? value)
    {
        if (_list is { } list)
        {
            _lock.EnterReadLock();

            try
            {
                foreach (var pair in list)
                {
                    if (EqualityComparer.Equals(pair.Key, key))
                    {
                        value = pair.Value!;
                        return true;
                    }
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        value = default;
        return false;
    }

    public bool Remove(TKey key, [NotNullWhen(true)] out TValue? value)
    {
        if (_list is { } list)
        {
            _lock.EnterUpgradeableReadLock();

            try
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (EqualityComparer.Equals(list[i].Key, key))
                    {
                        // found the specified key,
                        // upgrade read lock to write lock
                        _lock.EnterWriteLock();

                        try
                        {
                            value = list[i].Value!;
                            list.RemoveAt(i);
                            return true;
                        }
                        finally
                        {
                            _lock.ExitWriteLock();
                        }
                    }
                }
            }
            finally
            {
                _lock.ExitUpgradeableReadLock();
            }
        }

        value = default;
        return false;
    }

    public void Clear()
    {
        _lock.EnterWriteLock();

        try
        {
            _list?.Clear();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public KeyValuePair<TKey, TValue>[] ToArray()
    {
        _lock.EnterReadLock();

        try
        {
            return _list?.ToArray() ?? [];
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public List<KeyValuePair<TKey, TValue>>.Enumerator GetEnumerator()
    {
        return _list?.GetEnumerator() ?? EmptyList.GetEnumerator();
    }

    IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
    {
        return GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
