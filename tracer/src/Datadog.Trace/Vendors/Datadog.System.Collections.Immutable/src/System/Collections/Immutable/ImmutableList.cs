﻿// Decompiled with JetBrains decompiler
// Type: System.Collections.Immutable.ImmutableList
// Assembly: System.Collections.Immutable, Version=7.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 5F9FF90F-0D16-4469-A104-76829D3705E2
// Assembly location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.dll
// XML documentation location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.xml

using System.Collections.Generic;


#nullable enable
namespace Datadog.System.Collections.Immutable
{
    /// <summary>Provides a set of initialization methods for instances of the <see cref="T:System.Collections.Immutable.ImmutableList`1" /> class.
    /// 
    /// NuGet package: System.Collections.Immutable (about immutable collections and how to install)</summary>
    public static class ImmutableList
  {
    /// <summary>Creates an empty immutable list.</summary>
    /// <typeparam name="T">The type of items to be stored in the .</typeparam>
    /// <returns>An empty immutable list.</returns>
    public static ImmutableList<T> Create<T>() => ImmutableList<T>.Empty;

    /// <summary>Creates a new immutable list that contains the specified item.</summary>
    /// <param name="item">The item to prepopulate the list with.</param>
    /// <typeparam name="T">The type of items in the .</typeparam>
    /// <returns>A new  that contains the specified item.</returns>
    public static ImmutableList<T> Create<T>(T item) => ImmutableList<T>.Empty.Add(item);

    /// <summary>Creates a new immutable list that contains the specified items.</summary>
    /// <param name="items">The items to add to the list.</param>
    /// <typeparam name="T">The type of items in the .</typeparam>
    /// <returns>An immutable list that contains the specified items.</returns>
    public static ImmutableList<T> CreateRange<T>(IEnumerable<T> items) => ImmutableList<T>.Empty.AddRange(items);

    /// <summary>Creates a new immutable list that contains the specified array of items.</summary>
    /// <param name="items">An array that contains the items to prepopulate the list with.</param>
    /// <typeparam name="T">The type of items in the .</typeparam>
    /// <returns>A new immutable list that contains the specified items.</returns>
    public static ImmutableList<T> Create<T>(params T[] items) => ImmutableList<T>.Empty.AddRange((IEnumerable<T>) items);

    /// <summary>Creates a new immutable list builder.</summary>
    /// <typeparam name="T">The type of items stored by the collection.</typeparam>
    /// <returns>The immutable collection builder.</returns>
    public static ImmutableList<T>.Builder CreateBuilder<T>() => ImmutableList.Create<T>().ToBuilder();

    /// <summary>Enumerates a sequence and produces an immutable list of its contents.</summary>
    /// <param name="source">The sequence to enumerate.</param>
    /// <typeparam name="TSource">The type of the elements in the sequence.</typeparam>
    /// <returns>An immutable list that contains the items in the specified sequence.</returns>
    public static ImmutableList<TSource> ToImmutableList<TSource>(this IEnumerable<TSource> source) => source is ImmutableList<TSource> immutableList ? immutableList : ImmutableList<TSource>.Empty.AddRange(source);

    /// <summary>Creates an immutable list from the current contents of the builder's collection.</summary>
    /// <param name="builder">The builder to create the immutable list from.</param>
    /// <typeparam name="TSource">The type of the elements in the list.</typeparam>
    /// <returns>An immutable list that contains the current contents in the builder's collection.</returns>
    public static ImmutableList<TSource> ToImmutableList<TSource>(
      this ImmutableList<TSource>.Builder builder)
    {
      Requires.NotNull<ImmutableList<TSource>.Builder>(builder, nameof (builder));
      return builder.ToImmutable();
    }

