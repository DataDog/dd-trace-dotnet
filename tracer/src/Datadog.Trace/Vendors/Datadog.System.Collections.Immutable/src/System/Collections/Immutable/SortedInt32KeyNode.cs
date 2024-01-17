﻿// Decompiled with JetBrains decompiler
// Type: System.Collections.Immutable.SortedInt32KeyNode`1
// Assembly: System.Collections.Immutable, Version=7.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 5F9FF90F-0D16-4469-A104-76829D3705E2
// Assembly location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.dll
// XML documentation location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.xml

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using Datadog.System.Diagnostics.CodeAnalysis;


#nullable enable
namespace Datadog.System.Collections.Immutable
{
    [DebuggerDisplay("{_key} = {_value}")]
  internal sealed class SortedInt32KeyNode<TValue> : IBinaryTree
  {
    internal static readonly SortedInt32KeyNode<TValue> EmptyNode = new SortedInt32KeyNode<TValue>();
    private readonly int _key;

    #nullable disable
    private readonly TValue _value;
    private bool _frozen;
    private byte _height;
    private SortedInt32KeyNode<TValue> _left;
    private SortedInt32KeyNode<TValue> _right;

    private SortedInt32KeyNode() => this._frozen = true;

    private SortedInt32KeyNode(
      int key,
      TValue value,
      SortedInt32KeyNode<TValue> left,
      SortedInt32KeyNode<TValue> right,
      bool frozen = false)
    {
      Requires.NotNull<SortedInt32KeyNode<TValue>>(left, nameof (left));
      Requires.NotNull<SortedInt32KeyNode<TValue>>(right, nameof (right));
      this._key = key;
      this._value = value;
      this._left = left;
      this._right = right;
      this._frozen = frozen;
      this._height = checked ((byte) (1 + (int) Math.Max(left._height, right._height)));
    }

    public bool IsEmpty => this._left == null;

    public int Height => (int) this._height;


    #nullable enable
    public SortedInt32KeyNode<TValue>? Left => this._left;

    public SortedInt32KeyNode<TValue>? Right => this._right;

    IBinaryTree? IBinaryTree.Left => (IBinaryTree) this._left;

    IBinaryTree? IBinaryTree.Right => (IBinaryTree) this._right;

    int IBinaryTree.Count => throw new NotSupportedException();

    public KeyValuePair<int, TValue> Value => new KeyValuePair<int, TValue>(this._key, this._value);

    internal IEnumerable<TValue> Values
    {
      get
      {
        foreach (KeyValuePair<int, TValue> keyValuePair in this)
          yield return keyValuePair.Value;
      }
    }

    public SortedInt32KeyNode<
    #nullable disable
    TValue>.Enumerator GetEnumerator() => new SortedInt32KeyNode<TValue>.Enumerator(this);


    #nullable enable
    internal SortedInt32KeyNode<TValue> SetItem(
      int key,
      TValue value,
      IEqualityComparer<TValue> valueComparer,
      out bool replacedExistingValue,
      out bool mutated)
    {
      Requires.NotNull<IEqualityComparer<TValue>>(valueComparer, nameof (valueComparer));
      return this.SetOrAdd(key, value, valueComparer, true, out replacedExistingValue, out mutated);
    }

    internal SortedInt32KeyNode<TValue> Remove(int key, out bool mutated) => this.RemoveRecursive(key, out mutated);

    internal TValue? GetValueOrDefault(int key)
    {
      for (SortedInt32KeyNode<TValue> sortedInt32KeyNode = this; !sortedInt32KeyNode.IsEmpty; sortedInt32KeyNode = key <= sortedInt32KeyNode._key ? sortedInt32KeyNode._left : sortedInt32KeyNode._right)
      {
        if (key == sortedInt32KeyNode._key)
          return sortedInt32KeyNode._value;
      }
      return default (TValue);
    }

