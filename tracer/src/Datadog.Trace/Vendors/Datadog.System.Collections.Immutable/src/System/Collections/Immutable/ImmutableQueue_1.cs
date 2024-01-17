﻿// Decompiled with JetBrains decompiler
// Type: System.Collections.Immutable.ImmutableQueue`1
// Assembly: System.Collections.Immutable, Version=7.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 5F9FF90F-0D16-4469-A104-76829D3705E2
// Assembly location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.dll
// XML documentation location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.xml

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;


#nullable enable
namespace Datadog.System.Collections.Immutable
{
    /// <summary>Represents an immutable queue.
    /// 
    /// NuGet package: System.Collections.Immutable (about immutable collections and how to install)</summary>
    /// <typeparam name="T">The type of elements in the queue.</typeparam>
    [DebuggerDisplay("IsEmpty = {IsEmpty}")]
  [DebuggerTypeProxy(typeof (ImmutableEnumerableDebuggerProxy<>))]
  public sealed class ImmutableQueue<T> : IImmutableQueue<T>, IEnumerable<T>, IEnumerable
  {

    #nullable disable
    private static readonly ImmutableQueue<T> s_EmptyField = new ImmutableQueue<T>(ImmutableStack<T>.Empty, ImmutableStack<T>.Empty);
    private readonly ImmutableStack<T> _backwards;
    private readonly ImmutableStack<T> _forwards;
    private ImmutableStack<T> _backwardsReversed;


    #nullable enable
    internal ImmutableQueue(ImmutableStack<T> forwards, ImmutableStack<T> backwards)
    {
      this._forwards = forwards;
      this._backwards = backwards;
    }

    /// <summary>Removes all objects from the immutable queue.</summary>
    /// <returns>The empty immutable queue.</returns>
    public ImmutableQueue<T> Clear() => ImmutableQueue<T>.Empty;

    /// <summary>Gets a value that indicates whether this immutable queue is empty.
    /// 
    /// NuGet package: System.Collections.Immutable (about immutable collections and how to install)</summary>
    /// <returns>
    /// <see langword="true" /> if this queue is empty; otherwise, <see langword="false" />.</returns>
    public bool IsEmpty => this._forwards.IsEmpty;

    /// <summary>Gets an empty immutable queue.</summary>
    /// <returns>An empty immutable queue.</returns>
    public static ImmutableQueue<T> Empty => ImmutableQueue<T>.s_EmptyField;


    #nullable disable
    /// <summary>Removes all elements from the immutable queue.</summary>
    /// <returns>The empty immutable queue.</returns>
    IImmutableQueue<T> IImmutableQueue<T>.Clear() => (IImmutableQueue<T>) this.Clear();


    #nullable enable
    private ImmutableStack<T> BackwardsReversed
    {
      get
      {
        if (this._backwardsReversed == null)
          this._backwardsReversed = this._backwards.Reverse();
        return this._backwardsReversed;
      }
    }

    /// <summary>Returns the element at the beginning of the immutable queue without removing it.</summary>
    /// <exception cref="T:System.InvalidOperationException">The queue is empty.</exception>
    /// <returns>The element at the beginning of the queue.</returns>
    public T Peek()
    {
      if (this.IsEmpty)
        throw new InvalidOperationException();
        //throw new InvalidOperationException(SR.InvalidEmptyOperation);
      return this._forwards.Peek();
    }

    /// <summary>Gets a read-only reference to the element at the front of the queue.</summary>
    /// <exception cref="T:System.InvalidOperationException">The queue is empty.</exception>
    /// <returns>Read-only reference to the element at the front of the queue.</returns>
    public ref readonly T PeekRef()
    {
      if (this.IsEmpty)
        throw new InvalidOperationException();
        //throw new InvalidOperationException(SR.InvalidEmptyOperation);
      return ref this._forwards.PeekRef();
    }

    /// <summary>Adds an element to the end of the immutable queue, and returns the new queue.</summary>
    /// <param name="value">The element to add.</param>
    /// <returns>The new immutable queue.</returns>
    public ImmutableQueue<T> Enqueue(T value) => this.IsEmpty ? new ImmutableQueue<T>(ImmutableStack.Create<T>(value), ImmutableStack<T>.Empty) : new ImmutableQueue<T>(this._forwards, this._backwards.Push(value));


    #nullable disable
    /// <summary>Adds an element to the end of the immutable queue, and returns the new queue.</summary>
    /// <param name="value">The element to add.</param>
    /// <returns>The new immutable queue.</returns>
    IImmutableQueue<T> IImmutableQueue<T>.Enqueue(T value) => (IImmutableQueue<T>) this.Enqueue(value);


    #nullable enable
    /// <summary>Removes the element at the beginning of the immutable queue, and returns the new queue.</summary>
    /// <exception cref="T:System.InvalidOperationException">The queue is empty.</exception>
    /// <returns>The new immutable queue; never <see langword="null" />.</returns>
    public ImmutableQueue<T> Dequeue()
    {
      if (this.IsEmpty)
        throw new InvalidOperationException();
       // throw new InvalidOperationException(SR.InvalidEmptyOperation);
      ImmutableStack<T> forwards = this._forwards.Pop();
      if (!forwards.IsEmpty)
        return new ImmutableQueue<T>(forwards, this._backwards);
      return this._backwards.IsEmpty ? ImmutableQueue<T>.Empty : new ImmutableQueue<T>(this.BackwardsReversed, ImmutableStack<T>.Empty);
    }

    /// <summary>Removes the item at the beginning of the immutable queue, and returns the new queue.</summary>
    /// <param name="value">When this method returns, contains the element from the beginning of the queue.</param>
    /// <exception cref="T:System.InvalidOperationException">The queue is empty.</exception>
    /// <returns>The new immutable queue with the beginning element removed.</returns>
    public ImmutableQueue<T> Dequeue(out T value)
    {
      value = this.Peek();
      return this.Dequeue();
    }