    /// <summary>Replaces the first equal element in the list with the specified element.</summary>
    /// <param name="list">The list to search.</param>
    /// <param name="oldValue">The element to replace.</param>
    /// <param name="newValue">The element to replace the old element with.</param>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <exception cref="T:System.ArgumentException">
    /// <paramref name="oldValue" /> does not exist in the list.</exception>
    /// <returns>The new list -- even if the value being replaced is equal to the new value for that position.</returns>
    public static IImmutableList<T> Replace<T>(this IImmutableList<T> list, T oldValue, T newValue)
    {
      Requires.NotNull<IImmutableList<T>>(list, nameof (list));
      return list.Replace(oldValue, newValue, (IEqualityComparer<T>) EqualityComparer<T>.Default);
    }

    /// <summary>Removes the specified value from this list.</summary>
    /// <param name="list">The list to search.</param>
    /// <param name="value">The value to remove.</param>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <returns>A new immutable list with the element removed, or this list if the element is not in this list.</returns>
    public static IImmutableList<T> Remove<T>(this IImmutableList<T> list, T value)
    {
      Requires.NotNull<IImmutableList<T>>(list, nameof (list));
      return list.Remove(value, (IEqualityComparer<T>) EqualityComparer<T>.Default);
    }

    /// <summary>Removes the specified values from this list.</summary>
    /// <param name="list">The list to search.</param>
    /// <param name="items">The items to remove if matches are found in this list.</param>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <returns>A new immutable list with the elements removed.</returns>
    public static IImmutableList<T> RemoveRange<T>(
      this IImmutableList<T> list,
      IEnumerable<T> items)
    {
      Requires.NotNull<IImmutableList<T>>(list, nameof (list));
      return list.RemoveRange(items, (IEqualityComparer<T>) EqualityComparer<T>.Default);
    }

    /// <summary>Searches for the specified object and returns the zero-based index of the first occurrence within the list.</summary>
    /// <param name="list">The list to search.</param>
    /// <param name="item">The object to locate in the list. The value can be null for reference types.</param>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <returns>The zero-based index of the first occurrence of item within the range of elements in the list that extends from index to the last element, if found; otherwise, -1.</returns>
    public static int IndexOf<T>(this IImmutableList<T> list, T item)
    {
      Requires.NotNull<IImmutableList<T>>(list, nameof (list));
      return list.IndexOf(item, 0, list.Count, (IEqualityComparer<T>) EqualityComparer<T>.Default);
    }

    /// <summary>Searches for the specified object and returns the zero-based index of the first occurrence within the list.</summary>
    /// <param name="list">The list to search.</param>
    /// <param name="item">The object to locate in the Immutable list. The value can be null for reference types.</param>
    /// <param name="equalityComparer">The equality comparer to use in the search.</param>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <returns>The zero-based index of the first occurrence of item within the range of elements in the immutable list that extends from index to the last element, if found; otherwise, -1.</returns>
    public static int IndexOf<T>(
      this IImmutableList<T> list,
      T item,
      IEqualityComparer<T>? equalityComparer)
    {
      Requires.NotNull<IImmutableList<T>>(list, nameof (list));
      return list.IndexOf(item, 0, list.Count, equalityComparer);
    }

    /// <summary>Searches for the specified object and returns the zero-based index of the first occurrence within the range of elements in the immutable list that extends from the specified index to the last element.</summary>
    /// <param name="list">The list to search.</param>
    /// <param name="item">The object to locate in the Immutable list. The value can be null for reference types.</param>
    /// <param name="startIndex">The zero-based starting index of the search. 0 (zero) is valid in an empty list.</param>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <returns>The zero-based index of the first occurrence of item within the range of elements in the Immutable list that extends from index to the last element, if found; otherwise, -1.</returns>
    public static int IndexOf<T>(this IImmutableList<T> list, T item, int startIndex)
    {
      Requires.NotNull<IImmutableList<T>>(list, nameof (list));
      return list.IndexOf(item, startIndex, list.Count - startIndex, (IEqualityComparer<T>) EqualityComparer<T>.Default);
    }

