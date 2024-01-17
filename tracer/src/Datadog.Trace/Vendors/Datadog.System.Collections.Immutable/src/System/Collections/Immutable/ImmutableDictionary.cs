﻿// Decompiled with JetBrains decompiler
// Type: System.Collections.Immutable.ImmutableDictionary
// Assembly: System.Collections.Immutable, Version=7.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 5F9FF90F-0D16-4469-A104-76829D3705E2
// Assembly location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.dll
// XML documentation location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.xml

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.System.Linq;


#nullable enable
namespace Datadog.System.Collections.Immutable
{
    /// <summary>Provides a set of initialization methods for instances of the <see cref="T:System.Collections.Immutable.ImmutableDictionary`2" /> class.
    /// 
    /// NuGet package: System.Collections.Immutable (about immutable collections and how to install)</summary>
    public static class ImmutableDictionary
  {
    /// <summary>Creates an empty immutable dictionary.</summary>
    /// <typeparam name="TKey">The type of keys stored by the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of values stored by the dictionary.</typeparam>
    /// <returns>An empty immutable dictionary.</returns>
    public static ImmutableDictionary<TKey, TValue> Create<TKey, TValue>() where TKey : notnull => ImmutableDictionary<TKey, TValue>.Empty;

    /// <summary>Creates an empty immutable dictionary that uses the specified key comparer.</summary>
    /// <param name="keyComparer">The implementation to use to determine the equality of keys in the dictionary.</param>
    /// <typeparam name="TKey">The type of keys stored by the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of values stored by the dictionary.</typeparam>
    /// <returns>An empty immutable dictionary.</returns>
    public static ImmutableDictionary<TKey, TValue> Create<TKey, TValue>(
      IEqualityComparer<TKey>? keyComparer)
      where TKey : notnull
    {
      return ImmutableDictionary<TKey, TValue>.Empty.WithComparers(keyComparer);
    }

    /// <summary>Creates an empty immutable dictionary that uses the specified key and value comparers.</summary>
    /// <param name="keyComparer">The implementation to use to determine the equality of keys in the dictionary.</param>
    /// <param name="valueComparer">The implementation to use to determine the equality of values in the dictionary.</param>
    /// <typeparam name="TKey">The type of keys stored by the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of values stored by the dictionary.</typeparam>
    /// <returns>An empty immutable dictionary.</returns>
    public static ImmutableDictionary<TKey, TValue> Create<TKey, TValue>(
      IEqualityComparer<TKey>? keyComparer,
      IEqualityComparer<TValue>? valueComparer)
      where TKey : notnull
    {
      return ImmutableDictionary<TKey, TValue>.Empty.WithComparers(keyComparer, valueComparer);
    }

    /// <summary>Creates a new immutable dictionary that contains the specified items.</summary>
    /// <param name="items">The items used to populate the dictionary before it's immutable.</param>
    /// <typeparam name="TKey">The type of keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
    /// <returns>A new immutable dictionary that contains the specified items.</returns>
    public static ImmutableDictionary<TKey, TValue> CreateRange<TKey, TValue>(
      IEnumerable<KeyValuePair<TKey, TValue>> items)
      where TKey : notnull
    {
      return ImmutableDictionary<TKey, TValue>.Empty.AddRange(items);
    }

    /// <summary>Creates a new immutable dictionary that contains the specified items and uses the specified key comparer.</summary>
    /// <param name="keyComparer">The comparer implementation to use to compare keys for equality.</param>
    /// <param name="items">The items to add to the dictionary before it's immutable.</param>
    /// <typeparam name="TKey">The type of keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
    /// <returns>A new immutable dictionary that contains the specified items and uses the specified comparer.</returns>
    public static ImmutableDictionary<TKey, TValue> CreateRange<TKey, TValue>(
      IEqualityComparer<TKey>? keyComparer,
      IEnumerable<KeyValuePair<TKey, TValue>> items)
      where TKey : notnull
    {
      return ImmutableDictionary<TKey, TValue>.Empty.WithComparers(keyComparer).AddRange(items);
    }

