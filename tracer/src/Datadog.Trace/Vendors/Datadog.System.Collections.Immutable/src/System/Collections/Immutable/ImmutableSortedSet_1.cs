﻿// Decompiled with JetBrains decompiler
// Type: System.Collections.Immutable.ImmutableSortedSet`1
// Assembly: System.Collections.Immutable, Version=7.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 5F9FF90F-0D16-4469-A104-76829D3705E2
// Assembly location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.dll
// XML documentation location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.xml

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using Datadog.System.Collections.Generic;
using Datadog.System.Diagnostics.CodeAnalysis;
using Datadog.System.Linq;


#nullable enable
namespace Datadog.System.Collections.Immutable
{
    /// <summary>Represents an immutable sorted set implementation.
    /// 
    /// NuGet package: System.Collections.Immutable (about immutable collections and how to install)</summary>
    /// <typeparam name="T">The type of elements in the set.</typeparam>
    [DebuggerDisplay("Count = {Count}")]
  [DebuggerTypeProxy(typeof (ImmutableEnumerableDebuggerProxy<>))]
  public sealed class ImmutableSortedSet<T> : 
    IImmutableSet<T>,
    IReadOnlyCollection<T>,
    IEnumerable<T>,
    IEnumerable,
    ISortKeyCollection<T>,
    IReadOnlyList<T>,
    IList<T>,
    ICollection<T>,
    ISet<T>,
    IList,
    ICollection,
    IStrongEnumerable<T, ImmutableSortedSet<T>.Enumerator>
  {
    private const float RefillOverIncrementalThreshold = 0.15f;
    /// <summary>Gets an empty immutable sorted set.</summary>
    public static readonly ImmutableSortedSet<T> Empty = new ImmutableSortedSet<T>();

    #nullable disable
    private readonly ImmutableSortedSet<T>.Node _root;
    private readonly IComparer<T> _comparer;


    #nullable enable
    internal ImmutableSortedSet(IComparer<T>? comparer = null)
    {
      this._root = ImmutableSortedSet<T>.Node.EmptyNode;
      this._comparer = comparer ?? (IComparer<T>) Comparer<T>.Default;
    }


    #nullable disable
    private ImmutableSortedSet(ImmutableSortedSet<T>.Node root, IComparer<T> comparer)
    {
      Requires.NotNull<ImmutableSortedSet<T>.Node>(root, nameof (root));
      Requires.NotNull<IComparer<T>>(comparer, nameof (comparer));
      root.Freeze();
      this._root = root;
      this._comparer = comparer;
    }


    #nullable enable
    /// <summary>Removes all elements from the immutable sorted set.</summary>
    /// <returns>An empty set with the elements removed.</returns>
    public ImmutableSortedSet<T> Clear() => !this._root.IsEmpty ? ImmutableSortedSet<T>.Empty.WithComparer(this._comparer) : this;

    /// <summary>Gets the maximum value in the immutable sorted set, as defined by the comparer.</summary>
    /// <returns>The maximum value in the set.</returns>
    public T? Max => this._root.Max;

    /// <summary>Gets the minimum value in the immutable sorted set, as defined by the comparer.</summary>
    /// <returns>The minimum value in the set.</returns>
    public T? Min => this._root.Min;

    /// <summary>Gets a value that indicates whether this immutable sorted set is empty.</summary>
    /// <returns>
    /// <see langword="true" /> if this set is empty; otherwise, <see langword="false" />.</returns>
    public bool IsEmpty => this._root.IsEmpty;

    /// <summary>Gets the number of elements in the immutable sorted set.</summary>
    /// <returns>The number of elements in the immutable sorted set.</returns>
    public int Count => this._root.Count;

    /// <summary>Gets the comparer used to sort keys in the immutable sorted set.</summary>
    /// <returns>The comparer used to sort keys.</returns>
    public IComparer<T> KeyComparer => this._comparer;

    internal IBinaryTree Root => (IBinaryTree) this._root;

    /// <summary>Gets the element of the immutable sorted set at the given index.</summary>
    /// <param name="index">The index of the element to retrieve from the sorted set.</param>
    /// <returns>The element at the given index.</returns>
    public T this[int index] => this._root.ItemRef(index);

    /// <summary>Gets a read-only reference of the element of the set at the given <paramref name="index" />.</summary>
    /// <param name="index">The 0-based index of the element in the set to return.</param>
    /// <returns>A read-only reference of the element at the given position.</returns>
    public ref readonly T ItemRef(int index) => ref this._root.ItemRef(index);

    /// <summary>Creates a collection that has the same contents as this immutable sorted set that can be efficiently manipulated by using standard mutable interfaces.</summary>
    /// <returns>The sorted set builder.</returns>
    public ImmutableSortedSet<
    #nullable disable
    T>.Builder ToBuilder() => new ImmutableSortedSet<T>.Builder(this);


    #nullable enable
    /// <summary>Adds the specified value to this immutable sorted set.</summary>
    /// <param name="value">The value to add.</param>
    /// <returns>A new set with the element added, or this set if the element is already in this set.</returns>
    public ImmutableSortedSet<T> Add(T value) => this.Wrap(this._root.Add(value, this._comparer, out bool _));

    /// <summary>Removes the specified value from this immutable sorted set.</summary>
    /// <param name="value">The element to remove.</param>
    /// <returns>A new immutable sorted set with the element removed, or this set if the element was not found in the set.</returns>
    public ImmutableSortedSet<T> Remove(T value) => this.Wrap(this._root.Remove(value, this._comparer, out bool _));

    /// <summary>Searches the set for a given value and returns the equal value it finds, if any.</summary>
    /// <param name="equalValue">The value to search for.</param>
    /// <param name="actualValue">The value from the set that the search found, or the original value if the search yielded no match.</param>
    /// <returns>A value indicating whether the search was successful.</returns>
    public bool TryGetValue(T equalValue, out T actualValue)
    {
      ImmutableSortedSet<T>.Node node = this._root.Search(equalValue, this._comparer);
      if (node.IsEmpty)
      {
        actualValue = equalValue;
        return false;
      }
      actualValue = node.Key;
      return true;
    }

    /// <summary>Creates an immutable sorted set that contains elements that exist both in this set and in the specified set.</summary>
    /// <param name="other">The set to intersect with this one.</param>
    /// <returns>A new immutable sorted set that contains any elements that exist in both sets.</returns>
    public ImmutableSortedSet<T> Intersect(IEnumerable<T> other)
    {
      Requires.NotNull<IEnumerable<T>>(other, nameof (other));
      ImmutableSortedSet<T> immutableSortedSet = this.Clear();
      foreach (T obj in other.GetEnumerableDisposable<T, ImmutableSortedSet<T>.Enumerator>())
      {
        if (this.Contains(obj))
          immutableSortedSet = immutableSortedSet.Add(obj);
      }
      return immutableSortedSet;
    }

    /// <summary>Removes a specified set of items from this immutable sorted set.</summary>
    /// <param name="other">The items to remove from this set.</param>
    /// <returns>A new set with the items removed; or the original set if none of the items were in the set.</returns>
    public ImmutableSortedSet<T> Except(IEnumerable<T> other)
    {
      Requires.NotNull<IEnumerable<T>>(other, nameof (other));
      ImmutableSortedSet<T>.Node root = this._root;
      foreach (T key in other.GetEnumerableDisposable<T, ImmutableSortedSet<T>.Enumerator>())
        root = root.Remove(key, this._comparer, out bool _);
      return this.Wrap(root);
    }

    /// <summary>Creates an immutable sorted set that contains elements that exist either in this set or in a given sequence, but not both.</summary>
    /// <param name="other">The other sequence of items.</param>
    /// <returns>The new immutable sorted set.</returns>
    public ImmutableSortedSet<T> SymmetricExcept(IEnumerable<T> other)
    {
      Requires.NotNull<IEnumerable<T>>(other, nameof (other));
      ImmutableSortedSet<T> range = ImmutableSortedSet.CreateRange<T>(this._comparer, other);
      ImmutableSortedSet<T> immutableSortedSet = this.Clear();
      foreach (T obj in this)
      {
        if (!range.Contains(obj))
          immutableSortedSet = immutableSortedSet.Add(obj);
      }
      foreach (T obj in range)
      {
        if (!this.Contains(obj))
          immutableSortedSet = immutableSortedSet.Add(obj);
      }
      return immutableSortedSet;
    }

    /// <summary>Adds a given set of items to this immutable sorted set.</summary>
    /// <param name="other">The items to add.</param>
    /// <returns>The new set with the items added; or the original set if all the items were already in the set.</returns>
    public ImmutableSortedSet<T> Union(IEnumerable<T> other)
    {
      Requires.NotNull<IEnumerable<T>>(other, nameof (other));
      ImmutableSortedSet<T> other1;
      if (ImmutableSortedSet<T>.TryCastToImmutableSortedSet(other, out other1) && other1.KeyComparer == this.KeyComparer)
      {
        if (other1.IsEmpty)
          return this;
        if (this.IsEmpty)
          return other1;
        if (other1.Count > this.Count)
          return other1.Union((IEnumerable<T>) this);
      }
      int count;
      return this.IsEmpty || other.TryGetCount<T>(out count) && (double) (this.Count + count) * 0.15000000596046448 > (double) this.Count ? this.LeafToRootRefill(other) : this.UnionIncremental(other);
    }

    /// <summary>Returns the immutable sorted set that has the specified key comparer.</summary>
    /// <param name="comparer">The comparer to check for.</param>
    /// <returns>The immutable sorted set that has the specified key comparer.</returns>
    public ImmutableSortedSet<T> WithComparer(IComparer<T>? comparer)
    {
      if (comparer == null)
        comparer = (IComparer<T>) Comparer<T>.Default;
      return comparer == this._comparer ? this : new ImmutableSortedSet<T>(ImmutableSortedSet<T>.Node.EmptyNode, comparer).Union((IEnumerable<T>) this);
    }

    /// <summary>Determines whether the current immutable sorted set and the specified collection contain the same elements.</summary>
    /// <param name="other">The collection to compare to the current set.</param>
    /// <returns>
    /// <see langword="true" /> if the sets are equal; otherwise, <see langword="false" />.</returns>
    public bool SetEquals(IEnumerable<T> other)
    {
      Requires.NotNull<IEnumerable<T>>(other, nameof (other));
      if (this == other)
        return true;
      SortedSet<T> sortedSet = new SortedSet<T>(other, this.KeyComparer);
      if (this.Count != sortedSet.Count)
        return false;
      int num = 0;
      foreach (T obj in sortedSet)
      {
        if (!this.Contains(obj))
          return false;
        ++num;
      }
      return num == this.Count;
    }

    /// <summary>Determines whether the current immutable sorted set is a proper (strict) subset of the specified collection.</summary>
    /// <param name="other">The collection to compare to the current set.</param>
    /// <returns>
    /// <see langword="true" /> if the current set is a proper subset of <paramref name="other" />; otherwise, <see langword="false" />.</returns>
    public bool IsProperSubsetOf(IEnumerable<T> other)
    {
      Requires.NotNull<IEnumerable<T>>(other, nameof (other));
      if (this.IsEmpty)
        return other.Any<T>();
      SortedSet<T> sortedSet = new SortedSet<T>(other, this.KeyComparer);
      if (this.Count >= sortedSet.Count)
        return false;
      int num = 0;
      bool flag = false;
      foreach (T obj in sortedSet)
      {
        if (this.Contains(obj))
          ++num;
        else
          flag = true;
        if (num == this.Count & flag)
          return true;
      }
      return false;
    }

    /// <summary>Determines whether the current immutable sorted set is a proper superset of a specified collection.</summary>
    /// <param name="other">The collection to compare to the current set.</param>
    /// <returns>
    /// <see langword="true" /> if the current set is a proper superset of <paramref name="other" />; otherwise, <see langword="false" />.</returns>
    public bool IsProperSupersetOf(IEnumerable<T> other)
    {
      Requires.NotNull<IEnumerable<T>>(other, nameof (other));
      if (this.IsEmpty)
        return false;
      int num = 0;
      foreach (T obj in other.GetEnumerableDisposable<T, ImmutableSortedSet<T>.Enumerator>())
      {
        ++num;
        if (!this.Contains(obj))
          return false;
      }
      return this.Count > num;
    }

    /// <summary>Determines whether the current immutable sorted set is a subset of a specified collection.</summary>
    /// <param name="other">The collection to compare to the current set.</param>
    /// <returns>
    /// <see langword="true" /> if the current set is a subset of <paramref name="other" />; otherwise, <see langword="false" />.</returns>
    public bool IsSubsetOf(IEnumerable<T> other)
    {
      Requires.NotNull<IEnumerable<T>>(other, nameof (other));
      if (this.IsEmpty)
        return true;
      SortedSet<T> sortedSet = new SortedSet<T>(other, this.KeyComparer);
      int num = 0;
      foreach (T obj in sortedSet)
      {
        if (this.Contains(obj))
          ++num;
      }
      return num == this.Count;
    }

    /// <summary>Determines whether the current immutable sorted set is a superset of a specified collection.</summary>
    /// <param name="other">The collection to compare to the current set.</param>
    /// <returns>
    /// <see langword="true" /> if the current set is a superset of <paramref name="other" />; otherwise, <see langword="false" />.</returns>
    public bool IsSupersetOf(IEnumerable<T> other)
    {
      Requires.NotNull<IEnumerable<T>>(other, nameof (other));
      foreach (T obj in other.GetEnumerableDisposable<T, ImmutableSortedSet<T>.Enumerator>())
      {
        if (!this.Contains(obj))
          return false;
      }
      return true;
    }

    /// <summary>Determines whether the current immutable sorted set and a specified collection share common elements.</summary>
    /// <param name="other">The collection to compare to the current set.</param>
    /// <returns>
    /// <see langword="true" /> if the current set and <paramref name="other" /> share at least one common element; otherwise, <see langword="false" />.</returns>
    public bool Overlaps(IEnumerable<T> other)
    {
      Requires.NotNull<IEnumerable<T>>(other, nameof (other));
      if (this.IsEmpty)
        return false;
      foreach (T obj in other.GetEnumerableDisposable<T, ImmutableSortedSet<T>.Enumerator>())
      {
        if (this.Contains(obj))
          return true;
      }
      return false;
    }

    /// <summary>Returns an <see cref="T:System.Collections.Generic.IEnumerable`1" /> that iterates over this immutable sorted set in reverse order.</summary>
    /// <returns>An enumerator that iterates over the immutable sorted set in reverse order.</returns>
    public IEnumerable<T> Reverse() => (IEnumerable<T>) new ImmutableSortedSet<T>.ReverseEnumerable(this._root);

    /// <summary>Gets the position within this immutable sorted set that the specified value appears in.</summary>
    /// <param name="item">The value whose position is being sought.</param>
    /// <returns>The index of the specified <paramref name="item" /> in the sorted set, if <paramref name="item" /> is found. If <paramref name="item" /> is not found and is less than one or more elements in this set, this method returns a negative number that is the bitwise complement of the index of the first element that is larger than value. If <paramref name="item" /> is not found and is greater than any of the elements in the set, this method returns a negative number that is the bitwise complement of the index of the last element plus 1.</returns>
    public int IndexOf(T item) => this._root.IndexOf(item, this._comparer);

    /// <summary>Determines whether this immutable sorted set contains the specified value.</summary>
    /// <param name="value">The value to check for.</param>
    /// <returns>
    /// <see langword="true" /> if the set contains the specified value; otherwise, <see langword="false" />.</returns>
    public bool Contains(T value) => this._root.Contains(value, this._comparer);


    #nullable disable
    /// <summary>Retrieves an empty immutable set that has the same sorting and ordering semantics as this instance.</summary>
    /// <returns>An empty set that has the same sorting and ordering semantics as this instance.</returns>
    IImmutableSet<T> IImmutableSet<T>.Clear() => (IImmutableSet<T>) this.Clear();

    /// <summary>Adds the specified element to this immutable set.</summary>
    /// <param name="value">The element to add.</param>
    /// <returns>A new set with the element added, or this set if the element is already in the set.</returns>
    IImmutableSet<T> IImmutableSet<T>.Add(T value) => (IImmutableSet<T>) this.Add(value);

    /// <summary>Removes the specified element from this immutable set.</summary>
    /// <param name="value">The element to remove.</param>
    /// <returns>A new set with the specified element removed, or the current set if the element cannot be found in the set.</returns>
    IImmutableSet<T> IImmutableSet<T>.Remove(T value) => (IImmutableSet<T>) this.Remove(value);

    /// <summary>Creates an immutable set that contains elements that exist in both this set and the specified set.</summary>
    /// <param name="other">The collection to compare to the current set.</param>
    /// <returns>A new immutable set that contains any elements that exist in both sets.</returns>
    IImmutableSet<T> IImmutableSet<T>.Intersect(IEnumerable<T> other) => (IImmutableSet<T>) this.Intersect(other);

    /// <summary>Removes the elements in the specified collection from the current immutable set.</summary>
    /// <param name="other">The items to remove from this set.</param>
    /// <returns>The new set with the items removed; or the original set if none of the items were in the set.</returns>
    IImmutableSet<T> IImmutableSet<T>.Except(IEnumerable<T> other) => (IImmutableSet<T>) this.Except(other);

    /// <summary>Creates an immutable set that contains only elements that are present either in the current set or in the specified collection, but not both.</summary>
    /// <param name="other">The collection to compare to the current set.</param>
    /// <returns>A new set that contains the elements that are present only in the current set or in the specified collection, but not both.</returns>
    IImmutableSet<T> IImmutableSet<T>.SymmetricExcept(IEnumerable<T> other) => (IImmutableSet<T>) this.SymmetricExcept(other);

    /// <summary>Creates a new immutable set that contains all elements that are present in either the current set or in the specified collection.</summary>
    /// <param name="other">The collection to add elements from.</param>
    /// <returns>A new immutable set with the items added; or the original set if all the items were already in the set.</returns>
    IImmutableSet<T> IImmutableSet<T>.Union(IEnumerable<T> other) => (IImmutableSet<T>) this.Union(other);

    /// <summary>Adds an element to the current set and returns a value to indicate if the element was successfully added.</summary>
    /// <param name="item">The element to add to the set.</param>
    /// <returns>
    /// <see langword="true" /> if the element is added to the set; <see langword="false" /> if the element is already in the set.</returns>
    bool ISet<T>.Add(T item) => throw new NotSupportedException();

    /// <summary>Removes all elements in the specified collection from the current set.</summary>
    /// <param name="other">The collection of items to remove from the set.</param>
    void ISet<T>.ExceptWith(IEnumerable<T> other) => throw new NotSupportedException();

    /// <summary>Modifies the current set so that it contains only elements that are also in a specified collection.</summary>
    /// <param name="other">The collection to compare to the current set.</param>
    void ISet<T>.IntersectWith(IEnumerable<T> other) => throw new NotSupportedException();

    /// <summary>Modifies the current set so that it contains only elements that are present either in the current set or in the specified collection, but not both.</summary>
    /// <param name="other">The collection to compare to the current set.</param>
    void ISet<T>.SymmetricExceptWith(IEnumerable<T> other) => throw new NotSupportedException();

    /// <summary>Modifies the current set so that it contains all elements that are present in either the current set or the specified collection.</summary>
    /// <param name="other">The collection to compare to the current set.</param>
    void ISet<T>.UnionWith(IEnumerable<T> other) => throw new NotSupportedException();

    /// <summary>Returns true, since immutable collections are always read-only. See the <see cref="T:System.Collections.Generic.ICollection`1" /> interface.</summary>
    /// <returns>A boolean value indicating whether the collection is read-only.</returns>
    bool ICollection<T>.IsReadOnly => true;

    /// <summary>Copies the elements of the collection to an array, starting at a particular array index.</summary>
    /// <param name="array">The one-dimensional array that is the destination of the elements copied from collection. The array must have zero-based indexing.</param>
    /// <param name="arrayIndex">The zero-based index in <paramref name="array" /> at which copying begins.</param>
    void ICollection<T>.CopyTo(T[] array, int arrayIndex) => this._root.CopyTo(array, arrayIndex);

    /// <summary>Adds the specified value to the collection.</summary>
    /// <param name="item">The value to add.</param>
    void ICollection<T>.Add(T item) => throw new NotSupportedException();

    /// <summary>Removes all the items from the collection.</summary>
    void ICollection<T>.Clear() => throw new NotSupportedException();

    /// <summary>Removes the first occurrence of a specific object from the collection.</summary>
    /// <param name="item">The object to remove from the collection.</param>
    /// <returns>
    /// <see langword="true" /> if <paramref name="item" /> was successfully removed from the collection; otherwise, <see langword="false" />.</returns>
    bool ICollection<T>.Remove(T item) => throw new NotSupportedException();


    #nullable enable
    /// <summary>See the <see cref="T:System.Collections.Generic.IList`1" /> interface.</summary>
    /// <param name="index">The zero-based index of the item to access.</param>
    /// <returns>The element stored at the specified index.</returns>
    T IList<
    #nullable disable
    T>.this[int index]
    {
      get => this[index];
      set => throw new NotSupportedException();
    }

    /// <summary>Inserts an item in the set at the specified index.</summary>
    /// <param name="index">The zero-based index at which <paramref name="item" /> should be inserted.</param>
    /// <param name="item">The object to insert into the set.</param>
    void IList<T>.Insert(int index, T item) => throw new NotSupportedException();

    /// <summary>Removes the item at the specified index.</summary>
    /// <param name="index">The zero-based index of the item to remove.</param>
    void IList<T>.RemoveAt(int index) => throw new NotSupportedException();

    /// <summary>Gets a value that indicates whether the <see cref="T:System.Collections.IList" /> has a fixed size.</summary>
    /// <returns>
    /// <see langword="true" /> if the <see cref="T:System.Collections.IList" /> has a fixed size; otherwise, <see langword="false" />.</returns>
    bool IList.IsFixedSize => true;

    /// <summary>Gets a value that indicates whether the <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only.</summary>
    /// <returns>
    /// <see langword="true" /> if the <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only; otherwise, <see langword="false" />.</returns>
    bool IList.IsReadOnly => true;


    #nullable enable
    /// <summary>See <see cref="T:System.Collections.ICollection" />.</summary>
    /// <returns>Object used for synchronizing access to the collection.</returns>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    object ICollection.SyncRoot => (object) this;

    /// <summary>Returns true, since immutable collections are always thread-safe. See the <see cref="T:System.Collections.ICollection" /> interface.</summary>
    /// <returns>A boolean value indicating whether the collection is thread-safe.</returns>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    bool ICollection.IsSynchronized => true;


    #nullable disable
    /// <summary>Adds an item to the set.</summary>
    /// <param name="value">The object to add to the set.</param>
    /// <exception cref="T:System.NotSupportedException">The set is read-only or has a fixed size.</exception>
    /// <returns>The position into which the new element was inserted, or -1 to indicate that the item was not inserted into the collection.</returns>
    int IList.Add(object value) => throw new NotSupportedException();

    /// <summary>Removes all items from the set.</summary>
    /// <exception cref="T:System.NotSupportedException">Thrown in all cases.</exception>
    void IList.Clear() => throw new NotSupportedException();

    /// <summary>Determines whether the set contains a specific value.</summary>
    /// <param name="value">The object to locate in the set.</param>
    /// <returns>
    /// <see langword="true" /> if the object is found in the set; otherwise, <see langword="false" />.</returns>
    bool IList.Contains(object value) => this.Contains((T) value);

    /// <summary>Determines the index of a specific item in the set.</summary>
    /// <param name="value">The object to locate in the set.</param>
    /// <returns>The index of <paramref name="value" /> if found in the list; otherwise, -1.</returns>
    int IList.IndexOf(object value) => this.IndexOf((T) value);

    /// <summary>Inserts an item into the set at the specified index.</summary>
    /// <param name="index">The zero-based index at which <paramref name="value" /> should be inserted.</param>
    /// <param name="value">The object to insert into the set.</param>
    /// <exception cref="T:System.NotSupportedException">The set is read-only or has a fixed size.</exception>
    void IList.Insert(int index, object value) => throw new NotSupportedException();

    /// <summary>Removes the first occurrence of a specific object from the set.</summary>
    /// <param name="value">The object to remove from the set.</param>
    /// <exception cref="T:System.NotSupportedException">The set is read-only or has a fixed size.</exception>
    void IList.Remove(object value) => throw new NotSupportedException();

    /// <summary>Removes the item at the specified index of the set.</summary>
    /// <param name="index">The zero-based index of the item to remove.</param>
    /// <exception cref="T:System.NotSupportedException">The set is read-only or has a fixed size.</exception>
    void IList.RemoveAt(int index) => throw new NotSupportedException();


    #nullable enable
    /// <summary>Gets or sets the <see cref="T:System.Object" /> at the specified index.</summary>
    /// <param name="index">The index.</param>
    /// <exception cref="T:System.NotSupportedException" />
    /// <returns>The <see cref="T:System.Object" />.</returns>
    object? IList.this[int index]
    {
      get => (object) this[index];
      set => throw new NotSupportedException();
    }


    #nullable disable
    /// <summary>Copies the elements of the set to an array, starting at a particular array index.</summary>
    /// <param name="array">The one-dimensional array that is the destination of the elements copied from the set. The array must have zero-based indexing.</param>
    /// <param name="index">The zero-based index in <paramref name="array" /> at which copying begins.</param>
    void ICollection.CopyTo(Array array, int index) => this._root.CopyTo(array, index);

    /// <summary>Returns an enumerator that iterates through the collection.</summary>
    /// <returns>An enumerator that can be used to iterate through the collection.</returns>
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => !this.IsEmpty ? (IEnumerator<T>) this.GetEnumerator() : Enumerable.Empty<T>().GetEnumerator();

    /// <summary>Returns an enumerator that iterates through a collection.</summary>
    /// <returns>An enumerator object that can be used to iterate through the collection.</returns>
    IEnumerator IEnumerable.GetEnumerator() => (IEnumerator) this.GetEnumerator();


    #nullable enable
    /// <summary>Returns an enumerator that iterates through the immutable sorted set.</summary>
    /// <returns>An enumerator that can be used to iterate through the set.</returns>
    public ImmutableSortedSet<
    #nullable disable
    T>.Enumerator GetEnumerator() => this._root.GetEnumerator();

    private static bool TryCastToImmutableSortedSet(
      IEnumerable<T> sequence,
      [NotNullWhen(true)] out ImmutableSortedSet<T> other)
    {
      other = sequence as ImmutableSortedSet<T>;
      if (other != null)
        return true;
      if (!(sequence is ImmutableSortedSet<T>.Builder builder))
        return false;
      other = builder.ToImmutable();
      return true;
    }

    private static ImmutableSortedSet<T> Wrap(
      ImmutableSortedSet<T>.Node root,
      IComparer<T> comparer)
    {
      return !root.IsEmpty ? new ImmutableSortedSet<T>(root, comparer) : ImmutableSortedSet<T>.Empty.WithComparer(comparer);
    }

    private ImmutableSortedSet<T> UnionIncremental(IEnumerable<T> items)
    {
      Requires.NotNull<IEnumerable<T>>(items, nameof (items));
      ImmutableSortedSet<T>.Node root = this._root;
      foreach (T key in items.GetEnumerableDisposable<T, ImmutableSortedSet<T>.Enumerator>())
        root = root.Add(key, this._comparer, out bool _);
      return this.Wrap(root);
    }

    private ImmutableSortedSet<T> Wrap(ImmutableSortedSet<T>.Node root)
    {
      if (root == this._root)
        return this;
      return !root.IsEmpty ? new ImmutableSortedSet<T>(root, this._comparer) : this.Clear();
    }

    private ImmutableSortedSet<T> LeafToRootRefill(IEnumerable<T> addedItems)
    {
      Requires.NotNull<IEnumerable<T>>(addedItems, nameof (addedItems));
      List<T> sequence;
      if (this.IsEmpty)
      {
        int count;
        if (addedItems.TryGetCount<T>(out count) && count == 0)
          return this;
        sequence = new List<T>(addedItems);
        if (sequence.Count == 0)
          return this;
      }
      else
      {
        sequence = new List<T>((IEnumerable<T>) this);
        sequence.AddRange(addedItems);
      }
      IComparer<T> keyComparer = this.KeyComparer;
      sequence.Sort(keyComparer);
      int index1 = 1;
      for (int index2 = 1; index2 < sequence.Count; ++index2)
      {
        if (keyComparer.Compare(sequence[index2], sequence[index2 - 1]) != 0)
          sequence[index1++] = sequence[index2];
      }
      sequence.RemoveRange(index1, sequence.Count - index1);
      return this.Wrap(ImmutableSortedSet<T>.Node.NodeTreeFromList(sequence.AsOrderedCollection<T>(), 0, sequence.Count));
    }


    #nullable enable
    /// <summary>Represents a sorted set that enables changes with little or no memory allocations, and efficiently manipulates or builds immutable sorted sets.
    /// 
    /// NuGet package: System.Collections.Immutable (about immutable collections and how to install)</summary>
    /// <typeparam name="T" />
    [DebuggerDisplay("Count = {Count}")]
    [DebuggerTypeProxy(typeof (ImmutableSortedSetBuilderDebuggerProxy<>))]
    public sealed class Builder : 
      ISortKeyCollection<T>,
      IReadOnlyCollection<T>,
      IEnumerable<T>,
      IEnumerable,
      ISet<T>,
      ICollection<T>,
      ICollection
    {

      #nullable disable
      private ImmutableSortedSet<T>.Node _root = ImmutableSortedSet<T>.Node.EmptyNode;
      private IComparer<T> _comparer = (IComparer<T>) Comparer<T>.Default;
      private ImmutableSortedSet<T> _immutable;
      private int _version;
      private object _syncRoot;


      #nullable enable
      internal Builder(ImmutableSortedSet<T> set)
      {
        Requires.NotNull<ImmutableSortedSet<T>>(set, nameof (set));
        this._root = set._root;
        this._comparer = set.KeyComparer;
        this._immutable = set;
      }

      /// <summary>Gets the number of elements in the immutable sorted set.</summary>
      /// <returns>The number of elements in this set.</returns>
      public int Count => this.Root.Count;

      /// <summary>Gets a value that indicates whether this instance is read-only.</summary>
      /// <returns>Always <see langword="false" />.</returns>
      bool ICollection<
      #nullable disable
      T>.IsReadOnly => false;


      #nullable enable
      /// <summary>Gets the element of the set at the given index.</summary>
      /// <param name="index">The 0-based index of the element in the set to return.</param>
      /// <returns>The element at the given position.</returns>
      public T this[int index] => this._root.ItemRef(index);

      /// <summary>Gets a read-only reference to the element of the set at the given <paramref name="index" />.</summary>
      /// <param name="index">The 0-based index of the element in the set to return.</param>
      /// <returns>A read-only reference to the element at the given position.</returns>
      public ref readonly T ItemRef(int index) => ref this._root.ItemRef(index);

      /// <summary>Gets the maximum value in the immutable sorted set, as defined by the comparer.</summary>
      /// <returns>The maximum value in the set.</returns>
      public T? Max => this._root.Max;

      /// <summary>Gets the minimum value in the immutable sorted set, as defined by the comparer.</summary>
      /// <returns>The minimum value in the set.</returns>
      public T? Min => this._root.Min;

      /// <summary>Gets or sets the object that is used to determine equality for the values in the immutable sorted set.</summary>
      /// <returns>The comparer that is used to determine equality for the values in the set.</returns>
      public IComparer<T> KeyComparer
      {
        get => this._comparer;
        set
        {
          Requires.NotNull<IComparer<T>>(value, nameof (value));
          if (value == this._comparer)
            return;
          ImmutableSortedSet<T>.Node node = ImmutableSortedSet<T>.Node.EmptyNode;
          foreach (T key in this)
            node = node.Add(key, value, out bool _);
          this._immutable = (ImmutableSortedSet<T>) null;
          this._comparer = value;
          this.Root = node;
        }
      }

      internal int Version => this._version;

      private ImmutableSortedSet<
      #nullable disable
      T>.Node Root
      {
        get => this._root;
        set
        {
          ++this._version;
          if (this._root == value)
            return;
          this._root = value;
          this._immutable = (ImmutableSortedSet<T>) null;
        }
      }


      #nullable enable
      /// <summary>Adds an element to the current set and returns a value to indicate whether the element was successfully added.</summary>
      /// <param name="item">The element to add to the set.</param>
      /// <returns>
      /// <see langword="true" /> if the element is added to the set; <see langword="false" /> if the element is already in the set.</returns>
      public bool Add(T item)
      {
        bool mutated;
        this.Root = this.Root.Add(item, this._comparer, out mutated);
        return mutated;
      }

      /// <summary>Removes the specified set of items from the current set.</summary>
      /// <param name="other">The collection of items to remove from the set.</param>
      public void ExceptWith(IEnumerable<T> other)
      {
        Requires.NotNull<IEnumerable<T>>(other, nameof (other));
        foreach (T key in other)
          this.Root = this.Root.Remove(key, this._comparer, out bool _);
      }

      /// <summary>Modifies the current set so that it contains only elements that are also in a specified collection.</summary>
      /// <param name="other">The collection to compare to the current set.</param>
      public void IntersectWith(IEnumerable<T> other)
      {
        Requires.NotNull<IEnumerable<T>>(other, nameof (other));
        ImmutableSortedSet<T>.Node node = ImmutableSortedSet<T>.Node.EmptyNode;
        foreach (T key in other)
        {
          if (this.Contains(key))
            node = node.Add(key, this._comparer, out bool _);
        }
        this.Root = node;
      }

      /// <summary>Determines whether the current set is a proper (strict) subset of a specified collection.</summary>
      /// <param name="other">The collection to compare to the current set.</param>
      /// <returns>
      /// <see langword="true" /> if the current set is a proper subset of <paramref name="other" />; otherwise, <see langword="false" />.</returns>
      public bool IsProperSubsetOf(IEnumerable<T> other) => this.ToImmutable().IsProperSubsetOf(other);

      /// <summary>Determines whether the current set is a proper (strict) superset of a specified collection.</summary>
      /// <param name="other">The collection to compare to the current set.</param>
      /// <returns>
      /// <see langword="true" /> if the current set is a proper superset of <paramref name="other" />; otherwise, <see langword="false" />.</returns>
      public bool IsProperSupersetOf(IEnumerable<T> other) => this.ToImmutable().IsProperSupersetOf(other);

      /// <summary>Determines whether the current set is a subset of a specified collection.</summary>
      /// <param name="other">The collection is compare to the current set.</param>
      /// <returns>
      /// <see langword="true" /> if the current set is a subset of <paramref name="other" />; otherwise, <see langword="false" />.</returns>
      public bool IsSubsetOf(IEnumerable<T> other) => this.ToImmutable().IsSubsetOf(other);

      /// <summary>Determines whether the current set is a superset of a specified collection.</summary>
      /// <param name="other">The collection to compare to the current set.</param>
      /// <returns>
      /// <see langword="true" /> if the current set is a superset of <paramref name="other" />; otherwise, <see langword="false" />.</returns>
      public bool IsSupersetOf(IEnumerable<T> other) => this.ToImmutable().IsSupersetOf(other);

      /// <summary>Determines whether the current set overlaps with the specified collection.</summary>
      /// <param name="other">The collection to compare to the current set.</param>
      /// <returns>
      /// <see langword="true" /> if the current set and <paramref name="other" /> share at least one common element; otherwise, <see langword="false" />.</returns>
      public bool Overlaps(IEnumerable<T> other) => this.ToImmutable().Overlaps(other);

      /// <summary>Determines whether the current set and the specified collection contain the same elements.</summary>
      /// <param name="other">The collection to compare to the current set.</param>
      /// <returns>
      /// <see langword="true" /> if the current set is equal to <paramref name="other" />; otherwise, <see langword="false" />.</returns>
      public bool SetEquals(IEnumerable<T> other) => this.ToImmutable().SetEquals(other);

      /// <summary>Modifies the current set so that it contains only elements that are present either in the current set or in the specified collection, but not both.</summary>
      /// <param name="other">The collection to compare to the current set.</param>
      public void SymmetricExceptWith(IEnumerable<T> other) => this.Root = this.ToImmutable().SymmetricExcept(other)._root;

      /// <summary>Modifies the current set so that it contains all elements that are present in both the current set and in the specified collection.</summary>
      /// <param name="other">The collection to compare to the current state.</param>
      public void UnionWith(IEnumerable<T> other)
      {
        Requires.NotNull<IEnumerable<T>>(other, nameof (other));
        foreach (T key in other)
          this.Root = this.Root.Add(key, this._comparer, out bool _);
      }


      #nullable disable
      /// <summary>Adds an element to the current set and returns a value to indicate whether the element was successfully added.</summary>
      /// <param name="item">The element to add to the set.</param>
      void ICollection<T>.Add(T item) => this.Add(item);

      /// <summary>Removes all elements from this set.</summary>
      public void Clear() => this.Root = ImmutableSortedSet<T>.Node.EmptyNode;


      #nullable enable
      /// <summary>Determines whether the set contains the specified object.</summary>
      /// <param name="item">The object to locate in the set.</param>
      /// <returns>
      /// <see langword="true" /> if <paramref name="item" /> is found in the set; otherwise, <see langword="false" />.</returns>
      public bool Contains(T item) => this.Root.Contains(item, this._comparer);


      #nullable disable
      /// <summary>Copies the elements of the collection to an array, starting at a particular array index.</summary>
      /// <param name="array">The one-dimensional array that is the destination of the elements copied from collection. The array must have zero-based indexing.</param>
      /// <param name="arrayIndex">The zero-based index in <paramref name="array" /> at which copying begins.</param>
      void ICollection<T>.CopyTo(T[] array, int arrayIndex) => this._root.CopyTo(array, arrayIndex);


      #nullable enable
      /// <summary>Removes the first occurrence of the specified object from the set.</summary>
      /// <param name="item">The object to remove from the set.</param>
      /// <returns>
      /// <see langword="true" /> if <paramref name="item" /> was removed from the set; <see langword="false" /> if <paramref name="item" /> was not found in the set.</returns>
      public bool Remove(T item)
      {
        bool mutated;
        this.Root = this.Root.Remove(item, this._comparer, out mutated);
        return mutated;
      }

      /// <summary>Returns an enumerator that iterates through the set.</summary>
      /// <returns>A enumerator that can be used to iterate through the set.</returns>
      public ImmutableSortedSet<T>.Enumerator GetEnumerator() => this.Root.GetEnumerator(this);


      #nullable disable
      /// <summary>Returns an enumerator that iterates through the collection.</summary>
      /// <returns>A enumerator that can be used to iterate through the collection.</returns>
      IEnumerator<T> IEnumerable<T>.GetEnumerator() => (IEnumerator<T>) this.Root.GetEnumerator();

      /// <summary>Returns an enumerator that iterates through the collection.</summary>
      /// <returns>A enumerator that can be used to iterate through the collection.</returns>
      IEnumerator IEnumerable.GetEnumerator() => (IEnumerator) this.GetEnumerator();


      #nullable enable
      /// <summary>Searches for the first index within this set that the specified value is contained.</summary>
      /// <param name="item">The value to locate within the set.</param>
      /// <returns>The index of the specified <paramref name="item" /> in the sorted set, if <paramref name="item" /> is found.  If <paramref name="item" /> is not found and <paramref name="item" /> is less than one or more elements in this set, returns a negative number that is the bitwise complement of the index of the first element that's larger than <paramref name="item" />. If <paramref name="item" /> is not found and <paramref name="item" /> is greater than any of the elements in the set, returns a negative number that is the bitwise complement of (the index of the last element plus 1).</returns>
      public int IndexOf(T item) => this.Root.IndexOf(item, this._comparer);

      /// <summary>Returns an enumerator that iterates over the immutable sorted set in reverse order.</summary>
      /// <returns>An enumerator that iterates over the set in reverse order.</returns>
      public IEnumerable<T> Reverse() => (IEnumerable<T>) new ImmutableSortedSet<T>.ReverseEnumerable(this._root);

      /// <summary>Creates an immutable sorted set based on the contents of this instance.</summary>
      /// <returns>An immutable set.</returns>
      public ImmutableSortedSet<T> ToImmutable() => this._immutable ?? (this._immutable = ImmutableSortedSet<T>.Wrap(this.Root, this._comparer));

      /// <summary>Searches the set for a given value and returns the equal value it finds, if any.</summary>
      /// <param name="equalValue">The value for which to search.</param>
      /// <param name="actualValue">The value from the set that the search found, or the original value if the search yielded no match.</param>
      /// <returns>A value indicating whether the search was successful.</returns>
      public bool TryGetValue(T equalValue, out T actualValue)
      {
        ImmutableSortedSet<T>.Node node = this._root.Search(equalValue, this._comparer);
        if (!node.IsEmpty)
        {
          actualValue = node.Key;
          return true;
        }
        actualValue = equalValue;
        return false;
      }


      #nullable disable
      /// <summary>Copies the elements of the set to an array, starting at a particular array index.</summary>
      /// <param name="array">The one-dimensional array that is the destination of the elements copied from the set. The array must have zero-based indexing.</param>
      /// <param name="arrayIndex">The zero-based index in <paramref name="array" /> at which copying begins.</param>
      void ICollection.CopyTo(Array array, int arrayIndex) => this.Root.CopyTo(array, arrayIndex);

      /// <summary>Gets a value that indicates whether access to the <see cref="T:System.Collections.ICollection" /> is synchronized (thread-safe).</summary>
      /// <returns>
      /// <see langword="true" /> if access to the <see cref="T:System.Collections.ICollection" /> is synchronized (thread-safe); otherwise, <see langword="false" />.</returns>
      [DebuggerBrowsable(DebuggerBrowsableState.Never)]
      bool ICollection.IsSynchronized => false;


      #nullable enable
      /// <summary>Gets an object that can be used to synchronize access to the <see cref="T:System.Collections.ICollection" />.</summary>
      /// <returns>An object that can be used to synchronize access to the <see cref="T:System.Collections.ICollection" />.</returns>
      [DebuggerBrowsable(DebuggerBrowsableState.Never)]
      object ICollection.SyncRoot
      {
        get
        {
          if (this._syncRoot == null)
            Interlocked.CompareExchange<object>(ref this._syncRoot, new object(), (object) null);
          return this._syncRoot;
        }
      }
    }


    #nullable disable
    private sealed class ReverseEnumerable : IEnumerable<T>, IEnumerable
    {
      private readonly ImmutableSortedSet<T>.Node _root;

      internal ReverseEnumerable(ImmutableSortedSet<T>.Node root)
      {
        Requires.NotNull<ImmutableSortedSet<T>.Node>(root, nameof (root));
        this._root = root;
      }

      public IEnumerator<T> GetEnumerator() => this._root.Reverse();

      IEnumerator IEnumerable.GetEnumerator() => (IEnumerator) this.GetEnumerator();
    }


    #nullable enable
    /// <summary>Enumerates the contents of a binary tree.
    /// 
    /// NuGet package: System.Collections.Immutable (about immutable collections and how to install)</summary>
    /// <typeparam name="T" />
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public struct Enumerator : 
      IEnumerator<T>,
      IDisposable,
      IEnumerator,
      ISecurePooledObjectUser,
      IStrongEnumerator<T>
    {

      #nullable disable
      private readonly ImmutableSortedSet<T>.Builder _builder;
      private readonly int _poolUserId;
      private readonly bool _reverse;
      private ImmutableSortedSet<T>.Node _root;
      private SecurePooledObject<Stack<RefAsValueType<ImmutableSortedSet<T>.Node>>> _stack;
      private ImmutableSortedSet<T>.Node _current;
      private int _enumeratingBuilderVersion;


      #nullable enable
      internal Enumerator(
        ImmutableSortedSet<
        #nullable disable
        T>.Node root,

        #nullable enable
        ImmutableSortedSet<
        #nullable disable
        T>.Builder
        #nullable enable
        ? builder = null,
        bool reverse = false)
      {
        Requires.NotNull<ImmutableSortedSet<T>.Node>(root, nameof (root));
        this._root = root;
        this._builder = builder;
        this._current = (ImmutableSortedSet<T>.Node) null;
        this._reverse = reverse;
        this._enumeratingBuilderVersion = builder != null ? builder.Version : -1;
        this._poolUserId = SecureObjectPool.NewId();
        this._stack = (SecurePooledObject<Stack<RefAsValueType<ImmutableSortedSet<T>.Node>>>) null;
        if (!SecureObjectPool<Stack<RefAsValueType<ImmutableSortedSet<T>.Node>>, ImmutableSortedSet<T>.Enumerator>.TryTake(this, out this._stack))
          this._stack = SecureObjectPool<Stack<RefAsValueType<ImmutableSortedSet<T>.Node>>, ImmutableSortedSet<T>.Enumerator>.PrepNew(this, new Stack<RefAsValueType<ImmutableSortedSet<T>.Node>>(root.Height));
        this.PushNext(this._root);
      }

      int ISecurePooledObjectUser.PoolUserId => this._poolUserId;

      /// <summary>Gets the element at the current position of the enumerator.
      /// 
      /// NuGet package: System.Collections.Immutable (about immutable collections and how to install)</summary>
      /// <returns>The element at the current position of the enumerator.</returns>
      public T Current
      {
        get
        {
          this.ThrowIfDisposed();
          if (this._current != null)
            return this._current.Value;
          throw new InvalidOperationException();
        }
      }

      /// <summary>The current element.</summary>
      /// <returns>The element in the collection at the current position of the enumerator.</returns>
      object? IEnumerator.Current => (object) this.Current;

      /// <summary>Releases the resources used by the current instance of the <see cref="T:System.Collections.Immutable.ImmutableSortedSet`1.Enumerator" /> class.
      /// 
      /// NuGet package: System.Collections.Immutable (about immutable collections and how to install)</summary>
      public void Dispose()
      {
        this._root = (ImmutableSortedSet<T>.Node) null;
        this._current = (ImmutableSortedSet<T>.Node) null;
        Stack<RefAsValueType<ImmutableSortedSet<T>.Node>> stack;
        if (this._stack == null || !this._stack.TryUse<ImmutableSortedSet<T>.Enumerator>(ref this, out stack))
          return;
        stack.ClearFastWhenEmpty<RefAsValueType<ImmutableSortedSet<T>.Node>>();
        SecureObjectPool<Stack<RefAsValueType<ImmutableSortedSet<T>.Node>>, ImmutableSortedSet<T>.Enumerator>.TryAdd(this, this._stack);
        this._stack = (SecurePooledObject<Stack<RefAsValueType<ImmutableSortedSet<T>.Node>>>) null;
      }

      /// <summary>Advances the enumerator to the next element of the immutable sorted set.
      /// 
      /// NuGet package: System.Collections.Immutable (about immutable collections and how to install)</summary>
      /// <returns>
      /// <see langword="true" /> if the enumerator was successfully advanced to the next element; <see langword="false" /> if the enumerator has passed the end of the sorted set.</returns>
      public bool MoveNext()
      {
        this.ThrowIfDisposed();
        this.ThrowIfChanged();
        Stack<RefAsValueType<ImmutableSortedSet<T>.Node>> refAsValueTypeStack = this._stack.Use<ImmutableSortedSet<T>.Enumerator>(ref this);
        if (refAsValueTypeStack.Count > 0)
        {
          ImmutableSortedSet<T>.Node node = refAsValueTypeStack.Pop().Value;
          this._current = node;
          this.PushNext(this._reverse ? node.Left : node.Right);
          return true;
        }
        this._current = (ImmutableSortedSet<T>.Node) null;
        return false;
      }

      /// <summary>Sets the enumerator to its initial position, which is before the first element in the immutable sorted set.
      /// 
      /// NuGet package: System.Collections.Immutable (about immutable collections and how to install)</summary>
      public void Reset()
      {
        this.ThrowIfDisposed();
        this._enumeratingBuilderVersion = this._builder != null ? this._builder.Version : -1;
        this._current = (ImmutableSortedSet<T>.Node) null;
        this._stack.Use<ImmutableSortedSet<T>.Enumerator>(ref this).ClearFastWhenEmpty<RefAsValueType<ImmutableSortedSet<T>.Node>>();
        this.PushNext(this._root);
      }

      private void ThrowIfDisposed()
      {
        if (this._root != null && (this._stack == null || this._stack.IsOwned<ImmutableSortedSet<T>.Enumerator>(ref this)))
          return;
        Requires.FailObjectDisposed<ImmutableSortedSet<T>.Enumerator>(this);
      }

      private void ThrowIfChanged()
      {
        if (this._builder != null && this._builder.Version != this._enumeratingBuilderVersion)
          throw new InvalidOperationException();
      }


      #nullable disable
      private void PushNext(ImmutableSortedSet<T>.Node node)
      {
        Requires.NotNull<ImmutableSortedSet<T>.Node>(node, nameof (node));
        Stack<RefAsValueType<ImmutableSortedSet<T>.Node>> refAsValueTypeStack = this._stack.Use<ImmutableSortedSet<T>.Enumerator>(ref this);
        for (; !node.IsEmpty; node = this._reverse ? node.Right : node.Left)
          refAsValueTypeStack.Push(new RefAsValueType<ImmutableSortedSet<T>.Node>(node));
      }
    }


    #nullable enable
    [DebuggerDisplay("{_key}")]
    internal sealed class Node : IBinaryTree<T>, IBinaryTree, IEnumerable<T>, IEnumerable
    {
      internal static readonly ImmutableSortedSet<
      #nullable disable
      T>.Node EmptyNode = new ImmutableSortedSet<T>.Node();
      private readonly T _key;
      private bool _frozen;
      private byte _height;
      private int _count;
      private ImmutableSortedSet<T>.Node _left;
      private ImmutableSortedSet<T>.Node _right;

      private Node() => this._frozen = true;

      private Node(
        T key,
        ImmutableSortedSet<T>.Node left,
        ImmutableSortedSet<T>.Node right,
        bool frozen = false)
      {
        Requires.NotNull<ImmutableSortedSet<T>.Node>(left, nameof (left));
        Requires.NotNull<ImmutableSortedSet<T>.Node>(right, nameof (right));
        this._key = key;
        this._left = left;
        this._right = right;
        this._height = checked ((byte) (1 + (int) Math.Max(left._height, right._height)));
        this._count = 1 + left._count + right._count;
        this._frozen = frozen;
      }

      public bool IsEmpty => this._left == null;

      public int Height => (int) this._height;


      #nullable enable
      public ImmutableSortedSet<
      #nullable disable
      T>.Node
      #nullable enable
      ? Left => this._left;

      IBinaryTree? IBinaryTree.Left => (IBinaryTree) this._left;

      public ImmutableSortedSet<
      #nullable disable
      T>.Node
      #nullable enable
      ? Right => this._right;

      IBinaryTree? IBinaryTree.Right => (IBinaryTree) this._right;

      IBinaryTree<T>? IBinaryTree<
      #nullable disable
      T>.Left => (IBinaryTree<T>) this._left;


      #nullable enable
      IBinaryTree<T>? IBinaryTree<
      #nullable disable
      T>.Right => (IBinaryTree<T>) this._right;


      #nullable enable
      public T Value => this._key;

      public int Count => this._count;

      internal T Key => this._key;

      internal T? Max
      {
        get
        {
          if (this.IsEmpty)
            return default (T);
          ImmutableSortedSet<T>.Node node = this;
          while (!node._right.IsEmpty)
            node = node._right;
          return node._key;
        }
      }

      internal T? Min
      {
        get
        {
          if (this.IsEmpty)
            return default (T);
          ImmutableSortedSet<T>.Node node = this;
          while (!node._left.IsEmpty)
            node = node._left;
          return node._key;
        }
      }

      internal T this[int index]
      {
        get
        {
          Requires.Range(index >= 0 && index < this.Count, nameof (index));
          if (index < this._left._count)
            return this._left[index];
          return index > this._left._count ? this._right[index - this._left._count - 1] : this._key;
        }
      }

      internal ref readonly T ItemRef(int index)
      {
        Requires.Range(index >= 0 && index < this.Count, nameof (index));
        return ref this.ItemRefUnchecked(index);
      }


      #nullable disable
      private ref readonly T ItemRefUnchecked(int index)
      {
        if (index < this._left._count)
          return ref this._left.ItemRefUnchecked(index);
        return ref (index > this._left._count ? ref this._right.ItemRefUnchecked(index - this._left._count - 1) : ref this._key);
      }


      #nullable enable
      public ImmutableSortedSet<
      #nullable disable
      T>.Enumerator GetEnumerator() => new ImmutableSortedSet<T>.Enumerator(this);

      [ExcludeFromCodeCoverage]
      IEnumerator<T> IEnumerable<T>.GetEnumerator() => (IEnumerator<T>) this.GetEnumerator();

      [ExcludeFromCodeCoverage]
      IEnumerator IEnumerable.GetEnumerator() => (IEnumerator) this.GetEnumerator();


      #nullable enable
      internal ImmutableSortedSet<
      #nullable disable
      T>.Enumerator GetEnumerator(
      #nullable enable
      ImmutableSortedSet<
      #nullable disable
      T>.Builder builder) => new ImmutableSortedSet<T>.Enumerator(this, builder);


      #nullable enable
      internal void CopyTo(T[] array, int arrayIndex)
      {
        Requires.NotNull<T[]>(array, nameof (array));
        Requires.Range(arrayIndex >= 0, nameof (arrayIndex));
        Requires.Range(array.Length >= arrayIndex + this.Count, nameof (arrayIndex));
        foreach (T obj in this)
          array[arrayIndex++] = obj;
      }

      internal void CopyTo(Array array, int arrayIndex)
      {
        Requires.NotNull<Array>(array, nameof (array));
        Requires.Range(arrayIndex >= 0, nameof (arrayIndex));
        Requires.Range(array.Length >= arrayIndex + this.Count, nameof (arrayIndex));
        foreach (T obj in this)
          array.SetValue((object) obj, arrayIndex++);
      }

      internal ImmutableSortedSet<
      #nullable disable
      T>.Node Add(
      #nullable enable
      T key, IComparer<T> comparer, out bool mutated)
      {
        Requires.NotNull<IComparer<T>>(comparer, nameof (comparer));
        if (this.IsEmpty)
        {
          mutated = true;
          return new ImmutableSortedSet<T>.Node(key, this, this);
        }
        ImmutableSortedSet<T>.Node tree = this;
        int num = comparer.Compare(key, this._key);
        if (num > 0)
        {
          ImmutableSortedSet<T>.Node right = this._right.Add(key, comparer, out mutated);
          if (mutated)
            tree = this.Mutate(right: right);
        }
        else if (num < 0)
        {
          ImmutableSortedSet<T>.Node left = this._left.Add(key, comparer, out mutated);
          if (mutated)
            tree = this.Mutate(left);
        }
        else
        {
          mutated = false;
          return this;
        }
        return !mutated ? tree : ImmutableSortedSet<T>.Node.MakeBalanced(tree);
      }

      internal ImmutableSortedSet<
      #nullable disable
      T>.Node Remove(
      #nullable enable
      T key, IComparer<T> comparer, out bool mutated)
      {
        Requires.NotNull<IComparer<T>>(comparer, nameof (comparer));
        if (this.IsEmpty)
        {
          mutated = false;
          return this;
        }
        ImmutableSortedSet<T>.Node tree = this;
        int num = comparer.Compare(key, this._key);
        if (num == 0)
        {
          mutated = true;
          if (this._right.IsEmpty && this._left.IsEmpty)
            tree = ImmutableSortedSet<T>.Node.EmptyNode;
          else if (this._right.IsEmpty && !this._left.IsEmpty)
            tree = this._left;
          else if (!this._right.IsEmpty && this._left.IsEmpty)
          {
            tree = this._right;
          }
          else
          {
            ImmutableSortedSet<T>.Node node = this._right;
            while (!node._left.IsEmpty)
              node = node._left;
            ImmutableSortedSet<T>.Node right = this._right.Remove(node._key, comparer, out bool _);
            tree = node.Mutate(this._left, right);
          }
        }
        else if (num < 0)
        {
          ImmutableSortedSet<T>.Node left = this._left.Remove(key, comparer, out mutated);
          if (mutated)
            tree = this.Mutate(left);
        }
        else
        {
          ImmutableSortedSet<T>.Node right = this._right.Remove(key, comparer, out mutated);
          if (mutated)
            tree = this.Mutate(right: right);
        }
        return !tree.IsEmpty ? ImmutableSortedSet<T>.Node.MakeBalanced(tree) : tree;
      }

      internal bool Contains(T key, IComparer<T> comparer)
      {
        Requires.NotNull<IComparer<T>>(comparer, nameof (comparer));
        return !this.Search(key, comparer).IsEmpty;
      }

      internal void Freeze()
      {
        if (this._frozen)
          return;
        this._left.Freeze();
        this._right.Freeze();
        this._frozen = true;
      }

      internal ImmutableSortedSet<
      #nullable disable
      T>.Node Search(
      #nullable enable
      T key, IComparer<T> comparer)
      {
        Requires.NotNull<IComparer<T>>(comparer, nameof (comparer));
        if (this.IsEmpty)
          return this;
        int num = comparer.Compare(key, this._key);
        if (num == 0)
          return this;
        return num > 0 ? this._right.Search(key, comparer) : this._left.Search(key, comparer);
      }

      internal int IndexOf(T key, IComparer<T> comparer)
      {
        Requires.NotNull<IComparer<T>>(comparer, nameof (comparer));
        if (this.IsEmpty)
          return -1;
        int num1 = comparer.Compare(key, this._key);
        if (num1 == 0)
          return this._left.Count;
        if (num1 <= 0)
          return this._left.IndexOf(key, comparer);
        int num2 = this._right.IndexOf(key, comparer);
        bool flag = num2 < 0;
        if (flag)
          num2 = ~num2;
        int num3 = this._left.Count + 1 + num2;
        if (flag)
          num3 = ~num3;
        return num3;
      }

      internal IEnumerator<T> Reverse() => (IEnumerator<T>) new ImmutableSortedSet<T>.Enumerator(this, reverse: true);


      #nullable disable
      private static ImmutableSortedSet<T>.Node RotateLeft(ImmutableSortedSet<T>.Node tree)
      {
        Requires.NotNull<ImmutableSortedSet<T>.Node>(tree, nameof (tree));
        if (tree._right.IsEmpty)
          return tree;
        ImmutableSortedSet<T>.Node right = tree._right;
        return right.Mutate(tree.Mutate(right: right._left));
      }

      private static ImmutableSortedSet<T>.Node RotateRight(ImmutableSortedSet<T>.Node tree)
      {
        Requires.NotNull<ImmutableSortedSet<T>.Node>(tree, nameof (tree));
        if (tree._left.IsEmpty)
          return tree;
        ImmutableSortedSet<T>.Node left = tree._left;
        return left.Mutate(right: tree.Mutate(left._right));
      }

      private static ImmutableSortedSet<T>.Node DoubleLeft(ImmutableSortedSet<T>.Node tree)
      {
        Requires.NotNull<ImmutableSortedSet<T>.Node>(tree, nameof (tree));
        return tree._right.IsEmpty ? tree : ImmutableSortedSet<T>.Node.RotateLeft(tree.Mutate(right: ImmutableSortedSet<T>.Node.RotateRight(tree._right)));
      }

      private static ImmutableSortedSet<T>.Node DoubleRight(ImmutableSortedSet<T>.Node tree)
      {
        Requires.NotNull<ImmutableSortedSet<T>.Node>(tree, nameof (tree));
        return tree._left.IsEmpty ? tree : ImmutableSortedSet<T>.Node.RotateRight(tree.Mutate(ImmutableSortedSet<T>.Node.RotateLeft(tree._left)));
      }

      private static int Balance(ImmutableSortedSet<T>.Node tree)
      {
        Requires.NotNull<ImmutableSortedSet<T>.Node>(tree, nameof (tree));
        return (int) tree._right._height - (int) tree._left._height;
      }

      private static bool IsRightHeavy(ImmutableSortedSet<T>.Node tree)
      {
        Requires.NotNull<ImmutableSortedSet<T>.Node>(tree, nameof (tree));
        return ImmutableSortedSet<T>.Node.Balance(tree) >= 2;
      }

      private static bool IsLeftHeavy(ImmutableSortedSet<T>.Node tree)
      {
        Requires.NotNull<ImmutableSortedSet<T>.Node>(tree, nameof (tree));
        return ImmutableSortedSet<T>.Node.Balance(tree) <= -2;
      }

      private static ImmutableSortedSet<T>.Node MakeBalanced(ImmutableSortedSet<T>.Node tree)
      {
        Requires.NotNull<ImmutableSortedSet<T>.Node>(tree, nameof (tree));
        if (ImmutableSortedSet<T>.Node.IsRightHeavy(tree))
          return ImmutableSortedSet<T>.Node.Balance(tree._right) >= 0 ? ImmutableSortedSet<T>.Node.RotateLeft(tree) : ImmutableSortedSet<T>.Node.DoubleLeft(tree);
        if (!ImmutableSortedSet<T>.Node.IsLeftHeavy(tree))
          return tree;
        return ImmutableSortedSet<T>.Node.Balance(tree._left) <= 0 ? ImmutableSortedSet<T>.Node.RotateRight(tree) : ImmutableSortedSet<T>.Node.DoubleRight(tree);
      }


      #nullable enable
      internal static ImmutableSortedSet<
      #nullable disable
      T>.Node NodeTreeFromList(
      #nullable enable
      IOrderedCollection<T> items, int start, int length)
      {
        Requires.NotNull<IOrderedCollection<T>>(items, nameof (items));
        if (length == 0)
          return ImmutableSortedSet<T>.Node.EmptyNode;
        int length1 = (length - 1) / 2;
        int length2 = length - 1 - length1;
        ImmutableSortedSet<T>.Node left = ImmutableSortedSet<T>.Node.NodeTreeFromList(items, start, length2);
        ImmutableSortedSet<T>.Node right = ImmutableSortedSet<T>.Node.NodeTreeFromList(items, start + length2 + 1, length1);
        return new ImmutableSortedSet<T>.Node(items[start + length2], left, right, true);
      }


      #nullable disable
      private ImmutableSortedSet<T>.Node Mutate(
        ImmutableSortedSet<T>.Node left = null,
        ImmutableSortedSet<T>.Node right = null)
      {
        if (this._frozen)
          return new ImmutableSortedSet<T>.Node(this._key, left ?? this._left, right ?? this._right);
        if (left != null)
          this._left = left;
        if (right != null)
          this._right = right;
        this._height = checked ((byte) (1 + (int) Math.Max(this._left._height, this._right._height)));
        this._count = 1 + this._left._count + this._right._count;
        return this;
      }
    }
  }
}
