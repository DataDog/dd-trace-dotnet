﻿// Decompiled with JetBrains decompiler
// Type: System.Collections.Immutable.ImmutableInterlocked
// Assembly: System.Collections.Immutable, Version=7.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 5F9FF90F-0D16-4469-A104-76829D3705E2
// Assembly location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.dll
// XML documentation location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.xml

using System;
using System.Collections.Generic;
using System.Threading;
using Datadog.System.Diagnostics.CodeAnalysis;
using Datadog.System.Runtime.CompilerServices.Unsafe;


#nullable enable
namespace Datadog.System.Collections.Immutable
{
    /// <summary>Contains interlocked exchange mechanisms for immutable collections.
    /// 
    /// NuGet package: System.Collections.Immutable (about immutable collections and how to install)</summary>
    public static class ImmutableInterlocked
  {
    /// <summary>Mutates a value in-place with optimistic locking transaction semantics             via a specified transformation function.             The transformation is retried as many times as necessary to win the optimistic locking race.</summary>
    /// <param name="location">The variable or field to be changed, which may be accessed by multiple threads.</param>
    /// <param name="transformer">A function that mutates the value. This function should be side-effect free,              as it may run multiple times when races occur with other threads.</param>
    /// <typeparam name="T">The type of data.</typeparam>
    /// <returns>
    /// <see langword="true" /> if the location's value is changed by applying the result of the <paramref name="transformer" /> function; <see langword="false" /> if the location's value remained the same because the last invocation of <paramref name="transformer" /> returned the existing value.</returns>
    public static bool Update<T>(ref T location, Func<T, T> transformer) where T : class?
    {
      Requires.NotNull<Func<T, T>>(transformer, nameof (transformer));
      T comparand = Volatile.Read<T>(ref location);
      bool flag;
      do
      {
        T obj1 = transformer(comparand);
        if ((object) comparand == (object) obj1)
          return false;
        T obj2 = Interlocked.CompareExchange<T>(ref location, obj1, comparand);
        flag = (object) comparand == (object) obj2;
        comparand = obj2;
      }
      while (!flag);
      return true;
    }

    /// <summary>Mutates a value in-place with optimistic locking transaction semantics             via a specified transformation function.             The transformation is retried as many times as necessary to win the optimistic locking race.</summary>
    /// <param name="location">The variable or field to be changed, which may be accessed by multiple threads.</param>
    /// <param name="transformer">A function that mutates the value. This function should be side-effect free,              as it may run multiple times when races occur with other threads.</param>
    /// <param name="transformerArgument">The argument to pass to <paramref name="transformer" />.</param>
    /// <typeparam name="T">The type of data.</typeparam>
    /// <typeparam name="TArg">The type of argument passed to the <paramref name="transformer" />.</typeparam>
    /// <returns>
    /// <see langword="true" /> if the location's value is changed by applying the result of the <paramref name="transformer" /> function; <see langword="false" /> if the location's value remained the same because the last invocation of <paramref name="transformer" /> returned the existing value.</returns>
    public static bool Update<T, TArg>(
      ref T location,
      Func<T, TArg, T> transformer,
      TArg transformerArgument)
      where T : class?
    {
      Requires.NotNull<Func<T, TArg, T>>(transformer, nameof (transformer));
      T comparand = Volatile.Read<T>(ref location);
      bool flag;
      do
      {
        T obj1 = transformer(comparand, transformerArgument);
        if ((object) comparand == (object) obj1)
          return false;
        T obj2 = Interlocked.CompareExchange<T>(ref location, obj1, comparand);
        flag = (object) comparand == (object) obj2;
        comparand = obj2;
      }
      while (!flag);
      return true;
    }