    internal bool TryGetValue(int key, [MaybeNullWhen(false)] out TValue value)
    {
      for (SortedInt32KeyNode<TValue> sortedInt32KeyNode = this; !sortedInt32KeyNode.IsEmpty; sortedInt32KeyNode = key <= sortedInt32KeyNode._key ? sortedInt32KeyNode._left : sortedInt32KeyNode._right)
      {
        if (key == sortedInt32KeyNode._key)
        {
          value = sortedInt32KeyNode._value;
          return true;
        }
      }
      value = default (TValue);
      return false;
    }

    internal void Freeze(Action<KeyValuePair<int, TValue>>? freezeAction = null)
    {
      if (this._frozen)
        return;
      if (freezeAction != null)
        freezeAction(new KeyValuePair<int, TValue>(this._key, this._value));
      this._left.Freeze(freezeAction);
      this._right.Freeze(freezeAction);
      this._frozen = true;
    }


    #nullable disable
    private static SortedInt32KeyNode<TValue> RotateLeft(SortedInt32KeyNode<TValue> tree)
    {
      Requires.NotNull<SortedInt32KeyNode<TValue>>(tree, nameof (tree));
      if (tree._right.IsEmpty)
        return tree;
      SortedInt32KeyNode<TValue> right = tree._right;
      return right.Mutate(tree.Mutate(right: right._left));
    }

    private static SortedInt32KeyNode<TValue> RotateRight(SortedInt32KeyNode<TValue> tree)
    {
      Requires.NotNull<SortedInt32KeyNode<TValue>>(tree, nameof (tree));
      if (tree._left.IsEmpty)
        return tree;
      SortedInt32KeyNode<TValue> left = tree._left;
      return left.Mutate(right: tree.Mutate(left._right));
    }

    private static SortedInt32KeyNode<TValue> DoubleLeft(SortedInt32KeyNode<TValue> tree)
    {
      Requires.NotNull<SortedInt32KeyNode<TValue>>(tree, nameof (tree));
      return tree._right.IsEmpty ? tree : SortedInt32KeyNode<TValue>.RotateLeft(tree.Mutate(right: SortedInt32KeyNode<TValue>.RotateRight(tree._right)));
    }

    private static SortedInt32KeyNode<TValue> DoubleRight(SortedInt32KeyNode<TValue> tree)
    {
      Requires.NotNull<SortedInt32KeyNode<TValue>>(tree, nameof (tree));
      return tree._left.IsEmpty ? tree : SortedInt32KeyNode<TValue>.RotateRight(tree.Mutate(SortedInt32KeyNode<TValue>.RotateLeft(tree._left)));
    }

    private static int Balance(SortedInt32KeyNode<TValue> tree)
    {
      Requires.NotNull<SortedInt32KeyNode<TValue>>(tree, nameof (tree));
      return (int) tree._right._height - (int) tree._left._height;
    }

    private static bool IsRightHeavy(SortedInt32KeyNode<TValue> tree)
    {
      Requires.NotNull<SortedInt32KeyNode<TValue>>(tree, nameof (tree));
      return SortedInt32KeyNode<TValue>.Balance(tree) >= 2;
    }

    private static bool IsLeftHeavy(SortedInt32KeyNode<TValue> tree)
    {
      Requires.NotNull<SortedInt32KeyNode<TValue>>(tree, nameof (tree));
      return SortedInt32KeyNode<TValue>.Balance(tree) <= -2;
    }

    private static SortedInt32KeyNode<TValue> MakeBalanced(SortedInt32KeyNode<TValue> tree)
    {
      Requires.NotNull<SortedInt32KeyNode<TValue>>(tree, nameof (tree));
      if (SortedInt32KeyNode<TValue>.IsRightHeavy(tree))
        return SortedInt32KeyNode<TValue>.Balance(tree._right) >= 0 ? SortedInt32KeyNode<TValue>.RotateLeft(tree) : SortedInt32KeyNode<TValue>.DoubleLeft(tree);
      if (!SortedInt32KeyNode<TValue>.IsLeftHeavy(tree))
        return tree;
      return SortedInt32KeyNode<TValue>.Balance(tree._left) <= 0 ? SortedInt32KeyNode<TValue>.RotateRight(tree) : SortedInt32KeyNode<TValue>.DoubleRight(tree);
    }

