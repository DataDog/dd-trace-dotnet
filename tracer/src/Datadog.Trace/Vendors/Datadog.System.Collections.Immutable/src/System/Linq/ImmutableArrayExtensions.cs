﻿// Decompiled with JetBrains decompiler
// Type: System.Linq.ImmutableArrayExtensions
// Assembly: System.Collections.Immutable, Version=7.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 5F9FF90F-0D16-4469-A104-76829D3705E2
// Assembly location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.dll
// XML documentation location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.xml

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.System.Collections.Immutable;


#nullable enable
namespace Datadog.System.Linq
{
    /// <summary>LINQ extension method overrides that offer greater efficiency for <see cref="T:System.Collections.Immutable.ImmutableArray`1" /> than the standard LINQ methods
    /// 
    /// NuGet package: System.Collections.Immutable (about immutable collections and how to install)</summary>
    public static class ImmutableArrayExtensions
  {
    /// <summary>Projects each element of a sequence into a new form.</summary>
    /// <param name="immutableArray">The immutable array to select items from.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="T">The type of element contained by the collection.</typeparam>
    /// <typeparam name="TResult">The type of the result element.</typeparam>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements are the result of invoking the transform function on each element of source.</returns>
    public static IEnumerable<TResult> Select<T, TResult>(
      this ImmutableArray<T> immutableArray,
      Func<T, TResult> selector)
    {
      immutableArray.ThrowNullRefIfNotInitialized();
      return ((IEnumerable<T>) immutableArray.array).Select<T, TResult>(selector);
    }

    /// <summary>Projects each element of a sequence to an <see cref="T:System.Collections.Generic.IEnumerable`1" />,             flattens the resulting sequences into one sequence, and invokes a result             selector function on each element therein.</summary>
    /// <param name="immutableArray">The immutable array.</param>
    /// <param name="collectionSelector">A transform function to apply to each element of the input sequence.</param>
    /// <param name="resultSelector">A transform function to apply to each element of the intermediate sequence.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="immutableArray" />.</typeparam>
    /// <typeparam name="TCollection">The type of the intermediate elements collected by <paramref name="collectionSelector" />.</typeparam>
    /// <typeparam name="TResult">The type of the elements of the resulting sequence.</typeparam>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements are the result             of invoking the one-to-many transform function <paramref name="collectionSelector" /> on each             element of <paramref name="immutableArray" /> and then mapping each of those sequence elements and their             corresponding source element to a result element.</returns>
    public static IEnumerable<TResult> SelectMany<TSource, TCollection, TResult>(
      this ImmutableArray<TSource> immutableArray,
      Func<TSource, IEnumerable<TCollection>> collectionSelector,
      Func<TSource, TCollection, TResult> resultSelector)
    {
      immutableArray.ThrowNullRefIfNotInitialized();
      if (collectionSelector == null || resultSelector == null)
        return Enumerable.SelectMany<TSource, TCollection, TResult>(immutableArray, collectionSelector, resultSelector);
      return immutableArray.Length != 0 ? immutableArray.SelectManyIterator<TSource, TCollection, TResult>(collectionSelector, resultSelector) : Enumerable.Empty<TResult>();
    }

    /// <summary>Filters a sequence of values based on a predicate.</summary>
    /// <param name="immutableArray">The array to filter.</param>
    /// <param name="predicate">The condition to use for filtering the array content.</param>
    /// <typeparam name="T">The type of element contained by the collection.</typeparam>
    /// <returns>Returns <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains elements that meet the condition.</returns>
    public static IEnumerable<T> Where<T>(
      this ImmutableArray<T> immutableArray,
      Func<T, bool> predicate)
    {
      immutableArray.ThrowNullRefIfNotInitialized();
      return ((IEnumerable<T>) immutableArray.array).Where<T>(predicate);
    }

    /// <summary>Gets a value indicating whether the array contains any elements.</summary>
    /// <param name="immutableArray">The array to check for elements.</param>
    /// <typeparam name="T">The type of element contained by the collection.</typeparam>
    /// <returns>
    /// <see langword="true" /> if the array contains an elements; otherwise, <see langword="false" />.</returns>
    public static bool Any<T>(this ImmutableArray<T> immutableArray) => immutableArray.Length > 0;

