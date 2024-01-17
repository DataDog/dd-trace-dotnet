﻿// Decompiled with JetBrains decompiler
// Type: System.Collections.Immutable.ImmutableSortedDictionary
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
    /// <summary>Provides a set of initialization methods for instances of the <see cref="T:System.Collections.Immutable.ImmutableSortedDictionary`2" /> class.
    /// 
    /// NuGet package: System.Collections.Immutable (about immutable collections and how to install)</summary>
    public static class ImmutableSortedDictionary
  {
    /// <summary>Creates an empty immutable sorted dictionary.</summary>
    /// <typeparam name="TKey">The type of keys stored by the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of values stored by the dictionary.</typeparam>
    /// <returns>An empty immutable sorted dictionary.</returns>
    public static ImmutableSortedDictionary<TKey, TValue> Create<TKey, TValue>() where TKey : notnull => ImmutableSortedDictionary<TKey, TValue>.Empty;

    /// <summary>Creates an empty immutable sorted dictionary that uses the specified key comparer.</summary>
    /// <param name="keyComparer">The implementation to use to determine the equality of keys in the dictionary.</param>
    /// <typeparam name="TKey">The type of keys stored by the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of values stored by the dictionary.</typeparam>
    /// <returns>An empty immutable sorted dictionary.</returns>
    public static ImmutableSortedDictionary<TKey, TValue> Create<TKey, TValue>(
      IComparer<TKey>? keyComparer)
      where TKey : notnull
    {
      return ImmutableSortedDictionary<TKey, TValue>.Empty.WithComparers(keyComparer);
    }

    /// <summary>Creates an empty immutable sorted dictionary that uses the specified key and value comparers.</summary>
    /// <param name="keyComparer">The implementation to use to determine the equality of keys in the dictionary.</param>
    /// <param name="valueComparer">The implementation to use to determine the equality of values in the dictionary.</param>
    /// <typeparam name="TKey">The type of keys stored by the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of values stored by the dictionary.</typeparam>
    /// <returns>An empty immutable sorted dictionary.</returns>
    public static ImmutableSortedDictionary<TKey, TValue> Create<TKey, TValue>(
      IComparer<TKey>? keyComparer,
      IEqualityComparer<TValue>? valueComparer)
      where TKey : notnull
    {
      return ImmutableSortedDictionary<TKey, TValue>.Empty.WithComparers(keyComparer, valueComparer);
    }

    /// <summary>Creates an immutable sorted dictionary that contains the specified items and uses the default comparer.</summary>
    /// <param name="items">The items to add to the sorted dictionary before it's immutable.</param>
    /// <typeparam name="TKey">The type of keys stored in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of values stored in the dictionary.</typeparam>
    /// <returns>An immutable sorted dictionary that contains the specified items.</returns>
    public static ImmutableSortedDictionary<TKey, TValue> CreateRange<TKey, TValue>(
      IEnumerable<KeyValuePair<TKey, TValue>> items)
      where TKey : notnull
    {
      return ImmutableSortedDictionary<TKey, TValue>.Empty.AddRange(items);
    }

    /// <summary>Creates a new immutable sorted dictionary from the specified range of items with the specified key comparer.</summary>
    /// <param name="keyComparer">The comparer implementation to use to evaluate keys for equality and sorting.</param>
    /// <param name="items">The items to add to the sorted dictionary.</param>
    /// <typeparam name="TKey">The type of keys stored in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of values stored in the dictionary.</typeparam>
    /// <returns>The new immutable sorted dictionary that contains the specified items and uses the specified key comparer.</returns>
    public static ImmutableSortedDictionary<TKey, TValue> CreateRange<TKey, TValue>(
      IComparer<TKey>? keyComparer,
      IEnumerable<KeyValuePair<TKey, TValue>> items)
      where TKey : notnull
    {
      return ImmutableSortedDictionary<TKey, TValue>.Empty.WithComparers(keyComparer).AddRange(items);
    }

    /// <summary>Creates a new immutable sorted dictionary from the specified range of items with the specified key and value comparers.</summary>
    /// <param name="keyComparer">The comparer implementation to use to compare keys for equality and sorting.</param>
    /// <param name="valueComparer">The comparer implementation to use to compare values for equality.</param>
    /// <param name="items">The items to add to the sorted dictionary before it's immutable.</param>
    /// <typeparam name="TKey">The type of keys stored in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of values stored in the dictionary.</typeparam>
    /// <returns>An immutable sorted dictionary that contains the specified items and uses the specified comparers.</returns>
    public static ImmutableSortedDictionary<TKey, TValue> CreateRange<TKey, TValue>(
      IComparer<TKey>? keyComparer,
      IEqualityComparer<TValue>? valueComparer,
      IEnumerable<KeyValuePair<TKey, TValue>> items)
      where TKey : notnull
    {
      return ImmutableSortedDictionary<TKey, TValue>.Empty.WithComparers(keyComparer, valueComparer).AddRange(items);
    }

    /// <summary>Creates a new immutable sorted dictionary builder.</summary>
    /// <typeparam name="TKey">The type of keys stored by the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of values stored by the dictionary.</typeparam>
    /// <returns>The immutable collection builder.</returns>
    public static ImmutableSortedDictionary<TKey, TValue>.Builder CreateBuilder<TKey, TValue>() where TKey : notnull => ImmutableSortedDictionary.Create<TKey, TValue>().ToBuilder();

    /// <summary>Creates a new immutable sorted dictionary builder.</summary>
    /// <param name="keyComparer">The key comparer.</param>
    /// <typeparam name="TKey">The type of keys stored by the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of values stored by the dictionary.</typeparam>
    /// <returns>The immutable collection builder.</returns>
    public static ImmutableSortedDictionary<TKey, TValue>.Builder CreateBuilder<TKey, TValue>(
      IComparer<TKey>? keyComparer)
      where TKey : notnull
    {
      return ImmutableSortedDictionary.Create<TKey, TValue>(keyComparer).ToBuilder();
    }

    /// <summary>Creates a new immutable sorted dictionary builder.</summary>
    /// <param name="keyComparer">The key comparer.</param>
    /// <param name="valueComparer">The value comparer.</param>
    /// <typeparam name="TKey">The type of keys stored by the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of values stored by the dictionary.</typeparam>
    /// <returns>The immutable collection builder.</returns>
    public static ImmutableSortedDictionary<TKey, TValue>.Builder CreateBuilder<TKey, TValue>(
      IComparer<TKey>? keyComparer,
      IEqualityComparer<TValue>? valueComparer)
      where TKey : notnull
    {
      return ImmutableSortedDictionary.Create<TKey, TValue>(keyComparer, valueComparer).ToBuilder();
    }

    /// <summary>Enumerates and transforms a sequence, and produces an immutable sorted dictionary of its contents by using the specified key and value comparers.</summary>
    /// <param name="source">The sequence to enumerate to generate the dictionary.</param>
    /// <param name="keySelector">The function that will produce the key for the dictionary from each sequence element.</param>
    /// <param name="elementSelector">The function that will produce the value for the dictionary from each sequence element.</param>
    /// <param name="keyComparer">The key comparer to use for the dictionary.</param>
    /// <param name="valueComparer">The value comparer to use for the dictionary.</param>
    /// <typeparam name="TSource">The type of the elements in the sequence.</typeparam>
    /// <typeparam name="TKey">The type of the keys in the resulting dictionary.</typeparam>
    /// <typeparam name="TValue">The type of the values in the resulting dictionary.</typeparam>
    /// <returns>An immutable sorted dictionary that contains the items in the specified sequence.</returns>
    public static ImmutableSortedDictionary<TKey, TValue> ToImmutableSortedDictionary<TSource, TKey, TValue>(
      this IEnumerable<TSource> source,
      Func<TSource, TKey> keySelector,
      Func<TSource, TValue> elementSelector,
      IComparer<TKey>? keyComparer,
      IEqualityComparer<TValue>? valueComparer)
      where TKey : notnull
    {
      Requires.NotNull<IEnumerable<TSource>>(source, nameof (source));
      Requires.NotNull<Func<TSource, TKey>>(keySelector, nameof (keySelector));
      Requires.NotNull<Func<TSource, TValue>>(elementSelector, nameof (elementSelector));
      return ImmutableSortedDictionary<TKey, TValue>.Empty.WithComparers(keyComparer, valueComparer).AddRange(source.Select<TSource, KeyValuePair<TKey, TValue>>((Func<TSource, KeyValuePair<TKey, TValue>>) (element => new KeyValuePair<TKey, TValue>(keySelector(element), elementSelector(element)))));
    }

    /// <summary>Creates an immutable sorted dictionary from the current contents of the builder's dictionary.</summary>
    /// <param name="builder">The builder to create the immutable sorted dictionary from.</param>
    /// <typeparam name="TKey">The type of the keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of the values in the dictionary.</typeparam>
    /// <returns>An immutable sorted dictionary that contains the current contents in the builder's dictionary.</returns>
    public static ImmutableSortedDictionary<TKey, TValue> ToImmutableSortedDictionary<TKey, TValue>(
      this ImmutableSortedDictionary<TKey, TValue>.Builder builder)
      where TKey : notnull
    {
      Requires.NotNull<ImmutableSortedDictionary<TKey, TValue>.Builder>(builder, nameof (builder));
      return builder.ToImmutable();
    }

    /// <summary>Enumerates and transforms a sequence, and produces an immutable sorted dictionary of its contents by using the specified key comparer.</summary>
    /// <param name="source">The sequence to enumerate to generate the dictionary.</param>
    /// <param name="keySelector">The function that will produce the key for the dictionary from each sequence element.</param>
    /// <param name="elementSelector">The function that will produce the value for the dictionary from each sequence element.</param>
    /// <param name="keyComparer">The key comparer to use for the dictionary.</param>
    /// <typeparam name="TSource">The type of the elements in the sequence.</typeparam>
    /// <typeparam name="TKey">The type of the keys in the resulting dictionary.</typeparam>
    /// <typeparam name="TValue">The type of the values in the resulting dictionary.</typeparam>
    /// <returns>An immutable dictionary that contains the items in the specified sequence.</returns>
    public static ImmutableSortedDictionary<TKey, TValue> ToImmutableSortedDictionary<TSource, TKey, TValue>(
      this IEnumerable<TSource> source,
      Func<TSource, TKey> keySelector,
      Func<TSource, TValue> elementSelector,
      IComparer<TKey>? keyComparer)
      where TKey : notnull
    {
      return source.ToImmutableSortedDictionary<TSource, TKey, TValue>(keySelector, elementSelector, keyComparer, (IEqualityComparer<TValue>) null);
    }

    /// <summary>Enumerates and transforms a sequence, and produces an immutable sorted dictionary of its contents.</summary>
    /// <param name="source">The sequence to enumerate to generate the dictionary.</param>
    /// <param name="keySelector">The function that will produce the key for the dictionary from each sequence element.</param>
    /// <param name="elementSelector">The function that will produce the value for the dictionary from each sequence element.</param>
    /// <typeparam name="TSource">The type of the elements in the sequence.</typeparam>
    /// <typeparam name="TKey">The type of the keys in the resulting dictionary.</typeparam>
    /// <typeparam name="TValue">The type of the values in the resulting dictionary.</typeparam>
    /// <returns>An immutable sorted dictionary that contains the items in the specified sequence.</returns>
    public static ImmutableSortedDictionary<TKey, TValue> ToImmutableSortedDictionary<TSource, TKey, TValue>(
      this IEnumerable<TSource> source,
      Func<TSource, TKey> keySelector,
      Func<TSource, TValue> elementSelector)
      where TKey : notnull
    {
      return source.ToImmutableSortedDictionary<TSource, TKey, TValue>(keySelector, elementSelector, (IComparer<TKey>) null, (IEqualityComparer<TValue>) null);
    }

    /// <summary>Enumerates a sequence of key/value pairs and produces an immutable sorted dictionary of its contents by using the specified key and value comparers.</summary>
    /// <param name="source">The sequence of key/value pairs to enumerate.</param>
    /// <param name="keyComparer">The key comparer to use when building the immutable dictionary.</param>
    /// <param name="valueComparer">The value comparer to use for the immutable dictionary.</param>
    /// <typeparam name="TKey">The type of the keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of the values in the dictionary.</typeparam>
    /// <returns>An immutable sorted dictionary that contains the key/value pairs in the specified sequence.</returns>
    public static ImmutableSortedDictionary<TKey, TValue> ToImmutableSortedDictionary<TKey, TValue>(
      this IEnumerable<KeyValuePair<TKey, TValue>> source,
      IComparer<TKey>? keyComparer,
      IEqualityComparer<TValue>? valueComparer)
      where TKey : notnull
    {
      Requires.NotNull<IEnumerable<KeyValuePair<TKey, TValue>>>(source, nameof (source));
      return source is ImmutableSortedDictionary<TKey, TValue> sortedDictionary ? sortedDictionary.WithComparers(keyComparer, valueComparer) : ImmutableSortedDictionary<TKey, TValue>.Empty.WithComparers(keyComparer, valueComparer).AddRange(source);
    }

    /// <summary>Enumerates a sequence of key/value pairs and produces an immutable dictionary of its contents by using the specified key comparer.</summary>
    /// <param name="source">The sequence of key/value pairs to enumerate.</param>
    /// <param name="keyComparer">The key comparer to use when building the immutable dictionary.</param>
    /// <typeparam name="TKey">The type of the keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of the values in the dictionary.</typeparam>
    /// <returns>An immutable sorted dictionary that contains the key/value pairs in the specified sequence.</returns>
    public static ImmutableSortedDictionary<TKey, TValue> ToImmutableSortedDictionary<TKey, TValue>(
      this IEnumerable<KeyValuePair<TKey, TValue>> source,
      IComparer<TKey>? keyComparer)
      where TKey : notnull
    {
      return source.ToImmutableSortedDictionary<TKey, TValue>(keyComparer, (IEqualityComparer<TValue>) null);
    }

    /// <summary>Enumerates a sequence of key/value pairs and produces an immutable sorted dictionary of its contents.</summary>
    /// <param name="source">The sequence of key/value pairs to enumerate.</param>
    /// <typeparam name="TKey">The type of the keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of the values in the dictionary.</typeparam>
    /// <returns>An immutable sorted dictionary that contains the key/value pairs in the specified sequence.</returns>
    public static ImmutableSortedDictionary<TKey, TValue> ToImmutableSortedDictionary<TKey, TValue>(
      this IEnumerable<KeyValuePair<TKey, TValue>> source)
      where TKey : notnull
    {
      return source.ToImmutableSortedDictionary<TKey, TValue>((IComparer<TKey>) null, (IEqualityComparer<TValue>) null);
    }
  }
}