    private SortedInt32KeyNode<TValue> SetOrAdd(
      int key,
      TValue value,
      IEqualityComparer<TValue> valueComparer,
      bool overwriteExistingValue,
      out bool replacedExistingValue,
      out bool mutated)
    {
      replacedExistingValue = false;
      if (this.IsEmpty)
      {
        mutated = true;
        return new SortedInt32KeyNode<TValue>(key, value, this, this);
      }
      SortedInt32KeyNode<TValue> tree = this;
      if (key > this._key)
      {
        SortedInt32KeyNode<TValue> right = this._right.SetOrAdd(key, value, valueComparer, overwriteExistingValue, out replacedExistingValue, out mutated);
        if (mutated)
          tree = this.Mutate(right: right);
      }
      else if (key < this._key)
      {
        SortedInt32KeyNode<TValue> left = this._left.SetOrAdd(key, value, valueComparer, overwriteExistingValue, out replacedExistingValue, out mutated);
        if (mutated)
          tree = this.Mutate(left);
      }
      else
      {
        if (valueComparer.Equals(this._value, value))
        {
          mutated = false;
          return this;
        }
        if (!overwriteExistingValue)
          throw new ArgumentException();
        mutated = true;
        replacedExistingValue = true;
        tree = new SortedInt32KeyNode<TValue>(key, value, this._left, this._right);
      }
      return !mutated ? tree : SortedInt32KeyNode<TValue>.MakeBalanced(tree);
    }

    private SortedInt32KeyNode<TValue> RemoveRecursive(int key, out bool mutated)
    {
      if (this.IsEmpty)
      {
        mutated = false;
        return this;
      }
      SortedInt32KeyNode<TValue> tree = this;
      if (key == this._key)
      {
        mutated = true;
        if (this._right.IsEmpty && this._left.IsEmpty)
          tree = SortedInt32KeyNode<TValue>.EmptyNode;
        else if (this._right.IsEmpty && !this._left.IsEmpty)
          tree = this._left;
        else if (!this._right.IsEmpty && this._left.IsEmpty)
        {
          tree = this._right;
        }
        else
        {
          SortedInt32KeyNode<TValue> sortedInt32KeyNode = this._right;
          while (!sortedInt32KeyNode._left.IsEmpty)
            sortedInt32KeyNode = sortedInt32KeyNode._left;
          SortedInt32KeyNode<TValue> right = this._right.Remove(sortedInt32KeyNode._key, out bool _);
          tree = sortedInt32KeyNode.Mutate(this._left, right);
        }
      }
      else if (key < this._key)
      {
        SortedInt32KeyNode<TValue> left = this._left.Remove(key, out mutated);
        if (mutated)
          tree = this.Mutate(left);
      }
      else
      {
        SortedInt32KeyNode<TValue> right = this._right.Remove(key, out mutated);
        if (mutated)
          tree = this.Mutate(right: right);
      }
      return !tree.IsEmpty ? SortedInt32KeyNode<TValue>.MakeBalanced(tree) : tree;
    }

    private SortedInt32KeyNode<TValue> Mutate(
      SortedInt32KeyNode<TValue> left = null,
      SortedInt32KeyNode<TValue> right = null)
    {
      if (this._frozen)
        return new SortedInt32KeyNode<TValue>(this._key, this._value, left ?? this._left, right ?? this._right);
      if (left != null)
        this._left = left;
      if (right != null)
        this._right = right;
      this._height = checked ((byte) (1 + (int) Math.Max(this._left._height, this._right._height)));
      return this;
    }