    /// <summary>Mutates an immutable array in-place with optimistic locking transaction semantics via a specified transformation function.
    /// The transformation is retried as many times as necessary to win the optimistic locking race.</summary>
    /// <param name="location">The immutable array to be changed.</param>
    /// <param name="transformer">A function that produces the new array from the old. This function should be side-effect free, as it may run multiple times when races occur with other threads.</param>
    /// <typeparam name="T">The type of data in the immutable array.</typeparam>
    /// <returns>
    /// <see langword="true" /> if the location's value is changed by applying the result of the <paramref name="transformer" /> function; <see langword="false" /> if the location's value remained the same because the last invocation of <paramref name="transformer" /> returned the existing value.</returns>
    public static bool Update<T>(
      ref ImmutableArray<T> location,
      Func<ImmutableArray<T>, ImmutableArray<T>> transformer)
    {
      Requires.NotNull<Func<ImmutableArray<T>, ImmutableArray<T>>>(transformer, nameof (transformer));
      T[] objArray1 = Volatile.Read<T[]>(ref Unsafe.AsRef<T[]>(in location.array));
      bool flag;
      do
      {
        ImmutableArray<T> immutableArray = transformer(new ImmutableArray<T>(objArray1));
        if (objArray1 == immutableArray.array)
          return false;
        T[] objArray2 = Interlocked.CompareExchange<T[]>(ref Unsafe.AsRef<T[]>(in location.array), immutableArray.array, objArray1);
        flag = objArray1 == objArray2;
        objArray1 = objArray2;
      }
      while (!flag);
      return true;
    }

    /// <summary>Mutates an immutable array in-place with optimistic locking transaction semantics via a specified transformation function.
    /// The transformation is retried as many times as necessary to win the optimistic locking race.</summary>
    /// <param name="location">The immutable array to be changed.</param>
    /// <param name="transformer">A function that produces the new array from the old. This function should be side-effect free, as it may run multiple times when races occur with other threads.</param>
    /// <param name="transformerArgument">The argument to pass to <paramref name="transformer" />.</param>
    /// <typeparam name="T">The type of data in the immutable array.</typeparam>
    /// <typeparam name="TArg">The type of argument passed to the <paramref name="transformer" />.</typeparam>
    /// <returns>
    /// <see langword="true" /> if the location's value is changed by applying the result of the <paramref name="transformer" /> function; <see langword="false" /> if the location's value remained the same because the last invocation of <paramref name="transformer" /> returned the existing value.</returns>
    public static bool Update<T, TArg>(
      ref ImmutableArray<T> location,
      Func<ImmutableArray<T>, TArg, ImmutableArray<T>> transformer,
      TArg transformerArgument)
    {
      Requires.NotNull<Func<ImmutableArray<T>, TArg, ImmutableArray<T>>>(transformer, nameof (transformer));
      T[] objArray1 = Volatile.Read<T[]>(ref Unsafe.AsRef<T[]>(in location.array));
      bool flag;
      do
      {
        ImmutableArray<T> immutableArray = transformer(new ImmutableArray<T>(objArray1), transformerArgument);
        if (objArray1 == immutableArray.array)
          return false;
        T[] objArray2 = Interlocked.CompareExchange<T[]>(ref Unsafe.AsRef<T[]>(in location.array), immutableArray.array, objArray1);
        flag = objArray1 == objArray2;
        objArray1 = objArray2;
      }
      while (!flag);
      return true;
    }

    /// <summary>Sets an array to the specified array and returns a reference to the original array, as an atomic operation.</summary>
    /// <param name="location">The array to set to the specified value.</param>
    /// <param name="value">The value to which the <paramref name="location" /> parameter is set.</param>
    /// <typeparam name="T">The type of element stored by the array.</typeparam>
    /// <returns>The original value of <paramref name="location" />.</returns>
    public static ImmutableArray<T> InterlockedExchange<T>(
      ref ImmutableArray<T> location,
      ImmutableArray<T> value)
    {
      return new ImmutableArray<T>(Interlocked.Exchange<T[]>(ref Unsafe.AsRef<T[]>(in location.array), value.array));
    }