    /// <summary>Searches for the specified object and returns the zero-based index of the first occurrence within the range of elements in the immutable list that extends from the specified index to the last element.</summary>
    /// <param name="list">The list to search.</param>
    /// <param name="item">The object to locate in the Immutable list. The value can be null for reference types.</param>
    /// <param name="startIndex">The zero-based starting index of the search. 0 (zero) is valid in an empty list.</param>
    /// <param name="count">The number of elements in the section to search.</param>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <returns>The zero-based index of the first occurrence of item within the range of elements in the Immutable list that extends from index to the last element, if found; otherwise, -1.</returns>
    public static int IndexOf<T>(this IImmutableList<T> list, T item, int startIndex, int count)
    {
      Requires.NotNull<IImmutableList<T>>(list, nameof (list));
      return list.IndexOf(item, startIndex, count, (IEqualityComparer<T>) EqualityComparer<T>.Default);
    }

    /// <summary>Searches for the specified object and returns the zero-based index of the last occurrence within the entire immutable list.</summary>
    /// <param name="list">The list to search.</param>
    /// <param name="item">The object to locate in the Immutable list. The value can be null for reference types.</param>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <returns>The zero-based index of the last occurrence of item within the entire the Immutable list, if found; otherwise, -1.</returns>
    public static int LastIndexOf<T>(this IImmutableList<T> list, T item)
    {
      Requires.NotNull<IImmutableList<T>>(list, nameof (list));
      return list.Count == 0 ? -1 : list.LastIndexOf(item, list.Count - 1, list.Count, (IEqualityComparer<T>) EqualityComparer<T>.Default);
    }

    /// <summary>Searches for the specified object and returns the zero-based index of the last occurrence within the entire immutable list.</summary>
    /// <param name="list">The list to search.</param>
    /// <param name="item">The object to locate in the Immutable list. The value can be null for reference types.</param>
    /// <param name="equalityComparer">The equality comparer to use in the search.</param>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <returns>The zero-based index of the last occurrence of item within the entire the Immutable list, if found; otherwise, -1.</returns>
    public static int LastIndexOf<T>(
      this IImmutableList<T> list,
      T item,
      IEqualityComparer<T>? equalityComparer)
    {
      Requires.NotNull<IImmutableList<T>>(list, nameof (list));
      return list.Count == 0 ? -1 : list.LastIndexOf(item, list.Count - 1, list.Count, equalityComparer);
    }

    /// <summary>Searches for the specified object and returns the zero-based index of the last occurrence within the range of elements in the immutable list that extends from the first element to the specified index.</summary>
    /// <param name="list">The list to search.</param>
    /// <param name="item">The object to locate in the Immutable list. The value can be null for reference types.</param>
    /// <param name="startIndex">The zero-based starting index of the backward search.</param>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <returns>The zero-based index of the last occurrence of item within the range of elements in the Immutable list that extends from the first element to index, if found; otherwise, -1.</returns>
    public static int LastIndexOf<T>(this IImmutableList<T> list, T item, int startIndex)
    {
      Requires.NotNull<IImmutableList<T>>(list, nameof (list));
      return list.Count == 0 && startIndex == 0 ? -1 : list.LastIndexOf(item, startIndex, startIndex + 1, (IEqualityComparer<T>) EqualityComparer<T>.Default);
    }

    /// <summary>Searches for the specified object and returns the zero-based index of the last occurrence within the range of elements in the immutable list that extends from the first element to the specified index.</summary>
    /// <param name="list">The list to search.</param>
    /// <param name="item">The object to locate in the Immutable list. The value can be null for reference types.</param>
    /// <param name="startIndex">The zero-based starting index of the backward search.</param>
    /// <param name="count">The number of elements in the section to search.</param>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <returns>The zero-based index of the last occurrence of item within the range of elements in the Immutable list that extends from the first element to index, if found; otherwise, -1.</returns>
    public static int LastIndexOf<T>(
      this IImmutableList<T> list,
      T item,
      int startIndex,
      int count)
    {
      Requires.NotNull<IImmutableList<T>>(list, nameof (list));
      return list.LastIndexOf(item, startIndex, count, (IEqualityComparer<T>) EqualityComparer<T>.Default);
    }
  }
}
