﻿// Decompiled with JetBrains decompiler
// Type: System.Collections.Immutable.ImmutableList`1
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
using System.Runtime.CompilerServices;
using System.Threading;
using Datadog.System.Diagnostics.CodeAnalysis;
using Datadog.System.Linq;


#nullable enable
namespace Datadog.System.Collections.Immutable
{
    /// <summary>Represents an immutable list, which is a strongly typed list of objects that can be accessed by index.
    /// 
    /// NuGet package: System.Collections.Immutable (about immutable collections and how to install)</summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    [DebuggerDisplay("Count = {Count}")]
  [DebuggerTypeProxy(typeof (ImmutableEnumerableDebuggerProxy<>))]
  public sealed class ImmutableList<T> : 
    IImmutableList<T>,
    IReadOnlyList<T>,
    IReadOnlyCollection<T>,
    IEnumerable<T>,
    IEnumerable,
    IList<T>,
    ICollection<T>,
    IList,
    ICollection,
    IOrderedCollection<T>,
    IImmutableListQueries<T>,
    IStrongEnumerable<T, ImmutableList<T>.Enumerator>
  {
    /// <summary>Gets an empty immutable list.</summary>
    public static readonly ImmutableList<T> Empty = new ImmutableList<T>();

    #nullable disable
    private readonly ImmutableList<T>.Node _root;

    internal ImmutableList() => this._root = ImmutableList<T>.Node.EmptyNode;

    private ImmutableList(ImmutableList<T>.Node root)
    {
      Requires.NotNull<ImmutableList<T>.Node>(root, nameof (root));
      root.Freeze();
      this._root = root;
    }


    #nullable enable
    /// <summary>Removes all elements from the immutable list.</summary>
    /// <returns>An empty list that retains the same sort or unordered semantics that this instance has.</returns>
    public ImmutableList<T> Clear() => ImmutableList<T>.Empty;

    /// <summary>Searches the entire sorted list for an element using the default comparer and returns the zero-based index of the element.</summary>
    /// <param name="item">The object to locate. The value can be <see langword="null" /> for reference types.</param>
    /// <exception cref="T:System.InvalidOperationException">The default comparer cannot find a comparer implementation of the for type T.</exception>
    /// <returns>The zero-based index of item in the sorted List, if item is found; otherwise, a negative number that is the bitwise complement of the index of the next element that is larger than item or, if there is no larger element, the bitwise complement of <see cref="P:System.Collections.ICollection.Count" />.</returns>
    public int BinarySearch(T item) => this.BinarySearch(item, (IComparer<T>) null);

    /// <summary>Searches the entire sorted list for an element using the specified comparer and returns the zero-based index of the element.</summary>
    /// <param name="item">The object to locate. The value can be null for reference types.</param>
    /// <param name="comparer">The comparer implementation to use when comparing elements or null to use the default comparer.</param>
    /// <exception cref="T:System.InvalidOperationException">comparer is <see langword="null" />, and the default comparer cannot find an comparer implementation for type T.</exception>
    /// <returns>The zero-based index of item in the sorted List, if item is found; otherwise, a negative number that is the bitwise complement of the index of the next element that is larger than item or, if there is no larger element, the bitwise complement of <see cref="P:System.Collections.ICollection.Count" />.</returns>
    public int BinarySearch(T item, IComparer<T>? comparer) => this.BinarySearch(0, this.Count, item, comparer);

    /// <summary>Searches a range of elements in the sorted list for an element using the specified comparer and returns the zero-based index of the element.</summary>
    /// <param name="index">The zero-based starting index of the range to search.</param>
    /// <param name="count">The length of the range to search.</param>
    /// <param name="item">The object to locate. The value can be null for reference types.</param>
    /// <param name="comparer">The comparer implementation to use when comparing elements, or <see langword="null" /> to use the default comparer.</param>
    /// <exception cref="T:System.ArgumentOutOfRangeException">index is less than 0 or <paramref name="count" /> is less than 0.</exception>
    /// <exception cref="T:System.ArgumentException">index and <paramref name="count" /> do not denote a valid range in the list.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    /// <paramref name="comparer" /> is <see langword="null" />, and the default comparer cannot find an comparer implementation for type T.</exception>
    /// <returns>The zero-based index of item in the sorted list, if item is found; otherwise, a negative number that is the bitwise complement of the index of the next element that is larger than item or, if there is no larger element, the bitwise complement of <paramref name="count" />.</returns>
    public int BinarySearch(int index, int count, T item, IComparer<T>? comparer) => this._root.BinarySearch(index, count, item, comparer);

    /// <summary>Gets a value that indicates whether this list is empty.</summary>
    /// <returns>
    /// <see langword="true" /> if the list is empty; otherwise, <see langword="false" />.</returns>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public bool IsEmpty => this._root.IsEmpty;


    #nullable disable
    /// <summary>Retrieves an empty list that has the same sorting and ordering semantics as this instance.</summary>
    /// <returns>An empty list that has the same sorting and ordering semantics as this instance.</returns>
    IImmutableList<T> IImmutableList<T>.Clear() => (IImmutableList<T>) this.Clear();

    /// <summary>Gets the number of elements contained in the list.</summary>
    /// <returns>The number of elements in the list.</returns>
    public int Count => this._root.Count;


    #nullable enable
    /// <summary>See <see cref="T:System.Collections.ICollection" />.</summary>
    /// <returns>Object used for synchronizing access to the collection.</returns>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    object ICollection.SyncRoot => (object) this;

    /// <summary>This type is immutable, so it is always thread-safe. See the <see cref="T:System.Collections.ICollection" /> interface.</summary>
    /// <returns>Boolean value determining whether the collection is thread-safe.</returns>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    bool ICollection.IsSynchronized => true;

    /// <summary>Gets the element at the specified index of the list.</summary>
    /// <param name="index">The index of the element to retrieve.</param>
    /// <exception cref="T:System.IndexOutOfRangeException">In a get operation, <paramref name="index" /> is negative or not less than <see cref="P:System.Collections.Immutable.ImmutableList`1.Count" />.</exception>
    /// <returns>The element at the specified index.</returns>
    public T this[int index] => this._root.ItemRef(index);

    /// <summary>Gets a read-only reference to the element of the set at the given <paramref name="index" />.</summary>
    /// <param name="index">The 0-based index of the element in the set to return.</param>
    /// <exception cref="T:System.IndexOutOfRangeException">
    /// <paramref name="index" /> is negative or not less than <see cref="P:System.Collections.Immutable.ImmutableList`1.Count" />.</exception>
    /// <returns>A read-only reference to the element at the given position.</returns>
    public ref readonly T ItemRef(int index) => ref this._root.ItemRef(index);

    T IOrderedCollection<
    #nullable disable
    T>.this[int index] => this[index];


    #nullable enable
    /// <summary>Creates a list that has the same contents as this list and can be efficiently mutated across multiple operations using standard mutable interfaces.</summary>
    /// <returns>The created list with the same contents as this list.</returns>
    public ImmutableList<
    #nullable disable
    T>.Builder ToBuilder() => new ImmutableList<T>.Builder(this);


    #nullable enable
    /// <summary>Adds the specified object to the end of the immutable list.</summary>
    /// <param name="value">The object to add.</param>
    /// <returns>A new immutable list with the object added.</returns>
    public ImmutableList<T> Add(T value) => this.Wrap(this._root.Add(value));

    /// <summary>Adds the elements of the specified collection to the end of the immutable list.</summary>
    /// <param name="items">The collection whose elements will be added to the end of the list.</param>
    /// <returns>A new immutable list with the elements added.</returns>
    public ImmutableList<T> AddRange(IEnumerable<T> items)
    {
      Requires.NotNull<IEnumerable<T>>(items, nameof (items));
      return this.IsEmpty ? ImmutableList<T>.CreateRange(items) : this.Wrap(this._root.AddRange(items));
    }

    /// <summary>Inserts the specified object into the immutable list at the specified index.</summary>
    /// <param name="index">The zero-based index at which to insert the object.</param>
    /// <param name="item">The object to insert.</param>
    /// <returns>The new immutable list after the object is inserted.</returns>
    public ImmutableList<T> Insert(int index, T item)
    {
      Requires.Range(index >= 0 && index <= this.Count, nameof (index));
      return this.Wrap(this._root.Insert(index, item));
    }

    /// <summary>Inserts the elements of a collection into the immutable list at the specified index.</summary>
    /// <param name="index">The zero-based index at which to insert the elements.</param>
    /// <param name="items">The collection whose elements should be inserted.</param>
    /// <returns>The new immutable list after the elements are inserted.</returns>
    public ImmutableList<T> InsertRange(int index, IEnumerable<T> items)
    {
      Requires.Range(index >= 0 && index <= this.Count, nameof (index));
      Requires.NotNull<IEnumerable<T>>(items, nameof (items));
      return this.Wrap(this._root.InsertRange(index, items));
    }

    /// <summary>Removes the first occurrence of the specified object from this immutable list.</summary>
    /// <param name="value">The object to remove.</param>
    /// <returns>A new list with the object removed, or this list if the specified object is not in this list.</returns>
    public ImmutableList<T> Remove(T value) => this.Remove(value, (IEqualityComparer<T>) EqualityComparer<T>.Default);

    /// <summary>Removes the first occurrence of the object that matches the specified value from this immutable list.</summary>
    /// <param name="value">The value of the element to remove from the list.</param>
    /// <param name="equalityComparer">The equality comparer to use in the search.</param>
    /// <returns>A new list with the object removed, or this list if the specified object is not in this list.</returns>
    public ImmutableList<T> Remove(T value, IEqualityComparer<T>? equalityComparer)
    {
      int index = this.IndexOf<T>(value, equalityComparer);
      return index >= 0 ? this.RemoveAt(index) : this;
    }

    /// <summary>Removes a range of elements, starting from the specified index and containing the specified number of elements, from this immutable list.</summary>
    /// <param name="index">The starting index to begin removal.</param>
    /// <param name="count">The number of elements to remove.</param>
    /// <returns>A new list with the elements removed.</returns>
    public ImmutableList<T> RemoveRange(int index, int count)
    {
      Requires.Range(index >= 0 && index <= this.Count, nameof (index));
      Requires.Range(count >= 0 && index + count <= this.Count, nameof (count));
      ImmutableList<T>.Node root = this._root;
      int num = count;
      while (num-- > 0)
        root = root.RemoveAt(index);
      return this.Wrap(root);
    }

    /// <summary>Removes a range of elements from this immutable list.</summary>
    /// <param name="items">The collection whose elements should be removed if matches are found in this list.</param>
    /// <returns>A new list with the elements removed.</returns>
    public ImmutableList<T> RemoveRange(IEnumerable<T> items) => this.RemoveRange(items, (IEqualityComparer<T>) EqualityComparer<T>.Default);

    /// <summary>Removes the specified values from this list.</summary>
    /// <param name="items">The items to remove if matches are found in this list.</param>
    /// <param name="equalityComparer">The equality comparer to use in the search.</param>
    /// <returns>A new list with the elements removed.</returns>
    public ImmutableList<T> RemoveRange(IEnumerable<T> items, IEqualityComparer<T>? equalityComparer)
    {
      Requires.NotNull<IEnumerable<T>>(items, nameof (items));
      if (this.IsEmpty)
        return this;
      ImmutableList<T>.Node root = this._root;
      foreach (T obj in items.GetEnumerableDisposable<T, ImmutableList<T>.Enumerator>())
      {
        int index = root.IndexOf(obj, equalityComparer);
        if (index >= 0)
          root = root.RemoveAt(index);
      }
      return this.Wrap(root);
    }

    /// <summary>Removes the element at the specified index.</summary>
    /// <param name="index">The zero-based index of the element to remove.</param>
    /// <returns>A new list with the element removed.</returns>
    public ImmutableList<T> RemoveAt(int index)
    {
      Requires.Range(index >= 0 && index < this.Count, nameof (index));
      return this.Wrap(this._root.RemoveAt(index));
    }

    /// <summary>Removes all the elements that match the conditions defined by the specified predicate.</summary>
    /// <param name="match">The delegate that defines the conditions of the elements to remove.</param>
    /// <returns>The new list with the elements removed.</returns>
    public ImmutableList<T> RemoveAll(Predicate<T> match)
    {
      Requires.NotNull<Predicate<T>>(match, nameof (match));
      return this.Wrap(this._root.RemoveAll(match));
    }

    /// <summary>Replaces an element at a given position in the immutable list with the specified element.</summary>
    /// <param name="index">The position in the list of the element to replace.</param>
    /// <param name="value">The element to replace the old element with.</param>
    /// <returns>The new list with the replaced element, even if it is equal to the old element at that position.</returns>
    public ImmutableList<T> SetItem(int index, T value) => this.Wrap(this._root.ReplaceAt(index, value));

    /// <summary>Replaces the specified element in the immutable list with a new element.</summary>
    /// <param name="oldValue">The element to replace.</param>
    /// <param name="newValue">The element to replace <paramref name="oldValue" /> with.</param>
    /// <exception cref="T:System.ArgumentException">
    /// <paramref name="oldValue" /> does not exist in the immutable list.</exception>
    /// <returns>The new list with the replaced element, even if it is equal to the old element.</returns>
    public ImmutableList<T> Replace(T oldValue, T newValue) => this.Replace(oldValue, newValue, (IEqualityComparer<T>) EqualityComparer<T>.Default);

    /// <summary>Replaces the specified element in the immutable list with a new element.</summary>
    /// <param name="oldValue">The element to replace in the list.</param>
    /// <param name="newValue">The element to replace <paramref name="oldValue" /> with.</param>
    /// <param name="equalityComparer">The comparer to use to check for equality.</param>
    /// <returns>A new list with the object replaced, or this list if the specified object is not in this list.</returns>
    public ImmutableList<T> Replace(T oldValue, T newValue, IEqualityComparer<T>? equalityComparer)
    {
      int index = this.IndexOf<T>(oldValue, equalityComparer);
      return index >= 0 ? this.SetItem(index, newValue) : throw new ArgumentException( nameof (oldValue));
      //return index >= 0 ? this.SetItem(index, newValue) : throw new ArgumentException(SR.CannotFindOldValue, nameof (oldValue));
    }

    /// <summary>Reverses the order of the elements in the entire immutable list.</summary>
    /// <returns>The reversed list.</returns>
    public ImmutableList<T> Reverse() => this.Wrap(this._root.Reverse());

    /// <summary>Reverses the order of the elements in the specified range of the immutable list.</summary>
    /// <param name="index">The zero-based starting index of the range to reverse.</param>
    /// <param name="count">The number of elements in the range to reverse.</param>
    /// <returns>The reversed list.</returns>
    public ImmutableList<T> Reverse(int index, int count) => this.Wrap(this._root.Reverse(index, count));

    /// <summary>Sorts the elements in the entire immutable list using the default comparer.</summary>
    /// <returns>The sorted list.</returns>
    public ImmutableList<T> Sort() => this.Wrap(this._root.Sort());

    /// <summary>Sorts the elements in the entire immutable list using the specified comparer.</summary>
    /// <param name="comparison">The delegate to use when comparing elements.</param>
    /// <exception cref="T:System.ArgumentNullException">
    /// <paramref name="comparison" /> is <see langword="null" />.</exception>
    /// <returns>The sorted list.</returns>
    public ImmutableList<T> Sort(Comparison<T> comparison)
    {
      Requires.NotNull<Comparison<T>>(comparison, nameof (comparison));
      return this.Wrap(this._root.Sort(comparison));
    }

    /// <summary>Sorts the elements in the entire immutable list using the specified comparer.</summary>
    /// <param name="comparer">The implementation to use when comparing elements, or <see langword="null" /> to use the default comparer (<see cref="P:System.Collections.Generic.Comparer`1.Default" />).</param>
    /// <returns>The sorted list.</returns>
    public ImmutableList<T> Sort(IComparer<T>? comparer) => this.Wrap(this._root.Sort(comparer));

    /// <summary>Sorts a range of elements in the immutable list using the specified comparer.</summary>
    /// <param name="index">The zero-based starting index of the range to sort.</param>
    /// <param name="count">The length of the range to sort.</param>
    /// <param name="comparer">The implementation to use when comparing elements, or <see langword="null" /> to use the default comparer (<see cref="P:System.Collections.Generic.Comparer`1.Default" />).</param>
    /// <returns>The sorted list.</returns>
    public ImmutableList<T> Sort(int index, int count, IComparer<T>? comparer)
    {
      Requires.Range(index >= 0, nameof (index));
      Requires.Range(count >= 0, nameof (count));
      Requires.Range(index + count <= this.Count, nameof (count));
      return this.Wrap(this._root.Sort(index, count, comparer));
    }

    /// <summary>Performs the specified action on each element of the immutable list.</summary>
    /// <param name="action">The delegate to perform on each element of the immutable list.</param>
    public void ForEach(Action<T> action)
    {
      Requires.NotNull<Action<T>>(action, nameof (action));
      foreach (T obj in this)
        action(obj);
    }

    /// <summary>Copies the entire immutable list to a compatible one-dimensional array, starting at the beginning of the target array.</summary>
    /// <param name="array">The one-dimensional array that is the destination of the elements copied from the immutable list. The array must have zero-based indexing.</param>
    public void CopyTo(T[] array) => this._root.CopyTo(array);

    /// <summary>Copies the entire immutable list to a compatible one-dimensional array, starting at the specified index of the target array.</summary>
    /// <param name="array">The one-dimensional array that is the destination of the elements copied from the immutable list. The array must have zero-based indexing.</param>
    /// <param name="arrayIndex">The zero-based index in <paramref name="array" /> at which copying begins.</param>
    public void CopyTo(T[] array, int arrayIndex) => this._root.CopyTo(array, arrayIndex);

    /// <summary>Copies a range of elements from the immutable list to a compatible one-dimensional array, starting at the specified index of the target array.</summary>
    /// <param name="index">The zero-based index in the source immutable list at which copying begins.</param>
    /// <param name="array">The one-dimensional array that is the destination of the elements copied from the immutable list. The array must have zero-based indexing.</param>
    /// <param name="arrayIndex">The zero-based index in array at which copying begins.</param>
    /// <param name="count">The number of elements to copy.</param>
    public void CopyTo(int index, T[] array, int arrayIndex, int count) => this._root.CopyTo(index, array, arrayIndex, count);

    /// <summary>Creates a shallow copy of a range of elements in the source immutable list.</summary>
    /// <param name="index">The zero-based index at which the range starts.</param>
    /// <param name="count">The number of elements in the range.</param>
    /// <returns>A shallow copy of a range of elements in the source immutable list.</returns>
    public ImmutableList<T> GetRange(int index, int count)
    {
      Requires.Range(index >= 0, nameof (index));
      Requires.Range(count >= 0, nameof (count));
      Requires.Range(index + count <= this.Count, nameof (count));
      return this.Wrap(ImmutableList<T>.Node.NodeTreeFromList((IOrderedCollection<T>) this, index, count));
    }

    /// <summary>Converts the elements in the current immutable list to another type, and returns a list containing the converted elements.</summary>
    /// <param name="converter">A delegate that converts each element from one type to another type.</param>
    /// <typeparam name="TOutput">The type of the elements of the target array.</typeparam>
    /// <returns>A list of the target type containing the converted elements from the current <see cref="T:System.Collections.Immutable.ImmutableList`1" />.</returns>
    public ImmutableList<TOutput> ConvertAll<TOutput>(Func<T, TOutput> converter)
    {
      Requires.NotNull<Func<T, TOutput>>(converter, nameof (converter));
      return ImmutableList<TOutput>.WrapNode(this._root.ConvertAll<TOutput>(converter));
    }

    /// <summary>Determines whether the immutable list contains elements that match the conditions defined by the specified predicate.</summary>
    /// <param name="match">The delegate that defines the conditions of the elements to search for.</param>
    /// <returns>
    /// <see langword="true" /> if the immutable list contains one or more elements that match the conditions defined by the specified predicate; otherwise, <see langword="false" />.</returns>
    public bool Exists(Predicate<T> match) => this._root.Exists(match);

    /// <summary>Searches for an element that matches the conditions defined by the specified predicate, and returns the first occurrence within the entire immutable list.</summary>
    /// <param name="match">The delegate that defines the conditions of the element to search for.</param>
    /// <returns>The first element that matches the conditions defined by the specified predicate, if found; otherwise, the default value for type <paramref name="T" />.</returns>
    public T? Find(Predicate<T> match) => this._root.Find(match);

    /// <summary>Retrieves all the elements that match the conditions defined by the specified predicate.</summary>
    /// <param name="match">The delegate that defines the conditions of the elements to search for.</param>
    /// <returns>An immutable list that contains all the elements that match the conditions defined by the specified predicate, if found; otherwise, an empty immutable list.</returns>
    public ImmutableList<T> FindAll(Predicate<T> match) => this._root.FindAll(match);

    /// <summary>Searches for an element that matches the conditions defined by the specified predicate, and returns the zero-based index of the first occurrence within the entire immutable list.</summary>
    /// <param name="match">The delegate that defines the conditions of the element to search for.</param>
    /// <returns>The zero-based index of the first occurrence of an element that matches the conditions defined by match, if found; otherwise, -1.</returns>
    public int FindIndex(Predicate<T> match) => this._root.FindIndex(match);

    /// <summary>Searches for an element that matches the conditions defined by the specified predicate, and returns the zero-based index of the first occurrence within the range of elements in the immutable list that extends from the specified index to the last element.</summary>
    /// <param name="startIndex">The zero-based starting index of the search.</param>
    /// <param name="match">The delegate that defines the conditions of the element to search for.</param>
    /// <returns>The zero-based index of the first occurrence of an element that matches the conditions defined by match, if found; otherwise, ?1.</returns>
    public int FindIndex(int startIndex, Predicate<T> match) => this._root.FindIndex(startIndex, match);

    /// <summary>Searches for an element that matches the conditions defined by the specified predicate, and returns the zero-based index of the first occurrence within the range of elements in the immutable list that starts at the specified index and contains the specified number of elements.</summary>
    /// <param name="startIndex">The zero-based starting index of the search.</param>
    /// <param name="count">The number of elements in the section to search.</param>
    /// <param name="match">The delegate that defines the conditions of the element to search for.</param>
    /// <returns>The zero-based index of the first occurrence of an element that matches the conditions defined by match, if found; otherwise, ?1.</returns>
    public int FindIndex(int startIndex, int count, Predicate<T> match) => this._root.FindIndex(startIndex, count, match);

    /// <summary>Searches for an element that matches the conditions defined by the specified predicate, and returns the last occurrence within the entire immutable list.</summary>
    /// <param name="match">The delegate that defines the conditions of the element to search for.</param>
    /// <returns>The last element that matches the conditions defined by the specified predicate, if found; otherwise, the default value for type <paramref name="T" />.</returns>
    public T? FindLast(Predicate<T> match) => this._root.FindLast(match);

    /// <summary>Searches for an element that matches the conditions defined by the specified predicate, and returns the zero-based index of the last occurrence within the entire immutable list.</summary>
    /// <param name="match">The delegate that defines the conditions of the element to search for.</param>
    /// <returns>The zero-based index of the last occurrence of an element that matches the conditions defined by <paramref name="match" />, if found; otherwise, ?1.</returns>
    public int FindLastIndex(Predicate<T> match) => this._root.FindLastIndex(match);

    /// <summary>Searches for an element that matches the conditions defined by the specified predicate, and returns the zero-based index of the last occurrence within the range of elements in the immutable list that extends from the first element to the specified index.</summary>
    /// <param name="startIndex">The zero-based starting index of the backward search.</param>
    /// <param name="match">The delegate that defines the conditions of the element to search for.</param>
    /// <returns>The zero-based index of the last occurrence of an element that matches the conditions defined by <paramref name="match" />, if found; otherwise, ?1.</returns>
    public int FindLastIndex(int startIndex, Predicate<T> match) => this._root.FindLastIndex(startIndex, match);

    /// <summary>Searches for an element that matches the conditions defined by the specified predicate, and returns the zero-based index of the last occurrence within the range of elements in the immutable list that contains the specified number of elements and ends at the specified index.</summary>
    /// <param name="startIndex">The zero-based starting index of the backward search.</param>
    /// <param name="count">The number of elements in the section to search.</param>
    /// <param name="match">The delegate that defines the conditions of the element to search for.</param>
    /// <returns>The zero-based index of the last occurrence of an element that matches the conditions defined by <paramref name="match" />, if found; otherwise, ?1.</returns>
    public int FindLastIndex(int startIndex, int count, Predicate<T> match) => this._root.FindLastIndex(startIndex, count, match);

    /// <summary>Searches for the specified object and returns the zero-based index of the first occurrence within the range of elements in the list that starts at the specified index and contains the specified number of elements.</summary>
    /// <param name="item">The object to locate in the list The value can be null for reference types.</param>
    /// <param name="index">The zero-based starting index of the search. 0 (zero) is valid in an empty list.</param>
    /// <param name="count">The number of elements in the section to search.</param>
    /// <param name="equalityComparer">The equality comparer to use in the search.</param>
    /// <returns>The zero-based index of the first occurrence of item within the range of elements in the list that starts at index and contains count number of elements, if found; otherwise, -1.</returns>
    public int IndexOf(T item, int index, int count, IEqualityComparer<T>? equalityComparer) => this._root.IndexOf(item, index, count, equalityComparer);

    /// <summary>Searches for the specified object and returns the zero-based index of the last occurrence within the range of elements in the list that contains the specified number of elements and ends at the specified index.</summary>
    /// <param name="item">The object to locate in the list. The value can be null for reference types.</param>
    /// <param name="index">The zero-based starting index of the backward search.</param>
    /// <param name="count">The number of elements in the section to search.</param>
    /// <param name="equalityComparer">The equality comparer to use in the search.</param>
    /// <returns>The zero-based index of the last occurrence of item within the range of elements in the list that contains count number of elements and ends at index, if found; otherwise, -1.</returns>
    public int LastIndexOf(T item, int index, int count, IEqualityComparer<T>? equalityComparer) => this._root.LastIndexOf(item, index, count, equalityComparer);

    /// <summary>Determines whether every element in the immutable list matches the conditions defined by the specified predicate.</summary>
    /// <param name="match">The delegate that defines the conditions to check against the elements.</param>
    /// <returns>
    /// <see langword="true" /> if every element in the immutable list matches the conditions defined by the specified predicate; otherwise, <see langword="false" />. If the list has no elements, the return value is <see langword="true" />.</returns>
    public bool TrueForAll(Predicate<T> match) => this._root.TrueForAll(match);

    /// <summary>Determines whether this immutable list contains the specified value.</summary>
    /// <param name="value">The value to locate.</param>
    /// <returns>
    /// <see langword="true" /> if the list contains the specified value; otherwise, <see langword="false" />.</returns>
    public bool Contains(T value) => this._root.Contains(value, (IEqualityComparer<T>) EqualityComparer<T>.Default);

    /// <summary>Searches for the specified object and returns the zero-based index of the first occurrence within the entire immutable list.</summary>
    /// <param name="value">The object to locate in the immutable list. The value can be <see langword="null" /> for reference types.</param>
    /// <returns>The zero-based index of the first occurrence of <paramref name="value" /> within the entire immutable list, if found; otherwise, ?1.</returns>
    public int IndexOf(T value) => this.IndexOf<T>(value, (IEqualityComparer<T>) EqualityComparer<T>.Default);


    #nullable disable
    /// <summary>Adds the specified value to this immutable list.</summary>
    /// <param name="value">The value to add.</param>
    /// <returns>A new list with the element added.</returns>
    IImmutableList<T> IImmutableList<T>.Add(T value) => (IImmutableList<T>) this.Add(value);

    /// <summary>Adds the specified values to this immutable list.</summary>
    /// <param name="items">The values to add.</param>
    /// <returns>A new list with the elements added.</returns>
    IImmutableList<T> IImmutableList<T>.AddRange(IEnumerable<T> items) => (IImmutableList<T>) this.AddRange(items);

    /// <summary>Inserts the specified element at the specified index in the immutable list.</summary>
    /// <param name="index">The index at which to insert the value.</param>
    /// <param name="item">The element to insert.</param>
    /// <returns>A new immutable list that includes the specified element.</returns>
    IImmutableList<T> IImmutableList<T>.Insert(int index, T item) => (IImmutableList<T>) this.Insert(index, item);

    /// <summary>Inserts the specified elements at the specified index in the immutable list.</summary>
    /// <param name="index">The index at which to insert the elements.</param>
    /// <param name="items">The elements to insert.</param>
    /// <returns>A new immutable list that includes the specified elements.</returns>
    IImmutableList<T> IImmutableList<T>.InsertRange(int index, IEnumerable<T> items) => (IImmutableList<T>) this.InsertRange(index, items);

    /// <summary>Removes the element with the specified value from the list.</summary>
    /// <param name="value">The value of the element to remove from the list.</param>
    /// <param name="equalityComparer">The comparer to use to compare elements for equality.</param>
    /// <returns>A new <see cref="T:System.Collections.Immutable.ImmutableList`1" /> with the specified element removed.</returns>
    IImmutableList<T> IImmutableList<T>.Remove(T value, IEqualityComparer<T> equalityComparer) => (IImmutableList<T>) this.Remove(value, equalityComparer);

    /// <summary>Removes all the elements that match the conditions defined by the specified predicate.</summary>
    /// <param name="match">The delegate that defines the conditions of the elements to remove.</param>
    /// <returns>A new immutable list with the elements removed.</returns>
    IImmutableList<T> IImmutableList<T>.RemoveAll(Predicate<T> match) => (IImmutableList<T>) this.RemoveAll(match);

    /// <summary>Removes a range of elements from this immutable list that match the items specified.</summary>
    /// <param name="items">The range of items to remove from the list, if found.</param>
    /// <param name="equalityComparer">The equality comparer to use to compare elements.</param>
    /// <exception cref="T:System.ArgumentNullException">
    /// <paramref name="items" /> or <paramref name="equalityComparer" /> is <see langword="null" />.</exception>
    /// <returns>An immutable list with the items removed.</returns>
    IImmutableList<T> IImmutableList<T>.RemoveRange(
      IEnumerable<T> items,
      IEqualityComparer<T> equalityComparer)
    {
      return (IImmutableList<T>) this.RemoveRange(items, equalityComparer);
    }

    /// <summary>Removes the specified number of elements at the specified location from this list.</summary>
    /// <param name="index">The starting index of the range of elements to remove.</param>
    /// <param name="count">The number of elements to remove.</param>
    /// <returns>A new list with the elements removed.</returns>
    IImmutableList<T> IImmutableList<T>.RemoveRange(int index, int count) => (IImmutableList<T>) this.RemoveRange(index, count);

    /// <summary>Removes the element at the specified index of the immutable list.</summary>
    /// <param name="index">The index of the element to remove.</param>
    /// <returns>A new list with the element removed.</returns>
    IImmutableList<T> IImmutableList<T>.RemoveAt(int index) => (IImmutableList<T>) this.RemoveAt(index);

    /// <summary>Replaces an element in the list at a given position with the specified element.</summary>
    /// <param name="index">The position in the list of the element to replace.</param>
    /// <param name="value">The element to replace the old element with.</param>
    /// <returns>The new list.</returns>
    IImmutableList<T> IImmutableList<T>.SetItem(int index, T value) => (IImmutableList<T>) this.SetItem(index, value);

    /// <summary>Replaces an element in the list with the specified element.</summary>
    /// <param name="oldValue">The element to replace.</param>
    /// <param name="newValue">The element to replace the old element with.</param>
    /// <param name="equalityComparer">The equality comparer to use in the search.</param>
    /// <exception cref="T:System.ArgumentException">
    /// <paramref name="oldValue" /> does not exist in the list.</exception>
    /// <returns>The new list.</returns>
    IImmutableList<T> IImmutableList<T>.Replace(
      T oldValue,
      T newValue,
      IEqualityComparer<T> equalityComparer)
    {
      return (IImmutableList<T>) this.Replace(oldValue, newValue, equalityComparer);
    }

    /// <summary>Returns an enumerator that iterates through the immutable list.</summary>
    /// <returns>An enumerator that can be used to iterate through the list.</returns>
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => !this.IsEmpty ? (IEnumerator<T>) this.GetEnumerator() : Enumerable.Empty<T>().GetEnumerator();

    /// <summary>Returns an enumerator that iterates through the immutable list.</summary>
    /// <returns>An enumerator that can be used to iterate through the list.</returns>
    IEnumerator IEnumerable.GetEnumerator() => (IEnumerator) this.GetEnumerator();

    /// <summary>Inserts an object in the immutable list at the specified index.</summary>
    /// <param name="index">The zero-based index at which <paramref name="item" /> should be inserted.</param>
    /// <param name="item">The object to insert.</param>
    /// <exception cref="T:System.NotSupportedException" />
    void IList<T>.Insert(int index, T item) => throw new NotSupportedException();

    /// <summary>Removes the value at the specified index.</summary>
    /// <param name="index">The zero-based index of the item to remove.</param>
    /// <exception cref="T:System.NotSupportedException" />
    void IList<T>.RemoveAt(int index) => throw new NotSupportedException();


    #nullable enable
    /// <summary>Gets or sets the value at the specified index.</summary>
    /// <param name="index">The zero-based index of the item to access.</param>
    /// <exception cref="T:System.IndexOutOfRangeException">Thrown from getter when <paramref name="index" /> is negative or not less than <see cref="P:System.Collections.Immutable.ImmutableList`1.Count" />.</exception>
    /// <exception cref="T:System.NotSupportedException">Always thrown from the setter.</exception>
    /// <returns>Value stored in the specified index.</returns>
    T IList<
    #nullable disable
    T>.this[int index]
    {
      get => this[index];
      set => throw new NotSupportedException();
    }

    /// <summary>Adds the specified item to the immutable list.</summary>
    /// <param name="item">The item to add.</param>
    /// <exception cref="T:System.NotSupportedException">Always thrown.</exception>
    void ICollection<T>.Add(T item) => throw new NotSupportedException();

    /// <summary>Removes all items from the immutable list.</summary>
    /// <exception cref="T:System.NotSupportedException" />
    void ICollection<T>.Clear() => throw new NotSupportedException();

    /// <summary>Gets a value indicating whether the <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only.</summary>
    /// <returns>
    /// <see langword="true" /> if the <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only; otherwise, <see langword="false" />.</returns>
    bool ICollection<T>.IsReadOnly => true;

    /// <summary>Removes the first occurrence of a specific object from the immutable list.</summary>
    /// <param name="item">The object to remove.</param>
    /// <exception cref="T:System.NotSupportedException">Always thrown.</exception>
    /// <returns>
    /// <see langword="true" /> if <paramref name="item" /> was successfully removed from the list; otherwise, <see langword="false" />. This method also returns <see langword="false" /> if <paramref name="item" /> is not found in the original list.</returns>
    bool ICollection<T>.Remove(T item) => throw new NotSupportedException();

    /// <summary>Copies the entire immutable list to a compatible one-dimensional array, starting at the specified array index.</summary>
    /// <param name="array">The one-dimensional array that is the destination of the elements copied from immutable list.</param>
    /// <param name="arrayIndex">The zero-based index in <paramref name="array" /> at which copying begins.</param>
    void ICollection.CopyTo(Array array, int arrayIndex) => this._root.CopyTo(array, arrayIndex);

    /// <summary>Adds an item to the immutable list.</summary>
    /// <param name="value">The object to add to the list.</param>
    /// <exception cref="T:System.NotSupportedException">Always thrown.</exception>
    /// <returns>The position into which the new element was inserted, or -1 to indicate that the item was not inserted into the list.</returns>
    int IList.Add(object value) => throw new NotSupportedException();

    /// <summary>Removes the item at the specified index of the immutable list.</summary>
    /// <param name="index">The zero-based index of the item to remove.</param>
    /// <exception cref="T:System.NotSupportedException">Always thrown.</exception>
    void IList.RemoveAt(int index) => throw new NotSupportedException();

    /// <summary>Removes all items from the immutable list.</summary>
    /// <exception cref="T:System.NotSupportedException">Always thrown.</exception>
    void IList.Clear() => throw new NotSupportedException();

    /// <summary>Determines whether the immutable list contains a specific value.</summary>
    /// <param name="value">The object to locate in the list.</param>
    /// <exception cref="T:System.NotImplementedException" />
    /// <returns>
    /// <see langword="true" /> if the object is found in the list; otherwise, <see langword="false" />.</returns>
    bool IList.Contains(object value) => ImmutableList<T>.IsCompatibleObject(value) && this.Contains((T) value);

    /// <summary>Determines the index of a specific item in the immutable list.</summary>
    /// <param name="value">The object to locate in the list.</param>
    /// <exception cref="T:System.NotImplementedException" />
    /// <returns>The index of <paramref name="value" /> if found in the list; otherwise, -1.</returns>
    int IList.IndexOf(object value) => !ImmutableList<T>.IsCompatibleObject(value) ? -1 : this.IndexOf((T) value);

    /// <summary>Inserts an item into the immutable list at the specified index.</summary>
    /// <param name="index">The zero-based index at which <paramref name="value" /> should be inserted.</param>
    /// <param name="value">The object to insert into the list.</param>
    /// <exception cref="T:System.NotSupportedException">Always thrown.</exception>
    void IList.Insert(int index, object value) => throw new NotSupportedException();

    /// <summary>Gets a value indicating whether the <see cref="T:System.Collections.IList" /> has a fixed size.</summary>
    /// <returns>
    /// <see langword="true" /> if the <see cref="T:System.Collections.IList" /> has a fixed size; otherwise, <see langword="false" />.</returns>
    bool IList.IsFixedSize => true;

    /// <summary>Gets a value indicating whether the <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only.</summary>
    /// <returns>
    /// <see langword="true" /> if the <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only; otherwise, <see langword="false" />.</returns>
    bool IList.IsReadOnly => true;

    /// <summary>Removes the first occurrence of a specific object from the immutable list.</summary>
    /// <param name="value">The object to remove from the list.</param>
    /// <exception cref="T:System.NotSupportedException">Always thrown.</exception>
    void IList.Remove(object value) => throw new NotSupportedException();


    #nullable enable
    /// <summary>Gets or sets the <see cref="T:System.Object" /> at the specified index.</summary>
    /// <param name="index">The index.</param>
    /// <exception cref="T:System.IndexOutOfRangeException">Thrown from getter when <paramref name="index" /> is negative or not less than <see cref="P:System.Collections.Immutable.ImmutableList`1.Count" />.</exception>
    /// <exception cref="T:System.NotSupportedException">Always thrown from the setter.</exception>
    /// <returns>The value at the specified index.</returns>
    object? IList.this[int index]
    {
      get => (object) this[index];
      set => throw new NotSupportedException();
    }

    /// <summary>Returns an enumerator that iterates through the immutable list.</summary>
    /// <returns>An enumerator  that can be used to iterate through the immutable list.</returns>
    public ImmutableList<
    #nullable disable
    T>.Enumerator GetEnumerator() => new ImmutableList<T>.Enumerator(this._root);


    #nullable enable
    internal ImmutableList<
    #nullable disable
    T>.Node Root => this._root;

    private static ImmutableList<T> WrapNode(ImmutableList<T>.Node root) => !root.IsEmpty ? new ImmutableList<T>(root) : ImmutableList<T>.Empty;

    private static bool TryCastToImmutableList(IEnumerable<T> sequence, [NotNullWhen(true)] out ImmutableList<T> other)
    {
      other = sequence as ImmutableList<T>;
      if (other != null)
        return true;
      if (!(sequence is ImmutableList<T>.Builder builder))
        return false;
      other = builder.ToImmutable();
      return true;
    }

    private static bool IsCompatibleObject(object value)
    {
      if (value is T)
        return true;
      return value == null && (object) default (T) == null;
    }

    private ImmutableList<T> Wrap(ImmutableList<T>.Node root)
    {
      if (root == this._root)
        return this;
      return !root.IsEmpty ? new ImmutableList<T>(root) : this.Clear();
    }

    private static ImmutableList<T> CreateRange(IEnumerable<T> items)
    {
      ImmutableList<T> other;
      if (ImmutableList<T>.TryCastToImmutableList(items, out other))
        return other;
      IOrderedCollection<T> items1 = items.AsOrderedCollection<T>();
      return items1.Count == 0 ? ImmutableList<T>.Empty : new ImmutableList<T>(ImmutableList<T>.Node.NodeTreeFromList(items1, 0, items1.Count));
    }


    #nullable enable
    /// <summary>Represents a list that mutates with little or no memory allocations and that can produce or build on immutable list instances very efficiently.
    /// 
    /// NuGet package: System.Collections.Immutable (about immutable collections and how to install)</summary>
    /// <typeparam name="T" />
    [DebuggerDisplay("Count = {Count}")]
    [DebuggerTypeProxy(typeof (ImmutableListBuilderDebuggerProxy<>))]
    public sealed class Builder : 
      IList<T>,
      ICollection<T>,
      IEnumerable<T>,
      IEnumerable,
      IList,
      ICollection,
      IOrderedCollection<T>,
      IImmutableListQueries<T>,
      IReadOnlyList<T>,
      IReadOnlyCollection<T>
    {

      #nullable disable
      private ImmutableList<T>.Node _root = ImmutableList<T>.Node.EmptyNode;
      private ImmutableList<T> _immutable;
      private int _version;
      private object _syncRoot;


      #nullable enable
      internal Builder(ImmutableList<T> list)
      {
        Requires.NotNull<ImmutableList<T>>(list, nameof (list));
        this._root = list._root;
        this._immutable = list;
      }

      /// <summary>Gets the number of elements in this immutable list.</summary>
      /// <returns>The number of elements in this list.</returns>
      public int Count => this.Root.Count;

      /// <summary>Gets a value that indicates whether this instance is read-only.</summary>
      /// <returns>Always <see langword="false" />.</returns>
      bool ICollection<
      #nullable disable
      T>.IsReadOnly => false;

      internal int Version => this._version;


      #nullable enable
      internal ImmutableList<
      #nullable disable
      T>.Node Root
      {
        get => this._root;
        private set
        {
          ++this._version;
          if (this._root == value)
            return;
          this._root = value;
          this._immutable = (ImmutableList<T>) null;
        }
      }


      #nullable enable
      /// <summary>Gets or sets the value for a given index in the list.</summary>
      /// <param name="index">The index of the item to get or set.</param>
      /// <returns>The value at the specified index.</returns>
      public T this[int index]
      {
        get => this.Root.ItemRef(index);
        set => this.Root = this.Root.ReplaceAt(index, value);
      }

      T IOrderedCollection<
      #nullable disable
      T>.this[int index] => this[index];


      #nullable enable
      /// <summary>Gets a read-only reference to the value for a given <paramref name="index" /> into the list.</summary>
      /// <param name="index">The index of the desired element.</param>
      /// <returns>A read-only reference to the value at the specified <paramref name="index" />.</returns>
      public ref readonly T ItemRef(int index) => ref this.Root.ItemRef(index);

      /// <summary>Searches for the specified object and returns the zero-based index of the first occurrence within the range of elements in the immutable list.</summary>
      /// <param name="item">The object to locate in the immutable list. The value can be <see langword="null" /> for reference types.</param>
      /// <returns>The zero-based index of the first occurrence of <paramref name="item" /> within the range of elements in the immutable list, if found; otherwise, -1.</returns>
      public int IndexOf(T item) => this.Root.IndexOf(item, (IEqualityComparer<T>) EqualityComparer<T>.Default);

      /// <summary>Inserts an item to the immutable list at the specified index.</summary>
      /// <param name="index">The zero-based index at which <paramref name="item" /> should be inserted.</param>
      /// <param name="item">The object to insert into the immutable list.</param>
      public void Insert(int index, T item) => this.Root = this.Root.Insert(index, item);

      /// <summary>Removes the item at the specified index of the immutable list.</summary>
      /// <param name="index">The zero-based index of the item to remove from the list.</param>
      public void RemoveAt(int index) => this.Root = this.Root.RemoveAt(index);

      /// <summary>Adds an item to the immutable list.</summary>
      /// <param name="item">The item to add to the list.</param>
      public void Add(T item) => this.Root = this.Root.Add(item);

      /// <summary>Removes all items from the immutable list.</summary>
      public void Clear() => this.Root = ImmutableList<T>.Node.EmptyNode;

      /// <summary>Determines whether the immutable list contains a specific value.</summary>
      /// <param name="item">The object to locate in the list.</param>
      /// <returns>
      /// <see langword="true" /> if item is found in the list; otherwise, <see langword="false" />.</returns>
      public bool Contains(T item) => this.IndexOf(item) >= 0;

      /// <summary>Removes the first occurrence of a specific object from the immutable list.</summary>
      /// <param name="item">The object to remove from the list.</param>
      /// <returns>
      /// <see langword="true" /> if item was successfully removed from the list; otherwise, <see langword="false" />. This method also returns <see langword="false" /> if item is not found in the list.</returns>
      public bool Remove(T item)
      {
        int index = this.IndexOf(item);
        if (index < 0)
          return false;
        this.Root = this.Root.RemoveAt(index);
        return true;
      }

      /// <summary>Returns an enumerator that iterates through the collection.</summary>
      /// <returns>An enumerator that can be used to iterate through the list.</returns>
      public ImmutableList<T>.Enumerator GetEnumerator() => this.Root.GetEnumerator(this);


      #nullable disable
      /// <summary>Returns an enumerator that iterates through the collection.</summary>
      /// <returns>An enumerator that can be used to iterate through the collection.</returns>
      IEnumerator<T> IEnumerable<T>.GetEnumerator() => (IEnumerator<T>) this.GetEnumerator();

      /// <summary>Returns an enumerator that iterates through the collection.</summary>
      /// <returns>An enumerator that can be used to iterate through the collection.</returns>
      IEnumerator IEnumerable.GetEnumerator() => (IEnumerator) this.GetEnumerator();


      #nullable enable
      /// <summary>Performs the specified action on each element of the list.</summary>
      /// <param name="action">The delegate to perform on each element of the list.</param>
      public void ForEach(Action<T> action)
      {
        Requires.NotNull<Action<T>>(action, nameof (action));
        foreach (T obj in this)
          action(obj);
      }

      /// <summary>Copies the entire immutable list to a compatible one-dimensional array, starting at the beginning of the target array.</summary>
      /// <param name="array">The one-dimensional array that is the destination of the elements copied from the immutable list. The array must have zero-based indexing.</param>
      public void CopyTo(T[] array) => this._root.CopyTo(array);

      /// <summary>Copies the entire immutable list to a compatible one-dimensional array, starting at the specified index of the target array.</summary>
      /// <param name="array">The one-dimensional array that is the destination of the elements copied from the immutable list. The array must have zero-based indexing.</param>
      /// <param name="arrayIndex">The zero-based index in array at which copying begins.</param>
      public void CopyTo(T[] array, int arrayIndex) => this._root.CopyTo(array, arrayIndex);

      /// <summary>Copies the entire immutable list to a compatible one-dimensional array, starting at the specified index of the target array.</summary>
      /// <param name="index">The zero-based index in the source immutable list at which copying begins.</param>
      /// <param name="array">The one-dimensional array that is the destination of the elements copied from the immutable list. The array must have zero-based indexing.</param>
      /// <param name="arrayIndex">The zero-based index in <paramref name="array" /> at which copying begins.</param>
      /// <param name="count">The number of elements to copy.</param>
      public void CopyTo(int index, T[] array, int arrayIndex, int count) => this._root.CopyTo(index, array, arrayIndex, count);

      /// <summary>Creates a shallow copy of a range of elements in the source immutable list.</summary>
      /// <param name="index">The zero-based index at which the range starts.</param>
      /// <param name="count">The number of elements in the range.</param>
      /// <returns>A shallow copy of a range of elements in the source immutable list.</returns>
      public ImmutableList<T> GetRange(int index, int count)
      {
        Requires.Range(index >= 0, nameof (index));
        Requires.Range(count >= 0, nameof (count));
        Requires.Range(index + count <= this.Count, nameof (count));
        return ImmutableList<T>.WrapNode(ImmutableList<T>.Node.NodeTreeFromList((IOrderedCollection<T>) this, index, count));
      }

      /// <summary>Creates a new immutable list from the list represented by this builder by using the converter function.</summary>
      /// <param name="converter">The converter function.</param>
      /// <typeparam name="TOutput">The type of the output of the delegate converter function.</typeparam>
      /// <returns>A new immutable list from the list represented by this builder.</returns>
      public ImmutableList<TOutput> ConvertAll<TOutput>(Func<T, TOutput> converter)
      {
        Requires.NotNull<Func<T, TOutput>>(converter, nameof (converter));
        return ImmutableList<TOutput>.WrapNode(this._root.ConvertAll<TOutput>(converter));
      }

      /// <summary>Determines whether the immutable list contains elements that match the conditions defined by the specified predicate.</summary>
      /// <param name="match">The delegate that defines the conditions of the elements to search for.</param>
      /// <returns>
      /// <see langword="true" /> if the immutable list contains one or more elements that match the conditions defined by the specified predicate; otherwise, <see langword="false" />.</returns>
      public bool Exists(Predicate<T> match) => this._root.Exists(match);

      /// <summary>Searches for an element that matches the conditions defined by the specified predicate, and returns the first occurrence within the entire immutable list.</summary>
      /// <param name="match">The delegate that defines the conditions of the element to search for.</param>
      /// <returns>The first element that matches the conditions defined by the specified predicate, if found; otherwise, the default value for type <paramref name="T" />.</returns>
      public T? Find(Predicate<T> match) => this._root.Find(match);

      /// <summary>Retrieves all the elements that match the conditions defined by the specified predicate.</summary>
      /// <param name="match">The delegate that defines the conditions of the elements to search for.</param>
      /// <returns>An immutable list containing all the elements that match the conditions defined by the specified predicate, if found; otherwise, an empty immutable list.</returns>
      public ImmutableList<T> FindAll(Predicate<T> match) => this._root.FindAll(match);

      /// <summary>Searches for an element that matches the conditions defined by the specified predicate, and returns the zero-based index of the first occurrence within the entire immutable list.</summary>
      /// <param name="match">The delegate that defines the conditions of the element to search for.</param>
      /// <returns>The zero-based index of the first occurrence of an element that matches the conditions defined by <paramref name="match" />, if found; otherwise, -1.</returns>
      public int FindIndex(Predicate<T> match) => this._root.FindIndex(match);

      /// <summary>Searches for an element that matches the conditions defined by the specified predicate, and returns the zero-based index of the first occurrence within the range of elements in the immutable list that extends from the specified index to the last element.</summary>
      /// <param name="startIndex">The zero-based starting index of the search.</param>
      /// <param name="match">The delegate that defines the conditions of the element to search for.</param>
      /// <returns>The zero-based index of the first occurrence of an element that matches the conditions defined by <paramref name="match" />, if found; otherwise, -1.</returns>
      public int FindIndex(int startIndex, Predicate<T> match) => this._root.FindIndex(startIndex, match);

      /// <summary>Searches for an element that matches the conditions defined by the specified predicate, and returns the zero-based index of the first occurrence within the range of elements in the immutable list that starts at the specified index and contains the specified number of elements.</summary>
      /// <param name="startIndex">The zero-based starting index of the search.</param>
      /// <param name="count">The number of elements in the section to search.</param>
      /// <param name="match">The delegate that defines the conditions of the element to search for.</param>
      /// <returns>The zero-based index of the first occurrence of an element that matches the conditions defined by <paramref name="match" />, if found; otherwise, -1.</returns>
      public int FindIndex(int startIndex, int count, Predicate<T> match) => this._root.FindIndex(startIndex, count, match);

      /// <summary>Searches for an element that matches the conditions defined by the specified predicate, and returns the last occurrence within the entire immutable list.</summary>
      /// <param name="match">The delegate that defines the conditions of the element to search for.</param>
      /// <returns>The last element that matches the conditions defined by the specified predicate, found; otherwise, the default value for type <paramref name="T" />.</returns>
      public T? FindLast(Predicate<T> match) => this._root.FindLast(match);

      /// <summary>Searches for an element that matches the conditions defined by the specified predicate, and returns the zero-based index of the last occurrence within the entire immutable list.</summary>
      /// <param name="match">The delegate that defines the conditions of the element to search for.</param>
      /// <returns>The zero-based index of the last occurrence of an element that matches the conditions defined by <paramref name="match" />, if found; otherwise, -1.</returns>
      public int FindLastIndex(Predicate<T> match) => this._root.FindLastIndex(match);

      /// <summary>Searches for an element that matches the conditions defined by the specified predicate, and returns the zero-based index of the last occurrence within the range of elements in the immutable list that extends from the first element to the specified index.</summary>
      /// <param name="startIndex">The zero-based starting index of the backward search.</param>
      /// <param name="match">The delegate that defines the conditions of the element to search for.</param>
      /// <returns>The zero-based index of the last occurrence of an element that matches the conditions defined by <paramref name="match" />, if found; otherwise, -1.</returns>
      public int FindLastIndex(int startIndex, Predicate<T> match) => this._root.FindLastIndex(startIndex, match);

      /// <summary>Searches for an element that matches the conditions defined by the specified predicate, and returns the zero-based index of the last occurrence within the range of elements in the immutable list that contains the specified number of elements and ends at the specified index.</summary>
      /// <param name="startIndex">The zero-based starting index of the backward search.</param>
      /// <param name="count">The number of elements in the section to search.</param>
      /// <param name="match">The delegate that defines the conditions of the element to search for.</param>
      /// <returns>The zero-based index of the last occurrence of an element that matches the conditions defined by <paramref name="match" />, if found; otherwise, -1.</returns>
      public int FindLastIndex(int startIndex, int count, Predicate<T> match) => this._root.FindLastIndex(startIndex, count, match);

      /// <summary>Searches for the specified object and returns the zero-based index of the first occurrence within the range of elements in the immutable list that extends from the specified index to the last element.</summary>
      /// <param name="item">The object to locate in the immutable list. The value can be <see langword="null" /> for reference types.</param>
      /// <param name="index">The zero-based starting index of the search. 0 (zero) is valid in an empty list.</param>
      /// <returns>The zero-based index of the first occurrence of item within the range of elements in the immutable list that extends from <paramref name="index" /> to the last element, if found; otherwise, -1.</returns>
      public int IndexOf(T item, int index) => this._root.IndexOf(item, index, this.Count - index, (IEqualityComparer<T>) EqualityComparer<T>.Default);

      /// <summary>Searches for the specified object and returns the zero-based index of the first occurrence within the range of elements in the immutable list that starts at the specified index and contains the specified number of elements.</summary>
      /// <param name="item">The object to locate in the immutable list. The value can be <see langword="null" /> for reference types.</param>
      /// <param name="index">The zero-based starting index of the search. 0 (zero) is valid in an empty list.</param>
      /// <param name="count">The number of elements in the section to search.</param>
      /// <returns>The zero-based index of the first occurrence of item within the range of elements in the immutable list that starts at <paramref name="index" /> and contains <paramref name="count" /> number of elements, if found; otherwise, -1.</returns>
      public int IndexOf(T item, int index, int count) => this._root.IndexOf(item, index, count, (IEqualityComparer<T>) EqualityComparer<T>.Default);

      /// <summary>Searches for the specified object and returns the zero-based index of the first occurrence within the range of elements in the <see cref="T:System.Collections.Immutable.ImmutableList`1.Builder" /> that starts at the specified index and contains the specified number of elements.</summary>
      /// <param name="item">The object to locate in the immutable list. The value can be <see langword="null" /> for reference types.</param>
      /// <param name="index">The zero-based starting index of the search. 0 (zero) is valid in an empty list.</param>
      /// <param name="count">The number of elements to search.</param>
      /// <param name="equalityComparer">The value comparer to use for comparing elements for equality.</param>
      /// <returns>The zero-based index of the first occurrence of item within the range of elements in the immutable list that starts at <paramref name="index" /> and contains <paramref name="count" /> number of elements, if found; otherwise, -1</returns>
      public int IndexOf(T item, int index, int count, IEqualityComparer<T>? equalityComparer) => this._root.IndexOf(item, index, count, equalityComparer);

      /// <summary>Searches for the specified object and returns the zero-based index of the last occurrence within the entire immutable list.</summary>
      /// <param name="item">The object to locate in the immutable list. The value can be <see langword="null" /> for reference types.</param>
      /// <returns>The zero-based index of the last occurrence of <paramref name="item" /> within the entire immutable list, if found; otherwise, -1.</returns>
      public int LastIndexOf(T item) => this.Count == 0 ? -1 : this._root.LastIndexOf(item, this.Count - 1, this.Count, (IEqualityComparer<T>) EqualityComparer<T>.Default);

      /// <summary>Searches for the specified object and returns the zero-based index of the last occurrence within the range of elements in the immutable list that extends from the first element to the specified index.</summary>
      /// <param name="item">The object to locate in the immutable list. The value can be <see langword="null" /> for reference types.</param>
      /// <param name="startIndex">The zero-based starting index of the backward search.</param>
      /// <returns>The zero-based index of the last occurrence of <paramref name="item" /> within the range of elements in the immutable list that extends from the first element to <paramref name="index" />, if found; otherwise, -1.</returns>
      public int LastIndexOf(T item, int startIndex) => this.Count == 0 && startIndex == 0 ? -1 : this._root.LastIndexOf(item, startIndex, startIndex + 1, (IEqualityComparer<T>) EqualityComparer<T>.Default);

      /// <summary>Searches for the specified object and returns the zero-based index of the last occurrence within the range of elements in the immutable list that contains the specified number of elements and ends at the specified index.</summary>
      /// <param name="item">The object to locate in the immutable list. The value can be <see langword="null" /> for reference types.</param>
      /// <param name="startIndex">The zero-based starting index of the backward search.</param>
      /// <param name="count">The number of elements in the section to search.</param>
      /// <returns>The zero-based index of the last occurrence of <paramref name="item" /> within the range of elements in the immutable list that contains <paramref name="count" /> number of elements and ends at <paramref name="index" />, if found; otherwise, -1.</returns>
      public int LastIndexOf(T item, int startIndex, int count) => this._root.LastIndexOf(item, startIndex, count, (IEqualityComparer<T>) EqualityComparer<T>.Default);

      /// <summary>Searches for the specified object and returns the zero-based index of the last occurrence within the range of elements in the immutable list that contains the specified number of elements and ends at the specified index.</summary>
      /// <param name="item">The object to locate in the immutable list. The value can be <see langword="null" /> for reference types.</param>
      /// <param name="startIndex">The zero-based starting index of the search. 0 (zero) is valid in an empty list.</param>
      /// <param name="count">The number of elements to search.</param>
      /// <param name="equalityComparer">The value comparer to use for comparing elements for equality.</param>
      /// <returns>The zero-based index of the first occurrence of item within the range of elements in the immutable list that starts at <paramref name="index" /> and contains <paramref name="count" /> number of elements, if found; otherwise, -1</returns>
      public int LastIndexOf(
        T item,
        int startIndex,
        int count,
        IEqualityComparer<T>? equalityComparer)
      {
        return this._root.LastIndexOf(item, startIndex, count, equalityComparer);
      }

      /// <summary>Determines whether every element in the immutable list matches the conditions defined by the specified predicate.</summary>
      /// <param name="match">The delegate that defines the conditions to check against the elements.</param>
      /// <returns>
      /// <see langword="true" /> if every element in the immutable list matches the conditions defined by the specified predicate; otherwise, <see langword="false" />. If the list has no elements, the return value is <see langword="true" />.</returns>
      public bool TrueForAll(Predicate<T> match) => this._root.TrueForAll(match);

      /// <summary>Adds a series of elements to the end of this list.</summary>
      /// <param name="items">The elements to add to the end of the list.</param>
      public void AddRange(IEnumerable<T> items)
      {
        Requires.NotNull<IEnumerable<T>>(items, nameof (items));
        this.Root = this.Root.AddRange(items);
      }

      /// <summary>Inserts the elements of a collection into the immutable list at the specified index.</summary>
      /// <param name="index">The zero-based index at which the new elements should be inserted.</param>
      /// <param name="items">The collection whose elements should be inserted into the immutable list. The collection itself cannot be <see langword="null" />, but it can contain elements that are null, if type <c>T</c> is a reference type.</param>
      public void InsertRange(int index, IEnumerable<T> items)
      {
        Requires.Range(index >= 0 && index <= this.Count, nameof (index));
        Requires.NotNull<IEnumerable<T>>(items, nameof (items));
        this.Root = this.Root.InsertRange(index, items);
      }

      /// <summary>Removes all the elements that match the conditions defined by the specified predicate.</summary>
      /// <param name="match">The delegate that defines the conditions of the elements to remove.</param>
      /// <returns>The number of elements removed from the immutable list.</returns>
      public int RemoveAll(Predicate<T> match)
      {
        Requires.NotNull<Predicate<T>>(match, nameof (match));
        int count = this.Count;
        this.Root = this.Root.RemoveAll(match);
        return count - this.Count;
      }

      /// <summary>Removes the first occurrence matching the specified value from this list.</summary>
      /// <param name="item">The item to remove.</param>
      /// <param name="equalityComparer">The equality comparer to use in the search.
      /// If <see langword="null" />, <see cref="P:System.Collections.Generic.EqualityComparer`1.Default" /> is used.</param>
      /// <returns>A value indicating whether the specified element was found and removed from the collection.</returns>
      public bool Remove(T item, IEqualityComparer<T>? equalityComparer)
      {
        int index = this.IndexOf(item, 0, this.Count, equalityComparer);
        if (index < 0)
          return false;
        this.RemoveAt(index);
        return true;
      }

      /// <summary>Removes the specified range of values from this list.</summary>
      /// <param name="index">The starting index to begin removal.</param>
      /// <param name="count">The number of elements to remove.</param>
      public void RemoveRange(int index, int count)
      {
        Requires.Range(index >= 0 && index <= this.Count, nameof (index));
        Requires.Range(count >= 0 && index + count <= this.Count, nameof (count));
        int num = count;
        while (num-- > 0)
          this.RemoveAt(index);
      }

      /// <summary>Removes any first occurrences of the specified values from this list.</summary>
      /// <param name="items">The items to remove if matches are found in this list.</param>
      /// <param name="equalityComparer">The equality comparer to use in the search.
      /// If <see langword="null" />, <see cref="P:System.Collections.Generic.EqualityComparer`1.Default" /> is used.</param>
      public void RemoveRange(IEnumerable<T> items, IEqualityComparer<T>? equalityComparer)
      {
        Requires.NotNull<IEnumerable<T>>(items, nameof (items));
        foreach (T obj in items.GetEnumerableDisposable<T, ImmutableList<T>.Enumerator>())
        {
          int index = this.Root.IndexOf(obj, equalityComparer);
          if (index >= 0)
            this.RemoveAt(index);
        }
      }

      /// <summary>Removes any first occurrences of the specified values from this list.</summary>
      /// <param name="items">The items to remove if matches are found in this list.</param>
      public void RemoveRange(IEnumerable<T> items) => this.RemoveRange(items, (IEqualityComparer<T>) EqualityComparer<T>.Default);

      /// <summary>Replaces the first equal element in the list with the specified element.</summary>
      /// <param name="oldValue">The element to replace.</param>
      /// <param name="newValue">The element to replace the old element with.</param>
      /// <exception cref="T:System.ArgumentException">The old value does not exist in the list.</exception>
      public void Replace(T oldValue, T newValue) => this.Replace(oldValue, newValue, (IEqualityComparer<T>) EqualityComparer<T>.Default);

      /// <summary>Replaces the first equal element in the list with the specified element.</summary>
      /// <param name="oldValue">The element to replace.</param>
      /// <param name="newValue">The element to replace the old element with.</param>
      /// <param name="equalityComparer">The equality comparer to use in the search.
      /// If <see langword="null" />, <see cref="P:System.Collections.Generic.EqualityComparer`1.Default" /> is used.</param>
      /// <exception cref="T:System.ArgumentException">The old value does not exist in the list.</exception>
      public void Replace(T oldValue, T newValue, IEqualityComparer<T>? equalityComparer)
      {
        int index = this.IndexOf(oldValue, 0, this.Count, equalityComparer);
        if (index < 0)
          throw new ArgumentException( nameof (oldValue));
        this.Root = this.Root.ReplaceAt(index, newValue);
      }

      /// <summary>Reverses the order of the elements in the entire immutable list.</summary>
      public void Reverse() => this.Reverse(0, this.Count);

      /// <summary>Reverses the order of the elements in the specified range of the immutable list.</summary>
      /// <param name="index">The zero-based starting index of the range to reverse.</param>
      /// <param name="count">The number of elements in the range to reverse.</param>
      public void Reverse(int index, int count)
      {
        Requires.Range(index >= 0, nameof (index));
        Requires.Range(count >= 0, nameof (count));
        Requires.Range(index + count <= this.Count, nameof (count));
        this.Root = this.Root.Reverse(index, count);
      }

      /// <summary>Sorts the elements in the entire immutable list by using the default comparer.</summary>
      public void Sort() => this.Root = this.Root.Sort();

      /// <summary>Sorts the elements in the entire immutable list by using the specified comparison object.</summary>
      /// <param name="comparison">The object to use when comparing elements.</param>
      /// <exception cref="T:System.ArgumentNullException">
      /// <paramref name="comparison" /> is <see langword="null" />.</exception>
      public void Sort(Comparison<T> comparison)
      {
        Requires.NotNull<Comparison<T>>(comparison, nameof (comparison));
        this.Root = this.Root.Sort(comparison);
      }

      /// <summary>Sorts the elements in the entire immutable list by using the specified comparer.</summary>
      /// <param name="comparer">The implementation to use when comparing elements, or <see langword="null" /> to use the default comparer (<see cref="P:System.Collections.Generic.Comparer`1.Default" />).</param>
      public void Sort(IComparer<T>? comparer) => this.Root = this.Root.Sort(comparer);

      /// <summary>Sorts the elements in a range of elements in the immutable list  by using the specified comparer.</summary>
      /// <param name="index">The zero-based starting index of the range to sort.</param>
      /// <param name="count">The length of the range to sort.</param>
      /// <param name="comparer">The implementation to use when comparing elements, or <see langword="null" /> to use the default comparer (<see cref="P:System.Collections.Generic.Comparer`1.Default" />).</param>
      public void Sort(int index, int count, IComparer<T>? comparer)
      {
        Requires.Range(index >= 0, nameof (index));
        Requires.Range(count >= 0, nameof (count));
        Requires.Range(index + count <= this.Count, nameof (count));
        this.Root = this.Root.Sort(index, count, comparer);
      }

      /// <summary>Searches the entire <see cref="T:System.Collections.Immutable.ImmutableList`1.Builder" /> for an element using the default comparer and returns the zero-based index of the element.</summary>
      /// <param name="item">The object to locate. The value can be null for reference types.</param>
      /// <exception cref="T:System.InvalidOperationException">The default comparer <see cref="P:System.Collections.Generic.Comparer`1.Default" /> cannot find an implementation of the <see cref="T:System.IComparable`1" /> generic interface or the <see cref="T:System.IComparable" /> interface for type T.</exception>
      /// <returns>The zero-based index of item in the <see cref="T:System.Collections.Immutable.ImmutableList`1.Builder" />, if item is found; otherwise, a negative number that is the bitwise complement of the index of the next element that is larger than <paramref name="item" />.</returns>
      public int BinarySearch(T item) => this.BinarySearch(item, (IComparer<T>) null);

      /// <summary>Searches the entire <see cref="T:System.Collections.Immutable.ImmutableList`1.Builder" /> for an element using the specified comparer and returns the zero-based index of the element.</summary>
      /// <param name="item">The object to locate. This value can be null for reference types.</param>
      /// <param name="comparer">The implementation to use when comparing elements, or <see langword="null" /> for the default comparer.</param>
      /// <exception cref="T:System.InvalidOperationException">
      /// <paramref name="comparer" /> is <see langword="null" />, and the default comparer <see cref="P:System.Collections.Generic.Comparer`1.Default" /> cannot find an implementation of the <see cref="T:System.IComparable`1" /> generic interface or the <see cref="T:System.IComparable" /> interface for type T.</exception>
      /// <returns>The zero-based index of item in the <see cref="T:System.Collections.Immutable.ImmutableList`1.Builder" />, if item is found; otherwise, a negative number that is the bitwise complement of the index of the next element that is larger than <paramref name="item" />.</returns>
      public int BinarySearch(T item, IComparer<T>? comparer) => this.BinarySearch(0, this.Count, item, comparer);

      /// <summary>Searches the specified range of the <see cref="T:System.Collections.Immutable.ImmutableList`1.Builder" /> for an element using the specified comparer and returns the zero-based index of the element.</summary>
      /// <param name="index">The zero-based starting index of the range to search.</param>
      /// <param name="count">The length of the range to search.</param>
      /// <param name="item">The object to locate. This value can be null for reference types.</param>
      /// <param name="comparer">The implementation to use when comparing elements, or <see langword="null" /> for the default comparer.</param>
      /// <exception cref="T:System.ArgumentOutOfRangeException">
      ///         <paramref name="index" /> is less than 0.
      /// -or-
      /// 
      /// <paramref name="count" /> is less than 0.</exception>
      /// <exception cref="T:System.ArgumentException">
      /// <paramref name="index" /> and <paramref name="count" /> do not denote a valid range in the <see cref="T:System.Collections.Generic.List`1" />.</exception>
      /// <exception cref="T:System.InvalidOperationException">
      /// <paramref name="comparer" /> is <see langword="null" />, and the default comparer <see cref="P:System.Collections.Generic.Comparer`1.Default" /> cannot find an implementation of the <see cref="T:System.IComparable`1" /> generic interface or the <see cref="T:System.IComparable" /> interface for type T.</exception>
      /// <returns>The zero-based index of item in the <see cref="T:System.Collections.Immutable.ImmutableList`1.Builder" />, if item is found; otherwise, a negative number that is the bitwise complement of the index of the next element that is larger than <paramref name="item" />.</returns>
      public int BinarySearch(int index, int count, T item, IComparer<T>? comparer) => this.Root.BinarySearch(index, count, item, comparer);

      /// <summary>Creates an immutable list based on the contents of this instance.</summary>
      /// <returns>An immutable list.</returns>
      public ImmutableList<T> ToImmutable() => this._immutable ?? (this._immutable = ImmutableList<T>.WrapNode(this.Root));


      #nullable disable
      /// <summary>Adds an item to the list.</summary>
      /// <param name="value">The object to add to the list.</param>
      /// <exception cref="T:System.NotImplementedException" />
      /// <returns>The position into which the new element was inserted, or -1 to indicate that the item was not inserted into the collection.</returns>
      int IList.Add(object value)
      {
        this.Add((T) value);
        return this.Count - 1;
      }

      /// <summary>Removes all items from the list.</summary>
      /// <exception cref="T:System.NotImplementedException" />
      void IList.Clear() => this.Clear();

      /// <summary>Determines whether the list contains a specific value.</summary>
      /// <param name="value">The object to locate in the list.</param>
      /// <exception cref="T:System.NotImplementedException" />
      /// <returns>
      /// <see langword="true" /> if the <see cref="T:System.Object" /> is found in the list; otherwise, <see langword="false" />.</returns>
      bool IList.Contains(object value) => ImmutableList<T>.IsCompatibleObject(value) && this.Contains((T) value);

      /// <summary>Determines the index of a specific item in the list.</summary>
      /// <param name="value">The object to locate in the list.</param>
      /// <exception cref="T:System.NotImplementedException" />
      /// <returns>The index of <paramref name="value" /> if found in the list; otherwise, -1.</returns>
      int IList.IndexOf(object value) => ImmutableList<T>.IsCompatibleObject(value) ? this.IndexOf((T) value) : -1;

      /// <summary>Inserts an item to the list at the specified index.</summary>
      /// <param name="index">The zero-based index at which <paramref name="value" /> should be inserted.</param>
      /// <param name="value">The object to insert into the list.</param>
      /// <exception cref="T:System.NotImplementedException" />
      void IList.Insert(int index, object value) => this.Insert(index, (T) value);

      /// <summary>Gets a value that indicates whether the <see cref="T:System.Collections.IList" /> has a fixed size.</summary>
      /// <returns>
      /// <see langword="true" /> if the <see cref="T:System.Collections.IList" /> has a fixed size; otherwise, <see langword="false" />.</returns>
      bool IList.IsFixedSize => false;

      /// <summary>Gets a value that indicates whether the <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only.</summary>
      /// <returns>
      /// <see langword="true" /> if the <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only; otherwise, <see langword="false" />.</returns>
      bool IList.IsReadOnly => false;

      /// <summary>Removes the first occurrence of a specific object from the list.</summary>
      /// <param name="value">The object to remove from the list.</param>
      /// <exception cref="T:System.NotImplementedException" />
      void IList.Remove(object value)
      {
        if (!ImmutableList<T>.IsCompatibleObject(value))
          return;
        this.Remove((T) value);
      }


      #nullable enable
      /// <summary>Gets or sets the <see cref="T:System.Object" /> at the specified index.</summary>
      /// <param name="index">The index.</param>
      /// <returns>The object at the specified index.</returns>
      object? IList.this[int index]
      {
        get => (object) this[index];
        set => this[index] = (T) value;
      }


      #nullable disable
      /// <summary>Copies the elements of the list to an array, starting at a particular array index.</summary>
      /// <param name="array">The one-dimensional array that is the destination of the elements copied from the list. The array must have zero-based indexing.</param>
      /// <param name="arrayIndex">The zero-based index in <paramref name="array" /> at which copying begins.</param>
      /// <exception cref="T:System.NotImplementedException" />
      void ICollection.CopyTo(Array array, int arrayIndex) => this.Root.CopyTo(array, arrayIndex);

      /// <summary>Gets a value that indicates whether access to the <see cref="T:System.Collections.ICollection" /> is synchronized (thread safe).</summary>
      /// <returns>
      /// <see langword="true" /> if access to the <see cref="T:System.Collections.ICollection" /> is synchronized (thread safe); otherwise, <see langword="false" />.</returns>
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
      private readonly ImmutableList<T>.Builder _builder;
      private readonly int _poolUserId;
      private readonly int _startIndex;
      private readonly int _count;
      private int _remainingCount;
      private readonly bool _reversed;
      private ImmutableList<T>.Node _root;
      private SecurePooledObject<Stack<RefAsValueType<ImmutableList<T>.Node>>> _stack;
      private ImmutableList<T>.Node _current;
      private int _enumeratingBuilderVersion;


      #nullable enable
      internal Enumerator(
        ImmutableList<
        #nullable disable
        T>.Node root,

        #nullable enable
        ImmutableList<
        #nullable disable
        T>.Builder
        #nullable enable
        ? builder = null,
        int startIndex = -1,
        int count = -1,
        bool reversed = false)
      {
        Requires.NotNull<ImmutableList<T>.Node>(root, nameof (root));
        Requires.Range(startIndex >= -1, nameof (startIndex));
        Requires.Range(count >= -1, nameof (count));
        Requires.Argument(reversed || count == -1 || (startIndex == -1 ? 0 : startIndex) + count <= root.Count);
        Requires.Argument(!reversed || count == -1 || (startIndex == -1 ? root.Count - 1 : startIndex) - count + 1 >= 0);
        this._root = root;
        this._builder = builder;
        this._current = (ImmutableList<T>.Node) null;
        this._startIndex = startIndex >= 0 ? startIndex : (reversed ? root.Count - 1 : 0);
        this._count = count == -1 ? root.Count : count;
        this._remainingCount = this._count;
        this._reversed = reversed;
        this._enumeratingBuilderVersion = builder != null ? builder.Version : -1;
        this._poolUserId = SecureObjectPool.NewId();
        this._stack = (SecurePooledObject<Stack<RefAsValueType<ImmutableList<T>.Node>>>) null;
        if (this._count <= 0)
          return;
        if (!SecureObjectPool<Stack<RefAsValueType<ImmutableList<T>.Node>>, ImmutableList<T>.Enumerator>.TryTake(this, out this._stack))
          this._stack = SecureObjectPool<Stack<RefAsValueType<ImmutableList<T>.Node>>, ImmutableList<T>.Enumerator>.PrepNew(this, new Stack<RefAsValueType<ImmutableList<T>.Node>>(root.Height));
        this.ResetStack();
      }

      int ISecurePooledObjectUser.PoolUserId => this._poolUserId;

      /// <summary>Gets the element at the current position of the enumerator.</summary>
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

      /// <summary>Releases the resources used by the current instance of the <see cref="T:System.Collections.Immutable.ImmutableList`1.Enumerator" /> class.</summary>
      public void Dispose()
      {
        this._root = (ImmutableList<T>.Node) null;
        this._current = (ImmutableList<T>.Node) null;
        Stack<RefAsValueType<ImmutableList<T>.Node>> stack;
        if (this._stack != null && this._stack.TryUse<ImmutableList<T>.Enumerator>(ref this, out stack))
        {
          stack.ClearFastWhenEmpty<RefAsValueType<ImmutableList<T>.Node>>();
          SecureObjectPool<Stack<RefAsValueType<ImmutableList<T>.Node>>, ImmutableList<T>.Enumerator>.TryAdd(this, this._stack);
        }
        this._stack = (SecurePooledObject<Stack<RefAsValueType<ImmutableList<T>.Node>>>) null;
      }

      /// <summary>Advances enumeration to the next element of the immutable list.</summary>
      /// <returns>
      /// <see langword="true" /> if the enumerator was successfully advanced to the next element; <see langword="false" /> if the enumerator has passed the end of the list.</returns>
      public bool MoveNext()
      {
        this.ThrowIfDisposed();
        this.ThrowIfChanged();
        if (this._stack != null)
        {
          Stack<RefAsValueType<ImmutableList<T>.Node>> refAsValueTypeStack = this._stack.Use<ImmutableList<T>.Enumerator>(ref this);
          if (this._remainingCount > 0 && refAsValueTypeStack.Count > 0)
          {
            ImmutableList<T>.Node node = refAsValueTypeStack.Pop().Value;
            this._current = node;
            this.PushNext(this.NextBranch(node));
            --this._remainingCount;
            return true;
          }
        }
        this._current = (ImmutableList<T>.Node) null;
        return false;
      }

      /// <summary>Sets the enumerator to its initial position, which is before the first element in the immutable list.</summary>
      public void Reset()
      {
        this.ThrowIfDisposed();
        this._enumeratingBuilderVersion = this._builder != null ? this._builder.Version : -1;
        this._remainingCount = this._count;
        if (this._stack == null)
          return;
        this.ResetStack();
      }

      private void ResetStack()
      {
        Stack<RefAsValueType<ImmutableList<T>.Node>> stack = this._stack.Use<ImmutableList<T>.Enumerator>(ref this);
        stack.ClearFastWhenEmpty<RefAsValueType<ImmutableList<T>.Node>>();
        ImmutableList<T>.Node node = this._root;
        int num = this._reversed ? this._root.Count - this._startIndex - 1 : this._startIndex;
        while (!node.IsEmpty && num != this.PreviousBranch(node).Count)
        {
          if (num < this.PreviousBranch(node).Count)
          {
            stack.Push(new RefAsValueType<ImmutableList<T>.Node>(node));
            node = this.PreviousBranch(node);
          }
          else
          {
            num -= this.PreviousBranch(node).Count + 1;
            node = this.NextBranch(node);
          }
        }
        if (node.IsEmpty)
          return;
        stack.Push(new RefAsValueType<ImmutableList<T>.Node>(node));
      }


      #nullable disable
      private ImmutableList<T>.Node NextBranch(ImmutableList<T>.Node node) => !this._reversed ? node.Right : node.Left;

      private ImmutableList<T>.Node PreviousBranch(ImmutableList<T>.Node node) => !this._reversed ? node.Left : node.Right;

      private void ThrowIfDisposed()
      {
        if (this._root != null && (this._stack == null || this._stack.IsOwned<ImmutableList<T>.Enumerator>(ref this)))
          return;
        Requires.FailObjectDisposed<ImmutableList<T>.Enumerator>(this);
      }

      private void ThrowIfChanged()
      {
        if (this._builder != null && this._builder.Version != this._enumeratingBuilderVersion)
          throw new InvalidOperationException();
      }

      private void PushNext(ImmutableList<T>.Node node)
      {
        Requires.NotNull<ImmutableList<T>.Node>(node, nameof (node));
        if (node.IsEmpty)
          return;
        Stack<RefAsValueType<ImmutableList<T>.Node>> refAsValueTypeStack = this._stack.Use<ImmutableList<T>.Enumerator>(ref this);
        for (; !node.IsEmpty; node = this.PreviousBranch(node))
          refAsValueTypeStack.Push(new RefAsValueType<ImmutableList<T>.Node>(node));
      }
    }


    #nullable enable
    [DebuggerDisplay("{_key}")]
    internal sealed class Node : IBinaryTree<T>, IBinaryTree, IEnumerable<T>, IEnumerable
    {
      internal static readonly ImmutableList<
      #nullable disable
      T>.Node EmptyNode = new ImmutableList<T>.Node();
      private T _key;
      private bool _frozen;
      private byte _height;
      private int _count;
      private ImmutableList<T>.Node _left;
      private ImmutableList<T>.Node _right;

      private Node() => this._frozen = true;

      private Node(T key, ImmutableList<T>.Node left, ImmutableList<T>.Node right, bool frozen = false)
      {
        Requires.NotNull<ImmutableList<T>.Node>(left, nameof (left));
        Requires.NotNull<ImmutableList<T>.Node>(right, nameof (right));
        this._key = key;
        this._left = left;
        this._right = right;
        this._height = ImmutableList<T>.Node.ParentHeight(left, right);
        this._count = ImmutableList<T>.Node.ParentCount(left, right);
        this._frozen = frozen;
      }

      public bool IsEmpty => this._left == null;

      public int Height => (int) this._height;


      #nullable enable
      public ImmutableList<
      #nullable disable
      T>.Node
      #nullable enable
      ? Left => this._left;

      IBinaryTree? IBinaryTree.Left => (IBinaryTree) this._left;

      public ImmutableList<
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
      public ImmutableList<
      #nullable disable
      T>.Enumerator GetEnumerator() => new ImmutableList<T>.Enumerator(this);

      [ExcludeFromCodeCoverage]
      IEnumerator<T> IEnumerable<T>.GetEnumerator() => (IEnumerator<T>) this.GetEnumerator();

      [ExcludeFromCodeCoverage]
      IEnumerator IEnumerable.GetEnumerator() => (IEnumerator) this.GetEnumerator();


      #nullable enable
      internal ImmutableList<
      #nullable disable
      T>.Enumerator GetEnumerator(
      #nullable enable
      ImmutableList<
      #nullable disable
      T>.Builder builder) => new ImmutableList<T>.Enumerator(this, builder);


      #nullable enable
      internal static ImmutableList<
      #nullable disable
      T>.Node NodeTreeFromList(
      #nullable enable
      IOrderedCollection<T> items, int start, int length)
      {
        Requires.NotNull<IOrderedCollection<T>>(items, nameof (items));
        Requires.Range(start >= 0, nameof (start));
        Requires.Range(length >= 0, nameof (length));
        if (length == 0)
          return ImmutableList<T>.Node.EmptyNode;
        int length1 = (length - 1) / 2;
        int length2 = length - 1 - length1;
        ImmutableList<T>.Node left = ImmutableList<T>.Node.NodeTreeFromList(items, start, length2);
        ImmutableList<T>.Node right = ImmutableList<T>.Node.NodeTreeFromList(items, start + length2 + 1, length1);
        return new ImmutableList<T>.Node(items[start + length2], left, right, true);
      }

      internal ImmutableList<
      #nullable disable
      T>.Node Add(
      #nullable enable
      T key)
      {
        if (this.IsEmpty)
          return ImmutableList<T>.Node.CreateLeaf(key);
        ImmutableList<T>.Node node = this.MutateRight(this._right.Add(key));
        return !node.IsBalanced ? node.BalanceRight() : node;
      }

      internal ImmutableList<
      #nullable disable
      T>.Node Insert(int index, 
      #nullable enable
      T key)
      {
        Requires.Range(index >= 0 && index <= this.Count, nameof (index));
        if (this.IsEmpty)
          return ImmutableList<T>.Node.CreateLeaf(key);
        if (index <= this._left._count)
        {
          ImmutableList<T>.Node node = this.MutateLeft(this._left.Insert(index, key));
          return !node.IsBalanced ? node.BalanceLeft() : node;
        }
        ImmutableList<T>.Node node1 = this.MutateRight(this._right.Insert(index - this._left._count - 1, key));
        return !node1.IsBalanced ? node1.BalanceRight() : node1;
      }

      internal ImmutableList<
      #nullable disable
      T>.Node AddRange(
      #nullable enable
      IEnumerable<T> keys)
      {
        Requires.NotNull<IEnumerable<T>>(keys, nameof (keys));
        return this.IsEmpty ? ImmutableList<T>.Node.CreateRange(keys) : this.MutateRight(this._right.AddRange(keys)).BalanceMany();
      }

      internal ImmutableList<
      #nullable disable
      T>.Node InsertRange(int index, 
      #nullable enable
      IEnumerable<T> keys)
      {
        Requires.Range(index >= 0 && index <= this.Count, nameof (index));
        Requires.NotNull<IEnumerable<T>>(keys, nameof (keys));
        return this.IsEmpty ? ImmutableList<T>.Node.CreateRange(keys) : (index > this._left._count ? this.MutateRight(this._right.InsertRange(index - this._left._count - 1, keys)) : this.MutateLeft(this._left.InsertRange(index, keys))).BalanceMany();
      }

      internal ImmutableList<
      #nullable disable
      T>.Node RemoveAt(int index)
      {
        Requires.Range(index >= 0 && index < this.Count, nameof (index));
        ImmutableList<T>.Node node1;
        if (index == this._left._count)
        {
          if (this._right.IsEmpty && this._left.IsEmpty)
            node1 = ImmutableList<T>.Node.EmptyNode;
          else if (this._right.IsEmpty && !this._left.IsEmpty)
            node1 = this._left;
          else if (!this._right.IsEmpty && this._left.IsEmpty)
          {
            node1 = this._right;
          }
          else
          {
            ImmutableList<T>.Node node2 = this._right;
            while (!node2._left.IsEmpty)
              node2 = node2._left;
            ImmutableList<T>.Node right = this._right.RemoveAt(0);
            node1 = node2.MutateBoth(this._left, right);
          }
        }
        else
          node1 = index >= this._left._count ? this.MutateRight(this._right.RemoveAt(index - this._left._count - 1)) : this.MutateLeft(this._left.RemoveAt(index));
        return !node1.IsEmpty && !node1.IsBalanced ? node1.Balance() : node1;
      }


      #nullable enable
      internal ImmutableList<
      #nullable disable
      T>.Node RemoveAll(
      #nullable enable
      Predicate<T> match)
      {
        Requires.NotNull<Predicate<T>>(match, nameof (match));
        ImmutableList<T>.Node root = this;
        ImmutableList<T>.Enumerator enumerator = new ImmutableList<T>.Enumerator(root);
        try
        {
          int num = 0;
          while (enumerator.MoveNext())
          {
            if (match(enumerator.Current))
            {
              root = root.RemoveAt(num);
              enumerator.Dispose();
              enumerator = new ImmutableList<T>.Enumerator(root, startIndex: num);
            }
            else
              ++num;
          }
        }
        finally
        {
          enumerator.Dispose();
        }
        return root;
      }

      internal ImmutableList<
      #nullable disable
      T>.Node ReplaceAt(int index, 
      #nullable enable
      T value)
      {
        Requires.Range(index >= 0 && index < this.Count, nameof (index));
        return index != this._left._count ? (index >= this._left._count ? this.MutateRight(this._right.ReplaceAt(index - this._left._count - 1, value)) : this.MutateLeft(this._left.ReplaceAt(index, value))) : this.MutateKey(value);
      }

      internal ImmutableList<
      #nullable disable
      T>.Node Reverse() => this.Reverse(0, this.Count);


      #nullable enable
      internal ImmutableList<
      #nullable disable
      T>.Node Reverse(int index, int count)
      {
        Requires.Range(index >= 0, nameof (index));
        Requires.Range(count >= 0, nameof (count));
        Requires.Range(index + count <= this.Count, nameof (index));
        ImmutableList<T>.Node node = this;
        int index1 = index;
        for (int index2 = index + count - 1; index1 < index2; --index2)
        {
          T obj1 = node.ItemRef(index1);
          T obj2 = node.ItemRef(index2);
          node = node.ReplaceAt(index2, obj1).ReplaceAt(index1, obj2);
          ++index1;
        }
        return node;
      }


      #nullable enable
      internal ImmutableList<
      #nullable disable
      T>.Node Sort() => this.Sort((IComparer<T>) Comparer<T>.Default);


      #nullable enable
      internal ImmutableList<
      #nullable disable
      T>.Node Sort(
      #nullable enable
      Comparison<T> comparison)
      {
        Requires.NotNull<Comparison<T>>(comparison, nameof (comparison));
        T[] objArray = new T[this.Count];
        this.CopyTo(objArray);
        Array.Sort<T>(objArray, comparison);
        return ImmutableList<T>.Node.NodeTreeFromList(((IEnumerable<T>) objArray).AsOrderedCollection<T>(), 0, this.Count);
      }

      internal ImmutableList<
      #nullable disable
      T>.Node Sort(
      #nullable enable
      IComparer<T>? comparer) => this.Sort(0, this.Count, comparer);

      internal ImmutableList<
      #nullable disable
      T>.Node Sort(int index, int count, 
      #nullable enable
      IComparer<T>? comparer)
      {
        Requires.Range(index >= 0, nameof (index));
        Requires.Range(count >= 0, nameof (count));
        Requires.Argument(index + count <= this.Count);
        T[] objArray = new T[this.Count];
        this.CopyTo(objArray);
        Array.Sort<T>(objArray, index, count, comparer);
        return ImmutableList<T>.Node.NodeTreeFromList(((IEnumerable<T>) objArray).AsOrderedCollection<T>(), 0, this.Count);
      }

      internal int BinarySearch(int index, int count, T item, IComparer<T>? comparer)
      {
        Requires.Range(index >= 0, nameof (index));
        Requires.Range(count >= 0, nameof (count));
        if (comparer == null)
          comparer = (IComparer<T>) Comparer<T>.Default;
        if (this.IsEmpty || count <= 0)
          return ~index;
        int count1 = this._left.Count;
        if (index + count <= count1)
          return this._left.BinarySearch(index, count, item, comparer);
        if (index > count1)
        {
          int num1 = this._right.BinarySearch(index - count1 - 1, count, item, comparer);
          int num2 = count1 + 1;
          return num1 >= 0 ? num1 + num2 : num1 - num2;
        }
        int num3 = comparer.Compare(item, this._key);
        if (num3 == 0)
          return count1;
        if (num3 > 0)
        {
          int count2 = count - (count1 - index) - 1;
          int num4 = count2 < 0 ? -1 : this._right.BinarySearch(0, count2, item, comparer);
          int num5 = count1 + 1;
          return num4 >= 0 ? num4 + num5 : num4 - num5;
        }
        return index == count1 ? ~index : this._left.BinarySearch(index, count, item, comparer);
      }

      internal int IndexOf(T item, IEqualityComparer<T>? equalityComparer) => this.IndexOf(item, 0, this.Count, equalityComparer);

      internal bool Contains(T item, IEqualityComparer<T> equalityComparer) => ImmutableList<T>.Node.Contains(this, item, equalityComparer);

      internal int IndexOf(T item, int index, int count, IEqualityComparer<T>? equalityComparer)
      {
        Requires.Range(index >= 0, nameof (index));
        Requires.Range(count >= 0, nameof (count));
        Requires.Range(count <= this.Count, nameof (count));
        Requires.Range(index + count <= this.Count, nameof (count));
        if (equalityComparer == null)
          equalityComparer = (IEqualityComparer<T>) EqualityComparer<T>.Default;
        using (ImmutableList<T>.Enumerator enumerator = new ImmutableList<T>.Enumerator(this, startIndex: index, count: count))
        {
          while (enumerator.MoveNext())
          {
            if (equalityComparer.Equals(item, enumerator.Current))
              return index;
            ++index;
          }
        }
        return -1;
      }

      internal int LastIndexOf(
        T item,
        int index,
        int count,
        IEqualityComparer<T>? equalityComparer)
      {
        Requires.Range(index >= 0, nameof (index));
        Requires.Range(count >= 0 && count <= this.Count, nameof (count));
        Requires.Argument(index - count + 1 >= 0);
        if (equalityComparer == null)
          equalityComparer = (IEqualityComparer<T>) EqualityComparer<T>.Default;
        using (ImmutableList<T>.Enumerator enumerator = new ImmutableList<T>.Enumerator(this, startIndex: index, count: count, reversed: true))
        {
          while (enumerator.MoveNext())
          {
            if (equalityComparer.Equals(item, enumerator.Current))
              return index;
            --index;
          }
        }
        return -1;
      }

      internal void CopyTo(T[] array)
      {
        Requires.NotNull<T[]>(array, nameof (array));
        Requires.Range(array.Length >= this.Count, nameof (array));
        int num = 0;
        foreach (T obj in this)
          array[num++] = obj;
      }

      internal void CopyTo(T[] array, int arrayIndex)
      {
        Requires.NotNull<T[]>(array, nameof (array));
        Requires.Range(arrayIndex >= 0, nameof (arrayIndex));
        Requires.Range(array.Length >= arrayIndex + this.Count, nameof (arrayIndex));
        foreach (T obj in this)
          array[arrayIndex++] = obj;
      }

      internal void CopyTo(int index, T[] array, int arrayIndex, int count)
      {
        Requires.NotNull<T[]>(array, nameof (array));
        Requires.Range(index >= 0, nameof (index));
        Requires.Range(count >= 0, nameof (count));
        Requires.Range(index + count <= this.Count, nameof (count));
        Requires.Range(arrayIndex >= 0, nameof (arrayIndex));
        Requires.Range(arrayIndex + count <= array.Length, nameof (arrayIndex));
        using (ImmutableList<T>.Enumerator enumerator = new ImmutableList<T>.Enumerator(this, startIndex: index, count: count))
        {
          while (enumerator.MoveNext())
            array[arrayIndex++] = enumerator.Current;
        }
      }

      internal void CopyTo(Array array, int arrayIndex)
      {
        Requires.NotNull<Array>(array, nameof (array));
        Requires.Range(arrayIndex >= 0, nameof (arrayIndex));
        Requires.Range(array.Length >= arrayIndex + this.Count, nameof (arrayIndex));
        foreach (T obj in this)
          array.SetValue((object) obj, arrayIndex++);
      }

      internal ImmutableList<TOutput>.Node ConvertAll<TOutput>(Func<T, TOutput> converter)
      {
        ImmutableList<TOutput>.Node emptyNode = ImmutableList<TOutput>.Node.EmptyNode;
        return this.IsEmpty ? emptyNode : emptyNode.AddRange(this.Select<T, TOutput>(converter));
      }

      internal bool TrueForAll(Predicate<T> match)
      {
        Requires.NotNull<Predicate<T>>(match, nameof (match));
        foreach (T obj in this)
        {
          if (!match(obj))
            return false;
        }
        return true;
      }

      internal bool Exists(Predicate<T> match)
      {
        Requires.NotNull<Predicate<T>>(match, nameof (match));
        foreach (T obj in this)
        {
          if (match(obj))
            return true;
        }
        return false;
      }

      internal T? Find(Predicate<T> match)
      {
        Requires.NotNull<Predicate<T>>(match, nameof (match));
        foreach (T obj in this)
        {
          if (match(obj))
            return obj;
        }
        return default (T);
      }

      internal ImmutableList<T> FindAll(Predicate<T> match)
      {
        Requires.NotNull<Predicate<T>>(match, nameof (match));
        if (this.IsEmpty)
          return ImmutableList<T>.Empty;
        List<T> items = (List<T>) null;
        foreach (T obj in this)
        {
          if (match(obj))
          {
            if (items == null)
              items = new List<T>();
            items.Add(obj);
          }
        }
        return items == null ? ImmutableList<T>.Empty : ImmutableList.CreateRange<T>((IEnumerable<T>) items);
      }

      internal int FindIndex(Predicate<T> match)
      {
        Requires.NotNull<Predicate<T>>(match, nameof (match));
        return this.FindIndex(0, this._count, match);
      }

      internal int FindIndex(int startIndex, Predicate<T> match)
      {
        Requires.NotNull<Predicate<T>>(match, nameof (match));
        Requires.Range(startIndex >= 0 && startIndex <= this.Count, nameof (startIndex));
        return this.FindIndex(startIndex, this.Count - startIndex, match);
      }

      internal int FindIndex(int startIndex, int count, Predicate<T> match)
      {
        Requires.NotNull<Predicate<T>>(match, nameof (match));
        Requires.Range(startIndex >= 0, nameof (startIndex));
        Requires.Range(count >= 0, nameof (count));
        Requires.Range(startIndex + count <= this.Count, nameof (count));
        using (ImmutableList<T>.Enumerator enumerator = new ImmutableList<T>.Enumerator(this, startIndex: startIndex, count: count))
        {
          int index = startIndex;
          while (enumerator.MoveNext())
          {
            if (match(enumerator.Current))
              return index;
            ++index;
          }
        }
        return -1;
      }

      internal T? FindLast(Predicate<T> match)
      {
        Requires.NotNull<Predicate<T>>(match, nameof (match));
        using (ImmutableList<T>.Enumerator enumerator = new ImmutableList<T>.Enumerator(this, reversed: true))
        {
          while (enumerator.MoveNext())
          {
            if (match(enumerator.Current))
              return enumerator.Current;
          }
        }
        return default (T);
      }

      internal int FindLastIndex(Predicate<T> match)
      {
        Requires.NotNull<Predicate<T>>(match, nameof (match));
        return !this.IsEmpty ? this.FindLastIndex(this.Count - 1, this.Count, match) : -1;
      }

      internal int FindLastIndex(int startIndex, Predicate<T> match)
      {
        Requires.NotNull<Predicate<T>>(match, nameof (match));
        Requires.Range(startIndex >= 0, nameof (startIndex));
        Requires.Range(startIndex == 0 || startIndex < this.Count, nameof (startIndex));
        return !this.IsEmpty ? this.FindLastIndex(startIndex, startIndex + 1, match) : -1;
      }

      internal int FindLastIndex(int startIndex, int count, Predicate<T> match)
      {
        Requires.NotNull<Predicate<T>>(match, nameof (match));
        Requires.Range(startIndex >= 0, nameof (startIndex));
        Requires.Range(count <= this.Count, nameof (count));
        Requires.Range(startIndex - count + 1 >= 0, nameof (startIndex));
        using (ImmutableList<T>.Enumerator enumerator = new ImmutableList<T>.Enumerator(this, startIndex: startIndex, count: count, reversed: true))
        {
          int lastIndex = startIndex;
          while (enumerator.MoveNext())
          {
            if (match(enumerator.Current))
              return lastIndex;
            --lastIndex;
          }
        }
        return -1;
      }

      internal void Freeze()
      {
        if (this._frozen)
          return;
        this._left.Freeze();
        this._right.Freeze();
        this._frozen = true;
      }


      #nullable disable
      private ImmutableList<T>.Node RotateLeft() => this._right.MutateLeft(this.MutateRight(this._right._left));

      private ImmutableList<T>.Node RotateRight() => this._left.MutateRight(this.MutateLeft(this._left._right));

      private ImmutableList<T>.Node DoubleLeft()
      {
        ImmutableList<T>.Node right = this._right;
        ImmutableList<T>.Node left = right._left;
        return left.MutateBoth(this.MutateRight(left._left), right.MutateLeft(left._right));
      }

      private ImmutableList<T>.Node DoubleRight()
      {
        ImmutableList<T>.Node left = this._left;
        ImmutableList<T>.Node right = left._right;
        return right.MutateBoth(left.MutateRight(right._left), this.MutateLeft(right._right));
      }

      private int BalanceFactor => (int) this._right._height - (int) this._left._height;

      private bool IsRightHeavy => this.BalanceFactor >= 2;

      private bool IsLeftHeavy => this.BalanceFactor <= -2;

      private bool IsBalanced => (uint) (this.BalanceFactor + 1) <= 2U;

      private ImmutableList<T>.Node Balance() => !this.IsLeftHeavy ? this.BalanceRight() : this.BalanceLeft();

      private ImmutableList<T>.Node BalanceLeft() => this._left.BalanceFactor <= 0 ? this.RotateRight() : this.DoubleRight();

      private ImmutableList<T>.Node BalanceRight() => this._right.BalanceFactor >= 0 ? this.RotateLeft() : this.DoubleLeft();

      private ImmutableList<T>.Node BalanceMany()
      {
        ImmutableList<T>.Node node = this;
        while (!node.IsBalanced)
        {
          if (node.IsRightHeavy)
          {
            node = node.BalanceRight();
            node.MutateLeft(node._left.BalanceMany());
          }
          else
          {
            node = node.BalanceLeft();
            node.MutateRight(node._right.BalanceMany());
          }
        }
        return node;
      }

      private ImmutableList<T>.Node MutateBoth(
        ImmutableList<T>.Node left,
        ImmutableList<T>.Node right)
      {
        Requires.NotNull<ImmutableList<T>.Node>(left, nameof (left));
        Requires.NotNull<ImmutableList<T>.Node>(right, nameof (right));
        if (this._frozen)
          return new ImmutableList<T>.Node(this._key, left, right);
        this._left = left;
        this._right = right;
        this._height = ImmutableList<T>.Node.ParentHeight(left, right);
        this._count = ImmutableList<T>.Node.ParentCount(left, right);
        return this;
      }

      private ImmutableList<T>.Node MutateLeft(ImmutableList<T>.Node left)
      {
        Requires.NotNull<ImmutableList<T>.Node>(left, nameof (left));
        if (this._frozen)
          return new ImmutableList<T>.Node(this._key, left, this._right);
        this._left = left;
        this._height = ImmutableList<T>.Node.ParentHeight(left, this._right);
        this._count = ImmutableList<T>.Node.ParentCount(left, this._right);
        return this;
      }

      private ImmutableList<T>.Node MutateRight(ImmutableList<T>.Node right)
      {
        Requires.NotNull<ImmutableList<T>.Node>(right, nameof (right));
        if (this._frozen)
          return new ImmutableList<T>.Node(this._key, this._left, right);
        this._right = right;
        this._height = ImmutableList<T>.Node.ParentHeight(this._left, right);
        this._count = ImmutableList<T>.Node.ParentCount(this._left, right);
        return this;
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      private static byte ParentHeight(ImmutableList<T>.Node left, ImmutableList<T>.Node right) => checked ((byte) (1 + (int) Math.Max(left._height, right._height)));

      private static int ParentCount(ImmutableList<T>.Node left, ImmutableList<T>.Node right) => 1 + left._count + right._count;

      private ImmutableList<T>.Node MutateKey(T key)
      {
        if (this._frozen)
          return new ImmutableList<T>.Node(key, this._left, this._right);
        this._key = key;
        return this;
      }

      private static ImmutableList<T>.Node CreateRange(IEnumerable<T> keys)
      {
        ImmutableList<T> other;
        if (ImmutableList<T>.TryCastToImmutableList(keys, out other))
          return other._root;
        IOrderedCollection<T> items = keys.AsOrderedCollection<T>();
        return ImmutableList<T>.Node.NodeTreeFromList(items, 0, items.Count);
      }

      private static ImmutableList<T>.Node CreateLeaf(T key) => new ImmutableList<T>.Node(key, ImmutableList<T>.Node.EmptyNode, ImmutableList<T>.Node.EmptyNode);

      private static bool Contains(
        ImmutableList<T>.Node node,
        T value,
        IEqualityComparer<T> equalityComparer)
      {
        if (node.IsEmpty)
          return false;
        return equalityComparer.Equals(value, node._key) || ImmutableList<T>.Node.Contains(node._left, value, equalityComparer) || ImmutableList<T>.Node.Contains(node._right, value, equalityComparer);
      }
    }
  }
}