    /// <summary>Compares two immutable arrays for equality and, if they are equal, replaces one of the arrays.</summary>
    /// <param name="location">The destination, whose value is compared with <paramref name="comparand" /> and possibly replaced.</param>
    /// <param name="value">The value that replaces the destination value if the comparison results in equality.</param>
    /// <param name="comparand">The value that is compared to the value at <paramref name="location" />.</param>
    /// <typeparam name="T">The type of element stored by the array.</typeparam>
    /// <returns>The original value in <paramref name="location" />.</returns>
    public static ImmutableArray<T> InterlockedCompareExchange<T>(
      ref ImmutableArray<T> location,
      ImmutableArray<T> value,
      ImmutableArray<T> comparand)
    {
      return new ImmutableArray<T>(Interlocked.CompareExchange<T[]>(ref Unsafe.AsRef<T[]>(in location.array), value.array, comparand.array));
    }

    /// <summary>Sets an array to the specified array if the array has not been initialized.</summary>
    /// <param name="location">The array to set to the specified value.</param>
    /// <param name="value">The value to which the <paramref name="location" /> parameter is set, if it's not initialized.</param>
    /// <typeparam name="T">The type of element stored by the array.</typeparam>
    /// <returns>
    /// <see langword="true" /> if the array was assigned the specified value;  otherwise, <see langword="false" />.</returns>
    public static bool InterlockedInitialize<T>(
      ref ImmutableArray<T> location,
      ImmutableArray<T> value)
    {
      return ImmutableInterlocked.InterlockedCompareExchange<T>(ref location, value, new ImmutableArray<T>()).IsDefault;
    }

    /// <summary>Gets the value for the specified key from the dictionary, or if the key was not found, adds a new value to the dictionary.</summary>
    /// <param name="location">The variable or field to update if the specified is not in the dictionary.</param>
    /// <param name="key">The key for the value to retrieve or add.</param>
    /// <param name="valueFactory">The function to execute to obtain the value to insert into the dictionary if the key is not found.</param>
    /// <param name="factoryArgument">The argument to pass to the value factory.</param>
    /// <typeparam name="TKey">The type of the keys contained in the collection.</typeparam>
    /// <typeparam name="TValue">The type of the values contained in the collection.</typeparam>
    /// <typeparam name="TArg">The type of the argument supplied to the value factory.</typeparam>
    /// <returns>The value at the specified key or <paramref name="valueFactory" /> if the key was not present.</returns>
    public static TValue GetOrAdd<TKey, TValue, TArg>(
      ref ImmutableDictionary<TKey, TValue> location,
      TKey key,
      Func<TKey, TArg, TValue> valueFactory,
      TArg factoryArgument)
      where TKey : notnull
    {
      Requires.NotNull<Func<TKey, TArg, TValue>>(valueFactory, nameof (valueFactory));
      ImmutableDictionary<TKey, TValue> immutableDictionary = Volatile.Read<ImmutableDictionary<TKey, TValue>>(ref location);
      Requires.NotNull<ImmutableDictionary<TKey, TValue>>(immutableDictionary, nameof (location));
      TValue orAdd;
      if (immutableDictionary.TryGetValue(key, out orAdd))
        return orAdd;
      TValue obj = valueFactory(key, factoryArgument);
      return ImmutableInterlocked.GetOrAdd<TKey, TValue>(ref location, key, obj);
    }

