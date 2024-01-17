﻿// Decompiled with JetBrains decompiler
// Type: System.Collections.Immutable.ImmutableStack
// Assembly: System.Collections.Immutable, Version=7.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 5F9FF90F-0D16-4469-A104-76829D3705E2
// Assembly location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.dll
// XML documentation location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.xml

using System.Collections.Generic;


#nullable enable
namespace Datadog.System.Collections.Immutable
{
    /// <summary>Provides a set of initialization methods for instances of the <see cref="T:System.Collections.Immutable.ImmutableStack`1" /> class.
    /// 
    /// NuGet package: System.Collections.Immutable (about immutable collections and how to install)</summary>
    public static class ImmutableStack
  {
    /// <summary>Creates an empty immutable stack.</summary>
    /// <typeparam name="T">The type of items to be stored in the immutable stack.</typeparam>
    /// <returns>An empty immutable stack.</returns>
    public static ImmutableStack<T> Create<T>() => ImmutableStack<T>.Empty;

    /// <summary>Creates a new immutable stack that contains the specified item.</summary>
    /// <param name="item">The item to prepopulate the stack with.</param>
    /// <typeparam name="T">The type of items in the immutable stack.</typeparam>
    /// <returns>A new immutable collection that contains the specified item.</returns>
    public static ImmutableStack<T> Create<T>(T item) => ImmutableStack<T>.Empty.Push(item);

    /// <summary>Creates a new immutable stack that contains the specified items.</summary>
    /// <param name="items">The items to add to the stack before it's immutable.</param>
    /// <typeparam name="T">The type of items in the stack.</typeparam>
    /// <returns>An immutable stack that contains the specified items.</returns>
    public static ImmutableStack<T> CreateRange<T>(IEnumerable<T> items)
    {
      Requires.NotNull<IEnumerable<T>>(items, nameof (items));
      ImmutableStack<T> range = ImmutableStack<T>.Empty;
      foreach (T obj in items)
        range = range.Push(obj);
      return range;
    }

    /// <summary>Creates a new immutable stack that contains the specified array of items.</summary>
    /// <param name="items">An array that contains the items to prepopulate the stack with.</param>
    /// <typeparam name="T">The type of items in the immutable stack.</typeparam>
    /// <returns>A new immutable stack that contains the specified items.</returns>
    public static ImmutableStack<T> Create<T>(params T[] items)
    {
      Requires.NotNull<T[]>(items, nameof (items));
      ImmutableStack<T> immutableStack = ImmutableStack<T>.Empty;
      foreach (T obj in items)
        immutableStack = immutableStack.Push(obj);
      return immutableStack;
    }

    /// <summary>Removes the specified item from an immutable stack.</summary>
    /// <param name="stack">The stack to modify.</param>
    /// <param name="value">The item to remove from the stack.</param>
    /// <typeparam name="T">The type of items contained in the stack.</typeparam>
    /// <exception cref="T:System.InvalidOperationException">The stack is empty.</exception>
    /// <returns>A stack; never <see langword="null" />.</returns>
    public static IImmutableStack<T> Pop<T>(this IImmutableStack<T> stack, out T value)
    {
      Requires.NotNull<IImmutableStack<T>>(stack, nameof (stack));
      value = stack.Peek();
      return stack.Pop();
    }
  }
}