    /// <summary>Gets a value indicating whether the array contains any elements that match a specified condition.</summary>
    /// <param name="immutableArray">The array to check for elements.</param>
    /// <param name="predicate">The delegate that defines the condition to match to an element.</param>
    /// <typeparam name="T">The type of element contained by the collection.</typeparam>
    /// <returns>
    /// <see langword="true" /> if an element matches the specified condition; otherwise, <see langword="false" />.</returns>
    public static bool Any<T>(this ImmutableArray<T> immutableArray, Func<T, bool> predicate)
    {
      immutableArray.ThrowNullRefIfNotInitialized();
      Requires.NotNull<Func<T, bool>>(predicate, nameof (predicate));
      foreach (T obj in immutableArray.array)
      {
        if (predicate(obj))
          return true;
      }
      return false;
    }

    /// <summary>Gets a value indicating whether all elements in this array match a given condition.</summary>
    /// <param name="immutableArray">The array to check for matches.</param>
    /// <param name="predicate">The predicate.</param>
    /// <typeparam name="T">The type of element contained by the collection.</typeparam>
    /// <returns>
    /// <see langword="true" /> if every element of the source sequence passes the test in the specified predicate; otherwise, <see langword="false" />.</returns>
    public static bool All<T>(this ImmutableArray<T> immutableArray, Func<T, bool> predicate)
    {
      immutableArray.ThrowNullRefIfNotInitialized();
      Requires.NotNull<Func<T, bool>>(predicate, nameof (predicate));
      foreach (T obj in immutableArray.array)
      {
        if (!predicate(obj))
          return false;
      }
      return true;
    }

    /// <summary>Determines whether two sequences are equal according to an equality comparer.</summary>
    /// <param name="immutableArray">The array to use for comparison.</param>
    /// <param name="items">The items to use for comparison.</param>
    /// <param name="comparer">The comparer to use to check for equality.</param>
    /// <typeparam name="TDerived">The type of element in the compared array.</typeparam>
    /// <typeparam name="TBase">The type of element contained by the collection.</typeparam>
    /// <returns>
    /// <see langword="true" /> to indicate the sequences are equal; otherwise, <see langword="false" />.</returns>
    public static bool SequenceEqual<TDerived, TBase>(
      this ImmutableArray<TBase> immutableArray,
      ImmutableArray<TDerived> items,
      IEqualityComparer<TBase>? comparer = null)
      where TDerived : TBase
    {
      immutableArray.ThrowNullRefIfNotInitialized();
      items.ThrowNullRefIfNotInitialized();
      if (immutableArray.array.Equals(items.array))
        return true;
      if (immutableArray.Length != items.Length)
        return false;
      if (comparer == null)
        comparer = (IEqualityComparer<TBase>) EqualityComparer<TBase>.Default;
      for (int index = 0; index < immutableArray.Length; ++index)
      {
        if (!comparer.Equals(immutableArray.array[index], (TBase) items.array[index]))
          return false;
      }
      return true;
    }

    /// <summary>Determines whether two sequences are equal according to an equality comparer.</summary>
    /// <param name="immutableArray">The array to use for comparison.</param>
    /// <param name="items">The items to use for comparison.</param>
    /// <param name="comparer">The comparer to use to check for equality.</param>
    /// <typeparam name="TDerived">The type of element in the compared array.</typeparam>
    /// <typeparam name="TBase">The type of element contained by the collection.</typeparam>
    /// <returns>
    /// <see langword="true" /> to indicate the sequences are equal; otherwise, <see langword="false" />.</returns>
    public static bool SequenceEqual<TDerived, TBase>(
      this ImmutableArray<TBase> immutableArray,
      IEnumerable<TDerived> items,
      IEqualityComparer<TBase>? comparer = null)
      where TDerived : TBase
    {
      Requires.NotNull<IEnumerable<TDerived>>(items, nameof (items));
      if (comparer == null)
        comparer = (IEqualityComparer<TBase>) EqualityComparer<TBase>.Default;
      int index = 0;
      int length = immutableArray.Length;
      foreach (TDerived y in items)
      {
        if (index == length || !comparer.Equals(immutableArray[index], (TBase) y))
          return false;
        ++index;
      }
      return index == length;
    }