    /// <summary>Gets the value for the specified key from the dictionary, or if the key was not found, adds a new value to the dictionary.</summary>
    /// <param name="location">The variable or field to atomically update if the specified  is not in the dictionary.</param>
    /// <param name="key">The key for the value to retrieve or add.</param>
    /// <param name="valueFactory">The function to execute to obtain the value to insert into the dictionary if the key is not found. This delegate will not be invoked more than once.</param>
    /// <typeparam name="TKey">The type of the keys contained in the collection.</typeparam>
    /// <typeparam name="TValue">The type of the values contained in the collection.</typeparam>
    /// <returns>The value at the specified key or <paramref name="valueFactory" /> if the key was not present.</returns>
    public static TValue GetOrAdd<TKey, TValue>(
      ref ImmutableDictionary<TKey, TValue> location,
      TKey key,
      Func<TKey, TValue> valueFactory)
      where TKey : notnull
    {
      Requires.NotNull<Func<TKey, TValue>>(valueFactory, nameof (valueFactory));
      ImmutableDictionary<TKey, TValue> immutableDictionary = Volatile.Read<ImmutableDictionary<TKey, TValue>>(ref location);
      Requires.NotNull<ImmutableDictionary<TKey, TValue>>(immutableDictionary, nameof (location));
      TValue orAdd;
      if (immutableDictionary.TryGetValue(key, out orAdd))
        return orAdd;
      TValue obj = valueFactory(key);
      return ImmutableInterlocked.GetOrAdd<TKey, TValue>(ref location, key, obj);
    }

    /// <summary>Gets the value for the specified key from the dictionary, or if the key was not found, adds a new value to the dictionary.</summary>
    /// <param name="location">The variable or field to atomically update if the specified key is not in the dictionary.</param>
    /// <param name="key">The key for the value to get or add.</param>
    /// <param name="value">The value to add to the dictionary the key is not found.</param>
    /// <typeparam name="TKey">The type of the keys contained in the collection.</typeparam>
    /// <typeparam name="TValue">The type of the values contained in the collection.</typeparam>
    /// <returns>The value at the specified key or <paramref name="valueFactory" /> if the key was not present.</returns>
    public static TValue GetOrAdd<TKey, TValue>(
      ref ImmutableDictionary<TKey, TValue> location,
      TKey key,
      TValue value)
      where TKey : notnull
    {
      ImmutableDictionary<TKey, TValue> comparand = Volatile.Read<ImmutableDictionary<TKey, TValue>>(ref location);
      bool flag;
      do
      {
        Requires.NotNull<ImmutableDictionary<TKey, TValue>>(comparand, nameof (location));
        TValue orAdd;
        if (comparand.TryGetValue(key, out orAdd))
          return orAdd;
        ImmutableDictionary<TKey, TValue> immutableDictionary1 = comparand.Add(key, value);
        ImmutableDictionary<TKey, TValue> immutableDictionary2 = Interlocked.CompareExchange<ImmutableDictionary<TKey, TValue>>(ref location, immutableDictionary1, comparand);
        flag = comparand == immutableDictionary2;
        comparand = immutableDictionary2;
      }
      while (!flag);
      return value;
    }

    /// <summary>Obtains the value from a dictionary after having added it or updated an existing entry.</summary>
    /// <param name="location">The variable or field to atomically update if the specified  is not in the dictionary.</param>
    /// <param name="key">The key for the value to add or update.</param>
    /// <param name="addValueFactory">The function that receives the key and returns a new value to add to the dictionary when no value previously exists.</param>
    /// <param name="updateValueFactory">The function that receives the key and prior value and returns the new value with which to update the dictionary.</param>
    /// <typeparam name="TKey">The type of key stored by the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of value stored by the dictionary.</typeparam>
    /// <returns>The added or updated value.</returns>
    public static TValue AddOrUpdate<TKey, TValue>(
      ref ImmutableDictionary<TKey, TValue> location,
      TKey key,
      Func<TKey, TValue> addValueFactory,
      Func<TKey, TValue, TValue> updateValueFactory)
      where TKey : notnull
    {
      Requires.NotNull<Func<TKey, TValue>>(addValueFactory, nameof (addValueFactory));
      Requires.NotNull<Func<TKey, TValue, TValue>>(updateValueFactory, nameof (updateValueFactory));
      ImmutableDictionary<TKey, TValue> comparand = Volatile.Read<ImmutableDictionary<TKey, TValue>>(ref location);
      TValue obj1;
      bool flag;
      do
      {
        Requires.NotNull<ImmutableDictionary<TKey, TValue>>(comparand, nameof (location));
        TValue obj2;
        obj1 = !comparand.TryGetValue(key, out obj2) ? addValueFactory(key) : updateValueFactory(key, obj2);
        ImmutableDictionary<TKey, TValue> immutableDictionary1 = comparand.SetItem(key, obj1);
        if (comparand == immutableDictionary1)
          return obj2;
        ImmutableDictionary<TKey, TValue> immutableDictionary2 = Interlocked.CompareExchange<ImmutableDictionary<TKey, TValue>>(ref location, immutableDictionary1, comparand);
        flag = comparand == immutableDictionary2;
        comparand = immutableDictionary2;
      }
      while (!flag);
      return obj1;
    }