    /// <summary>Creates a new immutable dictionary that contains the specified items and uses the specified key comparer.</summary>
    /// <param name="keyComparer">The comparer implementation to use to compare keys for equality.</param>
    /// <param name="valueComparer">The comparer implementation to use to compare values for equality.</param>
    /// <param name="items">The items to add to the dictionary before it's immutable.</param>
    /// <typeparam name="TKey">The type of keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
    /// <returns>A new immutable dictionary that contains the specified items and uses the specified comparer.</returns>
    public static ImmutableDictionary<TKey, TValue> CreateRange<TKey, TValue>(
      IEqualityComparer<TKey>? keyComparer,
      IEqualityComparer<TValue>? valueComparer,
      IEnumerable<KeyValuePair<TKey, TValue>> items)
      where TKey : notnull
    {
      return ImmutableDictionary<TKey, TValue>.Empty.WithComparers(keyComparer, valueComparer).AddRange(items);
    }

    /// <summary>Creates a new immutable dictionary builder.</summary>
    /// <typeparam name="TKey">The type of keys stored by the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of values stored by the dictionary.</typeparam>
    /// <returns>The new builder.</returns>
    public static ImmutableDictionary<TKey, TValue>.Builder CreateBuilder<TKey, TValue>() where TKey : notnull => ImmutableDictionary.Create<TKey, TValue>().ToBuilder();

    /// <summary>Creates a new immutable dictionary builder.</summary>
    /// <param name="keyComparer">The key comparer.</param>
    /// <typeparam name="TKey">The type of keys stored by the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of values stored by the dictionary.</typeparam>
    /// <returns>The new builder.</returns>
    public static ImmutableDictionary<TKey, TValue>.Builder CreateBuilder<TKey, TValue>(
      IEqualityComparer<TKey>? keyComparer)
      where TKey : notnull
    {
      return ImmutableDictionary.Create<TKey, TValue>(keyComparer).ToBuilder();
    }

    /// <summary>Creates a new immutable dictionary builder.</summary>
    /// <param name="keyComparer">The key comparer.</param>
    /// <param name="valueComparer">The value comparer.</param>
    /// <typeparam name="TKey">The type of keys stored by the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of values stored by the dictionary.</typeparam>
    /// <returns>The new builder.</returns>
    public static ImmutableDictionary<TKey, TValue>.Builder CreateBuilder<TKey, TValue>(
      IEqualityComparer<TKey>? keyComparer,
      IEqualityComparer<TValue>? valueComparer)
      where TKey : notnull
    {
      return ImmutableDictionary.Create<TKey, TValue>(keyComparer, valueComparer).ToBuilder();
    }

    /// <summary>Enumerates and transforms a sequence, and produces an immutable dictionary of its contents by using the specified key and value comparers.</summary>
    /// <param name="source">The sequence to enumerate to generate the dictionary.</param>
    /// <param name="keySelector">The function that will produce the key for the dictionary from each sequence element.</param>
    /// <param name="elementSelector">The function that will produce the value for the dictionary from each sequence element.</param>
    /// <param name="keyComparer">The key comparer to use for the dictionary.</param>
    /// <param name="valueComparer">The value comparer to use for the dictionary.</param>
    /// <typeparam name="TSource">The type of the elements in the sequence.</typeparam>
    /// <typeparam name="TKey">The type of the keys in the resulting dictionary.</typeparam>
    /// <typeparam name="TValue">The type of the values in the resulting dictionary.</typeparam>
    /// <returns>An immutable dictionary that contains the items in the specified sequence.</returns>
    public static ImmutableDictionary<TKey, TValue> ToImmutableDictionary<TSource, TKey, TValue>(
      this IEnumerable<TSource> source,
      Func<TSource, TKey> keySelector,
      Func<TSource, TValue> elementSelector,
      IEqualityComparer<TKey>? keyComparer,
      IEqualityComparer<TValue>? valueComparer)
      where TKey : notnull
    {
      Requires.NotNull<IEnumerable<TSource>>(source, nameof (source));
      Requires.NotNull<Func<TSource, TKey>>(keySelector, nameof (keySelector));
      Requires.NotNull<Func<TSource, TValue>>(elementSelector, nameof (elementSelector));
      return ImmutableDictionary<TKey, TValue>.Empty.WithComparers(keyComparer, valueComparer).AddRange(source.Select<TSource, KeyValuePair<TKey, TValue>>((Func<TSource, KeyValuePair<TKey, TValue>>) (element => new KeyValuePair<TKey, TValue>(keySelector(element), elementSelector(element)))));
    }