    /// <summary>Determines whether two sequences are equal according to an equality comparer.</summary>
    /// <param name="immutableArray">The array to use for comparison.</param>
    /// <param name="items">The items to use for comparison.</param>
    /// <param name="predicate">The comparer to use to check for equality.</param>
    /// <typeparam name="TDerived">The type of element in the compared array.</typeparam>
    /// <typeparam name="TBase">The type of element contained by the collection.</typeparam>
    /// <returns>
    /// <see langword="true" /> to indicate the sequences are equal; otherwise, <see langword="false" />.</returns>
    public static bool SequenceEqual<TDerived, TBase>(
      this ImmutableArray<TBase> immutableArray,
      ImmutableArray<TDerived> items,
      Func<TBase, TBase, bool> predicate)
      where TDerived : TBase
    {
      Requires.NotNull<Func<TBase, TBase, bool>>(predicate, nameof (predicate));
      immutableArray.ThrowNullRefIfNotInitialized();
      items.ThrowNullRefIfNotInitialized();
      if (immutableArray.array.Equals(items.array))
        return true;
      if (immutableArray.Length != items.Length)
        return false;
      int index = 0;
      for (int length = immutableArray.Length; index < length; ++index)
      {
        if (!predicate(immutableArray[index], (TBase) items[index]))
          return false;
      }
      return true;
    }

    /// <summary>Applies a function to a sequence of elements in a cumulative way.</summary>
    /// <param name="immutableArray">The collection to apply the function to.</param>
    /// <param name="func">A function to be invoked on each element, in a cumulative way.</param>
    /// <typeparam name="T">The type of element contained by the collection.</typeparam>
    /// <returns>The final value after the cumulative function has been applied to all elements.</returns>
    public static T? Aggregate<T>(this ImmutableArray<T> immutableArray, Func<T, T, T> func)
    {
      Requires.NotNull<Func<T, T, T>>(func, nameof (func));
      if (immutableArray.Length == 0)
        return default (T);
      T obj = immutableArray[0];
      int index = 1;
      for (int length = immutableArray.Length; index < length; ++index)
        obj = func(obj, immutableArray[index]);
      return obj;
    }

    /// <summary>Applies a function to a sequence of elements in a cumulative way.</summary>
    /// <param name="immutableArray">The collection to apply the function to.</param>
    /// <param name="seed">The initial accumulator value.</param>
    /// <param name="func">A function to be invoked on each element, in a cumulative way.</param>
    /// <typeparam name="TAccumulate">The type of the accumulated value.</typeparam>
    /// <typeparam name="T">The type of element contained by the collection.</typeparam>
    /// <returns>The final accumulator value.</returns>
    public static TAccumulate Aggregate<TAccumulate, T>(
      this ImmutableArray<T> immutableArray,
      TAccumulate seed,
      Func<TAccumulate, T, TAccumulate> func)
    {
      Requires.NotNull<Func<TAccumulate, T, TAccumulate>>(func, nameof (func));
      TAccumulate accumulate = seed;
      foreach (T obj in immutableArray.array)
        accumulate = func(accumulate, obj);
      return accumulate;
    }

    /// <summary>Applies a function to a sequence of elements in a cumulative way.</summary>
    /// <param name="immutableArray">The collection to apply the function to.</param>
    /// <param name="seed">The initial accumulator value.</param>
    /// <param name="func">A function to be invoked on each element, in a cumulative way.</param>
    /// <param name="resultSelector">A function to transform the final accumulator value into the result type.</param>
    /// <typeparam name="TAccumulate">The type of the accumulated value.</typeparam>
    /// <typeparam name="TResult">The type of result returned by the result selector.</typeparam>
    /// <typeparam name="T">The type of element contained by the collection.</typeparam>
    /// <returns>The final accumulator value.</returns>
    public static TResult Aggregate<TAccumulate, TResult, T>(
      this ImmutableArray<T> immutableArray,
      TAccumulate seed,
      Func<TAccumulate, T, TAccumulate> func,
      Func<TAccumulate, TResult> resultSelector)
    {
      Requires.NotNull<Func<TAccumulate, TResult>>(resultSelector, nameof (resultSelector));
      return resultSelector(immutableArray.Aggregate<TAccumulate, T>(seed, func));
    }

    /// <summary>Returns the element at a specified index in the array.</summary>
    /// <param name="immutableArray">The array to find an element in.</param>
    /// <param name="index">The index for the element to retrieve.</param>
    /// <typeparam name="T">The type of element contained by the collection.</typeparam>
    /// <returns>The item at the specified index.</returns>
    public static T ElementAt<T>(this ImmutableArray<T> immutableArray, int index) => immutableArray[index];