    /// <summary>Obtains the value from a dictionary after having added it or updated an existing entry.</summary>
    /// <param name="location">The variable or field to atomically update if the specified  is not in the dictionary.</param>
    /// <param name="key">The key for the value to add or update.</param>
    /// <param name="addValue">The value to use if no previous value exists.</param>
    /// <param name="updateValueFactory">The function that receives the key and prior value and returns the new value with which to update the dictionary.</param>
    /// <typeparam name="TKey">The type of key stored by the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of value stored by the dictionary.</typeparam>
    /// <returns>The added or updated value.</returns>
    public static TValue AddOrUpdate<TKey, TValue>(
      ref ImmutableDictionary<TKey, TValue> location,
      TKey key,
      TValue addValue,
      Func<TKey, TValue, TValue> updateValueFactory)
      where TKey : notnull
    {
      Requires.NotNull<Func<TKey, TValue, TValue>>(updateValueFactory, nameof (updateValueFactory));
      ImmutableDictionary<TKey, TValue> comparand = Volatile.Read<ImmutableDictionary<TKey, TValue>>(ref location);
      TValue obj1;
      bool flag;
      do
      {
        Requires.NotNull<ImmutableDictionary<TKey, TValue>>(comparand, nameof (location));
        TValue obj2;
        obj1 = !comparand.TryGetValue(key, out obj2) ? addValue : updateValueFactory(key, obj2);
        ImmutableDictionary<TKey, TValue> immutableDictionary1 = comparand.SetItem(key, obj1);
        if (comparand == immutableDictionary1)
          return obj2;
        ImmutableDictionary<TKey, TValue> immutableDictionary2 = Interlocked.CompareExchange<ImmutableDictionary<TKey, TValue>>(ref location, immutableDictionary1, comparand);
        flag = comparand == immutableDictionary2;
        comparand = immutableDictionary2;
      }
      while (!flag);
      return obj1;
    }

    /// <summary>Adds the specified key and value to the dictionary if the key is not in the dictionary.</summary>
    /// <param name="location">The dictionary to update with the specified key and value.</param>
    /// <param name="key">The key to add, if is not already defined in the dictionary.</param>
    /// <param name="value">The value to add.</param>
    /// <typeparam name="TKey">The type of the keys contained in the collection.</typeparam>
    /// <typeparam name="TValue">The type of the values contained in the collection.</typeparam>
    /// <returns>
    /// <see langword="true" /> if the key is not in the dictionary; otherwise, <see langword="false" />.</returns>
    public static bool TryAdd<TKey, TValue>(
      ref ImmutableDictionary<TKey, TValue> location,
      TKey key,
      TValue value)
      where TKey : notnull
    {
      ImmutableDictionary<TKey, TValue> comparand = Volatile.Read<ImmutableDictionary<TKey, TValue>>(ref location);
      bool flag;
      do
      {
        Requires.NotNull<ImmutableDictionary<TKey, TValue>>(comparand, nameof (location));
        if (comparand.ContainsKey(key))
          return false;
        ImmutableDictionary<TKey, TValue> immutableDictionary1 = comparand.Add(key, value);
        ImmutableDictionary<TKey, TValue> immutableDictionary2 = Interlocked.CompareExchange<ImmutableDictionary<TKey, TValue>>(ref location, immutableDictionary1, comparand);
        flag = comparand == immutableDictionary2;
        comparand = immutableDictionary2;
      }
      while (!flag);
      return true;
    }