    #nullable enable
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public struct Enumerator : 
      IEnumerator<KeyValuePair<int, TValue>>,
      IDisposable,
      IEnumerator,
      ISecurePooledObjectUser
    {
      private readonly int _poolUserId;

      #nullable disable
      private SortedInt32KeyNode<TValue> _root;
      private SecurePooledObject<Stack<RefAsValueType<SortedInt32KeyNode<TValue>>>> _stack;
      private SortedInt32KeyNode<TValue> _current;


      #nullable enable
      internal Enumerator(SortedInt32KeyNode<TValue> root)
      {
        Requires.NotNull<SortedInt32KeyNode<TValue>>(root, nameof (root));
        this._root = root;
        this._current = (SortedInt32KeyNode<TValue>) null;
        this._poolUserId = SecureObjectPool.NewId();
        this._stack = (SecurePooledObject<Stack<RefAsValueType<SortedInt32KeyNode<TValue>>>>) null;
        if (this._root.IsEmpty)
          return;
        if (!SecureObjectPool<Stack<RefAsValueType<SortedInt32KeyNode<TValue>>>, SortedInt32KeyNode<TValue>.Enumerator>.TryTake(this, out this._stack))
          this._stack = SecureObjectPool<Stack<RefAsValueType<SortedInt32KeyNode<TValue>>>, SortedInt32KeyNode<TValue>.Enumerator>.PrepNew(this, new Stack<RefAsValueType<SortedInt32KeyNode<TValue>>>(root.Height));
        this.PushLeft(this._root);
      }

      public KeyValuePair<int, TValue> Current
      {
        get
        {
          this.ThrowIfDisposed();
          if (this._current != null)
            return this._current.Value;
          throw new InvalidOperationException();
        }
      }

      int ISecurePooledObjectUser.PoolUserId => this._poolUserId;

      object IEnumerator.Current => (object) this.Current;

      public void Dispose()
      {
        this._root = (SortedInt32KeyNode<TValue>) null;
        this._current = (SortedInt32KeyNode<TValue>) null;
        Stack<RefAsValueType<SortedInt32KeyNode<TValue>>> stack;
        if (this._stack != null && this._stack.TryUse<SortedInt32KeyNode<TValue>.Enumerator>(ref this, out stack))
        {
          stack.ClearFastWhenEmpty<RefAsValueType<SortedInt32KeyNode<TValue>>>();
          SecureObjectPool<Stack<RefAsValueType<SortedInt32KeyNode<TValue>>>, SortedInt32KeyNode<TValue>.Enumerator>.TryAdd(this, this._stack);
        }
        this._stack = (SecurePooledObject<Stack<RefAsValueType<SortedInt32KeyNode<TValue>>>>) null;
      }

      public bool MoveNext()
      {
        this.ThrowIfDisposed();
        if (this._stack != null)
        {
          Stack<RefAsValueType<SortedInt32KeyNode<TValue>>> refAsValueTypeStack = this._stack.Use<SortedInt32KeyNode<TValue>.Enumerator>(ref this);
          if (refAsValueTypeStack.Count > 0)
          {
            SortedInt32KeyNode<TValue> sortedInt32KeyNode = refAsValueTypeStack.Pop().Value;
            this._current = sortedInt32KeyNode;
            this.PushLeft(sortedInt32KeyNode.Right);
            return true;
          }
        }
        this._current = (SortedInt32KeyNode<TValue>) null;
        return false;
      }

      public void Reset()
      {
        this.ThrowIfDisposed();
        this._current = (SortedInt32KeyNode<TValue>) null;
        if (this._stack == null)
          return;
        this._stack.Use<SortedInt32KeyNode<TValue>.Enumerator>(ref this).ClearFastWhenEmpty<RefAsValueType<SortedInt32KeyNode<TValue>>>();
        this.PushLeft(this._root);
      }

      internal void ThrowIfDisposed()
      {
        if (this._root != null && (this._stack == null || this._stack.IsOwned<SortedInt32KeyNode<TValue>.Enumerator>(ref this)))
          return;
        Requires.FailObjectDisposed<SortedInt32KeyNode<TValue>.Enumerator>(this);
      }


      #nullable disable
      private void PushLeft(SortedInt32KeyNode<TValue> node)
      {
        Requires.NotNull<SortedInt32KeyNode<TValue>>(node, nameof (node));
        Stack<RefAsValueType<SortedInt32KeyNode<TValue>>> refAsValueTypeStack = this._stack.Use<SortedInt32KeyNode<TValue>.Enumerator>(ref this);
        for (; !node.IsEmpty; node = node.Left)
          refAsValueTypeStack.Push(new RefAsValueType<SortedInt32KeyNode<TValue>>(node));
      }
    }
  }
}