    /// <summary>Returns the element at a specified index in a sequence or a default value if the index is out of range.</summary>
    /// <param name="immutableArray">The array to find an element in.</param>
    /// <param name="index">The index for the element to retrieve.</param>
    /// <typeparam name="T">The type of element contained by the collection.</typeparam>
    /// <returns>The item at the specified index, or the default value if the index is not found.</returns>
    public static T? ElementAtOrDefault<T>(this ImmutableArray<T> immutableArray, int index) => index < 0 || index >= immutableArray.Length ? default (T) : immutableArray[index];

    /// <summary>Returns the first element in a sequence that satisfies a specified condition.</summary>
    /// <param name="immutableArray">The array to get an item from.</param>
    /// <param name="predicate">The delegate that defines the conditions of the element to search for.</param>
    /// <typeparam name="T">The type of element contained by the collection.</typeparam>
    /// <exception cref="T:System.InvalidOperationException">If the array is empty.</exception>
    /// <returns>The first item in the list if it meets the condition specified by <paramref name="predicate" />.</returns>
    public static T First<T>(this ImmutableArray<T> immutableArray, Func<T, bool> predicate)
    {
      Requires.NotNull<Func<T, bool>>(predicate, nameof (predicate));
      foreach (T obj in immutableArray.array)
      {
        if (predicate(obj))
          return obj;
      }
      return Enumerable.Empty<T>().First<T>();
    }

    /// <summary>Returns the first element in an array.</summary>
    /// <param name="immutableArray">The array to get an item from.</param>
    /// <typeparam name="T">The type of element contained by the collection.</typeparam>
    /// <exception cref="T:System.InvalidOperationException">If the array is empty.</exception>
    /// <returns>The first item in the array.</returns>
    public static T First<T>(this ImmutableArray<T> immutableArray) => immutableArray.Length <= 0 ? ((IEnumerable<T>) immutableArray.array).First<T>() : immutableArray[0];

    /// <summary>Returns the first element of a sequence, or a default value if the sequence contains no elements.</summary>
    /// <param name="immutableArray">The array to retrieve items from.</param>
    /// <typeparam name="T">The type of element contained by the collection.</typeparam>
    /// <returns>The first item in the list, if found; otherwise the default value for the item type.</returns>
    public static T? FirstOrDefault<T>(this ImmutableArray<T> immutableArray) => immutableArray.array.Length == 0 ? default (T) : immutableArray.array[0];

    /// <summary>Returns the first element of the sequence that satisfies a condition or a default value if no such element is found.</summary>
    /// <param name="immutableArray">The array to retrieve elements from.</param>
    /// <param name="predicate">The delegate that defines the conditions of the element to search for.</param>
    /// <typeparam name="T">The type of element contained by the collection.</typeparam>
    /// <returns>The first item in the list, if found; otherwise the default value for the item type.</returns>
    public static T? FirstOrDefault<T>(
      this ImmutableArray<T> immutableArray,
      Func<T, bool> predicate)
    {
      Requires.NotNull<Func<T, bool>>(predicate, nameof (predicate));
      foreach (T obj in immutableArray.array)
      {
        if (predicate(obj))
          return obj;
      }
      return default (T);
    }

    /// <summary>Returns the last element of the array.</summary>
    /// <param name="immutableArray">The array to retrieve items from.</param>
    /// <typeparam name="T">The type of element contained by the array.</typeparam>
    /// <exception cref="T:System.InvalidOperationException">The collection is empty.</exception>
    /// <returns>The last element in the array.</returns>
    public static T Last<T>(this ImmutableArray<T> immutableArray) => immutableArray.Length <= 0 ? ((IEnumerable<T>) immutableArray.array).Last<T>() : immutableArray[immutableArray.Length - 1];

    /// <summary>Returns the last element of a sequence that satisfies a specified condition.</summary>
    /// <param name="immutableArray">The array to retrieve elements from.</param>
    /// <param name="predicate">The delegate that defines the conditions of the element to retrieve.</param>
    /// <typeparam name="T">The type of element contained by the collection.</typeparam>
    /// <exception cref="T:System.InvalidOperationException">The collection is empty.</exception>
    /// <returns>The last element of the array that satisfies the <paramref name="predicate" /> condition.</returns>
    public static T Last<T>(this ImmutableArray<T> immutableArray, Func<T, bool> predicate)
    {
      Requires.NotNull<Func<T, bool>>(predicate, nameof (predicate));
      for (int index = immutableArray.Length - 1; index >= 0; --index)
      {
        if (predicate(immutableArray[index]))
          return immutableArray[index];
      }
      return Enumerable.Empty<T>().Last<T>();
    }

