﻿// Decompiled with JetBrains decompiler
// Type: System.Collections.Immutable.ImmutableHashSet
// Assembly: System.Collections.Immutable, Version=7.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 5F9FF90F-0D16-4469-A104-76829D3705E2
// Assembly location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.dll
// XML documentation location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.xml

using System.Collections.Generic;


#nullable enable
namespace Datadog.System.Collections.Immutable
{
    /// <summary>Provides a set of initialization methods for instances of the <see cref="T:System.Collections.Immutable.ImmutableHashSet`1" /> class.
    /// 
    /// NuGet package: System.Collections.Immutable (about immutable collections and how to install)</summary>
    public static class ImmutableHashSet
  {
    /// <summary>Creates an empty immutable hash set.</summary>
    /// <typeparam name="T">The type of items to be stored in the immutable hash set.</typeparam>
    /// <returns>An empty immutable hash set.</returns>
    public static ImmutableHashSet<T> Create<T>() => ImmutableHashSet<T>.Empty;

    /// <summary>Creates an empty immutable hash set that uses the specified equality comparer.</summary>
    /// <param name="equalityComparer">The object to use for comparing objects in the set for equality.</param>
    /// <typeparam name="T">The type of items in the immutable hash set.</typeparam>
    /// <returns>An empty immutable hash set.</returns>
    public static ImmutableHashSet<T> Create<T>(IEqualityComparer<T>? equalityComparer) => ImmutableHashSet<T>.Empty.WithComparer(equalityComparer);

    /// <summary>Creates a new immutable hash set that contains the specified item.</summary>
    /// <param name="item">The item to prepopulate the hash set with.</param>
    /// <typeparam name="T">The type of items in the immutable hash set.</typeparam>
    /// <returns>A new immutable hash set that contains the specified item.</returns>
    public static ImmutableHashSet<T> Create<T>(T item) => ImmutableHashSet<T>.Empty.Add(item);

    /// <summary>Creates a new immutable hash set that contains the specified item and uses the specified equality comparer for the set type.</summary>
    /// <param name="equalityComparer">The object to use for comparing objects in the set for equality.</param>
    /// <param name="item">The item to prepopulate the hash set with.</param>
    /// <typeparam name="T">The type of items in the immutable hash set.</typeparam>
    /// <returns>A new immutable hash set that contains the specified item.</returns>
    public static ImmutableHashSet<T> Create<T>(IEqualityComparer<T>? equalityComparer, T item) => ImmutableHashSet<T>.Empty.WithComparer(equalityComparer).Add(item);

    /// <summary>Creates a new immutable hash set prefilled with the specified items.</summary>
    /// <param name="items">The items to add to the hash set.</param>
    /// <typeparam name="T">The type of items stored by the collection.</typeparam>
    /// <returns>The new immutable hash set that contains the specified items.</returns>
    public static ImmutableHashSet<T> CreateRange<T>(IEnumerable<T> items) => ImmutableHashSet<T>.Empty.Union(items);

    /// <summary>Creates a new immutable hash set that contains the specified items and uses the specified equality comparer for the set type.</summary>
    /// <param name="equalityComparer">The object to use for comparing objects in the set for equality.</param>
    /// <param name="items">The items add to the collection before immutability is applied.</param>
    /// <typeparam name="T">The type of items stored in the collection.</typeparam>
    /// <returns>The new immutable hash set.</returns>
    public static ImmutableHashSet<T> CreateRange<T>(
      IEqualityComparer<T>? equalityComparer,
      IEnumerable<T> items)
    {
      return ImmutableHashSet<T>.Empty.WithComparer(equalityComparer).Union(items);
    }

    /// <summary>Creates a new immutable hash set that contains the specified array of items.</summary>
    /// <param name="items">An array that contains the items to prepopulate the hash set with.</param>
    /// <typeparam name="T">The type of items in the immutable hash set.</typeparam>
    /// <returns>A new immutable hash set that contains the specified items.</returns>
    public static ImmutableHashSet<T> Create<T>(params T[] items) => ImmutableHashSet<T>.Empty.Union((IEnumerable<T>) items);

    /// <summary>Creates a new immutable hash set that contains the items in the specified collection and uses the specified equality comparer for the set type.</summary>
    /// <param name="equalityComparer">The object to use for comparing objects in the set for equality.</param>
    /// <param name="items">An array that contains the items to prepopulate the hash set with.</param>
    /// <typeparam name="T">The type of items stored in the immutable hash set.</typeparam>
    /// <returns>A new immutable hash set that contains the specified items.</returns>
    public static ImmutableHashSet<T> Create<T>(
      IEqualityComparer<T>? equalityComparer,
      params T[] items)
    {
      return ImmutableHashSet<T>.Empty.WithComparer(equalityComparer).Union((IEnumerable<T>) items);
    }

    /// <summary>Creates a new immutable hash set builder.</summary>
    /// <typeparam name="T">The type of items stored by the collection.</typeparam>
    /// <returns>The immutable hash set builder.</returns>
    public static ImmutableHashSet<T>.Builder CreateBuilder<T>() => ImmutableHashSet.Create<T>().ToBuilder();

    /// <summary>Creates a new immutable hash set builder.</summary>
    /// <param name="equalityComparer">The object to use for comparing objects in the set for equality.</param>
    /// <typeparam name="T">The type of items stored by the collection.</typeparam>
    /// <returns>The new immutable hash set builder.</returns>
    public static ImmutableHashSet<T>.Builder CreateBuilder<T>(IEqualityComparer<T>? equalityComparer) => ImmutableHashSet.Create<T>(equalityComparer).ToBuilder();

    /// <summary>Enumerates a sequence, produces an immutable hash set of its contents, and uses the specified equality comparer for the set type.</summary>
    /// <param name="source">The sequence to enumerate.</param>
    /// <param name="equalityComparer">The object to use for comparing objects in the set for equality.</param>
    /// <typeparam name="TSource">The type of the elements in the sequence.</typeparam>
    /// <returns>An immutable hash set that contains the items in the specified sequence and uses the specified equality comparer.</returns>
    public static ImmutableHashSet<TSource> ToImmutableHashSet<TSource>(
      this IEnumerable<TSource> source,
      IEqualityComparer<TSource>? equalityComparer)
    {
      return source is ImmutableHashSet<TSource> immutableHashSet ? immutableHashSet.WithComparer(equalityComparer) : ImmutableHashSet<TSource>.Empty.WithComparer(equalityComparer).Union(source);
    }

    /// <summary>Creates an immutable hash set from the current contents of the builder's set.</summary>
    /// <param name="builder">The builder to create the immutable hash set from.</param>
    /// <typeparam name="TSource">The type of the elements in the hash set.</typeparam>
    /// <returns>An immutable hash set that contains the current contents in the builder's set.</returns>
    public static ImmutableHashSet<TSource> ToImmutableHashSet<TSource>(
      this ImmutableHashSet<TSource>.Builder builder)
    {
      Requires.NotNull<ImmutableHashSet<TSource>.Builder>(builder, nameof (builder));
      return builder.ToImmutable();
    }

    /// <summary>Enumerates a sequence and produces an immutable hash set of its contents.</summary>
    /// <param name="source">The sequence to enumerate.</param>
    /// <typeparam name="TSource">The type of the elements in the sequence.</typeparam>
    /// <returns>An immutable hash set that contains the items in the specified sequence.</returns>
    public static ImmutableHashSet<TSource> ToImmutableHashSet<TSource>(
      this IEnumerable<TSource> source)
    {
      return source.ToImmutableHashSet<TSource>((IEqualityComparer<TSource>) null);
    }
  }
}