    /// <summary>Creates an immutable dictionary from the current contents of the builder's dictionary.</summary>
    /// <param name="builder">The builder to create the immutable dictionary from.</param>
    /// <typeparam name="TKey">The type of the keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of the values in the dictionary.</typeparam>
    /// <returns>An immutable dictionary that contains the current contents in the builder's dictionary.</returns>
    public static ImmutableDictionary<TKey, TValue> ToImmutableDictionary<TKey, TValue>(
      this ImmutableDictionary<TKey, TValue>.Builder builder)
      where TKey : notnull
    {
      Requires.NotNull<ImmutableDictionary<TKey, TValue>.Builder>(builder, nameof (builder));
      return builder.ToImmutable();
    }

    /// <summary>Enumerates and transforms a sequence, and produces an immutable dictionary of its contents by using the specified key comparer.</summary>
    /// <param name="source">The sequence to enumerate to generate the dictionary.</param>
    /// <param name="keySelector">The function that will produce the key for the dictionary from each sequence element.</param>
    /// <param name="elementSelector">The function that will produce the value for the dictionary from each sequence element.</param>
    /// <param name="keyComparer">The key comparer to use for the dictionary.</param>
    /// <typeparam name="TSource">The type of the elements in the sequence.</typeparam>
    /// <typeparam name="TKey">The type of the keys in the resulting dictionary.</typeparam>
    /// <typeparam name="TValue">The type of the values in the resulting dictionary.</typeparam>
    /// <returns>An immutable dictionary that contains the items in the specified sequence.</returns>
    public static ImmutableDictionary<TKey, TValue> ToImmutableDictionary<TSource, TKey, TValue>(
      this IEnumerable<TSource> source,
      Func<TSource, TKey> keySelector,
      Func<TSource, TValue> elementSelector,
      IEqualityComparer<TKey>? keyComparer)
      where TKey : notnull
    {
      return source.ToImmutableDictionary<TSource, TKey, TValue>(keySelector, elementSelector, keyComparer, (IEqualityComparer<TValue>) null);
    }

    /// <summary>Constructs an immutable dictionary from an existing collection of elements, applying a transformation function to the source keys.</summary>
    /// <param name="source">The source collection used to generate the immutable dictionary.</param>
    /// <param name="keySelector">The function used to transform keys for the immutable dictionary.</param>
    /// <typeparam name="TSource">The type of element in the source collection.</typeparam>
    /// <typeparam name="TKey">The type of key in the resulting immutable dictionary.</typeparam>
    /// <returns>The immutable dictionary that contains elements from <paramref name="source" />, with keys transformed by applying <paramref name="keySelector" />.</returns>
    public static ImmutableDictionary<TKey, TSource> ToImmutableDictionary<TSource, TKey>(
      this IEnumerable<TSource> source,
      Func<TSource, TKey> keySelector)
      where TKey : notnull
    {
      return source.ToImmutableDictionary<TSource, TKey, TSource>(keySelector, (Func<TSource, TSource>) (v => v), (IEqualityComparer<TKey>) null, (IEqualityComparer<TSource>) null);
    }

