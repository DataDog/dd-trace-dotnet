﻿// Decompiled with JetBrains decompiler
// Type: System.Collections.Immutable.ImmutableHashSet`1
// Assembly: System.Collections.Immutable, Version=7.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 5F9FF90F-0D16-4469-A104-76829D3705E2
// Assembly location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.dll
// XML documentation location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.xml

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Datadog.System.Collections.Generic;
using Datadog.System.Linq;


#nullable enable
namespace Datadog.System.Collections.Immutable
{
    /// <summary>Represents an immutable, unordered hash set.
    /// 
    /// NuGet package: System.Collections.Immutable (about immutable collections and how to install)</summary>
    /// <typeparam name="T">The type of elements in the hash set.</typeparam>
    [DebuggerDisplay("Count = {Count}")]
  [DebuggerTypeProxy(typeof (ImmutableEnumerableDebuggerProxy<>))]
  public sealed class ImmutableHashSet<T> : 
    IImmutableSet<T>,
    IReadOnlyCollection<T>,
    IEnumerable<T>,
    IEnumerable,
    IHashKeyCollection<T>,
    ICollection<T>,
    ISet<T>,
    ICollection,
    IStrongEnumerable<T, ImmutableHashSet<T>.Enumerator>
  {
    /// <summary>Gets an immutable hash set for this type that uses the default <see cref="T:System.Collections.Generic.IEqualityComparer`1" />.</summary>
    public static readonly ImmutableHashSet<T> Empty = new ImmutableHashSet<T>(SortedInt32KeyNode<ImmutableHashSet<T>.HashBucket>.EmptyNode, (IEqualityComparer<T>) EqualityComparer<T>.Default, 0);

    #nullable disable
    private static readonly Action<KeyValuePair<int, ImmutableHashSet<T>.HashBucket>> s_FreezeBucketAction = (Action<KeyValuePair<int, ImmutableHashSet<T>.HashBucket>>) (kv => kv.Value.Freeze());
    private readonly IEqualityComparer<T> _equalityComparer;
    private readonly int _count;
    private readonly SortedInt32KeyNode<ImmutableHashSet<T>.HashBucket> _root;
    private readonly IEqualityComparer<ImmutableHashSet<T>.HashBucket> _hashBucketEqualityComparer;


    #nullable enable
    internal ImmutableHashSet(IEqualityComparer<T> equalityComparer)
      : this(SortedInt32KeyNode<ImmutableHashSet<T>.HashBucket>.EmptyNode, equalityComparer, 0)
    {
    }


    #nullable disable
    private ImmutableHashSet(
      SortedInt32KeyNode<ImmutableHashSet<T>.HashBucket> root,
      IEqualityComparer<T> equalityComparer,
      int count)
    {
      Requires.NotNull<SortedInt32KeyNode<ImmutableHashSet<T>.HashBucket>>(root, nameof (root));
      Requires.NotNull<IEqualityComparer<T>>(equalityComparer, nameof (equalityComparer));
      root.Freeze(ImmutableHashSet<T>.s_FreezeBucketAction);
      this._root = root;
      this._count = count;
      this._equalityComparer = equalityComparer;
      this._hashBucketEqualityComparer = ImmutableHashSet<T>.GetHashBucketEqualityComparer(equalityComparer);
    }


    #nullable enable
    /// <summary>Retrieves an empty immutable hash set that has the same sorting and ordering semantics as this instance.</summary>
    /// <returns>An empty hash set that has the same sorting and ordering semantics as this instance.</returns>
    public ImmutableHashSet<T> Clear() => !this.IsEmpty ? ImmutableHashSet<T>.Empty.WithComparer(this._equalityComparer) : this;

    /// <summary>Gets the number of elements in the immutable hash set.</summary>
    /// <returns>The number of elements in the hash set.</returns>
    public int Count => this._count;

    /// <summary>Gets a value that indicates whether the current immutable hash set is empty.</summary>
    /// <returns>
    /// <see langword="true" /> if this instance is empty; otherwise, <see langword="false" />.</returns>
    public bool IsEmpty => this.Count == 0;

    /// <summary>Gets the object that is used to obtain hash codes for the keys and to check the equality of values in the immutable hash set.</summary>
    /// <returns>The comparer used to obtain hash codes for the keys and check equality.</returns>
    public IEqualityComparer<T> KeyComparer => this._equalityComparer;


    #nullable disable
    /// <summary>Retrieves an empty set that has the same sorting and ordering semantics as this instance.</summary>
    /// <returns>An empty set that has the same sorting or ordering semantics as this instance.</returns>
    IImmutableSet<T> IImmutableSet<T>.Clear() => (IImmutableSet<T>) this.Clear();


    #nullable enable
    /// <summary>See <see cref="T:System.Collections.ICollection" />.</summary>
    /// <returns>An object that can be used to synchronize access to the <see cref="T:System.Collections.ICollection" />.</returns>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    object ICollection.SyncRoot => (object) this;

    /// <summary>See the <see cref="T:System.Collections.ICollection" /> interface.</summary>
    /// <returns>
    /// <see langword="true" /> if access to the <see cref="T:System.Collections.ICollection" /> is synchronized (thread safe); otherwise, <see langword="false" />.</returns>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    bool ICollection.IsSynchronized => true;

    internal IBinaryTree Root => (IBinaryTree) this._root;

    private ImmutableHashSet<
    #nullable disable
    T>.MutationInput Origin => new ImmutableHashSet<T>.MutationInput(this);


    #nullable enable
    /// <summary>Creates an immutable hash set that has the same contents as this set and can be efficiently mutated across multiple operations by using standard mutable interfaces.</summary>
    /// <returns>A set with the same contents as this set that can be efficiently mutated across multiple operations by using standard mutable interfaces.</returns>
    public ImmutableHashSet<
    #nullable disable
    T>.Builder ToBuilder() => new ImmutableHashSet<T>.Builder(this);


    #nullable enable
    /// <summary>Adds the specified element to the hash set.</summary>
    /// <param name="item">The element to add to the set.</param>
    /// <returns>A hash set that contains the added value and any values previously held by the  <see cref="T:System.Collections.Immutable.ImmutableHashSet`1" /> object.</returns>
    public ImmutableHashSet<T> Add(T item) => ImmutableHashSet<T>.Add(item, this.Origin).Finalize(this);

    /// <summary>Removes the specified element from this immutable hash set.</summary>
    /// <param name="item">The element to remove.</param>
    /// <returns>A new set with the specified element removed, or the current set if the element cannot be found in the set.</returns>
    public ImmutableHashSet<T> Remove(T item) => ImmutableHashSet<T>.Remove(item, this.Origin).Finalize(this);

    /// <summary>Searches the set for a given value and returns the equal value it finds, if any.</summary>
    /// <param name="equalValue">The value to search for.</param>
    /// <param name="actualValue">The value from the set that the search found, or the original value if the search yielded no match.</param>
    /// <returns>A value indicating whether the search was successful.</returns>
    public bool TryGetValue(T equalValue, out T actualValue)
    {
      ImmutableHashSet<T>.HashBucket hashBucket;
      if (this._root.TryGetValue((object) equalValue != null ? this._equalityComparer.GetHashCode(equalValue) : 0, out hashBucket))
        return hashBucket.TryExchange(equalValue, this._equalityComparer, out actualValue);
      actualValue = equalValue;
      return false;
    }

    /// <summary>Creates a new immutable hash set that contains all elements that are present in either the current set or in the specified collection.</summary>
    /// <param name="other">The collection to add elements from.</param>
    /// <returns>A new immutable hash set with the items added; or the original set if all the items were already in the set.</returns>
    public ImmutableHashSet<T> Union(IEnumerable<T> other)
    {
      Requires.NotNull<IEnumerable<T>>(other, nameof (other));
      return this.Union(other, false);
    }

    /// <summary>Creates an immutable hash set that contains elements that exist in both this set and the specified set.</summary>
    /// <param name="other">The collection to compare to the current set.</param>
    /// <returns>A new immutable set that contains any elements that exist in both sets.</returns>
    public ImmutableHashSet<T> Intersect(IEnumerable<T> other)
    {
      Requires.NotNull<IEnumerable<T>>(other, nameof (other));
      return ImmutableHashSet<T>.Intersect(other, this.Origin).Finalize(this);
    }

    /// <summary>Removes the elements in the specified collection from the current immutable hash set.</summary>
    /// <param name="other">The collection of items to remove from this set.</param>
    /// <returns>A new set with the items removed; or the original set if none of the items were in the set.</returns>
    public ImmutableHashSet<T> Except(IEnumerable<T> other)
    {
      Requires.NotNull<IEnumerable<T>>(other, nameof (other));
      return ImmutableHashSet<T>.Except(other, this._equalityComparer, this._hashBucketEqualityComparer, this._root).Finalize(this);
    }

    /// <summary>Creates an immutable hash set that contains only elements that are present either in the current set or in the specified collection, but not both.</summary>
    /// <param name="other">The collection to compare to the current set.</param>
    /// <returns>A new set that contains the elements that are present only in the current set or in the specified collection, but not both.</returns>
    public ImmutableHashSet<T> SymmetricExcept(IEnumerable<T> other)
    {
      Requires.NotNull<IEnumerable<T>>(other, nameof (other));
      return ImmutableHashSet<T>.SymmetricExcept(other, this.Origin).Finalize(this);
    }

    /// <summary>Determines whether the current immutable hash set and the specified collection contain the same elements.</summary>
    /// <param name="other">The collection to compare to the current set.</param>
    /// <returns>
    /// <see langword="true" /> if the sets are equal; otherwise, <see langword="false" />.</returns>
    public bool SetEquals(IEnumerable<T> other)
    {
      Requires.NotNull<IEnumerable<T>>(other, nameof (other));
      return this == other || ImmutableHashSet<T>.SetEquals(other, this.Origin);
    }

    /// <summary>Determines whether the current immutable hash set is a proper (strict) subset of a specified collection.</summary>
    /// <param name="other">The collection to compare to the current set.</param>
    /// <returns>
    /// <see langword="true" /> if the current set is a proper subset of the specified collection; otherwise, <see langword="false" />.</returns>
    public bool IsProperSubsetOf(IEnumerable<T> other)
    {
      Requires.NotNull<IEnumerable<T>>(other, nameof (other));
      return ImmutableHashSet<T>.IsProperSubsetOf(other, this.Origin);
    }

    /// <summary>Determines whether the current immutable hash set is a proper (strict) superset of a specified collection.</summary>
    /// <param name="other">The collection to compare to the current set.</param>
    /// <returns>
    /// <see langword="true" /> if the current set is a proper superset of the specified collection; otherwise, <see langword="false" />.</returns>
    public bool IsProperSupersetOf(IEnumerable<T> other)
    {
      Requires.NotNull<IEnumerable<T>>(other, nameof (other));
      return ImmutableHashSet<T>.IsProperSupersetOf(other, this.Origin);
    }

    /// <summary>Determines whether the current immutable hash set is a subset of a specified collection.</summary>
    /// <param name="other">The collection to compare to the current set.</param>
    /// <returns>
    /// <see langword="true" /> if the current set is a subset of the specified collection; otherwise, <see langword="false" />.</returns>
    public bool IsSubsetOf(IEnumerable<T> other)
    {
      Requires.NotNull<IEnumerable<T>>(other, nameof (other));
      return ImmutableHashSet<T>.IsSubsetOf(other, this.Origin);
    }

    /// <summary>Determines whether the current immutable hash set is a superset of a specified collection.</summary>
    /// <param name="other">The collection to compare to the current set.</param>
    /// <returns>
    /// <see langword="true" /> if the current set is a superset of the specified collection; otherwise, <see langword="false" />.</returns>
    public bool IsSupersetOf(IEnumerable<T> other)
    {
      Requires.NotNull<IEnumerable<T>>(other, nameof (other));
      return ImmutableHashSet<T>.IsSupersetOf(other, this.Origin);
    }

    /// <summary>Determines whether the current immutable hash set overlaps with the specified collection.</summary>
    /// <param name="other">The collection to compare to the current set.</param>
    /// <returns>
    /// <see langword="true" /> if the current set and the specified collection share at least one common element; otherwise, <see langword="false" />.</returns>
    public bool Overlaps(IEnumerable<T> other)
    {
      Requires.NotNull<IEnumerable<T>>(other, nameof (other));
      return ImmutableHashSet<T>.Overlaps(other, this.Origin);
    }


    #nullable disable
    /// <summary>Adds the specified element to this immutable set.</summary>
    /// <param name="item">The element to add.</param>
    /// <returns>A new set with the element added, or this set if the element is already in the set.</returns>
    IImmutableSet<T> IImmutableSet<T>.Add(T item) => (IImmutableSet<T>) this.Add(item);

    /// <summary>Removes the specified element from this immutable set.</summary>
    /// <param name="item">The element to remove.</param>
    /// <returns>A new set with the specified element removed, or the current set if the element cannot be found in the set.</returns>
    IImmutableSet<T> IImmutableSet<T>.Remove(T item) => (IImmutableSet<T>) this.Remove(item);

    /// <summary>Creates a new immutable set that contains all elements that are present in either the current set or in the specified collection.</summary>
    /// <param name="other">The collection to add elements from.</param>
    /// <returns>A new immutable set with the items added; or the original set if all the items were already in the set.</returns>
    IImmutableSet<T> IImmutableSet<T>.Union(IEnumerable<T> other) => (IImmutableSet<T>) this.Union(other);

    /// <summary>Creates an immutable set that contains elements that exist in both this set and the specified set.</summary>
    /// <param name="other">The collection to compare to the current set.</param>
    /// <returns>A new immutable set that contains any elements that exist in both sets.</returns>
    IImmutableSet<T> IImmutableSet<T>.Intersect(IEnumerable<T> other) => (IImmutableSet<T>) this.Intersect(other);

    /// <summary>Removes the elements in the specified collection from the current set.</summary>
    /// <param name="other">The collection of items to remove from this set.</param>
    /// <returns>A new set with the items removed; or the original set if none of the items were in the set.</returns>
    IImmutableSet<T> IImmutableSet<T>.Except(IEnumerable<T> other) => (IImmutableSet<T>) this.Except(other);

    /// <summary>Creates an immutable set that contains only elements that are present either in the current set or in the specified collection, but not both.</summary>
    /// <param name="other">The collection to compare to the current set.</param>
    /// <returns>A new set that contains the elements that are present only in the current set or in the specified collection, but not both.</returns>
    IImmutableSet<T> IImmutableSet<T>.SymmetricExcept(IEnumerable<T> other) => (IImmutableSet<T>) this.SymmetricExcept(other);


    #nullable enable
    /// <summary>Determines whether this immutable hash set contains the specified element.</summary>
    /// <param name="item">The object to locate in the immutable hash set.</param>
    /// <returns>
    /// <see langword="true" /> if <paramref name="item" /> is found in the <see cref="T:System.Collections.Immutable.ImmutableHashSet`1" />; otherwise, <see langword="false" />.</returns>
    public bool Contains(T item) => ImmutableHashSet<T>.Contains(item, this.Origin);

    /// <summary>Gets an instance of the immutable hash set that uses the specified equality comparer for its search methods.</summary>
    /// <param name="equalityComparer">The equality comparer to use.</param>
    /// <returns>An instance of this immutable hash set that uses the given comparer.</returns>
    public ImmutableHashSet<T> WithComparer(IEqualityComparer<T>? equalityComparer)
    {
      if (equalityComparer == null)
        equalityComparer = (IEqualityComparer<T>) EqualityComparer<T>.Default;
      return equalityComparer == this._equalityComparer ? this : new ImmutableHashSet<T>(equalityComparer).Union((IEnumerable<T>) this, true);
    }


    #nullable disable
    /// <summary>Adds an element to the current set and returns a value that indicates whether the element was successfully added.</summary>
    /// <param name="item">The element to add to the collection.</param>
    /// <returns>
    /// <see langword="true" /> if the element is added to the set; <see langword="false" /> if the element is already in the set.</returns>
    bool ISet<T>.Add(T item) => throw new NotSupportedException();

    /// <summary>Removes all elements in the specified collection from the current set.</summary>
    /// <param name="other">The collection of items to remove.</param>
    void ISet<T>.ExceptWith(IEnumerable<T> other) => throw new NotSupportedException();

    /// <summary>Modifies the current set so that it contains only elements that are also in a specified collection.</summary>
    /// <param name="other">The collection to compare to the current collection.</param>
    void ISet<T>.IntersectWith(IEnumerable<T> other) => throw new NotSupportedException();

    /// <summary>Modifies the current set so that it contains only elements that are present either in the current set or in the specified collection, but not both.</summary>
    /// <param name="other">The collection to compare to the current set.</param>
    void ISet<T>.SymmetricExceptWith(IEnumerable<T> other) => throw new NotSupportedException();

    /// <summary>Modifies the current set so that it contains all elements that are present in either the current set or in the specified collection.</summary>
    /// <param name="other">The collection to compare to the current set.</param>
    void ISet<T>.UnionWith(IEnumerable<T> other) => throw new NotSupportedException();

    /// <summary>See the <see cref="T:System.Collections.Generic.ICollection`1" /> interface.</summary>
    /// <returns>
    /// <see langword="true" /> if the <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only; otherwise, <see langword="false" />.</returns>
    bool ICollection<T>.IsReadOnly => true;

    /// <summary>Copies the elements of the set to an array, starting at a particular index.</summary>
    /// <param name="array">The one-dimensional array that is the destination of the elements copied from the set. The array must have zero-based indexing.</param>
    /// <param name="arrayIndex">The zero-based index in <paramref name="array" /> at which copying begins.</param>
    void ICollection<T>.CopyTo(T[] array, int arrayIndex)
    {
      Requires.NotNull<T[]>(array, nameof (array));
      Requires.Range(arrayIndex >= 0, nameof (arrayIndex));
      Requires.Range(array.Length >= arrayIndex + this.Count, nameof (arrayIndex));
      foreach (T obj in this)
        array[arrayIndex++] = obj;
    }

    /// <summary>Adds an item to the set.</summary>
    /// <param name="item">The object to add to the set.</param>
    /// <exception cref="T:System.NotSupportedException">The set is read-only.</exception>
    void ICollection<T>.Add(T item) => throw new NotSupportedException();

    /// <summary>Removes all items from this set.</summary>
    /// <exception cref="T:System.NotSupportedException">The set is read-only.</exception>
    void ICollection<T>.Clear() => throw new NotSupportedException();

    /// <summary>Removes the first occurrence of a specific object from the set.</summary>
    /// <param name="item">The object to remove from the set.</param>
    /// <returns>
    /// <see langword="true" /> if the element is successfully removed; otherwise, <see langword="false" />.</returns>
    bool ICollection<T>.Remove(T item) => throw new NotSupportedException();

    /// <summary>Copies the elements of the set to an array, starting at a particular index.</summary>
    /// <param name="array">The one-dimensional array that is the destination of the elements copied from the set. The array must have zero-based indexing.</param>
    /// <param name="arrayIndex">The zero-based index in <paramref name="array" /> at which copying begins.</param>
    void ICollection.CopyTo(Array array, int arrayIndex)
    {
      Requires.NotNull<Array>(array, nameof (array));
      Requires.Range(arrayIndex >= 0, nameof (arrayIndex));
      Requires.Range(array.Length >= arrayIndex + this.Count, nameof (arrayIndex));
      foreach (T obj in this)
        array.SetValue((object) obj, arrayIndex++);
    }


    #nullable enable
    /// <summary>Returns an enumerator that iterates through the collection.</summary>
    /// <returns>An enumerator that can be used to iterate through the collection.</returns>
    public ImmutableHashSet<
    #nullable disable
    T>.Enumerator GetEnumerator() => new ImmutableHashSet<T>.Enumerator(this._root);

    /// <summary>Returns an enumerator that iterates through the collection.</summary>
    /// <returns>An enumerator that iterates through the collection.</returns>
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => !this.IsEmpty ? (IEnumerator<T>) this.GetEnumerator() : Enumerable.Empty<T>().GetEnumerator();

    /// <summary>Returns an enumerator that iterates through a set.</summary>
    /// <returns>An enumerator that can be used to iterate through the set.</returns>
    IEnumerator IEnumerable.GetEnumerator() => (IEnumerator) this.GetEnumerator();

    private static bool IsSupersetOf(IEnumerable<T> other, ImmutableHashSet<T>.MutationInput origin)
    {
      Requires.NotNull<IEnumerable<T>>(other, nameof (other));
      foreach (T obj in other.GetEnumerableDisposable<T, ImmutableHashSet<T>.Enumerator>())
      {
        if (!ImmutableHashSet<T>.Contains(obj, origin))
          return false;
      }
      return true;
    }

    private static ImmutableHashSet<T>.MutationResult Add(
      T item,
      ImmutableHashSet<T>.MutationInput origin)
    {
      int hashCode = (object) item != null ? origin.EqualityComparer.GetHashCode(item) : 0;
      ImmutableHashSet<T>.OperationResult result;
      ImmutableHashSet<T>.HashBucket newBucket = origin.Root.GetValueOrDefault(hashCode).Add(item, origin.EqualityComparer, out result);
      return result == ImmutableHashSet<T>.OperationResult.NoChangeRequired ? new ImmutableHashSet<T>.MutationResult(origin.Root, 0) : new ImmutableHashSet<T>.MutationResult(ImmutableHashSet<T>.UpdateRoot(origin.Root, hashCode, origin.HashBucketEqualityComparer, newBucket), 1);
    }

    private static ImmutableHashSet<T>.MutationResult Remove(
      T item,
      ImmutableHashSet<T>.MutationInput origin)
    {
      ImmutableHashSet<T>.OperationResult result = ImmutableHashSet<T>.OperationResult.NoChangeRequired;
      int hashCode = (object) item != null ? origin.EqualityComparer.GetHashCode(item) : 0;
      SortedInt32KeyNode<ImmutableHashSet<T>.HashBucket> root = origin.Root;
      ImmutableHashSet<T>.HashBucket hashBucket;
      if (origin.Root.TryGetValue(hashCode, out hashBucket))
      {
        ImmutableHashSet<T>.HashBucket newBucket = hashBucket.Remove(item, origin.EqualityComparer, out result);
        if (result == ImmutableHashSet<T>.OperationResult.NoChangeRequired)
          return new ImmutableHashSet<T>.MutationResult(origin.Root, 0);
        root = ImmutableHashSet<T>.UpdateRoot(origin.Root, hashCode, origin.HashBucketEqualityComparer, newBucket);
      }
      return new ImmutableHashSet<T>.MutationResult(root, result == ImmutableHashSet<T>.OperationResult.SizeChanged ? -1 : 0);
    }

    private static bool Contains(T item, ImmutableHashSet<T>.MutationInput origin)
    {
      int hashCode = (object) item != null ? origin.EqualityComparer.GetHashCode(item) : 0;
      ImmutableHashSet<T>.HashBucket hashBucket;
      return origin.Root.TryGetValue(hashCode, out hashBucket) && hashBucket.Contains(item, origin.EqualityComparer);
    }

    private static ImmutableHashSet<T>.MutationResult Union(
      IEnumerable<T> other,
      ImmutableHashSet<T>.MutationInput origin)
    {
      Requires.NotNull<IEnumerable<T>>(other, nameof (other));
      int count = 0;
      SortedInt32KeyNode<ImmutableHashSet<T>.HashBucket> root = origin.Root;
      foreach (T obj in other.GetEnumerableDisposable<T, ImmutableHashSet<T>.Enumerator>())
      {
        int hashCode = (object) obj != null ? origin.EqualityComparer.GetHashCode(obj) : 0;
        ImmutableHashSet<T>.OperationResult result;
        ImmutableHashSet<T>.HashBucket newBucket = root.GetValueOrDefault(hashCode).Add(obj, origin.EqualityComparer, out result);
        if (result == ImmutableHashSet<T>.OperationResult.SizeChanged)
        {
          root = ImmutableHashSet<T>.UpdateRoot(root, hashCode, origin.HashBucketEqualityComparer, newBucket);
          ++count;
        }
      }
      return new ImmutableHashSet<T>.MutationResult(root, count);
    }

    private static bool Overlaps(IEnumerable<T> other, ImmutableHashSet<T>.MutationInput origin)
    {
      Requires.NotNull<IEnumerable<T>>(other, nameof (other));
      if (origin.Root.IsEmpty)
        return false;
      foreach (T obj in other.GetEnumerableDisposable<T, ImmutableHashSet<T>.Enumerator>())
      {
        if (ImmutableHashSet<T>.Contains(obj, origin))
          return true;
      }
      return false;
    }

    private static bool SetEquals(IEnumerable<T> other, ImmutableHashSet<T>.MutationInput origin)
    {
      Requires.NotNull<IEnumerable<T>>(other, nameof (other));
      HashSet<T> objSet = new HashSet<T>(other, origin.EqualityComparer);
      if (origin.Count != objSet.Count)
        return false;
      foreach (T obj in objSet)
      {
        if (!ImmutableHashSet<T>.Contains(obj, origin))
          return false;
      }
      return true;
    }

    private static SortedInt32KeyNode<ImmutableHashSet<T>.HashBucket> UpdateRoot(
      SortedInt32KeyNode<ImmutableHashSet<T>.HashBucket> root,
      int hashCode,
      IEqualityComparer<ImmutableHashSet<T>.HashBucket> hashBucketEqualityComparer,
      ImmutableHashSet<T>.HashBucket newBucket)
    {
      bool flag;
      return newBucket.IsEmpty ? root.Remove(hashCode, out flag) : root.SetItem(hashCode, newBucket, hashBucketEqualityComparer, out flag, out bool _);
    }

    private static ImmutableHashSet<T>.MutationResult Intersect(
      IEnumerable<T> other,
      ImmutableHashSet<T>.MutationInput origin)
    {
      Requires.NotNull<IEnumerable<T>>(other, nameof (other));
      SortedInt32KeyNode<ImmutableHashSet<T>.HashBucket> root = SortedInt32KeyNode<ImmutableHashSet<T>.HashBucket>.EmptyNode;
      int count = 0;
      foreach (T obj in other.GetEnumerableDisposable<T, ImmutableHashSet<T>.Enumerator>())
      {
        if (ImmutableHashSet<T>.Contains(obj, origin))
        {
          ImmutableHashSet<T>.MutationResult mutationResult = ImmutableHashSet<T>.Add(obj, new ImmutableHashSet<T>.MutationInput(root, origin.EqualityComparer, origin.HashBucketEqualityComparer, count));
          root = mutationResult.Root;
          count += mutationResult.Count;
        }
      }
      return new ImmutableHashSet<T>.MutationResult(root, count, ImmutableHashSet<T>.CountType.FinalValue);
    }

    private static ImmutableHashSet<T>.MutationResult Except(
      IEnumerable<T> other,
      IEqualityComparer<T> equalityComparer,
      IEqualityComparer<ImmutableHashSet<T>.HashBucket> hashBucketEqualityComparer,
      SortedInt32KeyNode<ImmutableHashSet<T>.HashBucket> root)
    {
      Requires.NotNull<IEnumerable<T>>(other, nameof (other));
      Requires.NotNull<IEqualityComparer<T>>(equalityComparer, nameof (equalityComparer));
      Requires.NotNull<SortedInt32KeyNode<ImmutableHashSet<T>.HashBucket>>(root, nameof (root));
      int count = 0;
      SortedInt32KeyNode<ImmutableHashSet<T>.HashBucket> root1 = root;
      foreach (T obj in other.GetEnumerableDisposable<T, ImmutableHashSet<T>.Enumerator>())
      {
        int hashCode = (object) obj != null ? equalityComparer.GetHashCode(obj) : 0;
        ImmutableHashSet<T>.HashBucket hashBucket;
        if (root1.TryGetValue(hashCode, out hashBucket))
        {
          ImmutableHashSet<T>.OperationResult result;
          ImmutableHashSet<T>.HashBucket newBucket = hashBucket.Remove(obj, equalityComparer, out result);
          if (result == ImmutableHashSet<T>.OperationResult.SizeChanged)
          {
            --count;
            root1 = ImmutableHashSet<T>.UpdateRoot(root1, hashCode, hashBucketEqualityComparer, newBucket);
          }
        }
      }
      return new ImmutableHashSet<T>.MutationResult(root1, count);
    }

    private static ImmutableHashSet<T>.MutationResult SymmetricExcept(
      IEnumerable<T> other,
      ImmutableHashSet<T>.MutationInput origin)
    {
      Requires.NotNull<IEnumerable<T>>(other, nameof (other));
      ImmutableHashSet<T> range = ImmutableHashSet.CreateRange<T>(origin.EqualityComparer, other);
      int count = 0;
      SortedInt32KeyNode<ImmutableHashSet<T>.HashBucket> root = SortedInt32KeyNode<ImmutableHashSet<T>.HashBucket>.EmptyNode;
      foreach (T obj in new ImmutableHashSet<T>.NodeEnumerable(origin.Root))
      {
        if (!range.Contains(obj))
        {
          ImmutableHashSet<T>.MutationResult mutationResult = ImmutableHashSet<T>.Add(obj, new ImmutableHashSet<T>.MutationInput(root, origin.EqualityComparer, origin.HashBucketEqualityComparer, count));
          root = mutationResult.Root;
          count += mutationResult.Count;
        }
      }
      foreach (T obj in range)
      {
        if (!ImmutableHashSet<T>.Contains(obj, origin))
        {
          ImmutableHashSet<T>.MutationResult mutationResult = ImmutableHashSet<T>.Add(obj, new ImmutableHashSet<T>.MutationInput(root, origin.EqualityComparer, origin.HashBucketEqualityComparer, count));
          root = mutationResult.Root;
          count += mutationResult.Count;
        }
      }
      return new ImmutableHashSet<T>.MutationResult(root, count, ImmutableHashSet<T>.CountType.FinalValue);
    }

    private static bool IsProperSubsetOf(
      IEnumerable<T> other,
      ImmutableHashSet<T>.MutationInput origin)
    {
      Requires.NotNull<IEnumerable<T>>(other, nameof (other));
      if (origin.Root.IsEmpty)
        return other.Any<T>();
      HashSet<T> objSet = new HashSet<T>(other, origin.EqualityComparer);
      if (origin.Count >= objSet.Count)
        return false;
      int num = 0;
      bool flag = false;
      foreach (T obj in objSet)
      {
        if (ImmutableHashSet<T>.Contains(obj, origin))
          ++num;
        else
          flag = true;
        if (num == origin.Count & flag)
          return true;
      }
      return false;
    }

    private static bool IsProperSupersetOf(
      IEnumerable<T> other,
      ImmutableHashSet<T>.MutationInput origin)
    {
      Requires.NotNull<IEnumerable<T>>(other, nameof (other));
      if (origin.Root.IsEmpty)
        return false;
      int num = 0;
      foreach (T obj in other.GetEnumerableDisposable<T, ImmutableHashSet<T>.Enumerator>())
      {
        ++num;
        if (!ImmutableHashSet<T>.Contains(obj, origin))
          return false;
      }
      return origin.Count > num;
    }

    private static bool IsSubsetOf(IEnumerable<T> other, ImmutableHashSet<T>.MutationInput origin)
    {
      Requires.NotNull<IEnumerable<T>>(other, nameof (other));
      if (origin.Root.IsEmpty)
        return true;
      HashSet<T> objSet = new HashSet<T>(other, origin.EqualityComparer);
      int num = 0;
      foreach (T obj in objSet)
      {
        if (ImmutableHashSet<T>.Contains(obj, origin))
          ++num;
      }
      return num == origin.Count;
    }

    private static ImmutableHashSet<T> Wrap(
      SortedInt32KeyNode<ImmutableHashSet<T>.HashBucket> root,
      IEqualityComparer<T> equalityComparer,
      int count)
    {
      Requires.NotNull<SortedInt32KeyNode<ImmutableHashSet<T>.HashBucket>>(root, nameof (root));
      Requires.NotNull<IEqualityComparer<T>>(equalityComparer, nameof (equalityComparer));
      Requires.Range(count >= 0, nameof (count));
      return new ImmutableHashSet<T>(root, equalityComparer, count);
    }

    private static IEqualityComparer<ImmutableHashSet<T>.HashBucket> GetHashBucketEqualityComparer(
      IEqualityComparer<T> valueComparer)
    {
      if (!ImmutableExtensions.IsValueType<T>())
        return ImmutableHashSet<T>.HashBucketByRefEqualityComparer.DefaultInstance;
      return valueComparer == EqualityComparer<T>.Default ? ImmutableHashSet<T>.HashBucketByValueEqualityComparer.DefaultInstance : (IEqualityComparer<ImmutableHashSet<T>.HashBucket>) new ImmutableHashSet<T>.HashBucketByValueEqualityComparer(valueComparer);
    }

    private ImmutableHashSet<T> Wrap(
      SortedInt32KeyNode<ImmutableHashSet<T>.HashBucket> root,
      int adjustedCountIfDifferentRoot)
    {
      return root == this._root ? this : new ImmutableHashSet<T>(root, this._equalityComparer, adjustedCountIfDifferentRoot);
    }

    private ImmutableHashSet<T> Union(IEnumerable<T> items, bool avoidWithComparer)
    {
      Requires.NotNull<IEnumerable<T>>(items, nameof (items));
      return this.IsEmpty && !avoidWithComparer && items is ImmutableHashSet<T> immutableHashSet ? immutableHashSet.WithComparer(this.KeyComparer) : ImmutableHashSet<T>.Union(items, this.Origin).Finalize(this);
    }

    private sealed class HashBucketByValueEqualityComparer : 
      IEqualityComparer<ImmutableHashSet<T>.HashBucket>
    {
      private static readonly IEqualityComparer<ImmutableHashSet<T>.HashBucket> s_defaultInstance = (IEqualityComparer<ImmutableHashSet<T>.HashBucket>) new ImmutableHashSet<T>.HashBucketByValueEqualityComparer((IEqualityComparer<T>) EqualityComparer<T>.Default);
      private readonly IEqualityComparer<T> _valueComparer;

      internal static IEqualityComparer<ImmutableHashSet<T>.HashBucket> DefaultInstance => ImmutableHashSet<T>.HashBucketByValueEqualityComparer.s_defaultInstance;

      internal HashBucketByValueEqualityComparer(IEqualityComparer<T> valueComparer)
      {
        Requires.NotNull<IEqualityComparer<T>>(valueComparer, nameof (valueComparer));
        this._valueComparer = valueComparer;
      }

      public bool Equals(ImmutableHashSet<T>.HashBucket x, ImmutableHashSet<T>.HashBucket y) => x.EqualsByValue(y, this._valueComparer);

      public int GetHashCode(ImmutableHashSet<T>.HashBucket obj) => throw new NotSupportedException();
    }

    private sealed class HashBucketByRefEqualityComparer : 
      IEqualityComparer<ImmutableHashSet<T>.HashBucket>
    {
      private static readonly IEqualityComparer<ImmutableHashSet<T>.HashBucket> s_defaultInstance = (IEqualityComparer<ImmutableHashSet<T>.HashBucket>) new ImmutableHashSet<T>.HashBucketByRefEqualityComparer();

      internal static IEqualityComparer<ImmutableHashSet<T>.HashBucket> DefaultInstance => ImmutableHashSet<T>.HashBucketByRefEqualityComparer.s_defaultInstance;

      private HashBucketByRefEqualityComparer()
      {
      }

      public bool Equals(ImmutableHashSet<T>.HashBucket x, ImmutableHashSet<T>.HashBucket y) => x.EqualsByRef(y);

      public int GetHashCode(ImmutableHashSet<T>.HashBucket obj) => throw new NotSupportedException();
    }


    #nullable enable
    /// <summary>Represents a hash set that mutates with little or no memory allocations and that can produce or build on immutable hash set instances very efficiently.
    /// 
    /// NuGet package: System.Collections.Immutable (about immutable collections and how to install)</summary>
    /// <typeparam name="T" />
    [DebuggerDisplay("Count = {Count}")]
    public sealed class Builder : 
      IReadOnlyCollection<T>,
      IEnumerable<T>,
      IEnumerable,
      ISet<T>,
      ICollection<T>
    {

      #nullable disable
      private SortedInt32KeyNode<ImmutableHashSet<T>.HashBucket> _root = SortedInt32KeyNode<ImmutableHashSet<T>.HashBucket>.EmptyNode;
      private IEqualityComparer<T> _equalityComparer;
      private readonly IEqualityComparer<ImmutableHashSet<T>.HashBucket> _hashBucketEqualityComparer;
      private int _count;
      private ImmutableHashSet<T> _immutable;
      private int _version;


      #nullable enable
      internal Builder(ImmutableHashSet<T> set)
      {
        Requires.NotNull<ImmutableHashSet<T>>(set, nameof (set));
        this._root = set._root;
        this._count = set._count;
        this._equalityComparer = set._equalityComparer;
        this._hashBucketEqualityComparer = set._hashBucketEqualityComparer;
        this._immutable = set;
      }

      /// <summary>Gets the number of elements contained in the immutable hash set.</summary>
      /// <returns>The number of elements contained in the immutable hash set.</returns>
      public int Count => this._count;

      /// <summary>Gets a value indicating whether the <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only.</summary>
      /// <returns>
      /// <see langword="true" /> if the <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only; otherwise, <see langword="false" />.</returns>
      bool ICollection<
      #nullable disable
      T>.IsReadOnly => false;


      #nullable enable
      /// <summary>Gets or sets the key comparer.</summary>
      /// <returns>The key comparer.</returns>
      public IEqualityComparer<T> KeyComparer
      {
        get => this._equalityComparer;
        set
        {
          Requires.NotNull<IEqualityComparer<T>>(value, nameof (value));
          if (value == this._equalityComparer)
            return;
          ImmutableHashSet<T>.MutationResult mutationResult = ImmutableHashSet<T>.Union((IEnumerable<T>) this, new ImmutableHashSet<T>.MutationInput(SortedInt32KeyNode<ImmutableHashSet<T>.HashBucket>.EmptyNode, value, this._hashBucketEqualityComparer, 0));
          this._immutable = (ImmutableHashSet<T>) null;
          this._equalityComparer = value;
          this.Root = mutationResult.Root;
          this._count = mutationResult.Count;
        }
      }

      internal int Version => this._version;

      private ImmutableHashSet<
      #nullable disable
      T>.MutationInput Origin => new ImmutableHashSet<T>.MutationInput(this.Root, this._equalityComparer, this._hashBucketEqualityComparer, this._count);


      #nullable enable
      private SortedInt32KeyNode<ImmutableHashSet<
      #nullable disable
      T>.HashBucket> Root
      {
        get => this._root;
        set
        {
          ++this._version;
          if (this._root == value)
            return;
          this._root = value;
          this._immutable = (ImmutableHashSet<T>) null;
        }
      }


      #nullable enable
      /// <summary>Returns an enumerator that iterates through the immutable hash set.</summary>
      /// <returns>An enumerator that can be used to iterate through the set.</returns>
      public ImmutableHashSet<
      #nullable disable
      T>.Enumerator GetEnumerator() => new ImmutableHashSet<T>.Enumerator(this._root, this);


      #nullable enable
      /// <summary>Creates an immutable hash set based on the contents of this instance.</summary>
      /// <returns>An immutable set.</returns>
      public ImmutableHashSet<T> ToImmutable() => this._immutable ?? (this._immutable = ImmutableHashSet<T>.Wrap(this._root, this._equalityComparer, this._count));

      /// <summary>Searches the set for a given value and returns the equal value it finds, if any.</summary>
      /// <param name="equalValue">The value for which to search.</param>
      /// <param name="actualValue">The value from the set that the search found, or the original value if the search yielded no match.</param>
      /// <returns>A value indicating whether the search was successful.</returns>
      public bool TryGetValue(T equalValue, out T actualValue)
      {
        ImmutableHashSet<T>.HashBucket hashBucket;
        if (this._root.TryGetValue((object) equalValue != null ? this._equalityComparer.GetHashCode(equalValue) : 0, out hashBucket))
          return hashBucket.TryExchange(equalValue, this._equalityComparer, out actualValue);
        actualValue = equalValue;
        return false;
      }

      /// <summary>Adds the specified item to the immutable hash set.</summary>
      /// <param name="item">The item to add.</param>
      /// <returns>
      /// <see langword="true" /> if the item did not already belong to the collection; otherwise, <see langword="false" />.</returns>
      public bool Add(T item)
      {
        ImmutableHashSet<T>.MutationResult result = ImmutableHashSet<T>.Add(item, this.Origin);
        this.Apply(result);
        return result.Count != 0;
      }

      /// <summary>Removes the first occurrence of a specific object from the immutable hash set.</summary>
      /// <param name="item">The object to remove from the set.</param>
      /// <exception cref="T:System.NotSupportedException">The set is read-only.</exception>
      /// <returns>
      /// <see langword="true" /> if <paramref name="item" /> was successfully removed from the set ; otherwise, <see langword="false" />. This method also returns <see langword="false" /> if <paramref name="item" /> is not found in the original set.</returns>
      public bool Remove(T item)
      {
        ImmutableHashSet<T>.MutationResult result = ImmutableHashSet<T>.Remove(item, this.Origin);
        this.Apply(result);
        return result.Count != 0;
      }

      /// <summary>Determines whether the immutable hash set contains a specific value.</summary>
      /// <param name="item">The object to locate in the hash set.</param>
      /// <returns>
      /// <see langword="true" /> if <paramref name="item" /> is found in the hash set ; otherwise, <see langword="false" />.</returns>
      public bool Contains(T item) => ImmutableHashSet<T>.Contains(item, this.Origin);

      /// <summary>Removes all items from the immutable hash set.</summary>
      /// <exception cref="T:System.NotSupportedException">The hash set is read-only.</exception>
      public void Clear()
      {
        this._count = 0;
        this.Root = SortedInt32KeyNode<ImmutableHashSet<T>.HashBucket>.EmptyNode;
      }

      /// <summary>Removes all elements in the specified collection from the current hash set.</summary>
      /// <param name="other">The collection of items to remove from the set.</param>
      public void ExceptWith(IEnumerable<T> other) => this.Apply(ImmutableHashSet<T>.Except(other, this._equalityComparer, this._hashBucketEqualityComparer, this._root));

      /// <summary>Modifies the current set so that it contains only elements that are also in a specified collection.</summary>
      /// <param name="other">The collection to compare to the current set.</param>
      public void IntersectWith(IEnumerable<T> other) => this.Apply(ImmutableHashSet<T>.Intersect(other, this.Origin));

      /// <summary>Determines whether the current set is a proper (strict) subset of a specified collection.</summary>
      /// <param name="other">The collection to compare to the current set.</param>
      /// <returns>
      /// <see langword="true" /> if the current set is a proper subset of <paramref name="other" />; otherwise, <see langword="false" />.</returns>
      public bool IsProperSubsetOf(IEnumerable<T> other) => ImmutableHashSet<T>.IsProperSubsetOf(other, this.Origin);

      /// <summary>Determines whether the current set is a proper (strict) superset of a specified collection.</summary>
      /// <param name="other">The collection to compare to the current set.</param>
      /// <returns>
      /// <see langword="true" /> if the current set is a proper superset of <paramref name="other" />; otherwise, <see langword="false" />.</returns>
      public bool IsProperSupersetOf(IEnumerable<T> other) => ImmutableHashSet<T>.IsProperSupersetOf(other, this.Origin);

      /// <summary>Determines whether the current set is a subset of a specified collection.</summary>
      /// <param name="other">The collection to compare to the current set.</param>
      /// <returns>
      /// <see langword="true" /> if the current set is a subset of <paramref name="other" />; otherwise, <see langword="false" />.</returns>
      public bool IsSubsetOf(IEnumerable<T> other) => ImmutableHashSet<T>.IsSubsetOf(other, this.Origin);

      /// <summary>Determines whether the current set is a superset of a specified collection.</summary>
      /// <param name="other">The collection to compare to the current set.</param>
      /// <returns>
      /// <see langword="true" /> if the current set is a superset of <paramref name="other" />; otherwise, <see langword="false" />.</returns>
      public bool IsSupersetOf(IEnumerable<T> other) => ImmutableHashSet<T>.IsSupersetOf(other, this.Origin);

      /// <summary>Determines whether the current set overlaps with the specified collection.</summary>
      /// <param name="other">The collection to compare to the current set.</param>
      /// <returns>
      /// <see langword="true" /> if the current set and <paramref name="other" /> share at least one common element; otherwise, <see langword="false" />.</returns>
      public bool Overlaps(IEnumerable<T> other) => ImmutableHashSet<T>.Overlaps(other, this.Origin);

      /// <summary>Determines whether the current set and the specified collection contain the same elements.</summary>
      /// <param name="other">The collection to compare to the current set.</param>
      /// <returns>
      /// <see langword="true" /> if the current set is equal to <paramref name="other" />; otherwise, <see langword="false" />.</returns>
      public bool SetEquals(IEnumerable<T> other) => this == other || ImmutableHashSet<T>.SetEquals(other, this.Origin);

      /// <summary>Modifies the current set so that it contains only elements that are present either in the current set or in the specified collection, but not both.</summary>
      /// <param name="other">The collection to compare to the current set.</param>
      public void SymmetricExceptWith(IEnumerable<T> other) => this.Apply(ImmutableHashSet<T>.SymmetricExcept(other, this.Origin));

      /// <summary>Modifies the current set so that it contains all elements that are present in both the current set and in the specified collection.</summary>
      /// <param name="other">The collection to compare to the current set.</param>
      public void UnionWith(IEnumerable<T> other) => this.Apply(ImmutableHashSet<T>.Union(other, this.Origin));


      #nullable disable
      /// <summary>Adds an item to the hash set.</summary>
      /// <param name="item">The object to add to the set.</param>
      /// <exception cref="T:System.NotSupportedException">The set is read-only.</exception>
      void ICollection<T>.Add(T item) => this.Add(item);

      /// <summary>Copies the elements of the hash set to an array, starting at a particular array index.</summary>
      /// <param name="array">The one-dimensional array that is the destination of the elements copied from the hash set. The array must have zero-based indexing.</param>
      /// <param name="arrayIndex">The zero-based index in <paramref name="array" /> at which copying begins.</param>
      void ICollection<T>.CopyTo(T[] array, int arrayIndex)
      {
        Requires.NotNull<T[]>(array, nameof (array));
        Requires.Range(arrayIndex >= 0, nameof (arrayIndex));
        Requires.Range(array.Length >= arrayIndex + this.Count, nameof (arrayIndex));
        foreach (T obj in this)
          array[arrayIndex++] = obj;
      }

      /// <summary>Returns an enumerator that iterates through the collection.</summary>
      /// <returns>An enumerator that can be used to iterate through the collection.</returns>
      IEnumerator<T> IEnumerable<T>.GetEnumerator() => (IEnumerator<T>) this.GetEnumerator();

      /// <summary>Returns an enumerator that iterates through a collection.</summary>
      /// <returns>An enumerator that can be used to iterate through the collection.</returns>
      IEnumerator IEnumerable.GetEnumerator() => (IEnumerator) this.GetEnumerator();

      private void Apply(ImmutableHashSet<T>.MutationResult result)
      {
        this.Root = result.Root;
        if (result.CountType == ImmutableHashSet<T>.CountType.Adjustment)
          this._count += result.Count;
        else
          this._count = result.Count;
      }
    }


    #nullable enable
    /// <summary>Enumerates the contents of the immutable hash set without allocating any memory.
    /// 
    /// NuGet package: System.Collections.Immutable (about immutable collections and how to install)</summary>
    /// <typeparam name="T" />
    public struct Enumerator : IEnumerator<T>, IDisposable, IEnumerator, IStrongEnumerator<T>
    {

      #nullable disable
      private readonly ImmutableHashSet<T>.Builder _builder;
      private SortedInt32KeyNode<ImmutableHashSet<T>.HashBucket>.Enumerator _mapEnumerator;
      private ImmutableHashSet<T>.HashBucket.Enumerator _bucketEnumerator;
      private int _enumeratingBuilderVersion;


      #nullable enable
      internal Enumerator(
        SortedInt32KeyNode<ImmutableHashSet<
        #nullable disable
        T>.HashBucket> root,

        #nullable enable
        ImmutableHashSet<
        #nullable disable
        T>.Builder
        #nullable enable
        ? builder = null)
      {
        this._builder = builder;
        this._mapEnumerator = new SortedInt32KeyNode<ImmutableHashSet<T>.HashBucket>.Enumerator(root);
        this._bucketEnumerator = new ImmutableHashSet<T>.HashBucket.Enumerator();
        this._enumeratingBuilderVersion = builder != null ? builder.Version : -1;
      }

      /// <summary>Gets the element at the current position of the enumerator.</summary>
      /// <returns>The element at the current position of the enumerator.</returns>
      public T Current
      {
        get
        {
          this._mapEnumerator.ThrowIfDisposed();
          return this._bucketEnumerator.Current;
        }
      }

      /// <summary>Gets the current element.</summary>
      /// <returns>The element in the collection at the current position of the enumerator.</returns>
      object? IEnumerator.Current => (object) this.Current;

      /// <summary>Advances the enumerator to the next element of the immutable hash set.</summary>
      /// <exception cref="T:System.InvalidOperationException">The hash set was modified after the enumerator was created.</exception>
      /// <returns>
      /// <see langword="true" /> if the enumerator was successfully advanced to the next element; <see langword="false" /> if the enumerator has passed the end of the hash set.</returns>
      public bool MoveNext()
      {
        this.ThrowIfChanged();
        if (this._bucketEnumerator.MoveNext())
          return true;
        if (!this._mapEnumerator.MoveNext())
          return false;
        this._bucketEnumerator = new ImmutableHashSet<T>.HashBucket.Enumerator(this._mapEnumerator.Current.Value);
        return this._bucketEnumerator.MoveNext();
      }

      /// <summary>Sets the enumerator to its initial position, which is before the first element in the hash set.</summary>
      /// <exception cref="T:System.InvalidOperationException">The hash set was modified after the enumerator was created.</exception>
      public void Reset()
      {
        this._enumeratingBuilderVersion = this._builder != null ? this._builder.Version : -1;
        this._mapEnumerator.Reset();
        this._bucketEnumerator.Dispose();
        this._bucketEnumerator = new ImmutableHashSet<T>.HashBucket.Enumerator();
      }

      /// <summary>Releases the resources used by the current instance of the <see cref="T:System.Collections.Immutable.ImmutableHashSet`1.Enumerator" /> class.</summary>
      public void Dispose()
      {
        this._mapEnumerator.Dispose();
        this._bucketEnumerator.Dispose();
      }

      private void ThrowIfChanged()
      {
        if (this._builder != null && this._builder.Version != this._enumeratingBuilderVersion)
          throw new InvalidOperationException();
      }
    }

    internal enum OperationResult
    {
      SizeChanged,
      NoChangeRequired,
    }

    internal readonly struct HashBucket
    {

      #nullable disable
      private readonly T _firstValue;
      private readonly ImmutableList<T>.Node _additionalElements;

      private HashBucket(T firstElement, ImmutableList<T>.Node additionalElements = null)
      {
        this._firstValue = firstElement;
        this._additionalElements = additionalElements ?? ImmutableList<T>.Node.EmptyNode;
      }

      internal bool IsEmpty => this._additionalElements == null;


      #nullable enable
      public ImmutableHashSet<
      #nullable disable
      T>.HashBucket.Enumerator GetEnumerator() => new ImmutableHashSet<T>.HashBucket.Enumerator(this);


      #nullable enable
      public override bool Equals(object? obj) => throw new NotSupportedException();

      public override int GetHashCode() => throw new NotSupportedException();

      internal bool EqualsByRef(ImmutableHashSet<
      #nullable disable
      T>.HashBucket other) => (object) this._firstValue == (object) other._firstValue && this._additionalElements == other._additionalElements;


      #nullable enable
      internal bool EqualsByValue(
        ImmutableHashSet<
        #nullable disable
        T>.HashBucket other,

        #nullable enable
        IEqualityComparer<T> valueComparer)
      {
        return valueComparer.Equals(this._firstValue, other._firstValue) && this._additionalElements == other._additionalElements;
      }

      internal ImmutableHashSet<
      #nullable disable
      T>.HashBucket Add(

        #nullable enable
        T value,
        IEqualityComparer<T> valueComparer,
        out ImmutableHashSet<
        #nullable disable
        T>.OperationResult result)
      {
        if (this.IsEmpty)
        {
          result = ImmutableHashSet<T>.OperationResult.SizeChanged;
          return new ImmutableHashSet<T>.HashBucket(value);
        }
        if (valueComparer.Equals(value, this._firstValue) || this._additionalElements.IndexOf(value, valueComparer) >= 0)
        {
          result = ImmutableHashSet<T>.OperationResult.NoChangeRequired;
          return this;
        }
        result = ImmutableHashSet<T>.OperationResult.SizeChanged;
        return new ImmutableHashSet<T>.HashBucket(this._firstValue, this._additionalElements.Add(value));
      }


      #nullable enable
      internal bool Contains(T value, IEqualityComparer<T> valueComparer)
      {
        if (this.IsEmpty)
          return false;
        return valueComparer.Equals(value, this._firstValue) || this._additionalElements.IndexOf(value, valueComparer) >= 0;
      }

      internal bool TryExchange(T value, IEqualityComparer<T> valueComparer, out T existingValue)
      {
        if (!this.IsEmpty)
        {
          if (valueComparer.Equals(value, this._firstValue))
          {
            existingValue = this._firstValue;
            return true;
          }
          int index = this._additionalElements.IndexOf(value, valueComparer);
          if (index >= 0)
          {
            existingValue = this._additionalElements.ItemRef(index);
            return true;
          }
        }
        existingValue = value;
        return false;
      }

      internal ImmutableHashSet<
      #nullable disable
      T>.HashBucket Remove(

        #nullable enable
        T value,
        IEqualityComparer<T> equalityComparer,
        out ImmutableHashSet<
        #nullable disable
        T>.OperationResult result)
      {
        if (this.IsEmpty)
        {
          result = ImmutableHashSet<T>.OperationResult.NoChangeRequired;
          return this;
        }
        if (equalityComparer.Equals(this._firstValue, value))
        {
          if (this._additionalElements.IsEmpty)
          {
            result = ImmutableHashSet<T>.OperationResult.SizeChanged;
            return new ImmutableHashSet<T>.HashBucket();
          }
          int count = this._additionalElements.Left.Count;
          result = ImmutableHashSet<T>.OperationResult.SizeChanged;
          return new ImmutableHashSet<T>.HashBucket(this._additionalElements.Key, this._additionalElements.RemoveAt(count));
        }
        int index = this._additionalElements.IndexOf(value, equalityComparer);
        if (index < 0)
        {
          result = ImmutableHashSet<T>.OperationResult.NoChangeRequired;
          return this;
        }
        result = ImmutableHashSet<T>.OperationResult.SizeChanged;
        return new ImmutableHashSet<T>.HashBucket(this._firstValue, this._additionalElements.RemoveAt(index));
      }

      internal void Freeze() => this._additionalElements?.Freeze();


      #nullable enable
      internal struct Enumerator : IEnumerator<T>, IDisposable, IEnumerator
      {

        #nullable disable
        private readonly ImmutableHashSet<T>.HashBucket _bucket;
        private bool _disposed;
        private ImmutableHashSet<T>.HashBucket.Enumerator.Position _currentPosition;
        private ImmutableList<T>.Enumerator _additionalEnumerator;


        #nullable enable
        internal Enumerator(ImmutableHashSet<
        #nullable disable
        T>.HashBucket bucket)
        {
          this._disposed = false;
          this._bucket = bucket;
          this._currentPosition = ImmutableHashSet<T>.HashBucket.Enumerator.Position.BeforeFirst;
          this._additionalEnumerator = new ImmutableList<T>.Enumerator();
        }


        #nullable enable
        object? IEnumerator.Current => (object) this.Current;

        public T Current
        {
          get
          {
            this.ThrowIfDisposed();
            switch (this._currentPosition)
            {
              case ImmutableHashSet<T>.HashBucket.Enumerator.Position.First:
                return this._bucket._firstValue;
              case ImmutableHashSet<T>.HashBucket.Enumerator.Position.Additional:
                return this._additionalEnumerator.Current;
              default:
                throw new InvalidOperationException();
            }
          }
        }

        public bool MoveNext()
        {
          this.ThrowIfDisposed();
          if (this._bucket.IsEmpty)
          {
            this._currentPosition = ImmutableHashSet<T>.HashBucket.Enumerator.Position.End;
            return false;
          }
          switch (this._currentPosition)
          {
            case ImmutableHashSet<T>.HashBucket.Enumerator.Position.BeforeFirst:
              this._currentPosition = ImmutableHashSet<T>.HashBucket.Enumerator.Position.First;
              return true;
            case ImmutableHashSet<T>.HashBucket.Enumerator.Position.First:
              if (this._bucket._additionalElements.IsEmpty)
              {
                this._currentPosition = ImmutableHashSet<T>.HashBucket.Enumerator.Position.End;
                return false;
              }
              this._currentPosition = ImmutableHashSet<T>.HashBucket.Enumerator.Position.Additional;
              this._additionalEnumerator = new ImmutableList<T>.Enumerator(this._bucket._additionalElements);
              return this._additionalEnumerator.MoveNext();
            case ImmutableHashSet<T>.HashBucket.Enumerator.Position.Additional:
              return this._additionalEnumerator.MoveNext();
            case ImmutableHashSet<T>.HashBucket.Enumerator.Position.End:
              return false;
            default:
              throw new InvalidOperationException();
          }
        }

        public void Reset()
        {
          this.ThrowIfDisposed();
          this._additionalEnumerator.Dispose();
          this._currentPosition = ImmutableHashSet<T>.HashBucket.Enumerator.Position.BeforeFirst;
        }

        public void Dispose()
        {
          this._disposed = true;
          this._additionalEnumerator.Dispose();
        }

        private void ThrowIfDisposed()
        {
          if (!this._disposed)
            return;
          Requires.FailObjectDisposed<ImmutableHashSet<T>.HashBucket.Enumerator>(this);
        }


        #nullable disable
        private enum Position
        {
          BeforeFirst,
          First,
          Additional,
          End,
        }
      }
    }

    private readonly struct MutationInput
    {
      private readonly SortedInt32KeyNode<ImmutableHashSet<T>.HashBucket> _root;
      private readonly IEqualityComparer<T> _equalityComparer;
      private readonly int _count;
      private readonly IEqualityComparer<ImmutableHashSet<T>.HashBucket> _hashBucketEqualityComparer;

      internal MutationInput(ImmutableHashSet<T> set)
      {
        Requires.NotNull<ImmutableHashSet<T>>(set, nameof (set));
        this._root = set._root;
        this._equalityComparer = set._equalityComparer;
        this._count = set._count;
        this._hashBucketEqualityComparer = set._hashBucketEqualityComparer;
      }

      internal MutationInput(
        SortedInt32KeyNode<ImmutableHashSet<T>.HashBucket> root,
        IEqualityComparer<T> equalityComparer,
        IEqualityComparer<ImmutableHashSet<T>.HashBucket> hashBucketEqualityComparer,
        int count)
      {
        Requires.NotNull<SortedInt32KeyNode<ImmutableHashSet<T>.HashBucket>>(root, nameof (root));
        Requires.NotNull<IEqualityComparer<T>>(equalityComparer, nameof (equalityComparer));
        Requires.Range(count >= 0, nameof (count));
        Requires.NotNull<IEqualityComparer<ImmutableHashSet<T>.HashBucket>>(hashBucketEqualityComparer, nameof (hashBucketEqualityComparer));
        this._root = root;
        this._equalityComparer = equalityComparer;
        this._count = count;
        this._hashBucketEqualityComparer = hashBucketEqualityComparer;
      }

      internal SortedInt32KeyNode<ImmutableHashSet<T>.HashBucket> Root => this._root;

      internal IEqualityComparer<T> EqualityComparer => this._equalityComparer;

      internal int Count => this._count;

      internal IEqualityComparer<ImmutableHashSet<T>.HashBucket> HashBucketEqualityComparer => this._hashBucketEqualityComparer;
    }

    private enum CountType
    {
      Adjustment,
      FinalValue,
    }

    private readonly struct MutationResult
    {
      private readonly SortedInt32KeyNode<ImmutableHashSet<T>.HashBucket> _root;
      private readonly int _count;
      private readonly ImmutableHashSet<T>.CountType _countType;

      internal MutationResult(
        SortedInt32KeyNode<ImmutableHashSet<T>.HashBucket> root,
        int count,
        ImmutableHashSet<T>.CountType countType = ImmutableHashSet<T>.CountType.Adjustment)
      {
        Requires.NotNull<SortedInt32KeyNode<ImmutableHashSet<T>.HashBucket>>(root, nameof (root));
        this._root = root;
        this._count = count;
        this._countType = countType;
      }

      internal SortedInt32KeyNode<ImmutableHashSet<T>.HashBucket> Root => this._root;

      internal int Count => this._count;

      internal ImmutableHashSet<T>.CountType CountType => this._countType;

      internal ImmutableHashSet<T> Finalize(ImmutableHashSet<T> priorSet)
      {
        Requires.NotNull<ImmutableHashSet<T>>(priorSet, nameof (priorSet));
        int count = this.Count;
        if (this.CountType == ImmutableHashSet<T>.CountType.Adjustment)
          count += priorSet._count;
        return priorSet.Wrap(this.Root, count);
      }
    }

    private readonly struct NodeEnumerable : IEnumerable<T>, IEnumerable
    {
      private readonly SortedInt32KeyNode<ImmutableHashSet<T>.HashBucket> _root;

      internal NodeEnumerable(
        SortedInt32KeyNode<ImmutableHashSet<T>.HashBucket> root)
      {
        Requires.NotNull<SortedInt32KeyNode<ImmutableHashSet<T>.HashBucket>>(root, nameof (root));
        this._root = root;
      }

      public ImmutableHashSet<T>.Enumerator GetEnumerator() => new ImmutableHashSet<T>.Enumerator(this._root);

      [ExcludeFromCodeCoverage]
      IEnumerator<T> IEnumerable<T>.GetEnumerator() => (IEnumerator<T>) this.GetEnumerator();

      [ExcludeFromCodeCoverage]
      IEnumerator IEnumerable.GetEnumerator() => (IEnumerator) this.GetEnumerator();
    }
  }
}