    /// <summary>Returns the last element of a sequence, or a default value if the sequence contains no elements.</summary>
    /// <param name="immutableArray">The array to retrieve items from.</param>
    /// <typeparam name="T">The type of element contained by the collection.</typeparam>
    /// <returns>The last element of a sequence, or a default value if the sequence contains no elements.</returns>
    public static T? LastOrDefault<T>(this ImmutableArray<T> immutableArray)
    {
      immutableArray.ThrowNullRefIfNotInitialized();
      return ((IEnumerable<T>) immutableArray.array).LastOrDefault<T>();
    }

    /// <summary>Returns the last element of a sequence that satisfies a condition or a default value if no such element is found.</summary>
    /// <param name="immutableArray">The array to retrieve an element from.</param>
    /// <param name="predicate">The delegate that defines the conditions of the element to search for.</param>
    /// <typeparam name="T">The type of element contained by the collection.</typeparam>
    /// <returns>The last element of a sequence, or a default value if the sequence contains no elements.</returns>
    public static T? LastOrDefault<T>(
      this ImmutableArray<T> immutableArray,
      Func<T, bool> predicate)
    {
      Requires.NotNull<Func<T, bool>>(predicate, nameof (predicate));
      for (int index = immutableArray.Length - 1; index >= 0; --index)
      {
        if (predicate(immutableArray[index]))
          return immutableArray[index];
      }
      return default (T);
    }

    /// <summary>Returns the only element of a sequence, and throws an exception if there is not exactly one element in the sequence.</summary>
    /// <param name="immutableArray">The array to retrieve the element from.</param>
    /// <typeparam name="T">The type of element contained by the collection.</typeparam>
    /// <returns>The element in the sequence.</returns>
    public static T Single<T>(this ImmutableArray<T> immutableArray)
    {
      immutableArray.ThrowNullRefIfNotInitialized();
      return ((IEnumerable<T>) immutableArray.array).Single<T>();
    }

    /// <summary>Returns the only element of a sequence that satisfies a specified condition, and throws an exception if more than one such element exists.</summary>
    /// <param name="immutableArray">The immutable array to return a single element from.</param>
    /// <param name="predicate">The function to test whether an element should be returned.</param>
    /// <typeparam name="T">The type of element contained by the collection.</typeparam>
    /// <returns>Returns <see cref="T:System.Boolean" />.</returns>
    public static T Single<T>(this ImmutableArray<T> immutableArray, Func<T, bool> predicate)
    {
      Requires.NotNull<Func<T, bool>>(predicate, nameof (predicate));
      bool flag = true;
      T obj1 = default (T);
      foreach (T obj2 in immutableArray.array)
      {
        if (predicate(obj2))
        {
          if (!flag)
          {
            int num = (int) ((IEnumerable<byte>) ImmutableArray.TwoElementArray).Single<byte>();
          }
          flag = false;
          obj1 = obj2;
        }
      }
      if (flag)
        Enumerable.Empty<T>().Single<T>();
      return obj1;
    }

    /// <summary>Returns the only element of the array, or a default value if the sequence is empty; this method throws an exception if there is more than one element in the sequence.</summary>
    /// <param name="immutableArray">The array.</param>
    /// <typeparam name="T">The type of element contained by the collection.</typeparam>
    /// <exception cref="T:System.InvalidOperationException">
    /// <paramref name="source" /> contains more than one element.</exception>
    /// <returns>The element in the array, or the default value if the array is empty.</returns>
    public static T? SingleOrDefault<T>(this ImmutableArray<T> immutableArray)
    {
      immutableArray.ThrowNullRefIfNotInitialized();
      return ((IEnumerable<T>) immutableArray.array).SingleOrDefault<T>();
    }