    /// <summary>Constructs an immutable dictionary based on some transformation of a sequence.</summary>
    /// <param name="source">The source collection used to generate the immutable dictionary.</param>
    /// <param name="keySelector">The function used to transform keys for the immutable dictionary.</param>
    /// <param name="keyComparer">The key comparer to use for the dictionary.</param>
    /// <typeparam name="TSource">The type of element in the source collection.</typeparam>
    /// <typeparam name="TKey">The type of key in the resulting immutable dictionary.</typeparam>
    /// <returns>The immutable dictionary that contains elements from <paramref name="source" />, with keys transformed by applying <paramref name="keySelector" />.</returns>
    public static ImmutableDictionary<TKey, TSource> ToImmutableDictionary<TSource, TKey>(
      this IEnumerable<TSource> source,
      Func<TSource, TKey> keySelector,
      IEqualityComparer<TKey>? keyComparer)
      where TKey : notnull
    {
      return source.ToImmutableDictionary<TSource, TKey, TSource>(keySelector, (Func<TSource, TSource>) (v => v), keyComparer, (IEqualityComparer<TSource>) null);
    }

    /// <summary>Enumerates and transforms a sequence, and produces an immutable dictionary of its contents.</summary>
    /// <param name="source">The sequence to enumerate to generate the dictionary.</param>
    /// <param name="keySelector">The function that will produce the key for the dictionary from each sequence element.</param>
    /// <param name="elementSelector">The function that will produce the value for the dictionary from each sequence element.</param>
    /// <typeparam name="TSource">The type of the elements in the sequence.</typeparam>
    /// <typeparam name="TKey">The type of the keys in the resulting dictionary.</typeparam>
    /// <typeparam name="TValue">The type of the values in the resulting dictionary.</typeparam>
    /// <returns>An immutable dictionary that contains the items in the specified sequence.</returns>
    public static ImmutableDictionary<TKey, TValue> ToImmutableDictionary<TSource, TKey, TValue>(
      this IEnumerable<TSource> source,
      Func<TSource, TKey> keySelector,
      Func<TSource, TValue> elementSelector)
      where TKey : notnull
    {
      return source.ToImmutableDictionary<TSource, TKey, TValue>(keySelector, elementSelector, (IEqualityComparer<TKey>) null, (IEqualityComparer<TValue>) null);
    }

    /// <summary>Enumerates a sequence of key/value pairs and produces an immutable dictionary of its contents by using the specified key and value comparers.</summary>
    /// <param name="source">The sequence of key/value pairs to enumerate.</param>
    /// <param name="keyComparer">The key comparer to use when building the immutable dictionary.</param>
    /// <param name="valueComparer">The value comparer to use for the immutable dictionary.</param>
    /// <typeparam name="TKey">The type of the keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of the values in the dictionary.</typeparam>
    /// <returns>An immutable dictionary that contains the key/value pairs in the specified sequence.</returns>
    public static ImmutableDictionary<TKey, TValue> ToImmutableDictionary<TKey, TValue>(
      this IEnumerable<KeyValuePair<TKey, TValue>> source,
      IEqualityComparer<TKey>? keyComparer,
      IEqualityComparer<TValue>? valueComparer)
      where TKey : notnull
    {
      Requires.NotNull<IEnumerable<KeyValuePair<TKey, TValue>>>(source, nameof (source));
      return source is ImmutableDictionary<TKey, TValue> immutableDictionary ? immutableDictionary.WithComparers(keyComparer, valueComparer) : ImmutableDictionary<TKey, TValue>.Empty.WithComparers(keyComparer, valueComparer).AddRange(source);
    }