    /// <summary>Sets the specified key to the specified value if the specified key already is set to a specific value.</summary>
    /// <param name="location">The dictionary to update.</param>
    /// <param name="key">The key to update.</param>
    /// <param name="newValue">The new value to set.</param>
    /// <param name="comparisonValue">The current value for <paramref name="key" /> in order for the update to succeed.</param>
    /// <typeparam name="TKey">The type of the keys contained in the collection.</typeparam>
    /// <typeparam name="TValue">The type of the values contained in the collection.</typeparam>
    /// <returns>
    /// <see langword="true" /> if <paramref name="key" /> and <paramref name="comparisonValue" /> are present in the dictionary and comparison was updated to <paramref name="newValue" />; otherwise, <see langword="false" />.</returns>
    public static bool TryUpdate<TKey, TValue>(
      ref ImmutableDictionary<TKey, TValue> location,
      TKey key,
      TValue newValue,
      TValue comparisonValue)
      where TKey : notnull
    {
      EqualityComparer<TValue> equalityComparer = EqualityComparer<TValue>.Default;
      ImmutableDictionary<TKey, TValue> comparand = Volatile.Read<ImmutableDictionary<TKey, TValue>>(ref location);
      bool flag;
      do
      {
        Requires.NotNull<ImmutableDictionary<TKey, TValue>>(comparand, nameof (location));
        TValue x;
        if (!comparand.TryGetValue(key, out x) || !equalityComparer.Equals(x, comparisonValue))
          return false;
        ImmutableDictionary<TKey, TValue> immutableDictionary1 = comparand.SetItem(key, newValue);
        ImmutableDictionary<TKey, TValue> immutableDictionary2 = Interlocked.CompareExchange<ImmutableDictionary<TKey, TValue>>(ref location, immutableDictionary1, comparand);
        flag = comparand == immutableDictionary2;
        comparand = immutableDictionary2;
      }
      while (!flag);
      return true;
    }

    /// <summary>Removes the element with the specified key, if the key exists.</summary>
    /// <param name="location">The dictionary to update.</param>
    /// <param name="key">The key to remove.</param>
    /// <param name="value">Receives the value of the removed item, if the dictionary is not empty.</param>
    /// <typeparam name="TKey">The type of the keys contained in the collection.</typeparam>
    /// <typeparam name="TValue">The type of the values contained in the collection.</typeparam>
    /// <returns>
    /// <see langword="true" /> if the key was found and removed; otherwise, <see langword="false" />.</returns>
    public static bool TryRemove<TKey, TValue>(
      ref ImmutableDictionary<TKey, TValue> location,
      TKey key,
      [MaybeNullWhen(false)] out TValue value)
      where TKey : notnull
    {
      ImmutableDictionary<TKey, TValue> comparand = Volatile.Read<ImmutableDictionary<TKey, TValue>>(ref location);
      bool flag;
      do
      {
        Requires.NotNull<ImmutableDictionary<TKey, TValue>>(comparand, nameof (location));
        if (!comparand.TryGetValue(key, out value))
          return false;
        ImmutableDictionary<TKey, TValue> immutableDictionary1 = comparand.Remove(key);
        ImmutableDictionary<TKey, TValue> immutableDictionary2 = Interlocked.CompareExchange<ImmutableDictionary<TKey, TValue>>(ref location, immutableDictionary1, comparand);
        flag = comparand == immutableDictionary2;
        comparand = immutableDictionary2;
      }
      while (!flag);
      return true;
    }

