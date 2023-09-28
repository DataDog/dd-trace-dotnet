// Decompiled with JetBrains decompiler
// Type: System.Collections.Immutable.IImmutableQueue`1
// Assembly: System.Collections.Immutable, Version=7.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 5F9FF90F-0D16-4469-A104-76829D3705E2
// Assembly location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.dll
// XML documentation location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.xml

using System.Collections;
using System.Collections.Generic;


#nullable enable
namespace Datadog.System.Collections.Immutable
{
    /// <summary>Represents an immutable first-in, first-out collection of objects.
    /// 
    /// NuGet package: System.Collections.Immutable (about immutable collections and how to install)</summary>
    /// <typeparam name="T">The type of elements in the queue.</typeparam>
    public interface IImmutableQueue<T> : IEnumerable<T>, IEnumerable
  {
    /// <summary>Gets a value that indicates whether this immutable queue is empty.</summary>
    /// <returns>
    /// <see langword="true" /> if this queue is empty; otherwise, <see langword="false" />.</returns>
    bool IsEmpty { get; }

    /// <summary>Returns a new queue with all the elements removed.</summary>
    /// <returns>An empty immutable queue.</returns>
    IImmutableQueue<T> Clear();

    /// <summary>Returns the element at the beginning of the immutable queue without removing it.</summary>
    /// <exception cref="T:System.InvalidOperationException">The queue is empty.</exception>
    /// <returns>The element at the beginning of the queue.</returns>
    T Peek();

    /// <summary>Adds an element to the end of the immutable queue, and returns the new queue.</summary>
    /// <param name="value">The element to add.</param>
    /// <returns>The new immutable queue with the specified element added.</returns>
    IImmutableQueue<T> Enqueue(T value);

    /// <summary>Removes the first element in the immutable queue, and returns the new queue.</summary>
    /// <exception cref="T:System.InvalidOperationException">The queue is empty.</exception>
    /// <returns>The new immutable queue with the first element removed. This value is never <see langword="null" />.</returns>
    IImmutableQueue<T> Dequeue();
  }
}