    /// <summary>Enumerates a sequence of key/value pairs and produces an immutable dictionary of its contents by using the specified key comparer.</summary>
    /// <param name="source">The sequence of key/value pairs to enumerate.</param>
    /// <param name="keyComparer">The key comparer to use when building the immutable dictionary.</param>
    /// <typeparam name="TKey">The type of the keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of the values in the dictionary.</typeparam>
    /// <returns>An immutable dictionary that contains the key/value pairs in the specified sequence.</returns>
    public static ImmutableDictionary<TKey, TValue> ToImmutableDictionary<TKey, TValue>(
      this IEnumerable<KeyValuePair<TKey, TValue>> source,
      IEqualityComparer<TKey>? keyComparer)
      where TKey : notnull
    {
      return source.ToImmutableDictionary<TKey, TValue>(keyComparer, (IEqualityComparer<TValue>) null);
    }

    /// <summary>Enumerates a sequence of key/value pairs and produces an immutable dictionary of its contents.</summary>
    /// <param name="source">The sequence of key/value pairs to enumerate.</param>
    /// <typeparam name="TKey">The type of the keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of the values in the dictionary.</typeparam>
    /// <returns>An immutable dictionary that contains the key/value pairs in the specified sequence.</returns>
    public static ImmutableDictionary<TKey, TValue> ToImmutableDictionary<TKey, TValue>(
      this IEnumerable<KeyValuePair<TKey, TValue>> source)
      where TKey : notnull
    {
      return source.ToImmutableDictionary<TKey, TValue>((IEqualityComparer<TKey>) null, (IEqualityComparer<TValue>) null);
    }

    /// <summary>Determines whether the specified immutable dictionary contains the specified key/value pair.</summary>
    /// <param name="map">The immutable dictionary to search.</param>
    /// <param name="key">The key to locate in the immutable dictionary.</param>
    /// <param name="value">The value to locate on the specified key, if the key is found.</param>
    /// <typeparam name="TKey">The type of the keys in the immutable dictionary.</typeparam>
    /// <typeparam name="TValue">The type of the values in the immutable dictionary.</typeparam>
    /// <returns>
    /// <see langword="true" /> if this map contains the specified key/value pair; otherwise, <see langword="false" />.</returns>
    public static bool Contains<TKey, TValue>(
      this IImmutableDictionary<TKey, TValue> map,
      TKey key,
      TValue value)
      where TKey : notnull
    {
      Requires.NotNull<IImmutableDictionary<TKey, TValue>>(map, nameof (map));
      Requires.NotNullAllowStructs<TKey>(key, nameof (key));
      return map.Contains(new KeyValuePair<TKey, TValue>(key, value));
    }

    /// <summary>Gets the value for a given key if a matching key exists in the dictionary.</summary>
    /// <param name="dictionary">The dictionary to retrieve the value from.</param>
    /// <param name="key">The key to search for.</param>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <returns>The value for the key, or <c>default(TValue)</c> if no matching key was found.</returns>
    public static TValue? GetValueOrDefault<TKey, TValue>(
      this IImmutableDictionary<TKey, TValue> dictionary,
      TKey key)
      where TKey : notnull
    {
      return dictionary.GetValueOrDefault<TKey, TValue>(key, default (TValue));
    }

    /// <summary>Gets the value for a given key if a matching key exists in the dictionary.</summary>
    /// <param name="dictionary">The dictionary to retrieve the value from.</param>
    /// <param name="key">The key to search for.</param>
    /// <param name="defaultValue">The default value to return if no matching key is found in the dictionary.</param>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <returns>The value for the key, or <paramref name="defaultValue" /> if no matching key was found.</returns>
    public static TValue GetValueOrDefault<TKey, TValue>(
      this IImmutableDictionary<TKey, TValue> dictionary,
      TKey key,
      TValue defaultValue)
      where TKey : notnull
    {
      Requires.NotNull<IImmutableDictionary<TKey, TValue>>(dictionary, nameof (dictionary));
      Requires.NotNullAllowStructs<TKey>(key, nameof (key));
      TValue obj;
      return dictionary.TryGetValue(key, out obj) ? obj : defaultValue;
    }
  }
}