    /// <summary>Removes an element from the top of the stack, if there is an element to remove.</summary>
    /// <param name="location">The stack to update.</param>
    /// <param name="value">Receives the value removed from the stack, if the stack is not empty.</param>
    /// <typeparam name="T">The type of items in the stack.</typeparam>
    /// <returns>
    /// <see langword="true" /> if an element is removed from the stack; otherwise, <see langword="false" />.</returns>
    public static bool TryPop<T>(ref ImmutableStack<T> location, [MaybeNullWhen(false)] out T value)
    {
      ImmutableStack<T> comparand = Volatile.Read<ImmutableStack<T>>(ref location);
      bool flag;
      do
      {
        Requires.NotNull<ImmutableStack<T>>(comparand, nameof (location));
        if (comparand.IsEmpty)
        {
          value = default (T);
          return false;
        }
        ImmutableStack<T> immutableStack1 = comparand.Pop(out value);
        ImmutableStack<T> immutableStack2 = Interlocked.CompareExchange<ImmutableStack<T>>(ref location, immutableStack1, comparand);
        flag = comparand == immutableStack2;
        comparand = immutableStack2;
      }
      while (!flag);
      return true;
    }

    /// <summary>Pushes a new element onto the stack.</summary>
    /// <param name="location">The stack to update.</param>
    /// <param name="value">The value to push on the stack.</param>
    /// <typeparam name="T">The type of items in the stack.</typeparam>
    public static void Push<T>(ref ImmutableStack<T> location, T value)
    {
      ImmutableStack<T> comparand = Volatile.Read<ImmutableStack<T>>(ref location);
      bool flag;
      do
      {
        Requires.NotNull<ImmutableStack<T>>(comparand, nameof (location));
        ImmutableStack<T> immutableStack1 = comparand.Push(value);
        ImmutableStack<T> immutableStack2 = Interlocked.CompareExchange<ImmutableStack<T>>(ref location, immutableStack1, comparand);
        flag = comparand == immutableStack2;
        comparand = immutableStack2;
      }
      while (!flag);
    }

    /// <summary>Atomically removes and returns the specified element at the head of the queue, if the queue is not empty.</summary>
    /// <param name="location">The variable or field to atomically update.</param>
    /// <param name="value">Set to the value from the head of the queue, if the queue not empty.</param>
    /// <typeparam name="T">The type of items in the queue.</typeparam>
    /// <returns>
    /// <see langword="true" /> if the queue is not empty and the head element is removed; otherwise, <see langword="false" />.</returns>
    public static bool TryDequeue<T>(ref ImmutableQueue<T> location, [MaybeNullWhen(false)] out T value)
    {
      ImmutableQueue<T> comparand = Volatile.Read<ImmutableQueue<T>>(ref location);
      bool flag;
      do
      {
        Requires.NotNull<ImmutableQueue<T>>(comparand, nameof (location));
        if (comparand.IsEmpty)
        {
          value = default (T);
          return false;
        }
        ImmutableQueue<T> immutableQueue1 = comparand.Dequeue(out value);
        ImmutableQueue<T> immutableQueue2 = Interlocked.CompareExchange<ImmutableQueue<T>>(ref location, immutableQueue1, comparand);
        flag = comparand == immutableQueue2;
        comparand = immutableQueue2;
      }
      while (!flag);
      return true;
    }

    /// <summary>Atomically enqueues an element to the end of a queue.</summary>
    /// <param name="location">The variable or field to atomically update.</param>
    /// <param name="value">The value to enqueue.</param>
    /// <typeparam name="T">The type of items contained in the collection.</typeparam>
    public static void Enqueue<T>(ref ImmutableQueue<T> location, T value)
    {
      ImmutableQueue<T> comparand = Volatile.Read<ImmutableQueue<T>>(ref location);
      bool flag;
      do
      {
        Requires.NotNull<ImmutableQueue<T>>(comparand, nameof (location));
        ImmutableQueue<T> immutableQueue1 = comparand.Enqueue(value);
        ImmutableQueue<T> immutableQueue2 = Interlocked.CompareExchange<ImmutableQueue<T>>(ref location, immutableQueue1, comparand);
        flag = comparand == immutableQueue2;
        comparand = immutableQueue2;
      }
      while (!flag);
    }
  }
}
