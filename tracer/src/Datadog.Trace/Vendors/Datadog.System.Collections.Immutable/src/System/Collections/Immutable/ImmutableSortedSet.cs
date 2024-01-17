﻿// Decompiled with JetBrains decompiler
// Type: System.Collections.Immutable.ImmutableSortedSet
// Assembly: System.Collections.Immutable, Version=7.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 5F9FF90F-0D16-4469-A104-76829D3705E2
// Assembly location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.dll
// XML documentation location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.xml

using System.Collections.Generic;


#nullable enable
namespace Datadog.System.Collections.Immutable
{
    /// <summary>Provides a set of initialization methods for instances of the <see cref="T:System.Collections.Immutable.ImmutableSortedSet`1" /> class.
    /// 
    /// NuGet package: System.Collections.Immutable (about immutable collections and how to install)</summary>
    public static class ImmutableSortedSet
  {
    /// <summary>Creates an empty immutable sorted set.</summary>
    /// <typeparam name="T">The type of items to be stored in the immutable set.</typeparam>
    /// <returns>An empty immutable sorted set.</returns>
    public static ImmutableSortedSet<T> Create<T>() => ImmutableSortedSet<T>.Empty;

    /// <summary>Creates an empty immutable sorted set that uses the specified comparer.</summary>
    /// <param name="comparer">The implementation to use when comparing items in the set.</param>
    /// <typeparam name="T">The type of items in the immutable set.</typeparam>
    /// <returns>An empty immutable set.</returns>
    public static ImmutableSortedSet<T> Create<T>(IComparer<T>? comparer) => ImmutableSortedSet<T>.Empty.WithComparer(comparer);

    /// <summary>Creates a new immutable sorted set that contains the specified item.</summary>
    /// <param name="item">The item to prepopulate the set with.</param>
    /// <typeparam name="T">The type of items in the immutable set.</typeparam>
    /// <returns>A new immutable set that contains the specified item.</returns>
    public static ImmutableSortedSet<T> Create<T>(T item) => ImmutableSortedSet<T>.Empty.Add(item);

    /// <summary>Creates a new immutable sorted set that contains the specified item and uses the specified comparer.</summary>
    /// <param name="comparer">The implementation to use when comparing items in the set.</param>
    /// <param name="item">The item to prepopulate the set with.</param>
    /// <typeparam name="T">The type of items stored in the immutable set.</typeparam>
    /// <returns>A new immutable set that contains the specified item.</returns>
    public static ImmutableSortedSet<T> Create<T>(IComparer<T>? comparer, T item) => ImmutableSortedSet<T>.Empty.WithComparer(comparer).Add(item);

    /// <summary>Creates a new immutable collection that contains the specified items.</summary>
    /// <param name="items">The items to add to the set with before it's immutable.</param>
    /// <typeparam name="T">The type of items stored by the collection.</typeparam>
    /// <returns>The new immutable set that contains the specified items.</returns>
    public static ImmutableSortedSet<T> CreateRange<T>(IEnumerable<T> items) => ImmutableSortedSet<T>.Empty.Union(items);

    /// <summary>Creates a new immutable collection that contains the specified items.</summary>
    /// <param name="comparer">The comparer to use to compare elements in this set.</param>
    /// <param name="items">The items to add to the set before it's immutable.</param>
    /// <typeparam name="T">The type of items stored by the collection.</typeparam>
    /// <returns>The new immutable set that contains the specified items.</returns>
    public static ImmutableSortedSet<T> CreateRange<T>(IComparer<T>? comparer, IEnumerable<T> items) => ImmutableSortedSet<T>.Empty.WithComparer(comparer).Union(items);

    /// <summary>Creates a new immutable sorted set that contains the specified array of items.</summary>
    /// <param name="items">An array that contains the items to prepopulate the set with.</param>
    /// <typeparam name="T">The type of items in the immutable set.</typeparam>
    /// <returns>A new immutable set that contains the specified items.</returns>
    public static ImmutableSortedSet<T> Create<T>(params T[] items) => ImmutableSortedSet<T>.Empty.Union((IEnumerable<T>) items);

    /// <summary>Creates a new immutable sorted set that contains the specified array of items and uses the specified comparer.</summary>
    /// <param name="comparer">The implementation to use when comparing items in the set.</param>
    /// <param name="items">An array that contains the items to prepopulate the set with.</param>
    /// <typeparam name="T">The type of items in the immutable set.</typeparam>
    /// <returns>A new immutable set that contains the specified items.</returns>
    public static ImmutableSortedSet<T> Create<T>(IComparer<T>? comparer, params T[] items) => ImmutableSortedSet<T>.Empty.WithComparer(comparer).Union((IEnumerable<T>) items);

    /// <summary>Returns a collection that can be used to build an immutable sorted set.</summary>
    /// <typeparam name="T">The type of items stored by the collection.</typeparam>
    /// <returns>The immutable collection builder.</returns>
    public static ImmutableSortedSet<T>.Builder CreateBuilder<T>() => ImmutableSortedSet.Create<T>().ToBuilder();

    /// <summary>Returns a collection that can be used to build an immutable sorted set.</summary>
    /// <param name="comparer">The comparer used to compare items in the set for equality.</param>
    /// <typeparam name="T">The type of items stored by the collection.</typeparam>
    /// <returns>The immutable collection.</returns>
    public static ImmutableSortedSet<T>.Builder CreateBuilder<T>(IComparer<T>? comparer) => ImmutableSortedSet.Create<T>(comparer).ToBuilder();

    /// <summary>Enumerates a sequence, produces an immutable sorted set of its contents, and uses the specified comparer.</summary>
    /// <param name="source">The sequence to enumerate.</param>
    /// <param name="comparer">The comparer to use for initializing and adding members to the sorted set.</param>
    /// <typeparam name="TSource">The type of the elements in the sequence.</typeparam>
    /// <returns>An immutable sorted set that contains the items in the specified sequence.</returns>
    public static ImmutableSortedSet<TSource> ToImmutableSortedSet<TSource>(
      this IEnumerable<TSource> source,
      IComparer<TSource>? comparer)
    {
      return source is ImmutableSortedSet<TSource> immutableSortedSet ? immutableSortedSet.WithComparer(comparer) : ImmutableSortedSet<TSource>.Empty.WithComparer(comparer).Union(source);
    }

    /// <summary>Enumerates a sequence and produces an immutable sorted set of its contents.</summary>
    /// <param name="source">The sequence to enumerate.</param>
    /// <typeparam name="TSource">The type of the elements in the sequence.</typeparam>
    /// <returns>An immutable sorted set that contains the items in the specified sequence.</returns>
    public static ImmutableSortedSet<TSource> ToImmutableSortedSet<TSource>(
      this IEnumerable<TSource> source)
    {
      return source.ToImmutableSortedSet<TSource>((IComparer<TSource>) null);
    }

    /// <summary>Creates an immutable sorted set from the current contents of the builder's set.</summary>
    /// <param name="builder">The builder to create the immutable sorted set from.</param>
    /// <typeparam name="TSource">The type of the elements in the immutable sorted set.</typeparam>
    /// <returns>An immutable sorted set that contains the current contents in the builder's set.</returns>
    public static ImmutableSortedSet<TSource> ToImmutableSortedSet<TSource>(
      this ImmutableSortedSet<TSource>.Builder builder)
    {
      Requires.NotNull<ImmutableSortedSet<TSource>.Builder>(builder, nameof (builder));
      return builder.ToImmutable();
    }
  }
}
