﻿// Decompiled with JetBrains decompiler
// Type: System.Collections.Immutable.ImmutableStack`1
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
    /// <summary>Represents an immutable stack.
    /// 
    /// NuGet package: System.Collections.Immutable (about immutable collections and how to install)</summary>
    /// <typeparam name="T">The type of element on the stack.</typeparam>
    [DebuggerDisplay("IsEmpty = {IsEmpty}; Top = {_head}")]
  [DebuggerTypeProxy(typeof (ImmutableEnumerableDebuggerProxy<>))]
  public sealed class ImmutableStack<T> : IImmutableStack<T>, IEnumerable<T>, IEnumerable
  {

    #nullable disable
    private static readonly ImmutableStack<T> s_EmptyField = new ImmutableStack<T>();
    private readonly T _head;
    private readonly ImmutableStack<T> _tail;

    private ImmutableStack()
    {
    }

    private ImmutableStack(T head, ImmutableStack<T> tail)
    {
      this._head = head;
      this._tail = tail;
    }


    #nullable enable
    /// <summary>Gets an empty immutable stack.</summary>
    /// <returns>An empty immutable stack.</returns>
    public static ImmutableStack<T> Empty => ImmutableStack<T>.s_EmptyField;

    /// <summary>Removes all objects from the immutable stack.</summary>
    /// <returns>An empty immutable stack.</returns>
    public ImmutableStack<T> Clear() => ImmutableStack<T>.Empty;


    #nullable disable
    /// <summary>Removes all elements from the immutable stack.</summary>
    /// <returns>The empty immutable stack.</returns>
    IImmutableStack<T> IImmutableStack<T>.Clear() => (IImmutableStack<T>) this.Clear();

    /// <summary>Gets a value that indicates whether this instance of the immutable stack is empty.</summary>
    /// <returns>
    /// <see langword="true" /> if this instance is empty; otherwise, <see langword="false" />.</returns>
    public bool IsEmpty => this._tail == null;


    #nullable enable
    /// <summary>Returns the object at the top of the stack without removing it.</summary>
    /// <exception cref="T:System.InvalidOperationException">The stack is empty.</exception>
    /// <returns>The object at the top of the stack.</returns>
    public T Peek()
    {
      if (this.IsEmpty)
        throw new InvalidOperationException();
        //throw new InvalidOperationException(SR.InvalidEmptyOperation);
      return this._head;
    }

    /// <summary>Gets a read-only reference to the element on the top of the stack.</summary>
    /// <exception cref="T:System.InvalidOperationException">The stack is empty.</exception>
    /// <returns>A read-only reference to the element on the top of the stack.</returns>
    public ref readonly T PeekRef()
    {
      if (this.IsEmpty)
        throw new InvalidOperationException();
        //throw new InvalidOperationException(SR.InvalidEmptyOperation);
      return ref this._head;
    }

    /// <summary>Inserts an object at the top of the immutable stack and returns the new stack.</summary>
    /// <param name="value">The object to push onto the stack.</param>
    /// <returns>The new stack.</returns>
    public ImmutableStack<T> Push(T value) => new ImmutableStack<T>(value, this);


    #nullable disable
    /// <summary>Inserts an element at the top of the immutable stack and returns the new stack.</summary>
    /// <param name="value">The element to push onto the stack.</param>
    /// <returns>The new stack.</returns>
    IImmutableStack<T> IImmutableStack<T>.Push(T value) => (IImmutableStack<T>) this.Push(value);


    #nullable enable
    /// <summary>Removes the element at the top of the immutable stack and returns the stack after the removal.</summary>
    /// <exception cref="T:System.InvalidOperationException">The stack is empty.</exception>
    /// <returns>A stack; never <see langword="null" />.</returns>
    public ImmutableStack<T> Pop()
    {
      if (this.IsEmpty)
        throw new InvalidOperationException();
        //throw new InvalidOperationException(SR.InvalidEmptyOperation);
      return this._tail;
    }

    /// <summary>Removes the specified element from the immutable stack and returns the stack after the removal.</summary>
    /// <param name="value">The value to remove from the stack.</param>
    /// <returns>A stack; never <see langword="null" />.</returns>
    public ImmutableStack<T> Pop(out T value)
    {
      value = this.Peek();
      return this.Pop();
    }


    #nullable disable
    /// <summary>Removes the element at the top of the immutable stack and returns the new stack.</summary>
    /// <exception cref="T:System.InvalidOperationException">The stack is empty.</exception>
    /// <returns>The new stack; never <see langword="null" />.</returns>
    IImmutableStack<T> IImmutableStack<T>.Pop() => (IImmutableStack<T>) this.Pop();


    #nullable enable
    /// <summary>Returns an enumerator that iterates through the immutable stack.</summary>
    /// <returns>An enumerator that can be used to iterate through the stack.</returns>
    public ImmutableStack<
    #nullable disable
    T>.Enumerator GetEnumerator() => new ImmutableStack<T>.Enumerator(this);

    /// <summary>Returns an enumerator that iterates through the collection.</summary>
    /// <returns>An enumerator  that can be used to iterate through the collection.</returns>
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => !this.IsEmpty ? (IEnumerator<T>) new ImmutableStack<T>.EnumeratorObject(this) : Enumerable.Empty<T>().GetEnumerator();

    /// <summary>Returns an enumerator that iterates through a collection.</summary>
    /// <returns>An <see cref="T:System.Collections.IEnumerator" /> object that can be used to iterate through the collection.</returns>
    IEnumerator IEnumerable.GetEnumerator() => (IEnumerator) new ImmutableStack<T>.EnumeratorObject(this);


    #nullable enable
    internal ImmutableStack<T> Reverse()
    {
      ImmutableStack<T> immutableStack1 = this.Clear();
      for (ImmutableStack<T> immutableStack2 = this; !immutableStack2.IsEmpty; immutableStack2 = immutableStack2.Pop())
        immutableStack1 = immutableStack1.Push(immutableStack2.Peek());
      return immutableStack1;
    }

    /// <summary>Enumerates the contents of an immutable stack without allocating any memory.
    /// 
    /// NuGet package: System.Collections.Immutable (about immutable collections and how to install)</summary>
    /// <typeparam name="T" />
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public struct Enumerator
    {

      #nullable disable
      private readonly ImmutableStack<T> _originalStack;
      private ImmutableStack<T> _remainingStack;


      #nullable enable
      internal Enumerator(ImmutableStack<T> stack)
      {
        Requires.NotNull<ImmutableStack<T>>(stack, nameof (stack));
        this._originalStack = stack;
        this._remainingStack = (ImmutableStack<T>) null;
      }

      /// <summary>Gets the element at the current position of the enumerator.</summary>
      /// <returns>The element at the current position of the enumerator.</returns>
      public T Current
      {
        get
        {
          if (this._remainingStack == null || this._remainingStack.IsEmpty)
            throw new InvalidOperationException();
          return this._remainingStack.Peek();
        }
      }

      /// <summary>Advances the enumerator to the next element of the immutable stack.</summary>
      /// <returns>
      /// <see langword="true" /> if the enumerator was successfully advanced to the next element; <see langword="false" /> if the enumerator has passed the end of the stack.</returns>
      public bool MoveNext()
      {
        if (this._remainingStack == null)
          this._remainingStack = this._originalStack;
        else if (!this._remainingStack.IsEmpty)
          this._remainingStack = this._remainingStack.Pop();
        return !this._remainingStack.IsEmpty;
      }
    }


    #nullable disable
    private sealed class EnumeratorObject : IEnumerator<T>, IDisposable, IEnumerator
    {
      private readonly ImmutableStack<T> _originalStack;
      private ImmutableStack<T> _remainingStack;
      private bool _disposed;

      internal EnumeratorObject(ImmutableStack<T> stack)
      {
        Requires.NotNull<ImmutableStack<T>>(stack, nameof (stack));
        this._originalStack = stack;
      }

      public T Current
      {
        get
        {
          this.ThrowIfDisposed();
          return this._remainingStack != null && !this._remainingStack.IsEmpty ? this._remainingStack.Peek() : throw new InvalidOperationException();
        }
      }

      object IEnumerator.Current => (object) this.Current;

      public bool MoveNext()
      {
        this.ThrowIfDisposed();
        if (this._remainingStack == null)
          this._remainingStack = this._originalStack;
        else if (!this._remainingStack.IsEmpty)
          this._remainingStack = this._remainingStack.Pop();
        return !this._remainingStack.IsEmpty;
      }

      public void Reset()
      {
        this.ThrowIfDisposed();
        this._remainingStack = (ImmutableStack<T>) null;
      }

      public void Dispose() => this._disposed = true;

      private void ThrowIfDisposed()
      {
        if (!this._disposed)
          return;
        Requires.FailObjectDisposed<ImmutableStack<T>.EnumeratorObject>(this);
      }
    }
  }
}
