// Decompiled with JetBrains decompiler
// Type: System.Collections.Immutable.IImmutableStack`1
// Assembly: System.Collections.Immutable, Version=7.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 5F9FF90F-0D16-4469-A104-76829D3705E2
// Assembly location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.dll
// XML documentation location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.xml

using System.Collections;
using System.Collections.Generic;


#nullable enable
namespace Datadog.System.Collections.Immutable
{
    /// <summary>Represents an immutable last-in-first-out (LIFO) collection.
    /// 
    /// NuGet package: System.Collections.Immutable (about immutable collections and how to install)</summary>
    /// <typeparam name="T">The type of elements in the stack.</typeparam>
    public interface IImmutableStack<T> : IEnumerable<T>, IEnumerable
  {
    /// <summary>Gets a value that indicates whether this immutable stack is empty.</summary>
    /// <returns>
    /// <see langword="true" /> if this stack is empty; otherwise,<see langword="false" />.</returns>
    bool IsEmpty { get; }

    /// <summary>Removes all objects from the immutable stack.</summary>
    /// <returns>An empty immutable stack.</returns>
    IImmutableStack<T> Clear();

    /// <summary>Inserts an element at the top of the immutable stack and returns the new stack.</summary>
    /// <param name="value">The element to push onto the stack.</param>
    /// <returns>The new stack.</returns>
    IImmutableStack<T> Push(T value);

    /// <summary>Removes the element at the top of the immutable stack and returns the new stack.</summary>
    /// <exception cref="T:System.InvalidOperationException">The stack is empty.</exception>
    /// <returns>The new stack; never <see langword="null" />.</returns>
    IImmutableStack<T> Pop();

    /// <summary>Returns the element at the top of the immutable stack without removing it.</summary>
    /// <exception cref="T:System.InvalidOperationException">The stack is empty.</exception>
    /// <returns>The element at the top of the stack.</returns>
    T Peek();
  }
}
