﻿// Decompiled with JetBrains decompiler
// Type: System.Collections.Immutable.ImmutableArray`1
// Assembly: System.Collections.Immutable, Version=7.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 5F9FF90F-0D16-4469-A104-76829D3705E2
// Assembly location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.dll
// XML documentation location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.xml

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Datadog.System.Diagnostics.CodeAnalysis;
using Datadog.System.Runtime.Versioning;


#nullable enable
namespace Datadog.System.Collections.Immutable
{
    /// <summary>Represents an array that is immutable; meaning it cannot be changed once it is created.
    /// 
    /// NuGet package: System.Collections.Immutable (about immutable collections and how to install)</summary>
    /// <typeparam name="T">The type of element stored by the array.</typeparam>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    [NonVersionable]
    public readonly struct ImmutableArray<T> :
      IReadOnlyList<T>,
      IReadOnlyCollection<T>,
      IEnumerable<T>,
      IEnumerable,
      IList<T>,
      ICollection<T>,
      IEquatable<ImmutableArray<T>>,
      IList,
      ICollection,
      IImmutableArray,
      IStructuralComparable,
      IStructuralEquatable,
      IImmutableList<T>
    {
        /// <summary>Gets an empty immutable array.</summary>
        public static readonly ImmutableArray<T> Empty = new ImmutableArray<T>(new T[0]);
        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        internal readonly T[]? array;

        /// <summary>Gets or sets the element at the specified index in the read-only list.</summary>
        /// <param name="index">The zero-based index of the element to get.</param>
        /// <exception cref="T:System.NotSupportedException">Always thrown from the setter.</exception>
        /// <exception cref="T:System.InvalidOperationException">The <see cref="P:System.Collections.Immutable.ImmutableArray`1.IsDefault" /> property returns <see langword="true" />.</exception>
        /// <returns>The element at the specified index in the read-only list.</returns>
        T IList<
#nullable disable
        T>.this[int index]
        {
            get
            {
                ImmutableArray<T> immutableArray = this;
                immutableArray.ThrowInvalidOperationIfNotInitialized();
                return immutableArray[index];
            }
            set => throw new NotSupportedException();
        }

        /// <summary>Gets a value indicating whether this instance is read only.</summary>
        /// <returns>
        /// <see langword="true" /> if this instance is read only; otherwise, <see langword="false" />.</returns>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        bool ICollection<T>.IsReadOnly => true;

        /// <summary>Gets the number of items in the collection.</summary>
        /// <exception cref="T:System.InvalidOperationException">The <see cref="P:System.Collections.Immutable.ImmutableArray`1.IsDefault" /> property returns <see langword="true" />.</exception>
        /// <returns>Number of items in the collection.</returns>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        int ICollection<T>.Count
        {
            get
            {
                ImmutableArray<T> immutableArray = this;
                immutableArray.ThrowInvalidOperationIfNotInitialized();
                return immutableArray.Length;
            }
        }

        /// <summary>Gets the number of items in the collection.</summary>
        /// <exception cref="T:System.InvalidOperationException">The <see cref="P:System.Collections.Immutable.ImmutableArray`1.IsDefault" /> property returns <see langword="true" />.</exception>
        /// <returns>The number of items in the collection.</returns>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        int IReadOnlyCollection<T>.Count
        {
            get
            {
                ImmutableArray<T> immutableArray = this;
                immutableArray.ThrowInvalidOperationIfNotInitialized();
                return immutableArray.Length;
            }
        }


#nullable enable
        /// <summary>Gets the element at the specified index.</summary>
        /// <param name="index">The index.</param>
        /// <exception cref="T:System.InvalidOperationException">The <see cref="P:System.Collections.Immutable.ImmutableArray`1.IsDefault" /> property returns <see langword="true" />.</exception>
        /// <returns>The element.</returns>
        T IReadOnlyList<
#nullable disable
        T>.this[int index]
        {
            get
            {
                ImmutableArray<T> immutableArray = this;
                immutableArray.ThrowInvalidOperationIfNotInitialized();
                return immutableArray[index];
            }
        }


#nullable enable
        /// <summary>Creates a new read-only span over this immutable array.</summary>
        /// <returns>The read-only span representation of this immutable array.</returns>
        public ReadOnlySpan<T> AsSpan() => new ReadOnlySpan<T>(this.array);

        /// <summary>Creates a new read-only memory region over this immutable array.</summary>
        /// <returns>The read-only memory representation of this immutable array.</returns>
        public ReadOnlyMemory<T> AsMemory() => new ReadOnlyMemory<T>(this.array);

        /// <summary>Searches the array for the specified item.</summary>
        /// <param name="item">The item to search for.</param>
        /// <returns>The zero-based index position of the item if it is found, or -1 if it is not.</returns>
        public int IndexOf(T item)
        {
            ImmutableArray<T> immutableArray = this;
            return immutableArray.IndexOf(item, 0, immutableArray.Length, (IEqualityComparer<T>)EqualityComparer<T>.Default);
        }

        /// <summary>Searches the array for the specified item.</summary>
        /// <param name="item">The item to search for.</param>
        /// <param name="startIndex">The index at which to begin the search.</param>
        /// <param name="equalityComparer">The equality comparer to use in the search.</param>
        /// <returns>The zero-based index position of the item if it is found, or -1 if it is not.</returns>
        public int IndexOf(T item, int startIndex, IEqualityComparer<T>? equalityComparer)
        {
            ImmutableArray<T> immutableArray = this;
            return immutableArray.IndexOf(item, startIndex, immutableArray.Length - startIndex, equalityComparer);
        }

        /// <summary>Searches the array for the specified item.</summary>
        /// <param name="item">The item to search for.</param>
        /// <param name="startIndex">The index at which to begin the search.</param>
        /// <returns>The zero-based index position of the item if it is found, or -1 if it is not.</returns>
        public int IndexOf(T item, int startIndex)
        {
            ImmutableArray<T> immutableArray = this;
            return immutableArray.IndexOf(item, startIndex, immutableArray.Length - startIndex, (IEqualityComparer<T>)EqualityComparer<T>.Default);
        }

        /// <summary>Searches the array for the specified item.</summary>
        /// <param name="item">The item to search for.</param>
        /// <param name="startIndex">The index at which to begin the search.</param>
        /// <param name="count">The number of elements to search.</param>
        /// <returns>The zero-based index position of the item if it is found, or -1 if it is not.</returns>
        public int IndexOf(T item, int startIndex, int count) => this.IndexOf(item, startIndex, count, (IEqualityComparer<T>)EqualityComparer<T>.Default);

        /// <summary>Searches the array for the specified item.</summary>
        /// <param name="item">The item to search for.</param>
        /// <param name="startIndex">The index at which to begin the search.</param>
        /// <param name="count">The number of elements to search.</param>
        /// <param name="equalityComparer">The equality comparer to use in the search.</param>
        /// <returns>The zero-based index position of the item if it is found, or -1 if it is not.</returns>
        public int IndexOf(T item, int startIndex, int count, IEqualityComparer<T>? equalityComparer)
        {
            ImmutableArray<T> immutableArray = this;
            immutableArray.ThrowNullRefIfNotInitialized();
            if (count == 0 && startIndex == 0)
                return -1;
            Requires.Range(startIndex >= 0 && startIndex < immutableArray.Length, nameof(startIndex));
            Requires.Range(count >= 0 && startIndex + count <= immutableArray.Length, nameof(count));
            if (equalityComparer == null)
                equalityComparer = (IEqualityComparer<T>)EqualityComparer<T>.Default;
            if (equalityComparer == EqualityComparer<T>.Default)
                return Array.IndexOf<T>(immutableArray.array, item, startIndex, count);
            for (int index = startIndex; index < startIndex + count; ++index)
            {
                if (equalityComparer.Equals(immutableArray.array[index], item))
                    return index;
            }
            return -1;
        }

        /// <summary>Searches the array for the specified item; starting at the end of the array.</summary>
        /// <param name="item">The item to search for.</param>
        /// <returns>The 0-based index into the array where the item was found; or -1 if it could not be found.</returns>
        public int LastIndexOf(T item)
        {
            ImmutableArray<T> immutableArray = this;
            return immutableArray.IsEmpty ? -1 : immutableArray.LastIndexOf(item, immutableArray.Length - 1, immutableArray.Length, (IEqualityComparer<T>)EqualityComparer<T>.Default);
        }

        /// <summary>Searches the array for the specified item; starting at the end of the array.</summary>
        /// <param name="item">The item to search for.</param>
        /// <param name="startIndex">The index at which to begin the search.</param>
        /// <returns>The 0-based index into the array where the item was found; or -1 if it could not be found.</returns>
        public int LastIndexOf(T item, int startIndex)
        {
            ImmutableArray<T> immutableArray = this;
            return immutableArray.IsEmpty && startIndex == 0 ? -1 : immutableArray.LastIndexOf(item, startIndex, startIndex + 1, (IEqualityComparer<T>)EqualityComparer<T>.Default);
        }

        /// <summary>Searches the array for the specified item; starting at the end of the array.</summary>
        /// <param name="item">The item to search for.</param>
        /// <param name="startIndex">The index at which to begin the search.</param>
        /// <param name="count">The number of elements to search.</param>
        /// <returns>The 0-based index into the array where the item was found; or -1 if it could not be found.</returns>
        public int LastIndexOf(T item, int startIndex, int count) => this.LastIndexOf(item, startIndex, count, (IEqualityComparer<T>)EqualityComparer<T>.Default);

        /// <summary>Searches the array for the specified item; starting at the end of the array.</summary>
        /// <param name="item">The item to search for.</param>
        /// <param name="startIndex">The index at which to begin the search.</param>
        /// <param name="count">The number of elements to search.</param>
        /// <param name="equalityComparer">The equality comparer to use in the search.</param>
        /// <returns>The 0-based index into the array where the item was found; or -1 if it could not be found.</returns>
        public int LastIndexOf(
          T item,
          int startIndex,
          int count,
          IEqualityComparer<T>? equalityComparer)
        {
            ImmutableArray<T> immutableArray = this;
            immutableArray.ThrowNullRefIfNotInitialized();
            if (startIndex == 0 && count == 0)
                return -1;
            Requires.Range(startIndex >= 0 && startIndex < immutableArray.Length, nameof(startIndex));
            Requires.Range(count >= 0 && startIndex - count + 1 >= 0, nameof(count));
            if (equalityComparer == null)
                equalityComparer = (IEqualityComparer<T>)EqualityComparer<T>.Default;
            if (equalityComparer == EqualityComparer<T>.Default)
                return Array.LastIndexOf<T>(immutableArray.array, item, startIndex, count);
            for (int index = startIndex; index >= startIndex - count + 1; --index)
            {
                if (equalityComparer.Equals(item, immutableArray.array[index]))
                    return index;
            }
            return -1;
        }

        /// <summary>Determines whether the specified item exists in the array.</summary>
        /// <param name="item">The item to search for.</param>
        /// <returns>
        /// <see langword="true" /> if the specified item was found in the array; otherwise <see langword="false" />.</returns>
        public bool Contains(T item) => this.IndexOf(item) >= 0;

        /// <summary>Returns a new array with the specified value inserted at the specified position.</summary>
        /// <param name="index">The 0-based index into the array at which the new item should be added.</param>
        /// <param name="item">The item to insert at the start of the array.</param>
        /// <returns>A new array with the item inserted at the specified index.</returns>
        public ImmutableArray<T> Insert(int index, T item)
        {
            ImmutableArray<T> immutableArray = this;
            immutableArray.ThrowNullRefIfNotInitialized();
            Requires.Range(index >= 0 && index <= immutableArray.Length, nameof(index));
            if (immutableArray.IsEmpty)
                return ImmutableArray.Create<T>(item);
            T[] objArray = new T[immutableArray.Length + 1];
            objArray[index] = item;
            if (index != 0)
                Array.Copy((Array)immutableArray.array, (Array)objArray, index);
            if (index != immutableArray.Length)
                Array.Copy((Array)immutableArray.array, index, (Array)objArray, index + 1, immutableArray.Length - index);
            return new ImmutableArray<T>(objArray);
        }

        /// <summary>Inserts the specified values at the specified index.</summary>
        /// <param name="index">The index at which to insert the value.</param>
        /// <param name="items">The elements to insert.</param>
        /// <returns>A new immutable array with the items inserted at the specified index.</returns>
        public ImmutableArray<T> InsertRange(int index, IEnumerable<T> items)
        {
            ImmutableArray<T> immutableArray = this;
            immutableArray.ThrowNullRefIfNotInitialized();
            Requires.Range(index >= 0 && index <= immutableArray.Length, nameof(index));
            Requires.NotNull<IEnumerable<T>>(items, nameof(items));
            if (immutableArray.IsEmpty)
                return ImmutableArray.CreateRange<T>(items);
            int count = ImmutableExtensions.GetCount<T>(ref items);
            if (count == 0)
                return immutableArray;
            T[] objArray = new T[immutableArray.Length + count];
            if (index != 0)
                Array.Copy((Array)immutableArray.array, (Array)objArray, index);
            if (index != immutableArray.Length)
                Array.Copy((Array)immutableArray.array, index, (Array)objArray, index + count, immutableArray.Length - index);
            if (!items.TryCopyTo<T>(objArray, index))
            {
                int num = index;
                foreach (T obj in items)
                    objArray[num++] = obj;
            }
            return new ImmutableArray<T>(objArray);
        }

        /// <summary>Inserts the specified values at the specified index.</summary>
        /// <param name="index">The index at which to insert the value.</param>
        /// <param name="items">The elements to insert.</param>
        /// <returns>A new immutable array with the items inserted at the specified index.</returns>
        public ImmutableArray<T> InsertRange(int index, ImmutableArray<T> items)
        {
            ImmutableArray<T> immutableArray = this;
            immutableArray.ThrowNullRefIfNotInitialized();
            items.ThrowNullRefIfNotInitialized();
            Requires.Range(index >= 0 && index <= immutableArray.Length, nameof(index));
            if (immutableArray.IsEmpty)
                return items;
            return items.IsEmpty ? immutableArray : immutableArray.InsertSpanRangeInternal(index, items.AsSpan());
        }

        /// <summary>Returns a copy of the original array with the specified item added to the end.</summary>
        /// <param name="item">The item to be added to the end of the array.</param>
        /// <returns>A new array with the specified item added to the end.</returns>
        public ImmutableArray<T> Add(T item)
        {
            ImmutableArray<T> immutableArray = this;
            return immutableArray.IsEmpty ? ImmutableArray.Create<T>(item) : immutableArray.Insert(immutableArray.Length, item);
        }

        /// <summary>Returns a copy of the original array with the specified elements added to the end of the array.</summary>
        /// <param name="items">The elements to add to the array.</param>
        /// <returns>A new array with the elements added.</returns>
        public ImmutableArray<T> AddRange(IEnumerable<T> items)
        {
            ImmutableArray<T> immutableArray = this;
            return immutableArray.InsertRange(immutableArray.Length, items);
        }

        /// <summary>Adds the specified items to the end of the array.</summary>
        /// <param name="items">The values to add.</param>
        /// <param name="length">The number of elements from the source array to add.</param>
        /// <returns>A new list with the elements added.</returns>
        public ImmutableArray<T> AddRange(T[] items, int length)
        {
            ImmutableArray<T> immutableArray = this;
            immutableArray.ThrowNullRefIfNotInitialized();
            Requires.NotNull<T[]>(items, nameof(items));
            Requires.Range(length >= 0 && length <= items.Length, nameof(length));
            if (items.Length == 0 || length == 0)
                return immutableArray;
            if (immutableArray.IsEmpty)
                return ImmutableArray.Create<T>(items, 0, length);
            T[] objArray = new T[immutableArray.Length + length];
            Array.Copy((Array)immutableArray.array, (Array)objArray, immutableArray.Length);
            Array.Copy((Array)items, 0, (Array)objArray, immutableArray.Length, length);
            return new ImmutableArray<T>(objArray);
        }

        /// <summary>Adds the specified items to the end of the array.</summary>
        /// <param name="items">The values to add.</param>
        /// <typeparam name="TDerived">The type that derives from the type of item already in the array.</typeparam>
        /// <returns>A new list with the elements added.</returns>
        public ImmutableArray<T> AddRange<TDerived>(TDerived[] items) where TDerived : T
        {
            ImmutableArray<T> immutableArray = this;
            immutableArray.ThrowNullRefIfNotInitialized();
            Requires.NotNull<TDerived[]>(items, nameof(items));
            if (items.Length == 0)
                return immutableArray;
            T[] objArray = new T[immutableArray.Length + items.Length];
            Array.Copy((Array)immutableArray.array, (Array)objArray, immutableArray.Length);
            Array.Copy((Array)items, 0, (Array)objArray, immutableArray.Length, items.Length);
            return new ImmutableArray<T>(objArray);
        }

        /// <summary>Adds the specified items to the end of the array.</summary>
        /// <param name="items">The values to add.</param>
        /// <param name="length">The number of elements from the source array to add.</param>
        /// <returns>A new list with the elements added.</returns>
        public ImmutableArray<T> AddRange(ImmutableArray<T> items, int length)
        {
            ImmutableArray<T> immutableArray = this;
            Requires.Range(length >= 0, nameof(length));
            return items.array != null ? immutableArray.AddRange(items.array, length) : immutableArray;
        }

        /// <summary>Adds the specified items to the end of the array.</summary>
        /// <param name="items">The values to add.</param>
        /// <typeparam name="TDerived">The type that derives from the type of item already in the array.</typeparam>
        /// <returns>A new list with the elements added.</returns>
        public ImmutableArray<T> AddRange<TDerived>(ImmutableArray<TDerived> items) where TDerived : T
        {
            ImmutableArray<T> immutableArray = this;
            return items.array != null ? immutableArray.AddRange<TDerived>(items.array) : immutableArray;
        }

        /// <summary>Returns a copy of the original array with the specified elements added to the end of the array.</summary>
        /// <param name="items">The elements to add to the array.</param>
        /// <returns>A new array with the elements added.</returns>
        public ImmutableArray<T> AddRange(ImmutableArray<T> items)
        {
            ImmutableArray<T> immutableArray = this;
            return immutableArray.InsertRange(immutableArray.Length, items);
        }

        /// <summary>Replaces the item at the specified index with the specified item.</summary>
        /// <param name="index">The index of the item to replace.</param>
        /// <param name="item">The item to add to the list.</param>
        /// <returns>The new array that contains <paramref name="item" /> at the specified index.</returns>
        public ImmutableArray<T> SetItem(int index, T item)
        {
            ImmutableArray<T> immutableArray = this;
            immutableArray.ThrowNullRefIfNotInitialized();
            Requires.Range(index >= 0 && index < immutableArray.Length, nameof(index));
            T[] objArray = new T[immutableArray.Length];
            Array.Copy((Array)immutableArray.array, (Array)objArray, immutableArray.Length);
            objArray[index] = item;
            return new ImmutableArray<T>(objArray);
        }

        /// <summary>Finds the first element in the array equal to the specified value and replaces the value with the specified new value.</summary>
        /// <param name="oldValue">The value to find and replace in the array.</param>
        /// <param name="newValue">The value to replace the <c>oldvalue</c> with.</param>
        /// <exception cref="T:System.ArgumentException">
        /// <paramref name="oldValue" /> is not found in the array.</exception>
        /// <returns>A new array that contains <paramref name="newValue" /> even if the new and old values are the same.</returns>
        public ImmutableArray<T> Replace(T oldValue, T newValue) => this.Replace(oldValue, newValue, (IEqualityComparer<T>)EqualityComparer<T>.Default);

        /// <summary>Finds the first element in the array equal to the specified value and replaces the value with the specified new value.</summary>
        /// <param name="oldValue">The value to find and replace in the array.</param>
        /// <param name="newValue">The value to replace the <c>oldvalue</c> with.</param>
        /// <param name="equalityComparer">The equality comparer to use to compare values.</param>
        /// <exception cref="T:System.ArgumentException">
        /// <paramref name="oldValue" /> is not found in the array.</exception>
        /// <returns>A new array that contains <paramref name="newValue" /> even if the new and old values are the same.</returns>
        public ImmutableArray<T> Replace(T oldValue, T newValue, IEqualityComparer<T>? equalityComparer)
        {
            ImmutableArray<T> immutableArray = this;
            int index = immutableArray.IndexOf(oldValue, 0, immutableArray.Length, equalityComparer);
            if (index < 0)
                throw new ArgumentException( nameof(oldValue));
               // throw new ArgumentException(SR.CannotFindOldValue, nameof(oldValue));
            return immutableArray.SetItem(index, newValue);
        }

        /// <summary>Returns an array with the first occurrence of the specified element removed from the array. If no match is found, the current array is returned.</summary>
        /// <param name="item">The item to remove.</param>
        /// <returns>A new array with the item removed.</returns>
        public ImmutableArray<T> Remove(T item) => this.Remove(item, (IEqualityComparer<T>)EqualityComparer<T>.Default);

        /// <summary>Returns an array with the first occurrence of the specified element removed from the array.
        /// 
        /// If no match is found, the current array is returned.</summary>
        /// <param name="item">The item to remove.</param>
        /// <param name="equalityComparer">The equality comparer to use in the search.</param>
        /// <returns>A new array with the specified item removed.</returns>
        public ImmutableArray<T> Remove(T item, IEqualityComparer<T>? equalityComparer)
        {
            ImmutableArray<T> immutableArray = this;
            immutableArray.ThrowNullRefIfNotInitialized();
            int index = immutableArray.IndexOf(item, 0, immutableArray.Length, equalityComparer);
            return index >= 0 ? immutableArray.RemoveAt(index) : immutableArray;
        }

        /// <summary>Returns an array with the element at the specified position removed.</summary>
        /// <param name="index">The 0-based index of the element to remove from the returned array.</param>
        /// <returns>A new array with the item at the specified index removed.</returns>
        public ImmutableArray<T> RemoveAt(int index) => this.RemoveRange(index, 1);

        /// <summary>Returns an array with the elements at the specified position removed.</summary>
        /// <param name="index">The 0-based index of the starting element to remove from the array.</param>
        /// <param name="length">The number of elements to remove from the array.</param>
        /// <returns>The new array with the specified elements removed.</returns>
        public ImmutableArray<T> RemoveRange(int index, int length)
        {
            ImmutableArray<T> immutableArray = this;
            immutableArray.ThrowNullRefIfNotInitialized();
            Requires.Range(index >= 0 && index <= immutableArray.Length, nameof(index));
            Requires.Range(length >= 0 && index + length <= immutableArray.Length, nameof(length));
            if (length == 0)
                return immutableArray;
            T[] objArray = new T[immutableArray.Length - length];
            Array.Copy((Array)immutableArray.array, (Array)objArray, index);
            Array.Copy((Array)immutableArray.array, index + length, (Array)objArray, index, immutableArray.Length - index - length);
            return new ImmutableArray<T>(objArray);
        }

        /// <summary>Removes the specified items from this array.</summary>
        /// <param name="items">The items to remove if matches are found in this list.</param>
        /// <returns>A new array with the elements removed.</returns>
        public ImmutableArray<T> RemoveRange(IEnumerable<T> items) => this.RemoveRange(items, (IEqualityComparer<T>)EqualityComparer<T>.Default);

        /// <summary>Removes the specified items from this array.</summary>
        /// <param name="items">The items to remove if matches are found in this list.</param>
        /// <param name="equalityComparer">The equality comparer to use in the search.</param>
        /// <returns>A new array with the elements removed.</returns>
        public ImmutableArray<T> RemoveRange(
          IEnumerable<T> items,
          IEqualityComparer<T>? equalityComparer)
        {
            ImmutableArray<T> immutableArray = this;
            immutableArray.ThrowNullRefIfNotInitialized();
            Requires.NotNull<IEnumerable<T>>(items, nameof(items));
            SortedSet<int> indicesToRemove = new SortedSet<int>();
            foreach (T obj in items)
            {
                int num = -1;
            label_3:
                num = immutableArray.IndexOf(obj, num + 1, equalityComparer);
                if (num >= 0 && !indicesToRemove.Add(num) && num < immutableArray.Length - 1)
                    goto label_3;
            }
            return immutableArray.RemoveAtRange((ICollection<int>)indicesToRemove);
        }

        /// <summary>Removes the specified values from this list.</summary>
        /// <param name="items">The items to remove if matches are found in this list.</param>
        /// <returns>A new list with the elements removed.</returns>
        public ImmutableArray<T> RemoveRange(ImmutableArray<T> items) => this.RemoveRange(items, (IEqualityComparer<T>)EqualityComparer<T>.Default);

        /// <summary>Removes the specified items from this list.</summary>
        /// <param name="items">The items to remove if matches are found in this list.</param>
        /// <param name="equalityComparer">The equality comparer to use in the search.</param>
        /// <returns>A new array with the elements removed.</returns>
        public ImmutableArray<T> RemoveRange(
          ImmutableArray<T> items,
          IEqualityComparer<T>? equalityComparer)
        {
            Requires.NotNull<T[]>(items.array, nameof(items));
            return this.RemoveRange(items.AsSpan(), equalityComparer);
        }

        /// <summary>Removes all the items from the array that meet the specified condition.</summary>
        /// <param name="match">The delegate that defines the conditions of the elements to remove.</param>
        /// <returns>A new array with items that meet the specified condition removed.</returns>
        public ImmutableArray<T> RemoveAll(Predicate<T> match)
        {
            ImmutableArray<T> immutableArray = this;
            immutableArray.ThrowNullRefIfNotInitialized();
            Requires.NotNull<Predicate<T>>(match, nameof(match));
            if (immutableArray.IsEmpty)
                return immutableArray;
            List<int> indicesToRemove = (List<int>)null;
            for (int index = 0; index < immutableArray.array.Length; ++index)
            {
                if (match(immutableArray.array[index]))
                {
                    if (indicesToRemove == null)
                        indicesToRemove = new List<int>();
                    indicesToRemove.Add(index);
                }
            }
            return indicesToRemove == null ? immutableArray : immutableArray.RemoveAtRange((ICollection<int>)indicesToRemove);
        }

        /// <summary>Returns an array with all the elements removed.</summary>
        /// <returns>An array with all of the elements removed.</returns>
        public ImmutableArray<T> Clear() => ImmutableArray<T>.Empty;

        /// <summary>Sorts the elements in the immutable array using the default comparer.</summary>
        /// <returns>A new immutable array that contains the items in this array, in sorted order.</returns>
        public ImmutableArray<T> Sort()
        {
            ImmutableArray<T> immutableArray = this;
            return immutableArray.Sort(0, immutableArray.Length, (IComparer<T>)Comparer<T>.Default);
        }

        /// <summary>Sorts the elements in the entire <see cref="T:System.Collections.Immutable.ImmutableArray`1" /> using             the specified <see cref="T:System.Comparison`1" />.</summary>
        /// <param name="comparison">The <see cref="T:System.Comparison`1" /> to use when comparing elements.</param>
        /// <exception cref="T:System.ArgumentNullException">
        /// <paramref name="comparison" /> is null.</exception>
        /// <returns>The sorted list.</returns>
        public ImmutableArray<T> Sort(Comparison<T> comparison)
        {
            Requires.NotNull<Comparison<T>>(comparison, nameof(comparison));
            return this.Sort((IComparer<T>)Comparer<T>.Create(comparison));
        }

        /// <summary>Sorts the elements in the immutable array using the specified comparer.</summary>
        /// <param name="comparer">The implementation to use when comparing elements, or <see langword="null" /> to use the default comparer.</param>
        /// <returns>A new immutable array that contains the items in this array, in sorted order.</returns>
        public ImmutableArray<T> Sort(IComparer<T>? comparer)
        {
            ImmutableArray<T> immutableArray = this;
            return immutableArray.Sort(0, immutableArray.Length, comparer);
        }

        /// <summary>Sorts the specified elements in the immutable array using the specified comparer.</summary>
        /// <param name="index">The index of the first element to sort.</param>
        /// <param name="count">The number of elements to include in the sort.</param>
        /// <param name="comparer">The implementation to use when comparing elements, or <see langword="null" /> to use the default comparer.</param>
        /// <returns>A new immutable array that contains the items in this array, in sorted order.</returns>
        public ImmutableArray<T> Sort(int index, int count, IComparer<T>? comparer)
        {
            ImmutableArray<T> immutableArray = this;
            immutableArray.ThrowNullRefIfNotInitialized();
            Requires.Range(index >= 0, nameof(index));
            Requires.Range(count >= 0 && index + count <= immutableArray.Length, nameof(count));
            if (count > 1)
            {
                if (comparer == null)
                    comparer = (IComparer<T>)Comparer<T>.Default;
                bool flag = false;
                for (int index1 = index + 1; index1 < index + count; ++index1)
                {
                    if (comparer.Compare(immutableArray.array[index1 - 1], immutableArray.array[index1]) > 0)
                    {
                        flag = true;
                        break;
                    }
                }
                if (flag)
                {
                    T[] objArray = new T[immutableArray.Length];
                    Array.Copy((Array)immutableArray.array, (Array)objArray, immutableArray.Length);
                    Array.Sort<T>(objArray, index, count, comparer);
                    return new ImmutableArray<T>(objArray);
                }
            }
            return immutableArray;
        }

        /// <summary>Filters the elements of this array to those assignable to the specified type.</summary>
        /// <typeparam name="TResult">The type to filter the elements of the sequence on.</typeparam>
        /// <returns>An <see cref="T:System.Collections.IEnumerable" /> that contains elements from the input sequence of type of <paramref name="TResult" />.</returns>
        public IEnumerable<TResult> OfType<TResult>()
        {
            ImmutableArray<T> immutableArray = this;
            return immutableArray.array == null || immutableArray.array.Length == 0 ? Enumerable.Empty<TResult>() : immutableArray.array.OfType<TResult>();
        }

        public ImmutableArray<T> AddRange(ReadOnlySpan<T> items)
        {
            ImmutableArray<T> immutableArray = this;
            return immutableArray.InsertRange(immutableArray.Length, items);
        }

        /// <summary>Adds the specified values to this list.</summary>
        /// <param name="items">The values to add.</param>
        /// <returns>A new list with the elements added.</returns>
        public ImmutableArray<T> AddRange(params T[] items)
        {
            ImmutableArray<T> immutableArray = this;
            return immutableArray.InsertRange(immutableArray.Length, items);
        }

        /// <summary>Creates a <see cref="T:System.ReadOnlySpan`1" /> over the portion of the current <see cref="T:System.Collections.Immutable.ImmutableArray`1" />, beginning at a specified position for a specified length.</summary>
        /// <param name="start">The index at which to begin the span.</param>
        /// <param name="length">The number of items in the span.</param>
        /// <returns>The <see cref="T:System.ReadOnlySpan`1" /> representation of the <see cref="T:System.Collections.Immutable.ImmutableArray`1" />.</returns>
        public ReadOnlySpan<T> AsSpan(int start, int length) => new ReadOnlySpan<T>(this.array, start, length);

        public void CopyTo(Span<T> destination)
        {
            ImmutableArray<T> immutableArray = this;
            immutableArray.ThrowNullRefIfNotInitialized();
            Requires.Range(immutableArray.Length <= destination.Length, nameof(destination));
            immutableArray.AsSpan().CopyTo(destination);
        }

        /// <summary>Inserts the specified values at the specified index.</summary>
        /// <param name="index">The index at which to insert the value.</param>
        /// <param name="items">The elements to insert.</param>
        /// <returns>The new immutable collection.</returns>
        public ImmutableArray<T> InsertRange(int index, T[] items)
        {
            ImmutableArray<T> immutableArray = this;
            immutableArray.ThrowNullRefIfNotInitialized();
            Requires.Range(index >= 0 && index <= immutableArray.Length, nameof(index));
            Requires.NotNull<T[]>(items, nameof(items));
            if (items.Length == 0)
                return immutableArray;
            return immutableArray.IsEmpty ? new ImmutableArray<T>(items) : immutableArray.InsertSpanRangeInternal(index, (items));
        }

        public ImmutableArray<T> InsertRange(int index, ReadOnlySpan<T> items)
        {
            ImmutableArray<T> immutableArray = this;
            immutableArray.ThrowNullRefIfNotInitialized();
            Requires.Range(index >= 0 && index <= immutableArray.Length, nameof(index));
            if (items.IsEmpty)
                return immutableArray;
            return immutableArray.IsEmpty ? items.ToImmutableArray<T>() : immutableArray.InsertSpanRangeInternal(index, items);
        }

        public ImmutableArray<T> RemoveRange(
          ReadOnlySpan<T> items,
          IEqualityComparer<T>? equalityComparer = null)
        {
            ImmutableArray<T> immutableArray = this;
            immutableArray.ThrowNullRefIfNotInitialized();
            if (items.IsEmpty || immutableArray.IsEmpty)
                return immutableArray;
            if (items.Length == 1)
                return immutableArray.Remove(items[0], equalityComparer);
            SortedSet<int> indicesToRemove = new SortedSet<int>();
            ReadOnlySpan<T> readOnlySpan = items;
            for (int index = 0; index < readOnlySpan.Length; ++index)
            {
                T obj = readOnlySpan[index];
                int num = -1;
                do
                {
                    num = immutableArray.IndexOf(obj, num + 1, equalityComparer);
                }
                while (num >= 0 && !indicesToRemove.Add(num) && num < immutableArray.Length - 1);
            }
            return immutableArray.RemoveAtRange((ICollection<int>)indicesToRemove);
        }

        /// <summary>Removes the specified values from this list.</summary>
        /// <param name="items">The items to remove if matches are found in this list.</param>
        /// <param name="equalityComparer">The equality comparer to use in the search.</param>
        /// <returns>A new list with the elements removed.</returns>
        public ImmutableArray<T> RemoveRange(T[] items, IEqualityComparer<T>? equalityComparer = null)
        {
            ImmutableArray<T> immutableArray = this;
            immutableArray.ThrowNullRefIfNotInitialized();
            Requires.NotNull<T[]>(items, nameof(items));
            return immutableArray.RemoveRange(new ReadOnlySpan<T>(items), equalityComparer);
        }

        /// <summary>Forms a slice out of the current <see cref="T:System.Collections.Immutable.ImmutableArray`1" /> starting at a specified index for a specified length.</summary>
        /// <param name="start">The index at which to begin this slice.</param>
        /// <param name="length">The desired length for the slice.</param>
        /// <returns>An <see cref="T:System.Collections.Immutable.ImmutableArray`1" /> that consists of <paramref name="length" /> elements from the current <see cref="T:System.Collections.Immutable.ImmutableArray`1" />, starting at <paramref name="start" />.</returns>
        public ImmutableArray<T> Slice(int start, int length)
        {
            ImmutableArray<T> items = this;
            items.ThrowNullRefIfNotInitialized();
            return ImmutableArray.Create<T>(items, start, length);
        }


#nullable disable
        /// <summary>Throws <see cref="T:System.NotSupportedException" /> in all cases.</summary>
        /// <param name="index">The index of the location to insert the item.</param>
        /// <param name="item">The item to insert.</param>
        void IList<T>.Insert(int index, T item) => throw new NotSupportedException();

        /// <summary>Throws <see cref="T:System.NotSupportedException" /> in all cases.</summary>
        /// <param name="index">The index.</param>
        void IList<T>.RemoveAt(int index) => throw new NotSupportedException();

        /// <summary>Throws <see cref="T:System.NotSupportedException" /> in all cases.</summary>
        /// <param name="item">The item to add to the end of the array.</param>
        void ICollection<T>.Add(T item) => throw new NotSupportedException();

        /// <summary>Throws <see cref="T:System.NotSupportedException" /> in all cases.</summary>
        void ICollection<T>.Clear() => throw new NotSupportedException();

        /// <summary>Throws <see cref="T:System.NotSupportedException" /> in all cases.</summary>
        /// <param name="item">The object to remove from the array.</param>
        /// <returns>Throws <see cref="T:System.NotSupportedException" /> in all cases.</returns>
        bool ICollection<T>.Remove(T item) => throw new NotSupportedException();

        /// <summary>Returns an array with all the elements removed.</summary>
        /// <returns>An array with all the elements removed.</returns>
        IImmutableList<T> IImmutableList<T>.Clear()
        {
            ImmutableArray<T> immutableArray = this;
            immutableArray.ThrowInvalidOperationIfNotInitialized();
            return (IImmutableList<T>)immutableArray.Clear();
        }

        /// <summary>Returns a copy of the original array with the specified item added to the end.</summary>
        /// <param name="value">The value to add to the end of the array.</param>
        /// <returns>A new array with the specified item added to the end.</returns>
        IImmutableList<T> IImmutableList<T>.Add(T value)
        {
            ImmutableArray<T> immutableArray = this;
            immutableArray.ThrowInvalidOperationIfNotInitialized();
            return (IImmutableList<T>)immutableArray.Add(value);
        }

        /// <summary>Returns a copy of the original array with the specified elements added to the end of the array.</summary>
        /// <param name="items">The elements to add to the end of the array.</param>
        /// <returns>A new array with the elements added to the end.</returns>
        IImmutableList<T> IImmutableList<T>.AddRange(IEnumerable<T> items)
        {
            ImmutableArray<T> immutableArray = this;
            immutableArray.ThrowInvalidOperationIfNotInitialized();
            return (IImmutableList<T>)immutableArray.AddRange(items);
        }

        /// <summary>Returns a new array with the specified value inserted at the specified position.</summary>
        /// <param name="index">The 0-based index into the array at which the new item should be added.</param>
        /// <param name="element">The item to insert at the start of the array.</param>
        /// <returns>A new array with the specified value inserted.</returns>
        IImmutableList<T> IImmutableList<T>.Insert(int index, T element)
        {
            ImmutableArray<T> immutableArray = this;
            immutableArray.ThrowInvalidOperationIfNotInitialized();
            return (IImmutableList<T>)immutableArray.Insert(index, element);
        }

        /// <summary>Inserts the specified values at the specified index.</summary>
        /// <param name="index">The index at which to insert the value.</param>
        /// <param name="items">The elements to insert.</param>
        /// <returns>A new array with the specified values inserted.</returns>
        IImmutableList<T> IImmutableList<T>.InsertRange(int index, IEnumerable<T> items)
        {
            ImmutableArray<T> immutableArray = this;
            immutableArray.ThrowInvalidOperationIfNotInitialized();
            return (IImmutableList<T>)immutableArray.InsertRange(index, items);
        }

        /// <summary>Returns an array with the first occurrence of the specified element removed from the array; if no match is found, the current array is returned.</summary>
        /// <param name="value">The value to remove from the array.</param>
        /// <param name="equalityComparer">The equality comparer to use in the search.</param>
        /// <returns>A new array with the value removed.</returns>
        IImmutableList<T> IImmutableList<T>.Remove(T value, IEqualityComparer<T> equalityComparer)
        {
            ImmutableArray<T> immutableArray = this;
            immutableArray.ThrowInvalidOperationIfNotInitialized();
            return (IImmutableList<T>)immutableArray.Remove(value, equalityComparer);
        }

        /// <summary>Removes all the items from the array that meet the specified condition.</summary>
        /// <param name="match">The delegate that defines the conditions of the elements to remove.</param>
        /// <returns>A new array with items that meet the specified condition removed.</returns>
        IImmutableList<T> IImmutableList<T>.RemoveAll(Predicate<T> match)
        {
            ImmutableArray<T> immutableArray = this;
            immutableArray.ThrowInvalidOperationIfNotInitialized();
            return (IImmutableList<T>)immutableArray.RemoveAll(match);
        }

        /// <summary>Removes the specified items from this array.</summary>
        /// <param name="items">The items to remove if matches are found in this list.</param>
        /// <param name="equalityComparer">The equality comparer to use in the search.</param>
        /// <returns>A new array with the elements removed.</returns>
        IImmutableList<T> IImmutableList<T>.RemoveRange(
          IEnumerable<T> items,
          IEqualityComparer<T> equalityComparer)
        {
            ImmutableArray<T> immutableArray = this;
            immutableArray.ThrowInvalidOperationIfNotInitialized();
            return (IImmutableList<T>)immutableArray.RemoveRange(items, equalityComparer);
        }

        /// <summary>Returns an array with the elements at the specified position removed.</summary>
        /// <param name="index">The 0-based index of the starting element to remove from the array.</param>
        /// <param name="count">The number of elements to remove from the array.</param>
        /// <returns>The new array with the specified elements removed.</returns>
        IImmutableList<T> IImmutableList<T>.RemoveRange(int index, int count)
        {
            ImmutableArray<T> immutableArray = this;
            immutableArray.ThrowInvalidOperationIfNotInitialized();
            return (IImmutableList<T>)immutableArray.RemoveRange(index, count);
        }

        /// <summary>Returns an array with the element at the specified position removed.</summary>
        /// <param name="index">The 0-based index of the element to remove from the returned array.</param>
        /// <returns>A new array with the specified item removed.</returns>
        IImmutableList<T> IImmutableList<T>.RemoveAt(int index)
        {
            ImmutableArray<T> immutableArray = this;
            immutableArray.ThrowInvalidOperationIfNotInitialized();
            return (IImmutableList<T>)immutableArray.RemoveAt(index);
        }

        /// <summary>Replaces the item at the specified index with the specified item.</summary>
        /// <param name="index">The index of the item to replace.</param>
        /// <param name="value">The value to add to the list.</param>
        /// <returns>The new array that contains <paramref name="item" /> at the specified index.</returns>
        IImmutableList<T> IImmutableList<T>.SetItem(int index, T value)
        {
            ImmutableArray<T> immutableArray = this;
            immutableArray.ThrowInvalidOperationIfNotInitialized();
            return (IImmutableList<T>)immutableArray.SetItem(index, value);
        }

        /// <summary>Finds the first element in the array equal to the specified value and replaces the value with the specified new value.</summary>
        /// <param name="oldValue">The value to find and replace in the array.</param>
        /// <param name="newValue">The value to replace the <c>oldvalue</c> with.</param>
        /// <param name="equalityComparer">The equality comparer to use to compare values.</param>
        /// <exception cref="T:System.ArgumentException">
        /// <paramref name="oldValue" /> is not found in the array.</exception>
        /// <returns>A new array that contains <paramref name="newValue" /> even if the new and old values are the same.</returns>
        IImmutableList<T> IImmutableList<T>.Replace(
          T oldValue,
          T newValue,
          IEqualityComparer<T> equalityComparer)
        {
            ImmutableArray<T> immutableArray = this;
            immutableArray.ThrowInvalidOperationIfNotInitialized();
            return (IImmutableList<T>)immutableArray.Replace(oldValue, newValue, equalityComparer);
        }

        /// <summary>Throws <see cref="T:System.NotSupportedException" /> in all cases.</summary>
        /// <param name="value">The value to add to the array.</param>
        /// <exception cref="T:System.NotSupportedException">Thrown in all cases.</exception>
        /// <returns>Throws <see cref="T:System.NotSupportedException" /> in all cases.</returns>
        int IList.Add(object value) => throw new NotSupportedException();

        /// <summary>Throws <see cref="T:System.NotSupportedException" /> in all cases.</summary>
        /// <exception cref="T:System.NotSupportedException">Thrown in all cases.</exception>
        void IList.Clear() => throw new NotSupportedException();

        /// <summary>Throws <see cref="T:System.NotSupportedException" /> in all cases.</summary>
        /// <param name="value">The value to check for.</param>
        /// <returns>Throws <see cref="T:System.NotSupportedException" /> in all cases.</returns>
        bool IList.Contains(object value)
        {
            ImmutableArray<T> immutableArray = this;
            immutableArray.ThrowInvalidOperationIfNotInitialized();
            return immutableArray.Contains((T)value);
        }

        /// <summary>Gets the value at the specified index.</summary>
        /// <param name="value">The value to return the index of.</param>
        /// <returns>The value of the element at the specified index.</returns>
        int IList.IndexOf(object value)
        {
            ImmutableArray<T> immutableArray = this;
            immutableArray.ThrowInvalidOperationIfNotInitialized();
            return immutableArray.IndexOf((T)value);
        }

        /// <summary>Throws <see cref="T:System.NotSupportedException" /> in all cases.</summary>
        /// <param name="index">Index that indicates where to insert the item.</param>
        /// <param name="value">The value to insert.</param>
        /// <exception cref="T:System.NotSupportedException">Thrown in all cases.</exception>
        void IList.Insert(int index, object value) => throw new NotSupportedException();

        /// <summary>Gets a value indicating whether this instance is fixed size.</summary>
        /// <returns>
        /// <see langword="true" /> if this instance is fixed size; otherwise, <see langword="false" />.</returns>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        bool IList.IsFixedSize => true;

        /// <summary>Gets a value indicating whether this instance is read only.</summary>
        /// <returns>
        /// <see langword="true" /> if this instance is read only; otherwise, <see langword="false" />.</returns>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        bool IList.IsReadOnly => true;

        /// <summary>Gets the size of the array.</summary>
        /// <exception cref="T:System.InvalidOperationException">The <see cref="P:System.Collections.Immutable.ImmutableArray`1.IsDefault" /> property returns <see langword="true" />.</exception>
        /// <returns>The number of items in the collection.</returns>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        int ICollection.Count
        {
            get
            {
                ImmutableArray<T> immutableArray = this;
                immutableArray.ThrowInvalidOperationIfNotInitialized();
                return immutableArray.Length;
            }
        }

        /// <summary>See the <see cref="T:System.Collections.ICollection" /> interface. Always returns <see langword="true" /> since since immutable collections are thread-safe.</summary>
        /// <returns>Boolean value determining whether the collection is thread-safe.</returns>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        bool ICollection.IsSynchronized => true;


#nullable enable
        /// <summary>Gets the sync root.</summary>
        /// <returns>An object for synchronizing access to the collection.</returns>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        object ICollection.SyncRoot => throw new NotSupportedException();


#nullable disable
        /// <summary>Throws <see cref="T:System.NotSupportedException" /> in all cases.</summary>
        /// <param name="value">The value to remove from the array.</param>
        /// <exception cref="T:System.NotSupportedException">Thrown in all cases.</exception>
        void IList.Remove(object value) => throw new NotSupportedException();

        /// <summary>Throws <see cref="T:System.NotSupportedException" /> in all cases.</summary>
        /// <param name="index">The index of the item to remove.</param>
        /// <exception cref="T:System.NotSupportedException">Thrown in all cases.</exception>
        void IList.RemoveAt(int index) => throw new NotSupportedException();


#nullable enable
        /// <summary>Gets or sets the <see cref="T:System.Object" /> at the specified index.</summary>
        /// <param name="index">The index.</param>
        /// <exception cref="T:System.NotSupportedException">Always thrown from the setter.</exception>
        /// <exception cref="T:System.InvalidOperationException">The <see cref="P:System.Collections.Immutable.ImmutableArray`1.IsDefault" /> property returns <see langword="true" />.</exception>
        /// <returns>The object at the specified index.</returns>
        object? IList.this[int index]
        {
            get
            {
                ImmutableArray<T> immutableArray = this;
                immutableArray.ThrowInvalidOperationIfNotInitialized();
                return (object)immutableArray[index];
            }
            set => throw new NotSupportedException();
        }


#nullable disable
        /// <summary>Copies this array to another array starting at the specified index.</summary>
        /// <param name="array">The array to copy this array to.</param>
        /// <param name="index">The index in the destination array to start the copy operation.</param>
        void ICollection.CopyTo(Array array, int index)
        {
            ImmutableArray<T> immutableArray = this;
            immutableArray.ThrowInvalidOperationIfNotInitialized();
            Array.Copy((Array)immutableArray.array, 0, array, index, immutableArray.Length);
        }

        /// <summary>Determines whether this array is structurally equal to the specified array.</summary>
        /// <param name="other">The array to compare with the current instance.</param>
        /// <param name="comparer">An object that determines whether the current instance and other are structurally equal.</param>
        /// <returns>
        /// <see langword="true" /> if the two arrays are structurally equal; otherwise, <see langword="false" />.</returns>
        bool IStructuralEquatable.Equals(object other, IEqualityComparer comparer)
        {
            ImmutableArray<T> immutableArray1 = this;
            Array pattern_0 = null;
            switch (other)
            {
                case IImmutableArray immutableArray2:
                    pattern_0 = immutableArray2.Array;
                    if (immutableArray1.array == null && pattern_0 == null)
                        return true;
                    if (immutableArray1.array == null)
                        return false;
                    break;
            }
            return ((IStructuralEquatable)immutableArray1.array).Equals((object)pattern_0, comparer);
        }

        /// <summary>Returns a hash code for the current instance.</summary>
        /// <param name="comparer">An object that computes the hash code of the current object.</param>
        /// <returns>The hash code for the current instance.</returns>
        int IStructuralEquatable.GetHashCode(IEqualityComparer comparer)
        {
            ImmutableArray<T> immutableArray = this;
            IStructuralEquatable array = (IStructuralEquatable)immutableArray.array;
            return array == null ? immutableArray.GetHashCode() : array.GetHashCode(comparer);
        }

        /// <summary>Determines whether the current collection element precedes, occurs in the same position as, or follows another element in the sort order.</summary>
        /// <param name="other">The element to compare with the current instance.</param>
        /// <param name="comparer">The object used to compare members of the current array with the corresponding members of other array.</param>
        /// <exception cref="T:System.ArgumentException">The arrays are not the same length.</exception>
        /// <returns>An integer that indicates whether the current element precedes, is in the same position or follows the other element.</returns>
        int IStructuralComparable.CompareTo(object other, IComparer comparer)
        {
            ImmutableArray<T> immutableArray1 = this;
            if (!(other is Array other1) && other is IImmutableArray immutableArray2)
            {
                other1 = immutableArray2.Array;
                if (immutableArray1.array == null && other1 == null)
                    return 0;
                if (immutableArray1.array == null ^ other1 == null)
                    throw new ArgumentException( nameof(other));
                    //throw new ArgumentException(SR.ArrayInitializedStateNotEqual, nameof(other));
            }

            other1 = null;
            if (other1 == null)
                //throw new ArgumentException(SR.ArrayLengthsNotEqual, nameof(other));
                throw new ArgumentException( nameof(other));
            return ((IStructuralComparable)immutableArray1.array ?? throw new ArgumentException( nameof(other))).CompareTo((object)other1, comparer);
           // return ((IStructuralComparable)immutableArray1.array ?? throw new ArgumentException(SR.ArrayInitializedStateNotEqual, nameof(other))).CompareTo((object)other1, comparer);
        }

        private ImmutableArray<T> RemoveAtRange(ICollection<int> indicesToRemove)
        {
            ImmutableArray<T> immutableArray = this;
            immutableArray.ThrowNullRefIfNotInitialized();
            Requires.NotNull<ICollection<int>>(indicesToRemove, nameof(indicesToRemove));
            if (indicesToRemove.Count == 0)
                return immutableArray;
            T[] objArray = new T[immutableArray.Length - indicesToRemove.Count];
            int destinationIndex = 0;
            int num1 = 0;
            int num2 = -1;
            foreach (int num3 in (IEnumerable<int>)indicesToRemove)
            {
                int length = num2 == -1 ? num3 : num3 - num2 - 1;
                Array.Copy((Array)immutableArray.array, destinationIndex + num1, (Array)objArray, destinationIndex, length);
                ++num1;
                destinationIndex += length;
                num2 = num3;
            }
            Array.Copy((Array)immutableArray.array, destinationIndex + num1, (Array)objArray, destinationIndex, immutableArray.Length - (destinationIndex + num1));
            return new ImmutableArray<T>(objArray);
        }

        private ImmutableArray<T> InsertSpanRangeInternal(int index, ReadOnlySpan<T> items)
        {
            T[] objArray = new T[this.Length + items.Length];
            if (index != 0)
                Array.Copy((Array)this.array, (Array)objArray, index);
            items.CopyTo(new Span<T>(objArray, index, items.Length));
            if (index != this.Length)
                Array.Copy((Array)this.array, index, (Array)objArray, index + items.Length, this.Length - index);
            return new ImmutableArray<T>(objArray);
        }


#nullable enable
        internal ImmutableArray(T[]? items) => this.array = items;

        /// <summary>Returns a value that indicates if two arrays are equal.</summary>
        /// <param name="left">The array to the left of the operator.</param>
        /// <param name="right">The array to the right of the operator.</param>
        /// <returns>
        /// <see langword="true" /> if the arrays are equal; otherwise, <see langword="false" />.</returns>
        [NonVersionable]
        public static bool operator ==(ImmutableArray<T> left, ImmutableArray<T> right) => left.Equals(right);

        /// <summary>Returns a value that indicates whether two arrays are not equal.</summary>
        /// <param name="left">The array to the left of the operator.</param>
        /// <param name="right">The array to the right of the operator.</param>
        /// <returns>
        /// <see langword="true" /> if the arrays are not equal; otherwise, <see langword="false" />.</returns>
        [NonVersionable]
        public static bool operator !=(ImmutableArray<T> left, ImmutableArray<T> right) => !left.Equals(right);

        /// <summary>Returns a value that indicates if two arrays are equal.</summary>
        /// <param name="left">The array to the left of the operator.</param>
        /// <param name="right">The array to the right of the operator.</param>
        /// <returns>
        /// <see langword="true" /> if the arrays are equal; otherwise, <see langword="false" />.</returns>
        public static bool operator ==(ImmutableArray<T>? left, ImmutableArray<T>? right) => left.GetValueOrDefault().Equals(right.GetValueOrDefault());

        /// <summary>Checks for inequality between two array.</summary>
        /// <param name="left">The object to the left of the operator.</param>
        /// <param name="right">The object to the right of the operator.</param>
        /// <returns>
        /// <see langword="true" /> if the two arrays are not equal; otherwise, <see langword="false" />.</returns>
        public static bool operator !=(ImmutableArray<T>? left, ImmutableArray<T>? right) => !left.GetValueOrDefault().Equals(right.GetValueOrDefault());

        /// <summary>Gets the element at the specified index in the immutable array.</summary>
        /// <param name="index">The zero-based index of the element to get.</param>
        /// <returns>The element at the specified index in the immutable array.</returns>
        public T this[int index]
        {
            [NonVersionable]
            get => this.array[index];
        }

        /// <summary>Gets a read-only reference to the element at the specified <paramref name="index" /> in the read-only list.</summary>
        /// <param name="index">The zero-based index of the element to get a reference to.</param>
        /// <returns>A read-only reference to the element at the specified <paramref name="index" /> in the read-only list.</returns>
        public ref readonly T ItemRef(int index) => ref this.array[index];

        /// <summary>Gets a value indicating whether this <see cref="T:System.Collections.Immutable.ImmutableArray`1" /> is empty.</summary>
        /// <returns>
        /// <see langword="true" /> if the <see cref="T:System.Collections.Immutable.ImmutableArray`1" /> is empty; otherwise, <see langword="false" />.</returns>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public bool IsEmpty
        {
            [NonVersionable]
            get => this.array.Length == 0;
        }

        /// <summary>Gets the number of elements in the array.</summary>
        /// <returns>The number of elements in the array.</returns>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public int Length
        {
            [NonVersionable]
            get => this.array.Length;
        }

        /// <summary>Gets a value indicating whether this array was declared but not initialized.</summary>
        /// <returns>
        /// <see langword="true" /> if the <see cref="T:System.Collections.Immutable.ImmutableArray`1" /> is <see langword="null" />; otherwise, <see langword="false" />.</returns>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public bool IsDefault => this.array == null;

        /// <summary>Gets a value indicating whether this <see cref="T:System.Collections.Immutable.ImmutableArray`1" /> is empty or is not initialized.</summary>
        /// <returns>
        /// <see langword="true" /> if the <see cref="T:System.Collections.Immutable.ImmutableArray`1" /> is <see langword="null" /> or <see cref="F:System.Collections.Immutable.ImmutableArray`1.Empty" />; otherwise, <see langword="false" />.</returns>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public bool IsDefaultOrEmpty
        {
            get
            {
                ImmutableArray<T> immutableArray = this;
                return immutableArray.array == null || immutableArray.array.Length == 0;
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        Array? IImmutableArray.Array => (Array)this.array;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay
        {
            get
            {
                ImmutableArray<T> immutableArray = this;
                return !immutableArray.IsDefault ? string.Format("Length = {0}", (object)immutableArray.Length) : "Uninitialized";
            }
        }

        /// <summary>Copies the contents of this array to the specified array.</summary>
        /// <param name="destination">The array to copy to.</param>
        public void CopyTo(T[] destination)
        {
            ImmutableArray<T> immutableArray = this;
            immutableArray.ThrowNullRefIfNotInitialized();
            Array.Copy((Array)immutableArray.array, (Array)destination, immutableArray.Length);
        }

        /// <summary>Copies the contents of this array to the specified array starting at the specified destination index.</summary>
        /// <param name="destination">The array to copy to.</param>
        /// <param name="destinationIndex">The index in <paramref name="array" /> where copying begins.</param>
        public void CopyTo(T[] destination, int destinationIndex)
        {
            ImmutableArray<T> immutableArray = this;
            immutableArray.ThrowNullRefIfNotInitialized();
            Array.Copy((Array)immutableArray.array, 0, (Array)destination, destinationIndex, immutableArray.Length);
        }

        /// <summary>Copies the specified items in this array to the specified array at the specified starting index.</summary>
        /// <param name="sourceIndex">The index of this array where copying begins.</param>
        /// <param name="destination">The array to copy to.</param>
        /// <param name="destinationIndex">The index in <paramref name="array" /> where copying begins.</param>
        /// <param name="length">The number of elements to copy from this array.</param>
        public void CopyTo(int sourceIndex, T[] destination, int destinationIndex, int length)
        {
            ImmutableArray<T> immutableArray = this;
            immutableArray.ThrowNullRefIfNotInitialized();
            Array.Copy((Array)immutableArray.array, sourceIndex, (Array)destination, destinationIndex, length);
        }

        /// <summary>Creates a mutable array that has the same contents as this array and can be efficiently mutated across multiple operations using standard mutable interfaces.</summary>
        /// <returns>The new builder with the same contents as this array.</returns>
        public ImmutableArray<T>.Builder ToBuilder()
        {
            ImmutableArray<T> items = this;
            if (items.Length == 0)
                return new ImmutableArray<T>.Builder();
            ImmutableArray<T>.Builder builder = new ImmutableArray<T>.Builder(items.Length);
            builder.AddRange(items);
            return builder;
        }

        /// <summary>Returns an enumerator that iterates through the contents of the array.</summary>
        /// <returns>An enumerator.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ImmutableArray<
#nullable disable
        T>.Enumerator GetEnumerator()
        {
            ImmutableArray<T> immutableArray = this;
            immutableArray.ThrowNullRefIfNotInitialized();
            return new ImmutableArray<T>.Enumerator(immutableArray.array);
        }

        /// <summary>Returns a hash code for this instance.</summary>
        /// <returns>A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table.</returns>
        public override int GetHashCode()
        {
            ImmutableArray<T> immutableArray = this;
            return immutableArray.array != null ? immutableArray.array.GetHashCode() : 0;
        }


#nullable enable
        /// <summary>Determines if this array is equal to the specified object.</summary>
        /// <param name="obj">The <see cref="T:System.Object" /> to compare with this array.</param>
        /// <returns>
        /// <see langword="true" /> if this array is equal to <paramref name="obj" />; otherwise, <see langword="false" />.</returns>
        public override bool Equals([NotNullWhen(true)] object? obj) => obj is IImmutableArray immutableArray && this.array == immutableArray.Array;

        /// <summary>Indicates whether specified array is equal to this array.</summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>
        /// <see langword="true" /> if <paramref name="other" /> is equal to this array; otherwise, <see langword="false" />.</returns>
        [NonVersionable]
        public bool Equals(ImmutableArray<T> other) => this.array == other.array;

        /// <summary>Initializes a new instance of the <see cref="T:System.Collections.Immutable.ImmutableArray`1" /> struct based on the contents of an existing instance, allowing a covariant static cast to efficiently reuse the existing array.</summary>
        /// <param name="items">The array to initialize the array with. No copy is made.</param>
        /// <typeparam name="TDerived">The type of array element to return.</typeparam>
        /// <returns>An immutable array instance with elements cast to the new type.</returns>
        public static ImmutableArray<
#nullable disable
        T> CastUp<TDerived>(ImmutableArray<
#nullable enable
        TDerived> items) where TDerived : class?, T => new ImmutableArray<T>((T[])items.array);

        /// <summary>Initializes a new instance of the <see cref="T:System.Collections.Immutable.ImmutableArray`1" /> struct by casting the underlying array to an array of type <typeparamref name="TOther" />.</summary>
        /// <typeparam name="TOther">The type of array element to return.</typeparam>
        /// <exception cref="T:System.InvalidCastException">The cast is illegal.</exception>
        /// <returns>An immutable array instance with elements cast to the new type.</returns>
        public ImmutableArray<
#nullable disable
        TOther> CastArray<TOther>() where TOther :
#nullable enable
        class? => new ImmutableArray<TOther>(this.array.Cast<TOther>().ToArray());
        //class? => new ImmutableArray<TOther>((TOther[])this.array);

        /// <summary>Returns a new immutable array that contains the elements of this array cast to a different type.</summary>
        /// <typeparam name="TOther">The type of array element to return.</typeparam>
        /// <returns>An immutable array that contains the elements of this array, cast to a different type. If the cast fails, returns an array whose <see cref="P:System.Collections.Immutable.ImmutableArray`1.IsDefault" /> property returns <see langword="true" />.</returns>
        public ImmutableArray<
#nullable disable
        TOther> As<TOther>() where TOther :
#nullable enable
        class? => new ImmutableArray<TOther>(this.array as TOther[]);


#nullable disable
        /// <summary>Returns an enumerator that iterates through the array.</summary>
        /// <exception cref="T:System.InvalidOperationException">The <see cref="P:System.Collections.Immutable.ImmutableArray`1.IsDefault" /> property returns <see langword="true" />.</exception>
        /// <returns>An enumerator that can be used to iterate through the array.</returns>
        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            ImmutableArray<T> immutableArray = this;
            immutableArray.ThrowInvalidOperationIfNotInitialized();
            return ImmutableArray<T>.EnumeratorObject.Create(immutableArray.array);
        }

        /// <summary>Returns an enumerator that iterates through the immutable array.</summary>
        /// <exception cref="T:System.InvalidOperationException">The <see cref="P:System.Collections.Immutable.ImmutableArray`1.IsDefault" /> property returns <see langword="true" />.</exception>
        /// <returns>An enumerator that iterates through the immutable array.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            ImmutableArray<T> immutableArray = this;
            immutableArray.ThrowInvalidOperationIfNotInitialized();
            return (IEnumerator)ImmutableArray<T>.EnumeratorObject.Create(immutableArray.array);
        }

        internal void ThrowNullRefIfNotInitialized()
        {
            int length = this.array.Length;
        }

        private void ThrowInvalidOperationIfNotInitialized()
        {
            if (this.IsDefault)
                //throw new InvalidOperationException(SR.InvalidOperationOnDefaultArray);
                throw new InvalidOperationException();
        }


#nullable enable
        /// <summary>A writable array accessor that can be converted into an <see cref="T:System.Collections.Immutable.ImmutableArray`1" /> instance without allocating extra memory.
        /// 
        /// NuGet package: System.Collections.Immutable (about immutable collections and how to install)</summary>
        /// <typeparam name="T" />
        [DebuggerDisplay("Count = {Count}")]
        [DebuggerTypeProxy(typeof(ImmutableArrayBuilderDebuggerProxy<>))]
        public sealed class Builder :
          IList<T>,
          ICollection<T>,
          IEnumerable<T>,
          IEnumerable,
          IReadOnlyList<T>,
          IReadOnlyCollection<T>
        {

#nullable disable
            private T[] _elements;
            private int _count;

            internal Builder(int capacity)
            {
                Requires.Range(capacity >= 0, nameof(capacity));
                this._elements = new T[capacity];
                this._count = 0;
            }

            internal Builder()
              : this(8)
            {
            }

            /// <summary>Gets or sets the length of the internal array. When set, the internal array is reallocated to the given capacity if it is not already the specified length.</summary>
            /// <returns>The length of the internal array.</returns>
            public int Capacity
            {
                get => this._elements.Length;
                set
                {
                    if (value < this._count)
                        throw new ArgumentException(nameof(value));
                    // throw new ArgumentException(SR.CapacityMustBeGreaterThanOrEqualToCount, nameof (value));
                    if (value == this._elements.Length)
                        return;
                    if (value > 0)
                    {
                        T[] destinationArray = new T[value];
                        if (this._count > 0)
                            Array.Copy((Array)this._elements, (Array)destinationArray, this._count);
                        this._elements = destinationArray;
                    }
                    else
                        this._elements = ImmutableArray<T>.Empty.array;
                }
            }

            /// <summary>Gets or sets the number of items in the array.</summary>
            /// <returns>The number of items in the array.</returns>
            public int Count
            {
                get => this._count;
                set
                {
                    Requires.Range(value >= 0, nameof(value));
                    if (value < this._count)
                    {
                        if (this._count - value > 64)
                        {
                            Array.Clear((Array)this._elements, value, this._count - value);
                        }
                        else
                        {
                            for (int index = value; index < this.Count; ++index)
                                this._elements[index] = default(T);
                        }
                    }
                    else if (value > this._count)
                        this.EnsureCapacity(value);
                    this._count = value;
                }
            }

            private static void ThrowIndexOutOfRangeException() => throw new IndexOutOfRangeException();


#nullable enable
            /// <summary>Gets or sets the item at the specified index.</summary>
            /// <param name="index">The index of the item to get or set.</param>
            /// <exception cref="T:System.IndexOutOfRangeException">The specified index is not in the array.</exception>
            /// <returns>The item at the specified index.</returns>
            public T this[int index]
            {
                get
                {
                    if (index >= this.Count)
                        ImmutableArray<T>.Builder.ThrowIndexOutOfRangeException();
                    return this._elements[index];
                }
                set
                {
                    if (index >= this.Count)
                        ImmutableArray<T>.Builder.ThrowIndexOutOfRangeException();
                    this._elements[index] = value;
                }
            }

            /// <summary>Gets a read-only reference to the element at the specified index.</summary>
            /// <param name="index">The item index.</param>
            /// <exception cref="T:System.IndexOutOfRangeException">
            /// <paramref name="index" /> is greater or equal to the array count.</exception>
            /// <returns>The read-only reference to the element at the specified index.</returns>
            public ref readonly T ItemRef(int index)
            {
                if (index >= this.Count)
                    ImmutableArray<T>.Builder.ThrowIndexOutOfRangeException();
                return ref this._elements[index];
            }

            /// <summary>Gets a value that indicates whether the <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only.</summary>
            /// <returns>
            /// <see langword="true" /> if the <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only; otherwise, <see langword="false" />.</returns>
            bool ICollection<
#nullable disable
            T>.IsReadOnly => false;


#nullable enable
            /// <summary>Returns an immutable array that contains the current contents of this <see cref="T:System.Collections.Immutable.ImmutableArray`1.Builder" />.</summary>
            /// <returns>An immutable array that contains the current contents of this <see cref="T:System.Collections.Immutable.ImmutableArray`1.Builder" />.</returns>
            public ImmutableArray<T> ToImmutable() => new ImmutableArray<T>(this.ToArray());

            /// <summary>Extracts the internal array as an <see cref="T:System.Collections.Immutable.ImmutableArray`1" /> and replaces it              with a zero length array.</summary>
            /// <exception cref="T:System.InvalidOperationException">When <see cref="P:System.Collections.Immutable.ImmutableArray`1.Builder.Count" /> doesn't              equal <see cref="P:System.Collections.Immutable.ImmutableArray`1.Builder.Capacity" />.</exception>
            /// <returns>An immutable array containing the elements of the builder.</returns>
            public ImmutableArray<T> MoveToImmutable()
            {
                if (this.Capacity != this.Count)
                    throw new InvalidOperationException();
                // throw new InvalidOperationException(SR.CapacityMustEqualCountOnMove);
                T[] elements = this._elements;
                this._elements = ImmutableArray<T>.Empty.array;
                this._count = 0;
                return new ImmutableArray<T>(elements);
            }

            /// <summary>Removes all items from the array.</summary>
            public void Clear() => this.Count = 0;

            /// <summary>Inserts an item in the array at the specified index.</summary>
            /// <param name="index">The zero-based index at which to insert the item.</param>
            /// <param name="item">The object to insert into the array.</param>
            public void Insert(int index, T item)
            {
                Requires.Range(index >= 0 && index <= this.Count, nameof(index));
                this.EnsureCapacity(this.Count + 1);
                if (index < this.Count)
                    Array.Copy((Array)this._elements, index, (Array)this._elements, index + 1, this.Count - index);
                ++this._count;
                this._elements[index] = item;
            }

            /// <summary>Inserts the specified values at the specified index.</summary>
            /// <param name="index">The index at which to insert the value.</param>
            /// <param name="items">The elements to insert.</param>
            public void InsertRange(int index, IEnumerable<T> items)
            {
                Requires.Range(index >= 0 && index <= this.Count, nameof(index));
                Requires.NotNull<IEnumerable<T>>(items, nameof(items));
                int count = ImmutableExtensions.GetCount<T>(ref items);
                this.EnsureCapacity(this.Count + count);
                if (index != this.Count)
                    Array.Copy((Array)this._elements, index, (Array)this._elements, index + count, this._count - index);
                if (!items.TryCopyTo<T>(this._elements, index))
                {
                    foreach (T obj in items)
                        this._elements[index++] = obj;
                }
                this._count += count;
            }

            /// <summary>Inserts the specified values at the specified index.</summary>
            /// <param name="index">The index at which to insert the value.</param>
            /// <param name="items">The elements to insert.</param>
            public void InsertRange(int index, ImmutableArray<T> items)
            {
                Requires.Range(index >= 0 && index <= this.Count, nameof(index));
                if (items.IsEmpty)
                    return;
                this.EnsureCapacity(this.Count + items.Length);
                if (index != this.Count)
                    Array.Copy((Array)this._elements, index, (Array)this._elements, index + items.Length, this._count - index);
                Array.Copy((Array)items.array, 0, (Array)this._elements, index, items.Length);
                this._count += items.Length;
            }

            /// <summary>Adds the specified item to the array.</summary>
            /// <param name="item">The object to add to the array.</param>
            public void Add(T item)
            {
                int capacity = this._count + 1;
                this.EnsureCapacity(capacity);
                this._elements[this._count] = item;
                this._count = capacity;
            }

            /// <summary>Adds the specified items to the end of the array.</summary>
            /// <param name="items">The items to add to the array.</param>
            public void AddRange(IEnumerable<T> items)
            {
                Requires.NotNull<IEnumerable<T>>(items, nameof(items));
                int count;
                if (items.TryGetCount<T>(out count))
                {
                    this.EnsureCapacity(this.Count + count);
                    if (items.TryCopyTo<T>(this._elements, this._count))
                    {
                        this._count += count;
                        return;
                    }
                }
                foreach (T obj in items)
                    this.Add(obj);
            }

            /// <summary>Adds the specified items to the end of the array.</summary>
            /// <param name="items">The items to add to the array.</param>
            public void AddRange(params T[] items)
            {
                Requires.NotNull<T[]>(items, nameof(items));
                int count = this.Count;
                this.Count += items.Length;
                Array.Copy((Array)items, 0, (Array)this._elements, count, items.Length);
            }

            /// <summary>Adds the specified items that derive from the type currently in the array, to the end of the array.</summary>
            /// <param name="items">The items to add to end of the array.</param>
            /// <typeparam name="TDerived">The type that derives from the type of item already in the array.</typeparam>
            public void AddRange<TDerived>(TDerived[] items) where TDerived : T
            {
                Requires.NotNull<TDerived[]>(items, nameof(items));
                int count = this.Count;
                this.Count += items.Length;
                Array.Copy((Array)items, 0, (Array)this._elements, count, items.Length);
            }

            /// <summary>Adds the specified items to the end of the array.</summary>
            /// <param name="items">The items to add to the array.</param>
            /// <param name="length">The number of elements from the source array to add.</param>
            public void AddRange(T[] items, int length)
            {
                Requires.NotNull<T[]>(items, nameof(items));
                Requires.Range(length >= 0 && length <= items.Length, nameof(length));
                int count = this.Count;
                this.Count += length;
                Array.Copy((Array)items, 0, (Array)this._elements, count, length);
            }

            /// <summary>Adds the specified items to the end of the array.</summary>
            /// <param name="items">The items to add to the array.</param>
            public void AddRange(ImmutableArray<T> items) => this.AddRange(items, items.Length);

            /// <summary>Adds the specified items to the end of the array.</summary>
            /// <param name="items">The items to add to the array.</param>
            /// <param name="length">The number of elements from the source array to add.</param>
            public void AddRange(ImmutableArray<T> items, int length)
            {
                Requires.Range(length >= 0, nameof(length));
                if (items.array == null)
                    return;
                this.AddRange(items.array, length);
            }

            public void AddRange(ReadOnlySpan<T> items)
            {
                int count = this.Count;
                this.Count += items.Length;
                items.CopyTo(new Span<T>(this._elements, count, items.Length));
            }

            public void AddRange<TDerived>(ReadOnlySpan<TDerived> items) where TDerived : T
            {
                int count = this.Count;
                this.Count += items.Length;
                Span<T> span = new Span<T>(this._elements, count, items.Length);
                for (int index = 0; index < items.Length; ++index)
                    span[index] = (T)items[index];
            }

            /// <summary>Adds the specified items that derive from the type currently in the array, to the end of the array.</summary>
            /// <param name="items">The items to add to the end of the array.</param>
            /// <typeparam name="TDerived">The type that derives from the type of item already in the array.</typeparam>
            public void AddRange<TDerived>(ImmutableArray<TDerived> items) where TDerived : T
            {
                if (items.array == null)
                    return;
                this.AddRange<TDerived>(items.array);
            }

            /// <summary>Adds the specified items to the end of the array.</summary>
            /// <param name="items">The items to add to the array.</param>
            public void AddRange(ImmutableArray<
#nullable disable
            T>.Builder items)
            {
                Requires.NotNull<ImmutableArray<T>.Builder>(items, nameof(items));
                this.AddRange(items._elements, items.Count);
            }


#nullable enable
            /// <summary>Adds the specified items that derive from the type currently in the array, to the end of the array.</summary>
            /// <param name="items">The items to add to the end of the array.</param>
            /// <typeparam name="TDerived">The type that derives from the type of item already in the array.</typeparam>
            public void AddRange<TDerived>(ImmutableArray<TDerived>.Builder items) where TDerived : T
            {
                Requires.NotNull<ImmutableArray<TDerived>.Builder>(items, nameof(items));
                this.AddRange<TDerived>(items._elements, items.Count);
            }

            /// <summary>Removes the specified element.</summary>
            /// <param name="element">The item to remove.</param>
            /// <returns>
            /// <see langword="true" /> if <paramref name="element" /> was found and removed; otherwise, <see langword="false" />.</returns>
            public bool Remove(T element)
            {
                int index = this.IndexOf(element);
                if (index < 0)
                    return false;
                this.RemoveAt(index);
                return true;
            }

            /// <summary>Removes the first occurrence of the specified element from the builder.
            /// If no match is found, the builder remains unchanged.</summary>
            /// <param name="element">The element to remove.</param>
            /// <param name="equalityComparer">The equality comparer to use in the search.
            /// If <see langword="null" />, <see cref="P:System.Collections.Generic.EqualityComparer`1.Default" /> is used.</param>
            /// <returns>A value indicating whether the specified element was found and removed from the collection.</returns>
            public bool Remove(T element, IEqualityComparer<T>? equalityComparer)
            {
                int index = this.IndexOf(element, 0, this._count, equalityComparer);
                if (index < 0)
                    return false;
                this.RemoveAt(index);
                return true;
            }

            /// <summary>Removes all the elements that match the conditions defined by the specified predicate.</summary>
            /// <param name="match">The <see cref="T:System.Predicate`1" /> delegate that defines the conditions of the elements to remove.</param>
            public void RemoveAll(Predicate<T> match)
            {
                List<int> indicesToRemove = (List<int>)null;
                for (int index = 0; index < this._count; ++index)
                {
                    if (match(this._elements[index]))
                    {
                        if (indicesToRemove == null)
                            indicesToRemove = new List<int>();
                        indicesToRemove.Add(index);
                    }
                }
                if (indicesToRemove == null)
                    return;
                this.RemoveAtRange((ICollection<int>)indicesToRemove);
            }

            /// <summary>Removes the item at the specified index from the array.</summary>
            /// <param name="index">The zero-based index of the item to remove.</param>
            public void RemoveAt(int index)
            {
                Requires.Range(index >= 0 && index < this.Count, nameof(index));
                if (index < this.Count - 1)
                    Array.Copy((Array)this._elements, index + 1, (Array)this._elements, index, this.Count - index - 1);
                --this.Count;
            }

            /// <summary>Removes the specified values from this list.</summary>
            /// <param name="index">The 0-based index into the array for the element to omit from the returned array.</param>
            /// <param name="length">The number of elements to remove.</param>
            public void RemoveRange(int index, int length)
            {
                Requires.Range(index >= 0 && index + length <= this._count, nameof(index));
                if (length == 0)
                    return;
                if (index + length < this._count)
                    Array.Copy((Array)this._elements, index + length, (Array)this._elements, index, this.Count - index - length);
                this._count -= length;
            }

            /// <summary>Removes the specified values from this list.</summary>
            /// <param name="items">The items to remove if matches are found in this list.</param>
            public void RemoveRange(IEnumerable<T> items) => this.RemoveRange(items, (IEqualityComparer<T>)EqualityComparer<T>.Default);

            /// <summary>Removes the specified values from this list.</summary>
            /// <param name="items">The items to remove if matches are found in this list.</param>
            /// <param name="equalityComparer">The equality comparer to use in the search.
            /// If <see langword="null" />, <see cref="P:System.Collections.Generic.EqualityComparer`1.Default" /> is used.</param>
            public void RemoveRange(IEnumerable<T> items, IEqualityComparer<T>? equalityComparer)
            {
                Requires.NotNull<IEnumerable<T>>(items, nameof(items));
                SortedSet<int> indicesToRemove = new SortedSet<int>();
                using (IEnumerator<T> enumerator = items.GetEnumerator())
                {
                label_5:
                    while (enumerator.MoveNext())
                    {
                        T current = enumerator.Current;
                        int num = this.IndexOf(current, 0, this._count, equalityComparer);
                        while (true)
                        {
                            if (num >= 0 && !indicesToRemove.Add(num) && num + 1 < this._count)
                                num = this.IndexOf(current, num + 1, equalityComparer);
                            else
                                goto label_5;
                        }
                    }
                }
                this.RemoveAtRange((ICollection<int>)indicesToRemove);
            }

            /// <summary>Replaces the first equal element in the list with the specified element.</summary>
            /// <param name="oldValue">The element to replace.</param>
            /// <param name="newValue">The element to replace the old element with.</param>
            public void Replace(T oldValue, T newValue) => this.Replace(oldValue, newValue, (IEqualityComparer<T>)EqualityComparer<T>.Default);

            /// <summary>Replaces the first equal element in the list with the specified element.</summary>
            /// <param name="oldValue">The element to replace.</param>
            /// <param name="newValue">The element to replace the old element with.</param>
            /// <param name="equalityComparer">The equality comparer to use in the search.
            /// If <see langword="null" />, <see cref="P:System.Collections.Generic.EqualityComparer`1.Default" /> is used.</param>
            public void Replace(T oldValue, T newValue, IEqualityComparer<T>? equalityComparer)
            {
                int index = this.IndexOf(oldValue, 0, this._count, equalityComparer);
                if (index < 0)
                    return;
                this._elements[index] = newValue;
            }

            /// <summary>Determines whether the array contains a specific value.</summary>
            /// <param name="item">The object to locate in the array.</param>
            /// <returns>
            /// <see langword="true" /> if the object is found; otherwise, <see langword="false" />.</returns>
            public bool Contains(T item) => this.IndexOf(item) >= 0;

            /// <summary>Creates a new array with the current contents of this <see cref="T:System.Collections.Immutable.ImmutableArray`1.Builder" />.</summary>
            /// <returns>A new array with the contents of this <see cref="T:System.Collections.Immutable.ImmutableArray`1.Builder" />.</returns>
            public T[] ToArray()
            {
                if (this.Count == 0)
                    return ImmutableArray<T>.Empty.array;
                T[] destinationArray = new T[this.Count];
                Array.Copy((Array)this._elements, (Array)destinationArray, this.Count);
                return destinationArray;
            }

            /// <summary>Copies the current contents to the specified array.</summary>
            /// <param name="array">The array to copy to.</param>
            /// <param name="index">The index to start the copy operation.</param>
            public void CopyTo(T[] array, int index)
            {
                Requires.NotNull<T[]>(array, nameof(array));
                Requires.Range(index >= 0 && index + this.Count <= array.Length, nameof(index));
                Array.Copy((Array)this._elements, 0, (Array)array, index, this.Count);
            }

            /// <summary>Copies the contents of this array to the specified array.</summary>
            /// <param name="destination">The array to copy to.</param>
            public void CopyTo(T[] destination)
            {
                Requires.NotNull<T[]>(destination, nameof(destination));
                Array.Copy((Array)this._elements, 0, (Array)destination, 0, this.Count);
            }

            /// <summary>Copies the contents of this array to the specified array.</summary>
            /// <param name="sourceIndex">The index into this collection of the first element to copy.</param>
            /// <param name="destination">The array to copy to.</param>
            /// <param name="destinationIndex">The index into the destination array to which the first copied element is written.</param>
            /// <param name="length">The number of elements to copy.</param>
            public void CopyTo(int sourceIndex, T[] destination, int destinationIndex, int length)
            {
                Requires.NotNull<T[]>(destination, nameof(destination));
                Requires.Range(length >= 0, nameof(length));
                Requires.Range(sourceIndex >= 0 && sourceIndex + length <= this.Count, nameof(sourceIndex));
                Requires.Range(destinationIndex >= 0 && destinationIndex + length <= destination.Length, nameof(destinationIndex));
                Array.Copy((Array)this._elements, sourceIndex, (Array)destination, destinationIndex, length);
            }

            private void EnsureCapacity(int capacity)
            {
                if (this._elements.Length >= capacity)
                    return;
                Array.Resize<T>(ref this._elements, Math.Max(this._elements.Length * 2, capacity));
            }

            /// <summary>Determines the index of a specific item in the array.</summary>
            /// <param name="item">The item to locate in the array.</param>
            /// <returns>The index of <paramref name="item" /> if it's found in the list; otherwise, -1.</returns>
            public int IndexOf(T item) => this.IndexOf(item, 0, this._count, (IEqualityComparer<T>)EqualityComparer<T>.Default);

            /// <summary>Determines the index of the specified item.</summary>
            /// <param name="item">The item to locate in the array.</param>
            /// <param name="startIndex">The starting position of the search.</param>
            /// <returns>The index of <paramref name="item" /> if it's found in the list; otherwise, -1.</returns>
            public int IndexOf(T item, int startIndex) => this.IndexOf(item, startIndex, this.Count - startIndex, (IEqualityComparer<T>)EqualityComparer<T>.Default);

            /// <summary>Determines the index of the specified item.</summary>
            /// <param name="item">The item to locate in the array.</param>
            /// <param name="startIndex">The starting position of the search.</param>
            /// <param name="count">The number of elements to search.</param>
            /// <returns>The index of <paramref name="item" /> if it's found in the list; otherwise, -1.</returns>
            public int IndexOf(T item, int startIndex, int count) => this.IndexOf(item, startIndex, count, (IEqualityComparer<T>)EqualityComparer<T>.Default);

            /// <summary>Determines the index for the specified item.</summary>
            /// <param name="item">The item to locate in the array.</param>
            /// <param name="startIndex">The index at which to begin the search.</param>
            /// <param name="count">The starting position of the search.</param>
            /// <param name="equalityComparer">The equality comparer to use in the search.</param>
            /// <returns>The index of <paramref name="item" /> if it's found in the list; otherwise, -1.</returns>
            public int IndexOf(T item, int startIndex, int count, IEqualityComparer<T>? equalityComparer)
            {
                if (count == 0 && startIndex == 0)
                    return -1;
                Requires.Range(startIndex >= 0 && startIndex < this.Count, nameof(startIndex));
                Requires.Range(count >= 0 && startIndex + count <= this.Count, nameof(count));
                if (equalityComparer == null)
                    equalityComparer = (IEqualityComparer<T>)EqualityComparer<T>.Default;
                if (equalityComparer == EqualityComparer<T>.Default)
                    return Array.IndexOf<T>(this._elements, item, startIndex, count);
                for (int index = startIndex; index < startIndex + count; ++index)
                {
                    if (equalityComparer.Equals(this._elements[index], item))
                        return index;
                }
                return -1;
            }

            /// <summary>Searches the array for the specified item.</summary>
            /// <param name="item">The item to search for.</param>
            /// <param name="startIndex">The index at which to begin the search.</param>
            /// <param name="equalityComparer">The equality comparer to use in the search.
            /// If <see langword="null" />, <see cref="P:System.Collections.Generic.EqualityComparer`1.Default" /> is used.</param>
            /// <returns>The 0-based index into the array where the item was found; or -1 if it could not be found.</returns>
            public int IndexOf(T item, int startIndex, IEqualityComparer<T>? equalityComparer) => this.IndexOf(item, startIndex, this.Count - startIndex, equalityComparer);

            /// <summary>Determines the 0-based index of the last occurrence of the specified item in this array.</summary>
            /// <param name="item">The item to search for.</param>
            /// <returns>The 0-based index where the item was found; or -1 if it could not be found.</returns>
            public int LastIndexOf(T item) => this.Count == 0 ? -1 : this.LastIndexOf(item, this.Count - 1, this.Count, (IEqualityComparer<T>)EqualityComparer<T>.Default);

            /// <summary>Determines the 0-based index of the last occurrence of the specified item in this array.</summary>
            /// <param name="item">The item to search for.</param>
            /// <param name="startIndex">The starting position of the search.</param>
            /// <returns>The 0-based index into the array where the item was found; or -1 if it could not be found.</returns>
            public int LastIndexOf(T item, int startIndex)
            {
                if (this.Count == 0 && startIndex == 0)
                    return -1;
                Requires.Range(startIndex >= 0 && startIndex < this.Count, nameof(startIndex));
                return this.LastIndexOf(item, startIndex, startIndex + 1, (IEqualityComparer<T>)EqualityComparer<T>.Default);
            }

            /// <summary>Determines the 0-based index of the last occurrence of the specified item in this array.</summary>
            /// <param name="item">The item to search for.</param>
            /// <param name="startIndex">The starting position of the search.</param>
            /// <param name="count">The number of elements to search.</param>
            /// <returns>The 0-based index into the array where the item was found; or -1 if it could not be found.</returns>
            public int LastIndexOf(T item, int startIndex, int count) => this.LastIndexOf(item, startIndex, count, (IEqualityComparer<T>)EqualityComparer<T>.Default);

            /// <summary>Determines the 0-based index of the last occurrence of the specified item in this array.</summary>
            /// <param name="item">The item to search for.</param>
            /// <param name="startIndex">The starting position of the search.</param>
            /// <param name="count">The number of elements to search.</param>
            /// <param name="equalityComparer">The equality comparer to use in the search.</param>
            /// <returns>The 0-based index into the array where the item was found; or -1 if it could not be found.</returns>
            public int LastIndexOf(
              T item,
              int startIndex,
              int count,
              IEqualityComparer<T>? equalityComparer)
            {
                if (count == 0 && startIndex == 0)
                    return -1;
                Requires.Range(startIndex >= 0 && startIndex < this.Count, nameof(startIndex));
                Requires.Range(count >= 0 && startIndex - count + 1 >= 0, nameof(count));
                if (equalityComparer == null)
                    equalityComparer = (IEqualityComparer<T>)EqualityComparer<T>.Default;
                if (equalityComparer == EqualityComparer<T>.Default)
                    return Array.LastIndexOf<T>(this._elements, item, startIndex, count);
                for (int index = startIndex; index >= startIndex - count + 1; --index)
                {
                    if (equalityComparer.Equals(item, this._elements[index]))
                        return index;
                }
                return -1;
            }

            /// <summary>Reverses the order of elements in the collection.</summary>
            public void Reverse()
            {
                int index1 = 0;
                int index2 = this._count - 1;
                T[] elements = this._elements;
                for (; index1 < index2; --index2)
                {
                    T obj = elements[index1];
                    elements[index1] = elements[index2];
                    elements[index2] = obj;
                    ++index1;
                }
            }

            /// <summary>Sorts the contents of the array.</summary>
            public void Sort()
            {
                if (this.Count <= 1)
                    return;
                Array.Sort<T>(this._elements, 0, this.Count, (IComparer<T>)Comparer<T>.Default);
            }

            /// <summary>Sorts the elements in the entire array using the specified <see cref="T:System.Comparison`1" />.</summary>
            /// <param name="comparison">The <see cref="T:System.Comparison`1" /> to use when comparing elements.</param>
            /// <exception cref="T:System.ArgumentNullException">
            /// <paramref name="comparison" /> is null.</exception>
            public void Sort(Comparison<T> comparison)
            {
                Requires.NotNull<Comparison<T>>(comparison, nameof(comparison));
                if (this.Count <= 1)
                    return;
                Array.Sort<T>(this._elements, 0, this._count, (IComparer<T>)Comparer<T>.Create(comparison));
            }

            /// <summary>Sorts the contents of the array.</summary>
            /// <param name="comparer">The comparer to use for sorting. If comparer is <see langword="null" />, the default comparer for the elements type in the array is used.</param>
            public void Sort(IComparer<T>? comparer)
            {
                if (this.Count <= 1)
                    return;
                Array.Sort<T>(this._elements, 0, this._count, comparer);
            }

            /// <summary>Sorts the contents of the array.</summary>
            /// <param name="index">The starting index for the sort.</param>
            /// <param name="count">The number of elements to include in the sort.</param>
            /// <param name="comparer">The comparer to use for sorting. If comparer is <see langword="null" />, the default comparer for the elements type in the array is used.</param>
            public void Sort(int index, int count, IComparer<T>? comparer)
            {
                Requires.Range(index >= 0, nameof(index));
                Requires.Range(count >= 0 && index + count <= this.Count, nameof(count));
                if (count <= 1)
                    return;
                Array.Sort<T>(this._elements, index, count, comparer);
            }

            public void CopyTo(Span<T> destination)
            {
                Requires.Range(this.Count <= destination.Length, nameof(destination));
                new ReadOnlySpan<T>(this._elements, 0, this.Count).CopyTo(destination);
            }

            /// <summary>Gets an object that can be used to iterate through the collection.</summary>
            /// <returns>An object that can be used to iterate through the collection.</returns>
            public IEnumerator<T> GetEnumerator()
            {
                for (int i = 0; i < this.Count; ++i)
                    yield return this[i];
            }


#nullable disable
            /// <summary>Returns an enumerator that iterates through the array.</summary>
            /// <returns>An enumerator that iterates through the array.</returns>
            IEnumerator<T> IEnumerable<T>.GetEnumerator() => this.GetEnumerator();

            /// <summary>Returns an enumerator that iterates through the array.</summary>
            /// <returns>An enumerator that iterates through the array.</returns>
            IEnumerator IEnumerable.GetEnumerator() => (IEnumerator)this.GetEnumerator();

            private void AddRange<TDerived>(TDerived[] items, int length) where TDerived : T
            {
                this.EnsureCapacity(this.Count + length);
                int count = this.Count;
                this.Count += length;
                T[] elements = this._elements;
                for (int index = 0; index < length; ++index)
                    elements[count + index] = (T)items[index];
            }

            private void RemoveAtRange(ICollection<int> indicesToRemove)
            {
                Requires.NotNull<ICollection<int>>(indicesToRemove, nameof(indicesToRemove));
                if (indicesToRemove.Count == 0)
                    return;
                int destinationIndex = 0;
                int num1 = 0;
                int num2 = -1;
                foreach (int num3 in (IEnumerable<int>)indicesToRemove)
                {
                    int length = num2 == -1 ? num3 : num3 - num2 - 1;
                    Array.Copy((Array)this._elements, destinationIndex + num1, (Array)this._elements, destinationIndex, length);
                    ++num1;
                    destinationIndex += length;
                    num2 = num3;
                }
                Array.Copy((Array)this._elements, destinationIndex + num1, (Array)this._elements, destinationIndex, this._elements.Length - (destinationIndex + num1));
                this._count -= indicesToRemove.Count;
            }
        }


#nullable enable
        /// <summary>An array enumerator.
        /// 
        /// NuGet package: System.Collections.Immutable (about immutable collections and how to install)</summary>
        /// <typeparam name="T" />
        public struct Enumerator
        {

#nullable disable
            private readonly T[] _array;
            private int _index;


#nullable enable
            internal Enumerator(T[] array)
            {
                this._array = array;
                this._index = -1;
            }

            /// <summary>Gets the current item.</summary>
            /// <returns>The current item.</returns>
            public T Current => this._array[this._index];

            /// <summary>Advances to the next value in the array.</summary>
            /// <returns>
            /// <see langword="true" /> if another item exists in the array; otherwise, <see langword="false" />.</returns>
            public bool MoveNext() => ++this._index < this._array.Length;
        }


#nullable disable
        private sealed class EnumeratorObject : IEnumerator<T>, IDisposable, IEnumerator
        {
            private static readonly IEnumerator<T> s_EmptyEnumerator = (IEnumerator<T>)new ImmutableArray<T>.EnumeratorObject(ImmutableArray<T>.Empty.array);
            private readonly T[] _array;
            private int _index;

            private EnumeratorObject(T[] array)
            {
                this._index = -1;
                this._array = array;
            }

            public T Current
            {
                get
                {
                    if ((uint)this._index < (uint)this._array.Length)
                        return this._array[this._index];
                    throw new InvalidOperationException();
                }
            }

            object IEnumerator.Current => (object)this.Current;

            public bool MoveNext()
            {
                int num = this._index + 1;
                int length = this._array.Length;
                if ((uint)num > (uint)length)
                    return false;
                this._index = num;
                return (uint)num < (uint)length;
            }

            void IEnumerator.Reset() => this._index = -1;

            public void Dispose()
            {
            }

            internal static IEnumerator<T> Create(T[] array) => array.Length != 0 ? (IEnumerator<T>)new ImmutableArray<T>.EnumeratorObject(array) : ImmutableArray<T>.EnumeratorObject.s_EmptyEnumerator;
        }
    }
}
