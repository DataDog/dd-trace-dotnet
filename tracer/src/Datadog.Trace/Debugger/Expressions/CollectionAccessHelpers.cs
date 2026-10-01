// <copyright file="CollectionAccessHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using Datadog.Trace.Debugger.Snapshots;
using TypeExtensions = Datadog.Trace.Debugger.Helpers.TypeExtensions;

namespace Datadog.Trace.Debugger.Expressions;

/// <summary>
/// Collection operations are compiled against the declared type, which a customer subclass or interface
/// implementation can satisfy, so the exact runtime type is checked before calling into the collection.
/// </summary>
internal static class CollectionAccessHelpers
{
    private static readonly ConcurrentDictionary<Type, KeyedLookup?> KeyedLookups = new();

    internal static TSource EnsureSupportedCollection<TSource>(TSource source)
    {
        if (source is null)
        {
            return source;
        }

        var runtimeType = source.GetType();
        if (!Redaction.IsSupportedCollection(runtimeType) && !Redaction.IsSupportedDictionary(runtimeType))
        {
            throw new InvalidOperationException($"{runtimeType.FullName} is not a supported collection type");
        }

        return source;
    }

    internal static object? GetDictionaryItem(IDictionary dictionary, object? key)
    {
        // A key lookup runs the dictionary's comparer, so it is only allowed when that comparer is a known framework one.
        var runtimeType = dictionary.GetType();
        if (key is null ||
            !TypeExtensions.IsSimple(key.GetType()) ||
            !Redaction.IsSupportedDictionary(runtimeType) ||
            KeyedLookups.GetOrAdd(runtimeType, CreateKeyedLookup) is not { } keyedLookup ||
            !keyedLookup.HasKnownComparer(dictionary))
        {
            throw new InvalidOperationException($"Key lookup on {runtimeType.FullName} is only supported for string, numeric or enum keys with a default comparer");
        }

        return dictionary[key];
    }

    private static KeyedLookup? CreateKeyedLookup(Type dictionaryType)
    {
        // Keys of a non-generic or object-keyed dictionary can be customer types whose Equals or CompareTo would run.
        var genericArguments = dictionaryType.GetGenericArguments();
        if (genericArguments.Length != 2 || !TypeExtensions.IsSimple(genericArguments[0]))
        {
            return null;
        }

        const BindingFlags instanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var comparerProperty = dictionaryType.GetProperty("Comparer", BindingFlags.Instance | BindingFlags.Public) ??
                               dictionaryType.GetProperty("KeyComparer", BindingFlags.Instance | BindingFlags.Public);

        // ConcurrentDictionary exposes its comparer only from .NET 5
        var comparerField = comparerProperty is null
                                ? dictionaryType.GetField("_comparer", instanceFlags) ?? dictionaryType.GetField("m_comparer", instanceFlags)
                                : null;

        var comparerType = comparerProperty?.PropertyType ?? comparerField?.FieldType;
        if (comparerType is not { IsGenericType: true })
        {
            return null;
        }

        var keyType = genericArguments[0];
        var comparerDefinition = comparerType.GetGenericTypeDefinition();
        Type defaultComparerType;
        if (comparerDefinition == typeof(IEqualityComparer<>))
        {
            defaultComparerType = typeof(EqualityComparer<>).MakeGenericType(keyType);
        }
        else if (comparerDefinition == typeof(IComparer<>))
        {
            defaultComparerType = typeof(Comparer<>).MakeGenericType(keyType);
        }
        else
        {
            return null;
        }

        var defaultComparer = defaultComparerType.GetProperty("Default", BindingFlags.Static | BindingFlags.Public)!.GetValue(null)!;
        object[] knownComparers = keyType == typeof(string)
                                      ? [defaultComparer, StringComparer.Ordinal, StringComparer.OrdinalIgnoreCase, StringComparer.InvariantCulture, StringComparer.InvariantCultureIgnoreCase]
                                      : [defaultComparer];

        return new KeyedLookup(comparerProperty, comparerField, knownComparers);
    }

    private sealed class KeyedLookup
    {
        private readonly PropertyInfo? _comparerProperty;
        private readonly FieldInfo? _comparerField;
        private readonly object[] _knownComparers;

        internal KeyedLookup(PropertyInfo? comparerProperty, FieldInfo? comparerField, object[] knownComparers)
        {
            _comparerProperty = comparerProperty;
            _comparerField = comparerField;
            _knownComparers = knownComparers;
        }

        internal bool HasKnownComparer(object dictionary)
        {
            var comparer = _comparerProperty is not null ? _comparerProperty.GetValue(dictionary) : _comparerField!.GetValue(dictionary);

            // a null comparer field means the framework falls back to the default comparer
            if (comparer is null)
            {
                return true;
            }

            foreach (var knownComparer in _knownComparers)
            {
                if (ReferenceEquals(comparer, knownComparer))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
