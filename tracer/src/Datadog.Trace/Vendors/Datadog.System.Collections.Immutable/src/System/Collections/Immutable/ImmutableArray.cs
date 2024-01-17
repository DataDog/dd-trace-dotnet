﻿// Decompiled with JetBrains decompiler
// Type: System.Collections.Immutable.ImmutableArray
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
    /// <summary>Provides methods for creating an array that is immutable; meaning it cannot be changed once it is created.
    /// 
    /// NuGet package: System.Collections.Immutable (about immutable collections and how to install)</summary>
    public static class ImmutableArray
  {
    internal static readonly byte[] TwoElementArray = new byte[2];

    /// <summary>Creates an empty immutable array.</summary>
    /// <typeparam name="T">The type of elements stored in the array.</typeparam>
    /// <returns>An empty immutable array.</returns>
    public static ImmutableArray<T> Create<T>() => ImmutableArray<T>.Empty;

    /// <summary>Creates an immutable array that contains the specified object.</summary>
    /// <param name="item">The object to store in the array.</param>
    /// <typeparam name="T">The type of elements stored in the array.</typeparam>
    /// <returns>An immutable array that contains the specified object.</returns>
    public static ImmutableArray<T> Create<T>(T item) => new ImmutableArray<T>(new T[1]
    {
      item
    });

    /// <summary>Creates an immutable array that contains the specified objects.</summary>
    /// <param name="item1">The first object to store in the array.</param>
    /// <param name="item2">The second object to store in the array.</param>
    /// <typeparam name="T">The type of elements stored in the array.</typeparam>
    /// <returns>An immutable array that contains the specified objects.</returns>
    public static ImmutableArray<T> Create<T>(T item1, T item2) => new ImmutableArray<T>(new T[2]
    {
      item1,
      item2
    });

    /// <summary>Creates an immutable array that contains the specified objects.</summary>
    /// <param name="item1">The first object to store in the array.</param>
    /// <param name="item2">The second object to store in the array.</param>
    /// <param name="item3">The third object to store in the array.</param>
    /// <typeparam name="T">The type of elements stored in the array.</typeparam>
    /// <returns>An immutable array that contains the specified objects.</returns>
    public static ImmutableArray<T> Create<T>(T item1, T item2, T item3) => new ImmutableArray<T>(new T[3]
    {
      item1,
      item2,
      item3
    });

    /// <summary>Creates an immutable array that contains the specified objects.</summary>
    /// <param name="item1">The first object to store in the array.</param>
    /// <param name="item2">The second object to store in the array.</param>
    /// <param name="item3">The third object to store in the array.</param>
    /// <param name="item4">The fourth object to store in the array.</param>
    /// <typeparam name="T">The type of elements stored in the array.</typeparam>
    /// <returns>An immutable array that contains the specified objects.</returns>
    public static ImmutableArray<T> Create<T>(T item1, T item2, T item3, T item4) => new ImmutableArray<T>(new T[4]
    {
      item1,
      item2,
      item3,
      item4
    });

    public static ImmutableArray<T> Create<T>(ReadOnlySpan<T> items) => items.IsEmpty ? ImmutableArray<T>.Empty : new ImmutableArray<T>(items.ToArray());

    public static ImmutableArray<T> Create<T>(Span<T> items) => ImmutableArray.Create<T>((items));

    public static ImmutableArray<T> ToImmutableArray<T>(this ReadOnlySpan<T> items) => ImmutableArray.Create<T>(items);

    public static ImmutableArray<T> ToImmutableArray<T>(this Span<T> items) => ImmutableArray.Create<T>((items));

    /// <summary>Creates a new <see cref="T:System.Collections.Immutable.ImmutableArray`1" /> populated with the specified items.</summary>
    /// <param name="items">The elements to add to the array.</param>
    /// <typeparam name="T">The type of element stored in the array.</typeparam>
    /// <returns>An immutable array that contains the specified items.</returns>
    public static ImmutableArray<T> CreateRange<T>(IEnumerable<T> items)
    {
      Requires.NotNull<IEnumerable<T>>(items, nameof (items));
      if (items is IImmutableArray immutableArray)
        return new ImmutableArray<T>((T[]) (immutableArray.Array ?? throw new InvalidOperationException()));
        //return new ImmutableArray<T>((T[]) (immutableArray.Array ?? throw new InvalidOperationException(SR.InvalidOperationOnDefaultArray)));
      int count;
      return items.TryGetCount<T>(out count) ? new ImmutableArray<T>(items.ToArray<T>(count)) : new ImmutableArray<T>(items.ToArray<T>());
    }

    /// <summary>Creates an immutable array from the specified array of objects.</summary>
    /// <param name="items">The array of objects to populate the array with.</param>
    /// <typeparam name="T">The type of elements stored in the array.</typeparam>
    /// <returns>An immutable array that contains the array of items.</returns>
    public static ImmutableArray<T> Create<T>(params T[]? items)
    {
      if (items == null || items.Length == 0)
        return ImmutableArray<T>.Empty;
      T[] objArray = new T[items.Length];
      Array.Copy((Array) items, (Array) objArray, items.Length);
      return new ImmutableArray<T>(objArray);
    }

    /// <summary>Creates an immutable array with specified objects from another array.</summary>
    /// <param name="items">The source array of objects.</param>
    /// <param name="start">The index of the first element to copy from <paramref name="items" />.</param>
    /// <param name="length">The number of elements from <paramref name="items" /> to include in this immutable array.</param>
    /// <typeparam name="T">The type of elements stored in the array.</typeparam>
    /// <returns>An immutable array that contains the specified objects from the source array.</returns>
    public static ImmutableArray<T> Create<T>(T[] items, int start, int length)
    {
      Requires.NotNull<T[]>(items, nameof (items));
      Requires.Range(start >= 0 && start <= items.Length, nameof (start));
      Requires.Range(length >= 0 && start + length <= items.Length, nameof (length));
      if (length == 0)
        return ImmutableArray.Create<T>();
      T[] items1 = new T[length];
      for (int index = 0; index < items1.Length; ++index)
        items1[index] = items[start + index];
      return new ImmutableArray<T>(items1);
    }

    /// <summary>Creates an immutable array with the specified objects from another immutable array.</summary>
    /// <param name="items">The source array of objects.</param>
    /// <param name="start">The index of the first element to copy from <paramref name="items" />.</param>
    /// <param name="length">The number of elements from <paramref name="items" /> to include in this immutable array.</param>
    /// <typeparam name="T">The type of elements stored in the array.</typeparam>
    /// <returns>An immutable array that contains the specified objects from the source array.</returns>
    public static ImmutableArray<T> Create<T>(ImmutableArray<T> items, int start, int length)
    {
      Requires.Range(start >= 0 && start <= items.Length, nameof (start));
      Requires.Range(length >= 0 && start + length <= items.Length, nameof (length));
      if (length == 0)
        return ImmutableArray.Create<T>();
      if (start == 0 && length == items.Length)
        return items;
      T[] objArray = new T[length];
      Array.Copy((Array) items.array, start, (Array) objArray, 0, length);
      return new ImmutableArray<T>(objArray);
    }

    /// <summary>Initializes a new instance of the <see cref="T:System.Collections.Immutable.ImmutableArray`1" /> struct.</summary>
    /// <param name="items">The source array to initialize the resulting array with.</param>
    /// <param name="selector">The function to apply to each element from the source array.</param>
    /// <typeparam name="TSource">The type of element stored in the source array.</typeparam>
    /// <typeparam name="TResult">The type of element to store in the target array.</typeparam>
    /// <returns>An immutable array that contains the specified items.</returns>
    public static ImmutableArray<TResult> CreateRange<TSource, TResult>(
      ImmutableArray<TSource> items,
      Func<TSource, TResult> selector)
    {
      Requires.NotNull<Func<TSource, TResult>>(selector, nameof (selector));
      int length = items.Length;
      if (length == 0)
        return ImmutableArray.Create<TResult>();
      TResult[] items1 = new TResult[length];
      for (int index = 0; index < items1.Length; ++index)
        items1[index] = selector(items[index]);
      return new ImmutableArray<TResult>(items1);
    }

    /// <summary>Initializes a new instance of the <see cref="T:System.Collections.Immutable.ImmutableArray`1" /> struct.</summary>
    /// <param name="items">The source array to initialize the resulting array with.</param>
    /// <param name="start">The index of the first element in the source array to include in the resulting array.</param>
    /// <param name="length">The number of elements from the source array to include in the resulting array.</param>
    /// <param name="selector">The function to apply to each element from the source array included in the resulting array.</param>
    /// <typeparam name="TSource">The type of element stored in the source array.</typeparam>
    /// <typeparam name="TResult">The type of element to store in the target array.</typeparam>
    /// <returns>An immutable array that contains the specified items.</returns>
    public static ImmutableArray<TResult> CreateRange<TSource, TResult>(
      ImmutableArray<TSource> items,
      int start,
      int length,
      Func<TSource, TResult> selector)
    {
      int length1 = items.Length;
      Requires.Range(start >= 0 && start <= length1, nameof (start));
      Requires.Range(length >= 0 && start + length <= length1, nameof (length));
      Requires.NotNull<Func<TSource, TResult>>(selector, nameof (selector));
      if (length == 0)
        return ImmutableArray.Create<TResult>();
      TResult[] items1 = new TResult[length];
      for (int index = 0; index < items1.Length; ++index)
        items1[index] = selector(items[index + start]);
      return new ImmutableArray<TResult>(items1);
    }

    /// <summary>Initializes a new instance of the <see cref="T:System.Collections.Immutable.ImmutableArray`1" /> struct.</summary>
    /// <param name="items">The source array to initialize the resulting array with.</param>
    /// <param name="selector">The function to apply to each element from the source array.</param>
    /// <param name="arg">An argument to be passed to the selector mapping function.</param>
    /// <typeparam name="TSource">The type of element stored in the source array.</typeparam>
    /// <typeparam name="TArg">The type of argument to pass to the selector mapping function.</typeparam>
    /// <typeparam name="TResult">The type of element to store in the target array.</typeparam>
    /// <returns>An immutable array that contains the specified items.</returns>
    public static ImmutableArray<TResult> CreateRange<TSource, TArg, TResult>(
      ImmutableArray<TSource> items,
      Func<TSource, TArg, TResult> selector,
      TArg arg)
    {
      Requires.NotNull<Func<TSource, TArg, TResult>>(selector, nameof (selector));
      int length = items.Length;
      if (length == 0)
        return ImmutableArray.Create<TResult>();
      TResult[] items1 = new TResult[length];
      for (int index = 0; index < items1.Length; ++index)
        items1[index] = selector(items[index], arg);
      return new ImmutableArray<TResult>(items1);
    }

    /// <summary>Initializes a new instance of the <see cref="T:System.Collections.Immutable.ImmutableArray`1" /> struct.</summary>
    /// <param name="items">The source array to initialize the resulting array with.</param>
    /// <param name="start">The index of the first element in the source array to include in the resulting array.</param>
    /// <param name="length">The number of elements from the source array to include in the resulting array.</param>
    /// <param name="selector">The function to apply to each element from the source array included in the resulting array.</param>
    /// <param name="arg">An argument to be passed to the selector mapping function.</param>
    /// <typeparam name="TSource">The type of element stored in the source array.</typeparam>
    /// <typeparam name="TArg">The type of argument to be passed to the selector mapping function.</typeparam>
    /// <typeparam name="TResult">The type of element to be stored in the target array.</typeparam>
    /// <returns>An immutable array that contains the specified items.</returns>
    public static ImmutableArray<TResult> CreateRange<TSource, TArg, TResult>(
      ImmutableArray<TSource> items,
      int start,
      int length,
      Func<TSource, TArg, TResult> selector,
      TArg arg)
    {
      int length1 = items.Length;
      Requires.Range(start >= 0 && start <= length1, nameof (start));
      Requires.Range(length >= 0 && start + length <= length1, nameof (length));
      Requires.NotNull<Func<TSource, TArg, TResult>>(selector, nameof (selector));
      if (length == 0)
        return ImmutableArray.Create<TResult>();
      TResult[] items1 = new TResult[length];
      for (int index = 0; index < items1.Length; ++index)
        items1[index] = selector(items[index + start], arg);
      return new ImmutableArray<TResult>(items1);
    }

    /// <summary>Creates a mutable array that can be converted to an <see cref="T:System.Collections.Immutable.ImmutableArray" /> without allocating new memory.</summary>
    /// <typeparam name="T">The type of elements stored in the builder.</typeparam>
    /// <returns>A mutable array of the specified type that can be efficiently converted to an immutable array.</returns>
    public static ImmutableArray<T>.Builder CreateBuilder<T>() => ImmutableArray.Create<T>().ToBuilder();

    /// <summary>Creates a mutable array that can be converted to an <see cref="T:System.Collections.Immutable.ImmutableArray" /> without allocating new memory.</summary>
    /// <param name="initialCapacity">The initial capacity of the builder.</param>
    /// <typeparam name="T">The type of elements stored in the builder.</typeparam>
    /// <returns>A mutable array of the specified type that can be efficiently converted to an immutable array.</returns>
    public static ImmutableArray<T>.Builder CreateBuilder<T>(int initialCapacity) => new ImmutableArray<T>.Builder(initialCapacity);

    /// <summary>Creates an immutable array from the specified collection.</summary>
    /// <param name="items">The collection of objects to copy to the immutable array.</param>
    /// <typeparam name="TSource">The type of elements contained in <paramref name="items" />.</typeparam>
    /// <returns>An immutable array that contains the specified collection of objects.</returns>
    public static ImmutableArray<TSource> ToImmutableArray<TSource>(this IEnumerable<TSource> items) => items is ImmutableArray<TSource> immutableArray ? immutableArray : ImmutableArray.CreateRange<TSource>(items);

    /// <summary>Creates an immutable array from the current contents of the builder's array.</summary>
    /// <param name="builder">The builder to create the immutable array from.</param>
    /// <typeparam name="TSource">The type of elements contained in the immutable array.</typeparam>
    /// <returns>An immutable array that contains the current contents of the builder's array.</returns>
    public static ImmutableArray<TSource> ToImmutableArray<TSource>(
      this ImmutableArray<TSource>.Builder builder)
    {
      Requires.NotNull<ImmutableArray<TSource>.Builder>(builder, nameof (builder));
      return builder.ToImmutable();
    }

    /// <summary>Searches the sorted immutable array for a specified element using the default comparer and returns the zero-based index of the element, if it's found.</summary>
    /// <param name="array">The sorted array to search.</param>
    /// <param name="value">The object to search for.</param>
    /// <typeparam name="T">The type of element stored in the array.</typeparam>
    /// <exception cref="T:System.InvalidOperationException">
    /// <paramref name="value" /> does not implement <see cref="T:System.IComparable" /> or the search encounters an element that does not implement <see cref="T:System.IComparable" />.</exception>
    /// <returns>The zero-based index of the item in the array, if item is found; otherwise, a negative number that is the bitwise complement of the index of the next element that is larger than <paramref name="value" /> or, if there is no larger element, the bitwise complement of <see cref="P:System.Collections.Generic.ICollection`1.Count" />.</returns>
    public static int BinarySearch<T>(this ImmutableArray<T> array, T value) => Array.BinarySearch<T>(array.array, value);

    /// <summary>Searches a sorted immutable array for a specified element and returns the zero-based index of the element, if it's found.</summary>
    /// <param name="array">The sorted array to search.</param>
    /// <param name="value">The object to search for.</param>
    /// <param name="comparer">The comparer implementation to use when comparing elements, or null to use the default comparer.</param>
    /// <typeparam name="T">The type of element stored in the array.</typeparam>
    /// <exception cref="T:System.InvalidOperationException">
    /// <paramref name="comparer" /> is null and <paramref name="value" /> does not implement <see cref="T:System.IComparable" /> or the search encounters an element that does not implement <see cref="T:System.IComparable" />.</exception>
    /// <returns>The zero-based index of the item in the array, if item is found; otherwise, a negative number that is the bitwise complement of the index of the next element that is larger than <paramref name="value" /> or, if there is no larger element, the bitwise complement of <see cref="P:System.Collections.Generic.ICollection`1.Count" />.</returns>
    public static int BinarySearch<T>(this ImmutableArray<T> array, T value, IComparer<T>? comparer) => Array.BinarySearch<T>(array.array, value, comparer);

    /// <summary>Searches a sorted immutable array for a specified element and returns the zero-based index of the element, if it's found.</summary>
    /// <param name="array">The sorted array to search.</param>
    /// <param name="index">The starting index of the range to search.</param>
    /// <param name="length">The length of the range to search.</param>
    /// <param name="value">The object to search for.</param>
    /// <typeparam name="T">The type of element stored in the array.</typeparam>
    /// <exception cref="T:System.InvalidOperationException">
    /// <paramref name="value" /> does not implement <see cref="T:System.IComparable" /> or the search encounters an element that does not implement <see cref="T:System.IComparable" />.</exception>
    /// <exception cref="T:System.ArgumentException">
    /// <paramref name="index" /> and <paramref name="length" /> do not specify a valid range in <paramref name="array" />.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">
    ///         <paramref name="index" /> is less than the lower bound of <paramref name="array" />.
    /// 
    /// -or-
    /// 
    /// <paramref name="length" /> is less than zero.</exception>
    /// <returns>The zero-based index of the item in the array, if item is found; otherwise, a negative number that is the bitwise complement of the index of the next element that is larger than <paramref name="value" /> or, if there is no larger element, the bitwise complement of <see cref="P:System.Collections.Generic.ICollection`1.Count" />.</returns>
    public static int BinarySearch<T>(
      this ImmutableArray<T> array,
      int index,
      int length,
      T value)
    {
      return Array.BinarySearch<T>(array.array, index, length, value);
    }

    /// <summary>Searches a sorted immutable array for a specified element and returns the zero-based index of the element.</summary>
    /// <param name="array">The sorted array to search.</param>
    /// <param name="index">The starting index of the range to search.</param>
    /// <param name="length">The length of the range to search.</param>
    /// <param name="value">The object to search for.</param>
    /// <param name="comparer">The comparer to use when comparing elements for equality or <see langword="null" /> to use the default comparer.</param>
    /// <typeparam name="T">The type of element stored in the array.</typeparam>
    /// <exception cref="T:System.InvalidOperationException">
    /// <paramref name="comparer" /> is null and <paramref name="value" /> does not implement <see cref="T:System.IComparable" /> or the search encounters an element that does not implement <see cref="T:System.IComparable" />.</exception>
    /// <exception cref="T:System.ArgumentException">
    ///         <paramref name="index" /> and <paramref name="length" /> do not specify a valid range in <paramref name="array" />.
    /// 
    /// -or-
    /// 
    /// <paramref name="comparer" /> is <see langword="null" />, and <paramref name="value" /> is of a type that is not compatible with the elements of <paramref name="array" />.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">
    ///         <paramref name="index" /> is less than the lower bound of <paramref name="array" />.
    /// 
    /// -or-
    /// 
    /// <paramref name="length" /> is less than zero.</exception>
    /// <returns>The zero-based index of the item in the array, if item is found; otherwise, a negative number that is the bitwise complement of the index of the next element that is larger than <paramref name="value" /> or, if there is no larger element, the bitwise complement of <see cref="P:System.Collections.Generic.ICollection`1.Count" />.</returns>
    public static int BinarySearch<T>(
      this ImmutableArray<T> array,
      int index,
      int length,
      T value,
      IComparer<T>? comparer)
    {
      return Array.BinarySearch<T>(array.array, index, length, value, comparer);
    }
  }
}