    /// <summary>Returns the only element of a sequence that satisfies a specified condition or a default value if no such element exists; this method throws an exception if more than one element satisfies the condition.</summary>
    /// <param name="immutableArray">The array to get the element from.</param>
    /// <param name="predicate">The condition the element must satisfy.</param>
    /// <typeparam name="T">The type of element contained by the collection.</typeparam>
    /// <exception cref="T:System.InvalidOperationException">More than one element satisfies the condition in <paramref name="predicate" />.</exception>
    /// <returns>The element if it satisfies the specified condition; otherwise the default element.</returns>
    public static T? SingleOrDefault<T>(
      this ImmutableArray<T> immutableArray,
      Func<T, bool> predicate)
    {
      Requires.NotNull<Func<T, bool>>(predicate, nameof (predicate));
      bool flag = true;
      T obj1 = default (T);
      foreach (T obj2 in immutableArray.array)
      {
        if (predicate(obj2))
        {
          if (!flag)
          {
            int num = (int) ((IEnumerable<byte>) ImmutableArray.TwoElementArray).Single<byte>();
          }
          flag = false;
          obj1 = obj2;
        }
      }
      return obj1;
    }

    /// <summary>Creates a dictionary based on the contents of this array.</summary>
    /// <param name="immutableArray">The array to create a dictionary from.</param>
    /// <param name="keySelector">The key selector.</param>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="T">The type of element contained by the collection.</typeparam>
    /// <returns>The newly initialized dictionary.</returns>
    public static Dictionary<TKey, T> ToDictionary<TKey, T>(
      this ImmutableArray<T> immutableArray,
      Func<T, TKey> keySelector)
      where TKey : notnull
    {
      return immutableArray.ToDictionary<TKey, T>(keySelector, (IEqualityComparer<TKey>) EqualityComparer<TKey>.Default);
    }

    /// <summary>Creates a dictionary based on the contents of this array.</summary>
    /// <param name="immutableArray">The array to create a dictionary from.</param>
    /// <param name="keySelector">The key selector.</param>
    /// <param name="elementSelector">The element selector.</param>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TElement">The type of the element.</typeparam>
    /// <typeparam name="T">The type of element contained by the collection.</typeparam>
    /// <returns>The newly initialized dictionary.</returns>
    public static Dictionary<TKey, TElement> ToDictionary<TKey, TElement, T>(
      this ImmutableArray<T> immutableArray,
      Func<T, TKey> keySelector,
      Func<T, TElement> elementSelector)
      where TKey : notnull
    {
      return immutableArray.ToDictionary<TKey, TElement, T>(keySelector, elementSelector, (IEqualityComparer<TKey>) EqualityComparer<TKey>.Default);
    }

    /// <summary>Creates a dictionary based on the contents of this array.</summary>
    /// <param name="immutableArray">The array to create a dictionary from.</param>
    /// <param name="keySelector">The key selector.</param>
    /// <param name="comparer">The comparer to initialize the dictionary with.</param>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="T">The type of element contained by the collection.</typeparam>
    /// <returns>The newly initialized dictionary.</returns>
    public static Dictionary<TKey, T> ToDictionary<TKey, T>(
      this ImmutableArray<T> immutableArray,
      Func<T, TKey> keySelector,
      IEqualityComparer<TKey>? comparer)
      where TKey : notnull
    {
      Requires.NotNull<Func<T, TKey>>(keySelector, nameof (keySelector));
      Dictionary<TKey, T> dictionary = new Dictionary<TKey, T>(immutableArray.Length, comparer);
      foreach (T immutable in immutableArray)
        dictionary.Add(keySelector(immutable), immutable);
      return dictionary;
    }

    /// <summary>Creates a dictionary based on the contents of this array.</summary>
    /// <param name="immutableArray">The array to create a dictionary from.</param>
    /// <param name="keySelector">The key selector.</param>
    /// <param name="elementSelector">The element selector.</param>
    /// <param name="comparer">The comparer to initialize the dictionary with.</param>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TElement">The type of the element.</typeparam>
    /// <typeparam name="T">The type of element contained by the collection.</typeparam>
    /// <returns>The newly initialized dictionary.</returns>
    public static Dictionary<TKey, TElement> ToDictionary<TKey, TElement, T>(
      this ImmutableArray<T> immutableArray,
      Func<T, TKey> keySelector,
      Func<T, TElement> elementSelector,
      IEqualityComparer<TKey>? comparer)
      where TKey : notnull
    {
      Requires.NotNull<Func<T, TKey>>(keySelector, nameof (keySelector));
      Requires.NotNull<Func<T, TElement>>(elementSelector, nameof (elementSelector));
      Dictionary<TKey, TElement> dictionary = new Dictionary<TKey, TElement>(immutableArray.Length, comparer);
      foreach (T obj in immutableArray.array)
        dictionary.Add(keySelector(obj), elementSelector(obj));
      return dictionary;
    }