    #nullable disable
    /// <summary>Removes the element at the beginning of the immutable queue, and returns the new queue.</summary>
    /// <exception cref="T:System.InvalidOperationException">The queue is empty.</exception>
    /// <returns>The new immutable queue; never <see langword="null" />.</returns>
    IImmutableQueue<T> IImmutableQueue<T>.Dequeue() => (IImmutableQueue<T>) this.Dequeue();


    #nullable enable
    /// <summary>Returns an enumerator that iterates through the immutable queue.</summary>
    /// <returns>An enumerator that can be used to iterate through the queue.</returns>
    public ImmutableQueue<
    #nullable disable
    T>.Enumerator GetEnumerator() => new ImmutableQueue<T>.Enumerator(this);

    /// <summary>Returns an enumerator that iterates through the collection.</summary>
    /// <returns>An enumerator  that can be used to iterate through the collection.</returns>
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => !this.IsEmpty ? (IEnumerator<T>) new ImmutableQueue<T>.EnumeratorObject(this) : Enumerable.Empty<T>().GetEnumerator();

    /// <summary>Returns an enumerator that iterates through a collection.</summary>
    /// <returns>An <see cref="T:System.Collections.IEnumerator" /> object that can be used to iterate through the collection.</returns>
    IEnumerator IEnumerable.GetEnumerator() => (IEnumerator) new ImmutableQueue<T>.EnumeratorObject(this);


    #nullable enable
    /// <summary>Enumerates the contents of an immutable queue without allocating any memory.
    /// 
    /// NuGet package: System.Collections.Immutable (about immutable collections and how to install)</summary>
    /// <typeparam name="T" />
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public struct Enumerator
    {

      #nullable disable
      private readonly ImmutableQueue<T> _originalQueue;
      private ImmutableStack<T> _remainingForwardsStack;
      private ImmutableStack<T> _remainingBackwardsStack;


      #nullable enable
      internal Enumerator(ImmutableQueue<T> queue)
      {
        this._originalQueue = queue;
        this._remainingForwardsStack = (ImmutableStack<T>) null;
        this._remainingBackwardsStack = (ImmutableStack<T>) null;
      }

      /// <summary>Gets the element at the current position of the enumerator.</summary>
      /// <returns>The element at the current position of the enumerator.</returns>
      public T Current
      {
        get
        {
          if (this._remainingForwardsStack == null)
            throw new InvalidOperationException();
          if (!this._remainingForwardsStack.IsEmpty)
            return this._remainingForwardsStack.Peek();
          if (!this._remainingBackwardsStack.IsEmpty)
            return this._remainingBackwardsStack.Peek();
          throw new InvalidOperationException();
        }
      }

      /// <summary>Advances the enumerator to the next element of the immutable queue.</summary>
      /// <returns>
      /// <see langword="true" /> if the enumerator was successfully advanced to the next element; <see langword="false" /> if the enumerator has passed the end of the queue.</returns>
      public bool MoveNext()
      {
        if (this._remainingForwardsStack == null)
        {
          this._remainingForwardsStack = this._originalQueue._forwards;
          this._remainingBackwardsStack = this._originalQueue.BackwardsReversed;
        }
        else if (!this._remainingForwardsStack.IsEmpty)
          this._remainingForwardsStack = this._remainingForwardsStack.Pop();
        else if (!this._remainingBackwardsStack.IsEmpty)
          this._remainingBackwardsStack = this._remainingBackwardsStack.Pop();
        return !this._remainingForwardsStack.IsEmpty || !this._remainingBackwardsStack.IsEmpty;
      }
    }


    #nullable disable
    private sealed class EnumeratorObject : IEnumerator<T>, IDisposable, IEnumerator
    {
      private readonly ImmutableQueue<T> _originalQueue;
      private ImmutableStack<T> _remainingForwardsStack;
      private ImmutableStack<T> _remainingBackwardsStack;
      private bool _disposed;

      internal EnumeratorObject(ImmutableQueue<T> queue) => this._originalQueue = queue;

      public T Current
      {
        get
        {
          this.ThrowIfDisposed();
          if (this._remainingForwardsStack == null)
            throw new InvalidOperationException();
          if (!this._remainingForwardsStack.IsEmpty)
            return this._remainingForwardsStack.Peek();
          return !this._remainingBackwardsStack.IsEmpty ? this._remainingBackwardsStack.Peek() : throw new InvalidOperationException();
        }
      }

      object IEnumerator.Current => (object) this.Current;

      public bool MoveNext()
      {
        this.ThrowIfDisposed();
        if (this._remainingForwardsStack == null)
        {
          this._remainingForwardsStack = this._originalQueue._forwards;
          this._remainingBackwardsStack = this._originalQueue.BackwardsReversed;
        }
        else if (!this._remainingForwardsStack.IsEmpty)
          this._remainingForwardsStack = this._remainingForwardsStack.Pop();
        else if (!this._remainingBackwardsStack.IsEmpty)
          this._remainingBackwardsStack = this._remainingBackwardsStack.Pop();
        return !this._remainingForwardsStack.IsEmpty || !this._remainingBackwardsStack.IsEmpty;
      }

      public void Reset()
      {
        this.ThrowIfDisposed();
        this._remainingBackwardsStack = (ImmutableStack<T>) null;
        this._remainingForwardsStack = (ImmutableStack<T>) null;
      }

      public void Dispose() => this._disposed = true;

      private void ThrowIfDisposed()
      {
        if (!this._disposed)
          return;
        Requires.FailObjectDisposed<ImmutableQueue<T>.EnumeratorObject>(this);
      }
    }
  }
}
