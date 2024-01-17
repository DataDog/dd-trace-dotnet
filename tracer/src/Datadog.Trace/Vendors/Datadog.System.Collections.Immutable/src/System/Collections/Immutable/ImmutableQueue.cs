﻿// Decompiled with JetBrains decompiler
// Type: System.Collections.Immutable.ImmutableQueue
// Assembly: System.Collections.Immutable, Version=7.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 5F9FF90F-0D16-4469-A104-76829D3705E2
// Assembly location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.dll
// XML documentation location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.xml

using System.Collections.Generic;


#nullable enable
namespace Datadog.System.Collections.Immutable
{
    /// <summary>Provides a set of initialization methods for instances of the <see cref="T:System.Collections.Immutable.ImmutableQueue`1" /> class.
    /// 
    /// NuGet package: System.Collections.Immutable (about immutable collections and how to install)</summary>
    public static class ImmutableQueue
  {
    /// <summary>Creates an empty immutable queue.</summary>
    /// <typeparam name="T">The type of items to be stored in the immutable queue.</typeparam>
    /// <returns>An empty immutable queue.</returns>
    public static ImmutableQueue<T> Create<T>() => ImmutableQueue<T>.Empty;

    /// <summary>Creates a new immutable queue that contains the specified item.</summary>
    /// <param name="item">The item to prepopulate the queue with.</param>
    /// <typeparam name="T">The type of items in the immutable queue.</typeparam>
    /// <returns>A new immutable queue that contains the specified item.</returns>
    public static ImmutableQueue<T> Create<T>(T item) => ImmutableQueue<T>.Empty.Enqueue(item);

    /// <summary>Creates a new immutable queue that contains the specified items.</summary>
    /// <param name="items">The items to add to the queue before immutability is applied.</param>
    /// <typeparam name="T">The type of elements in the queue.</typeparam>
    /// <returns>An immutable queue that contains the specified items.</returns>
    public static ImmutableQueue<T> CreateRange<T>(IEnumerable<T> items)
    {
      Requires.NotNull<IEnumerable<T>>(items, nameof (items));
      if (items is T[] objArray)
        return ImmutableQueue.Create<T>(objArray);
      using (IEnumerator<T> enumerator = items.GetEnumerator())
      {
        if (!enumerator.MoveNext())
          return ImmutableQueue<T>.Empty;
        ImmutableStack<T> forwards = ImmutableStack.Create<T>(enumerator.Current);
        ImmutableStack<T> backwards = ImmutableStack<T>.Empty;
        while (enumerator.MoveNext())
          backwards = backwards.Push(enumerator.Current);
        return new ImmutableQueue<T>(forwards, backwards);
      }
    }

    /// <summary>Creates a new immutable queue that contains the specified array of items.</summary>
    /// <param name="items">An array that contains the items to prepopulate the queue with.</param>
    /// <typeparam name="T">The type of items in the immutable queue.</typeparam>
    /// <returns>A new immutable queue that contains the specified items.</returns>
    public static ImmutableQueue<T> Create<T>(params T[] items)
    {
      Requires.NotNull<T[]>(items, nameof (items));
      if (items.Length == 0)
        return ImmutableQueue<T>.Empty;
      ImmutableStack<T> forwards = ImmutableStack<T>.Empty;
      for (int index = items.Length - 1; index >= 0; --index)
        forwards = forwards.Push(items[index]);
      return new ImmutableQueue<T>(forwards, ImmutableStack<T>.Empty);
    }

    /// <summary>Removes the item at the beginning of the immutable queue, and returns the new queue.</summary>
    /// <param name="queue">The queue to remove the item from.</param>
    /// <param name="value">When this method returns, contains the item from the beginning of the queue.</param>
    /// <typeparam name="T">The type of elements in the immutable queue.</typeparam>
    /// <exception cref="T:System.InvalidOperationException">The stack is empty.</exception>
    /// <returns>The new queue with the item removed.</returns>
    public static IImmutableQueue<T> Dequeue<T>(this IImmutableQueue<T> queue, out T value)
    {
      Requires.NotNull<IImmutableQueue<T>>(queue, nameof (queue));
      value = queue.Peek();
      return queue.Dequeue();
    }
  }
}