    /// <summary>Copies the contents of this array to a mutable array.</summary>
    /// <param name="immutableArray">The immutable array to copy into a mutable one.</param>
    /// <typeparam name="T">The type of element contained by the collection.</typeparam>
    /// <returns>The newly instantiated array.</returns>
    public static T[] ToArray<T>(this ImmutableArray<T> immutableArray)
    {
      immutableArray.ThrowNullRefIfNotInitialized();
      return immutableArray.array.Length == 0 ? ImmutableArray<T>.Empty.array : (T[]) immutableArray.array.Clone();
    }

    /// <summary>Returns the first element in the collection.</summary>
    /// <param name="builder">The builder to retrieve an item from.</param>
    /// <typeparam name="T">The type of items in the array.</typeparam>
    /// <exception cref="T:System.InvalidOperationException">If the array is empty.</exception>
    /// <returns>The first item in the list.</returns>
    public static T First<T>(this ImmutableArray<T>.Builder builder)
    {
      Requires.NotNull<ImmutableArray<T>.Builder>(builder, nameof (builder));
      return ImmutableArrayExtensions.Any<T>(builder) ? builder[0] : throw new InvalidOperationException();
    }

    /// <summary>Returns the first element in the collection, or the default value if the collection is empty.</summary>
    /// <param name="builder">The builder to retrieve an element from.</param>
    /// <typeparam name="T">The type of item in the builder.</typeparam>
    /// <returns>The first item in the list, if found; otherwise the default value for the item type.</returns>
    public static T? FirstOrDefault<T>(this ImmutableArray<T>.Builder builder)
    {
      Requires.NotNull<ImmutableArray<T>.Builder>(builder, nameof (builder));
      return !ImmutableArrayExtensions.Any<T>(builder) ? default (T) : builder[0];
    }

    /// <summary>Returns the last element in the collection.</summary>
    /// <param name="builder">The builder to retrieve elements from.</param>
    /// <typeparam name="T">The type of item in the builder.</typeparam>
    /// <exception cref="T:System.InvalidOperationException">The collection is empty.</exception>
    /// <returns>The last element in the builder.</returns>
    public static T Last<T>(this ImmutableArray<T>.Builder builder)
    {
      Requires.NotNull<ImmutableArray<T>.Builder>(builder, nameof (builder));
      return ImmutableArrayExtensions.Any<T>(builder) ? builder[builder.Count - 1] : throw new InvalidOperationException();
    }

    /// <summary>Returns the last element in the collection, or the default value if the collection is empty.</summary>
    /// <param name="builder">The builder to retrieve an element from.</param>
    /// <typeparam name="T">The type of item in the builder.</typeparam>
    /// <returns>The last element of a sequence, or a default value if the sequence contains no elements.</returns>
    public static T? LastOrDefault<T>(this ImmutableArray<T>.Builder builder)
    {
      Requires.NotNull<ImmutableArray<T>.Builder>(builder, nameof (builder));
      return !ImmutableArrayExtensions.Any<T>(builder) ? default (T) : builder[builder.Count - 1];
    }

    /// <summary>Returns a value indicating whether this collection contains any elements.</summary>
    /// <param name="builder">The builder to check for matches.</param>
    /// <typeparam name="T">The type of elements in the array.</typeparam>
    /// <returns>
    /// <see langword="true" /> if the array builder contains any elements; otherwise, <see langword="false" />.</returns>
    public static bool Any<T>(this ImmutableArray<T>.Builder builder)
    {
      Requires.NotNull<ImmutableArray<T>.Builder>(builder, nameof (builder));
      return builder.Count > 0;
    }


    #nullable disable
    private static IEnumerable<TResult> SelectManyIterator<TSource, TCollection, TResult>(
      this ImmutableArray<TSource> immutableArray,
      Func<TSource, IEnumerable<TCollection>> collectionSelector,
      Func<TSource, TCollection, TResult> resultSelector)
    {
      TSource[] sourceArray = immutableArray.array;
      for (int index = 0; index < sourceArray.Length; ++index)
      {
        TSource item = sourceArray[index];
        foreach (TCollection collection in collectionSelector(item))
          yield return resultSelector(item, collection);
        item = default (TSource);
      }
      sourceArray = (TSource[]) null;
    }
  }
}
