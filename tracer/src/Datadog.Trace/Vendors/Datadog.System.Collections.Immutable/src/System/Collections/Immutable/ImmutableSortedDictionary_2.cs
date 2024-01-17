﻿// Decompiled with JetBrains decompiler
// Type: System.Collections.Immutable.ImmutableSortedDictionary`2
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
using System.Threading;
using Datadog.System.Collections.Generic;
using Datadog.System.Diagnostics.CodeAnalysis;
using Datadog.System.Linq;


#nullable enable
namespace Datadog.System.Collections.Immutable
{
    /// <summary>Represents an immutable sorted dictionary.
    /// 
    /// NuGet package: System.Collections.Immutable (about immutable collections and how to install)</summary>
    /// <typeparam name="TKey">The type of the key contained in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of the value contained in the dictionary.</typeparam>
    [DebuggerDisplay("Count = {Count}")]
    [DebuggerTypeProxy(typeof(ImmutableDictionaryDebuggerProxy<,>))]
    public sealed class ImmutableSortedDictionary<TKey, TValue> :
      IImmutableDictionary<TKey, TValue>,
      IReadOnlyDictionary<TKey, TValue>,
      IReadOnlyCollection<KeyValuePair<TKey, TValue>>,
      IEnumerable<KeyValuePair<TKey, TValue>>,
      IEnumerable,
      ISortKeyCollection<TKey>,
      IDictionary<TKey, TValue>,
      ICollection<KeyValuePair<TKey, TValue>>,
      IDictionary,
      ICollection
      where TKey : notnull
    {
        /// <summary>Gets an empty immutable sorted dictionary.</summary>
        public static readonly ImmutableSortedDictionary<TKey, TValue> Empty = new ImmutableSortedDictionary<TKey, TValue>();

#nullable disable
        private readonly ImmutableSortedDictionary<TKey, TValue>.Node _root;
        private readonly int _count;
        private readonly IComparer<TKey> _keyComparer;
        private readonly IEqualityComparer<TValue> _valueComparer;


#nullable enable
        internal ImmutableSortedDictionary(
          IComparer<TKey>? keyComparer = null,
          IEqualityComparer<TValue>? valueComparer = null)
        {
            this._keyComparer = keyComparer ?? (IComparer<TKey>)Comparer<TKey>.Default;
            this._valueComparer = valueComparer ?? (IEqualityComparer<TValue>)EqualityComparer<TValue>.Default;
            this._root = ImmutableSortedDictionary<TKey, TValue>.Node.EmptyNode;
        }


#nullable disable
        private ImmutableSortedDictionary(
          ImmutableSortedDictionary<TKey, TValue>.Node root,
          int count,
          IComparer<TKey> keyComparer,
          IEqualityComparer<TValue> valueComparer)
        {
            Requires.NotNull<ImmutableSortedDictionary<TKey, TValue>.Node>(root, nameof(root));
            Requires.Range(count >= 0, nameof(count));
            Requires.NotNull<IComparer<TKey>>(keyComparer, nameof(keyComparer));
            Requires.NotNull<IEqualityComparer<TValue>>(valueComparer, nameof(valueComparer));
            root.Freeze();
            this._root = root;
            this._count = count;
            this._keyComparer = keyComparer;
            this._valueComparer = valueComparer;
        }


#nullable enable
        /// <summary>Retrieves an empty immutable sorted dictionary that has the same ordering and key/value comparison rules as this dictionary instance.</summary>
        /// <returns>An empty dictionary with equivalent ordering and key/value comparison rules.</returns>
        public ImmutableSortedDictionary<TKey, TValue> Clear() => !this._root.IsEmpty ? ImmutableSortedDictionary<TKey, TValue>.Empty.WithComparers(this._keyComparer, this._valueComparer) : this;

        /// <summary>Gets the value comparer used to determine whether values are equal.</summary>
        /// <returns>The value comparer used to determine whether values are equal.</returns>
        public IEqualityComparer<TValue> ValueComparer => this._valueComparer;

        /// <summary>Gets a value that indicates whether this instance of the immutable sorted dictionary is empty.</summary>
        /// <returns>
        /// <see langword="true" /> if this instance is empty; otherwise, <see langword="false" />.</returns>
        public bool IsEmpty => this._root.IsEmpty;

        /// <summary>Gets the number of key/value pairs in the immutable sorted dictionary.</summary>
        /// <returns>The number of key/value pairs in the dictionary.</returns>
        public int Count => this._count;

        /// <summary>Gets the keys in the immutable sorted dictionary.</summary>
        /// <returns>The keys in the immutable dictionary.</returns>
        public IEnumerable<TKey> Keys => this._root.Keys;

        /// <summary>Gets the values in the immutable sorted dictionary.</summary>
        /// <returns>The values in the dictionary.</returns>
        public IEnumerable<TValue> Values => this._root.Values;


#nullable disable
        /// <summary>See the <see cref="T:System.Collections.Immutable.IImmutableDictionary`2" /> interface.</summary>
        /// <returns>The <see cref="T:System.Collections.Immutable.IImmutableDictionary`2" /> instance.</returns>
        IImmutableDictionary<TKey, TValue> IImmutableDictionary<TKey, TValue>.Clear() => (IImmutableDictionary<TKey, TValue>)this.Clear();


#nullable enable
        /// <summary>Gets the keys.</summary>
        /// <returns>A collection containing the keys.</returns>
        ICollection<TKey> IDictionary<
#nullable disable
        TKey, TValue>.Keys => (ICollection<TKey>)new KeysCollectionAccessor<TKey, TValue>((IImmutableDictionary<TKey, TValue>)this);


#nullable enable
        /// <summary>Gets the values.</summary>
        /// <returns>A collection containing the values.</returns>
        ICollection<TValue> IDictionary<
#nullable disable
        TKey, TValue>.Values => (ICollection<TValue>)new ValuesCollectionAccessor<TKey, TValue>((IImmutableDictionary<TKey, TValue>)this);

        /// <summary>Gets a value indicating whether the <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only.</summary>
        /// <returns>
        /// <see langword="true" /> if the <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only; otherwise, <see langword="false" />.</returns>
        bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => true;


#nullable enable
        /// <summary>Gets the key comparer for the immutable sorted dictionary.</summary>
        /// <returns>The key comparer for the dictionary.</returns>
        public IComparer<TKey> KeyComparer => this._keyComparer;

        internal ImmutableSortedDictionary<
#nullable disable
        TKey, TValue>.Node Root => this._root;


#nullable enable
        /// <summary>Gets the <paramref name="TValue" /> associated with the specified key.</summary>
        /// <param name="key">The key to retrieve the value for.</param>
        /// <returns>The value associated with the specified key. If no results are found, the operation throws an exception.</returns>
        public TValue this[TKey key]
        {
            get
            {
                Requires.NotNullAllowStructs<TKey>(key, nameof(key));
                TValue obj;
                if (this.TryGetValue(key, out obj))
                    return obj;
                throw new KeyNotFoundException(SR.Format(SR.Arg_KeyNotFoundWithKey, (object)key.ToString()));
            }
        }

        /// <summary>Returns a read-only reference to the value associated with the provided <paramref name="key" />.</summary>
        /// <param name="key">Key of the entry to be looked up.</param>
        /// <exception cref="T:System.Collections.Generic.KeyNotFoundException">The <paramref name="key" /> is not present.</exception>
        /// <returns>A read-only reference to the value associated with the provided <paramref name="key" />.</returns>
        public ref readonly TValue ValueRef(TKey key)
        {
            Requires.NotNullAllowStructs<TKey>(key, nameof(key));
            return ref this._root.ValueRef(key, this._keyComparer);
        }

        /// <summary>Gets or sets the <typeparamref name="TValue" /> with the specified key.</summary>
        /// <param name="key">The object to use as the key of the element to access.</param>
        /// <returns>An object of type <typeparamref name="TValue" /> associated with the <paramref name="key" />.</returns>
        TValue IDictionary<
#nullable disable
        TKey, TValue>.this[TKey key]
        {
            get => this[key];
            set => throw new NotSupportedException();
        }


#nullable enable
        /// <summary>Creates an immutable sorted dictionary with the same contents as this dictionary that can be efficiently mutated across multiple operations by using standard mutable interfaces.</summary>
        /// <returns>A collection with the same contents as this dictionary.</returns>
        public ImmutableSortedDictionary<
#nullable disable
        TKey, TValue>.Builder ToBuilder() => new ImmutableSortedDictionary<TKey, TValue>.Builder(this);


#nullable enable
        /// <summary>Adds an element with the specified key and value to the immutable sorted dictionary.</summary>
        /// <param name="key">The key of the entry to add.</param>
        /// <param name="value">The value of entry to add.</param>
        /// <exception cref="T:System.ArgumentException">The given key already exists in the dictionary but has a different value.</exception>
        /// <returns>A new immutable sorted dictionary that contains the additional key/value pair.</returns>
        public ImmutableSortedDictionary<TKey, TValue> Add(TKey key, TValue value)
        {
            Requires.NotNullAllowStructs<TKey>(key, nameof(key));
            return this.Wrap(this._root.Add(key, value, this._keyComparer, this._valueComparer, out bool _), this._count + 1);
        }

        /// <summary>Sets the specified key and value in the immutable sorted dictionary, possibly overwriting an existing value for the given key.</summary>
        /// <param name="key">The key of the entry to add.</param>
        /// <param name="value">The key value to set.</param>
        /// <returns>A new immutable sorted dictionary that contains the specified key/value pair.</returns>
        public ImmutableSortedDictionary<TKey, TValue> SetItem(TKey key, TValue value)
        {
            Requires.NotNullAllowStructs<TKey>(key, nameof(key));
            bool replacedExistingValue;
            return this.Wrap(this._root.SetItem(key, value, this._keyComparer, this._valueComparer, out replacedExistingValue, out bool _), replacedExistingValue ? this._count : this._count + 1);
        }

        /// <summary>Sets the specified key/value pairs in the immutable sorted dictionary, possibly overwriting existing values for the keys.</summary>
        /// <param name="items">The key/value pairs to set in the dictionary. If any of the keys already exist in the dictionary, this method will overwrite their previous values.</param>
        /// <returns>An immutable dictionary that contains the specified key/value pairs.</returns>
        public ImmutableSortedDictionary<TKey, TValue> SetItems(
          IEnumerable<KeyValuePair<TKey, TValue>> items)
        {
            Requires.NotNull<IEnumerable<KeyValuePair<TKey, TValue>>>(items, nameof(items));
            return this.AddRange(items, true, false);
        }

        /// <summary>Adds the specific key/value pairs to the immutable sorted dictionary.</summary>
        /// <param name="items">The key/value pairs to add.</param>
        /// <exception cref="T:System.ArgumentException">One of the given keys already exists in the dictionary but has a different value.</exception>
        /// <returns>A new immutable dictionary that contains the additional key/value pairs.</returns>
        public ImmutableSortedDictionary<TKey, TValue> AddRange(
          IEnumerable<KeyValuePair<TKey, TValue>> items)
        {
            Requires.NotNull<IEnumerable<KeyValuePair<TKey, TValue>>>(items, nameof(items));
            return this.AddRange(items, false, false);
        }

        /// <summary>Removes the element with the specified value from the immutable sorted dictionary.</summary>
        /// <param name="value">The value of the element to remove.</param>
        /// <returns>A new immutable dictionary with the specified element removed; or this instance if the specified value cannot be found in the dictionary.</returns>
        public ImmutableSortedDictionary<TKey, TValue> Remove(TKey value)
        {
            Requires.NotNullAllowStructs<TKey>(value, nameof(value));
            return this.Wrap(this._root.Remove(value, this._keyComparer, out bool _), this._count - 1);
        }

        /// <summary>Removes the elements with the specified keys from the immutable sorted dictionary.</summary>
        /// <param name="keys">The keys of the elements to remove.</param>
        /// <returns>A new immutable dictionary with the specified keys removed; or this instance if the specified keys cannot be found in the dictionary.</returns>
        public ImmutableSortedDictionary<TKey, TValue> RemoveRange(IEnumerable<TKey> keys)
        {
            Requires.NotNull<IEnumerable<TKey>>(keys, nameof(keys));
            ImmutableSortedDictionary<TKey, TValue>.Node root = this._root;
            int count = this._count;
            foreach (TKey key in keys)
            {
                bool mutated;
                ImmutableSortedDictionary<TKey, TValue>.Node node = root.Remove(key, this._keyComparer, out mutated);
                if (mutated)
                {
                    root = node;
                    --count;
                }
            }
            return this.Wrap(root, count);
        }

        /// <summary>Gets an instance of the immutable sorted dictionary that uses the specified key and value comparers.</summary>
        /// <param name="keyComparer">The key comparer to use.</param>
        /// <param name="valueComparer">The value comparer to use.</param>
        /// <returns>An instance of the immutable dictionary that uses the given comparers.</returns>
        public ImmutableSortedDictionary<TKey, TValue> WithComparers(
          IComparer<TKey>? keyComparer,
          IEqualityComparer<TValue>? valueComparer)
        {
            if (keyComparer == null)
                keyComparer = (IComparer<TKey>)Comparer<TKey>.Default;
            if (valueComparer == null)
                valueComparer = (IEqualityComparer<TValue>)EqualityComparer<TValue>.Default;
            if (keyComparer != this._keyComparer)
                return new ImmutableSortedDictionary<TKey, TValue>(ImmutableSortedDictionary<TKey, TValue>.Node.EmptyNode, 0, keyComparer, valueComparer).AddRange((IEnumerable<KeyValuePair<TKey, TValue>>)this, false, true);
            return valueComparer == this._valueComparer ? this : new ImmutableSortedDictionary<TKey, TValue>(this._root, this._count, this._keyComparer, valueComparer);
        }

        /// <summary>Gets an instance of the immutable sorted dictionary that uses the specified key comparer.</summary>
        /// <param name="keyComparer">The key comparer to use.</param>
        /// <returns>An instance of the immutable dictionary that uses the given comparer.</returns>
        public ImmutableSortedDictionary<TKey, TValue> WithComparers(IComparer<TKey>? keyComparer) => this.WithComparers(keyComparer, this._valueComparer);

        /// <summary>Determines whether the immutable sorted dictionary contains an element with the specified value.</summary>
        /// <param name="value">The value to locate. The value can be <see langword="null" /> for reference types.</param>
        /// <returns>
        /// <see langword="true" /> if the dictionary contains an element with the specified value; otherwise, <see langword="false" />.</returns>
        public bool ContainsValue(TValue value) => this._root.ContainsValue(value, this._valueComparer);


#nullable disable
        /// <summary>See the <see cref="T:System.Collections.Immutable.IImmutableDictionary`2" /> interface.</summary>
        /// <param name="key">Key of the entry to be added.</param>
        /// <param name="value">Value of the entry to be added.</param>
        /// <returns>The <see cref="T:System.Collections.Immutable.IImmutableDictionary`2" /> instance.</returns>
        IImmutableDictionary<TKey, TValue> IImmutableDictionary<TKey, TValue>.Add(
          TKey key,
          TValue value)
        {
            return (IImmutableDictionary<TKey, TValue>)this.Add(key, value);
        }

        /// <summary>See the <see cref="T:System.Collections.Immutable.IImmutableDictionary`2" /> interface.</summary>
        /// <param name="key">Key of entry to be updated.</param>
        /// <param name="value">Value of entry to be updated.</param>
        /// <returns>The <see cref="T:System.Collections.Immutable.IImmutableDictionary`2" /> instance.</returns>
        IImmutableDictionary<TKey, TValue> IImmutableDictionary<TKey, TValue>.SetItem(
          TKey key,
          TValue value)
        {
            return (IImmutableDictionary<TKey, TValue>)this.SetItem(key, value);
        }

        /// <summary>Applies a given set of key-value pairs to an immutable dictionary, replacing any conflicting keys in the resulting dictionary.</summary>
        /// <param name="items">A set of key-value pairs to set on the map.</param>
        /// <returns>The <see cref="T:System.Collections.Immutable.IImmutableDictionary`2" /> instance.</returns>
        IImmutableDictionary<TKey, TValue> IImmutableDictionary<TKey, TValue>.SetItems(
          IEnumerable<KeyValuePair<TKey, TValue>> items)
        {
            return (IImmutableDictionary<TKey, TValue>)this.SetItems(items);
        }

        /// <summary>See the <see cref="T:System.Collections.Immutable.IImmutableDictionary`2" /> interface.</summary>
        /// <param name="pairs">Sequence of key/value pairs to be added.</param>
        /// <returns>The <see cref="T:System.Collections.Immutable.IImmutableDictionary`2" /> instance.</returns>
        IImmutableDictionary<TKey, TValue> IImmutableDictionary<TKey, TValue>.AddRange(
          IEnumerable<KeyValuePair<TKey, TValue>> pairs)
        {
            return (IImmutableDictionary<TKey, TValue>)this.AddRange(pairs);
        }

        /// <summary>See the <see cref="T:System.Collections.Immutable.IImmutableDictionary`2" /> interface.</summary>
        /// <param name="keys">Sequence of keys to be removed.</param>
        /// <returns>The <see cref="T:System.Collections.Immutable.IImmutableDictionary`2" /> instance.</returns>
        IImmutableDictionary<TKey, TValue> IImmutableDictionary<TKey, TValue>.RemoveRange(
          IEnumerable<TKey> keys)
        {
            return (IImmutableDictionary<TKey, TValue>)this.RemoveRange(keys);
        }

        /// <summary>See the <see cref="T:System.Collections.Immutable.IImmutableDictionary`2" /> interface.</summary>
        /// <param name="key">Key of entry to be removed.</param>
        /// <returns>The <see cref="T:System.Collections.Immutable.IImmutableDictionary`2" /> instance.</returns>
        IImmutableDictionary<TKey, TValue> IImmutableDictionary<TKey, TValue>.Remove(TKey key) => (IImmutableDictionary<TKey, TValue>)this.Remove(key);


#nullable enable
        /// <summary>Determines whether this immutable sorted map contains the specified key.</summary>
        /// <param name="key">The key to locate.</param>
        /// <returns>
        /// <see langword="true" /> if the immutable dictionary contains the specified key; otherwise, <see langword="false" />.</returns>
        public bool ContainsKey(TKey key)
        {
            Requires.NotNullAllowStructs<TKey>(key, nameof(key));
            return this._root.ContainsKey(key, this._keyComparer);
        }

        /// <summary>Determines whether this immutable sorted dictionary contains the specified key/value pair.</summary>
        /// <param name="pair">The key/value pair to locate.</param>
        /// <returns>
        /// <see langword="true" /> if the specified key/value pair is found in the dictionary; otherwise, <see langword="false" />.</returns>
        public bool Contains(KeyValuePair<TKey, TValue> pair) => this._root.Contains(pair, this._keyComparer, this._valueComparer);

        /// <summary>Gets the value associated with the specified key.</summary>
        /// <param name="key">The key whose value will be retrieved.</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, contains the default value for the type of the <paramref name="value" /> parameter.</param>
        /// <returns>
        /// <see langword="true" /> if the dictionary contains an element with the specified key; otherwise, <see langword="false" />.</returns>
        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            Requires.NotNullAllowStructs<TKey>(key, nameof(key));
            return this._root.TryGetValue(key, this._keyComparer, out value);
        }

        /// <summary>Determines whether this dictionary contains a specified key.</summary>
        /// <param name="equalKey">The key to search for.</param>
        /// <param name="actualKey">The matching key located in the dictionary if found, or <c>equalkey</c> if no match is found.</param>
        /// <returns>
        /// <see langword="true" /> if a match for <paramref name="equalKey" /> is found; otherwise, <see langword="false" />.</returns>
        public bool TryGetKey(TKey equalKey, out TKey actualKey)
        {
            Requires.NotNullAllowStructs<TKey>(equalKey, nameof(equalKey));
            return this._root.TryGetKey(equalKey, this._keyComparer, out actualKey);
        }


#nullable disable
        /// <summary>Adds an element with the provided key and value to the generic dictionary.</summary>
        /// <param name="key">The object to use as the key of the element to add.</param>
        /// <param name="value">The object to use as the value of the element to add.</param>
        /// <exception cref="T:System.ArgumentNullException">
        /// <paramref name="key" /> is <see langword="null" />.</exception>
        /// <exception cref="T:System.ArgumentException">An element with the same key already exists in the <see cref="T:System.Collections.Generic.IDictionary`2" />.</exception>
        /// <exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.IDictionary`2" /> is read-only.</exception>
        void IDictionary<TKey, TValue>.Add(TKey key, TValue value) => throw new NotSupportedException();

        /// <summary>Removes the element with the specified key from the generic dictionary.</summary>
        /// <param name="key">The key of the element to remove.</param>
        /// <exception cref="T:System.ArgumentNullException">
        /// <paramref name="key" /> is <see langword="null" />.</exception>
        /// <exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.IDictionary`2" /> is read-only.</exception>
        /// <returns>
        /// <see langword="true" /> if the element is successfully removed; otherwise, <see langword="false" />.  This method also returns <see langword="false" /> if <paramref name="key" /> was not found in the original generic dictionary.</returns>
        bool IDictionary<TKey, TValue>.Remove(TKey key) => throw new NotSupportedException();

        /// <summary>Adds an item to the <see cref="T:System.Collections.Generic.ICollection`1" />.</summary>
        /// <param name="item">The object to add to the <see cref="T:System.Collections.Generic.ICollection`1" />.</param>
        void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item) => throw new NotSupportedException();

        /// <summary>Removes all items from the <see cref="T:System.Collections.Generic.ICollection`1" />.</summary>
        void ICollection<KeyValuePair<TKey, TValue>>.Clear() => throw new NotSupportedException();

        /// <summary>Removes the first occurrence of a specific object from the <see cref="T:System.Collections.Generic.ICollection`1" />.</summary>
        /// <param name="item">The object to remove from the <see cref="T:System.Collections.Generic.ICollection`1" />.</param>
        /// <returns>
        /// <see langword="true" /> if <paramref name="item" /> was successfully removed from the <see cref="T:System.Collections.Generic.ICollection`1" />; otherwise, <see langword="false" />. This method also returns <see langword="false" /> if <paramref name="item" /> is not found in the original <see cref="T:System.Collections.Generic.ICollection`1" />.</returns>
        bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item) => throw new NotSupportedException();

        /// <summary>Copies the elements of the <see cref="T:System.Collections.Generic.ICollection`1" /> to an <see cref="T:System.Array" />, starting at a particular <see cref="T:System.Array" /> index.</summary>
        /// <param name="array">The one-dimensional <see cref="T:System.Array" /> that is the destination of the elements copied from <see cref="T:System.Collections.Generic.ICollection`1" />. The <see cref="T:System.Array" /> must have zero-based indexing.</param>
        /// <param name="arrayIndex">The zero-based index in <paramref name="array" /> at which copying begins.</param>
        void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(
          KeyValuePair<TKey, TValue>[] array,
          int arrayIndex)
        {
            Requires.NotNull<KeyValuePair<TKey, TValue>[]>(array, nameof(array));
            Requires.Range(arrayIndex >= 0, nameof(arrayIndex));
            Requires.Range(array.Length >= arrayIndex + this.Count, nameof(arrayIndex));
            foreach (KeyValuePair<TKey, TValue> keyValuePair in this)
                array[arrayIndex++] = keyValuePair;
        }

        /// <summary>Gets a value indicating whether the <see cref="T:System.Collections.IDictionary" /> object has a fixed size.</summary>
        /// <returns>
        /// <see langword="true" /> if the <see cref="T:System.Collections.IDictionary" /> object has a fixed size; otherwise, <see langword="false" />.</returns>
        bool IDictionary.IsFixedSize => true;

        /// <summary>Gets a value indicating whether the <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only.</summary>
        /// <returns>
        /// <see langword="true" /> if the <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only; otherwise, <see langword="false" />.</returns>
        bool IDictionary.IsReadOnly => true;


#nullable enable
        /// <summary>Gets an <see cref="T:System.Collections.Generic.ICollection`1" /> containing the keys of the <see cref="T:System.Collections.Generic.IDictionary`2" />.</summary>
        /// <returns>An <see cref="T:System.Collections.Generic.ICollection`1" /> containing the keys of the object that implements <see cref="T:System.Collections.Generic.IDictionary`2" />.</returns>
        ICollection IDictionary.Keys => (ICollection)new KeysCollectionAccessor<TKey, TValue>((IImmutableDictionary<TKey, TValue>)this);

        /// <summary>Gets an <see cref="T:System.Collections.Generic.ICollection`1" /> containing the values in the <see cref="T:System.Collections.Generic.IDictionary`2" />.</summary>
        /// <returns>An <see cref="T:System.Collections.Generic.ICollection`1" /> containing the values in the object that implements <see cref="T:System.Collections.Generic.IDictionary`2" />.</returns>
        ICollection IDictionary.Values => (ICollection)new ValuesCollectionAccessor<TKey, TValue>((IImmutableDictionary<TKey, TValue>)this);


#nullable disable
        /// <summary>Adds an element with the provided key and value to the dictionary object.</summary>
        /// <param name="key">The object to use as the key of the element to add.</param>
        /// <param name="value">The object to use as the value of the element to add.</param>
        void IDictionary.Add(object key, object value) => throw new NotSupportedException();

        /// <summary>Determines whether the immutable dictionary object contains an element with the specified key.</summary>
        /// <param name="key">The key to locate in the dictionary object.</param>
        /// <returns>
        /// <see langword="true" /> if the dictionary contains an element with the key; otherwise, <see langword="false" />.</returns>
        bool IDictionary.Contains(object key) => this.ContainsKey((TKey)key);

        /// <summary>Returns an <see cref="T:System.Collections.IDictionaryEnumerator" /> object for the immutable dictionary object.</summary>
        /// <returns>An enumerator object for the dictionary object.</returns>
        IDictionaryEnumerator IDictionary.GetEnumerator() => (IDictionaryEnumerator)new DictionaryEnumerator<TKey, TValue>((IEnumerator<KeyValuePair<TKey, TValue>>)this.GetEnumerator());

        /// <summary>Removes the element with the specified key from the immutable dictionary object.</summary>
        /// <param name="key">The key of the element to remove.</param>
        void IDictionary.Remove(object key) => throw new NotSupportedException();


#nullable enable
        /// <summary>Gets or sets the element with the specified key.</summary>
        /// <param name="key">The key of the element to be accessed.</param>
        /// <returns>Value stored under the specified key.</returns>
        object? IDictionary.this[
#nullable disable
        object key]
        {
            get => (object)this[(TKey)key];
            set => throw new NotSupportedException();
        }

        /// <summary>Clears this instance.</summary>
        /// <exception cref="T:System.NotSupportedException">The dictionary object is read-only.</exception>
        void IDictionary.Clear() => throw new NotSupportedException();

        /// <summary>Copies the elements of the dictionary to an array, starting at a particular array index.</summary>
        /// <param name="array">The one-dimensional array that is the destination of the elements copied from the dictionary. The array must have zero-based indexing.</param>
        /// <param name="index">The zero-based index in <paramref name="array" /> at which copying begins.</param>
        void ICollection.CopyTo(Array array, int index) => this._root.CopyTo(array, index, this.Count);


#nullable enable
        /// <summary>Gets an object that can be used to synchronize access to the <see cref="T:System.Collections.ICollection" />.</summary>
        /// <returns>An object that can be used to synchronize access to the <see cref="T:System.Collections.ICollection" />.</returns>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        object ICollection.SyncRoot => (object)this;

        /// <summary>Gets a value indicating whether access to the <see cref="T:System.Collections.ICollection" /> is synchronized (thread safe).</summary>
        /// <returns>
        /// <see langword="true" /> if access to the <see cref="T:System.Collections.ICollection" /> is synchronized (thread-safe); otherwise, <see langword="false" />.</returns>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        bool ICollection.IsSynchronized => true;


#nullable disable
        /// <summary>Returns an enumerator that iterates through the collection.</summary>
        /// <returns>An enumerator that can be used to iterate through the collection.</returns>
        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() => !this.IsEmpty ? (IEnumerator<KeyValuePair<TKey, TValue>>)this.GetEnumerator() : Enumerable.Empty<KeyValuePair<TKey, TValue>>().GetEnumerator();

        /// <summary>Returns an enumerator that iterates through a collection.</summary>
        /// <returns>An enumerator object that can be used to iterate through the collection.</returns>
        IEnumerator IEnumerable.GetEnumerator() => (IEnumerator)this.GetEnumerator();


#nullable enable
        /// <summary>Returns an enumerator that iterates through the immutable sorted dictionary.</summary>
        /// <returns>An enumerator that can be used to iterate through the dictionary.</returns>
        public ImmutableSortedDictionary<
#nullable disable
        TKey, TValue>.Enumerator GetEnumerator() => this._root.GetEnumerator();

        private static ImmutableSortedDictionary<TKey, TValue> Wrap(
          ImmutableSortedDictionary<TKey, TValue>.Node root,
          int count,
          IComparer<TKey> keyComparer,
          IEqualityComparer<TValue> valueComparer)
        {
            return !root.IsEmpty ? new ImmutableSortedDictionary<TKey, TValue>(root, count, keyComparer, valueComparer) : ImmutableSortedDictionary<TKey, TValue>.Empty.WithComparers(keyComparer, valueComparer);
        }

        private static bool TryCastToImmutableMap(
          IEnumerable<KeyValuePair<TKey, TValue>> sequence,
          [NotNullWhen(true)] out ImmutableSortedDictionary<TKey, TValue> other)
        {
            other = sequence as ImmutableSortedDictionary<TKey, TValue>;
            if (other != null)
                return true;
            if (!(sequence is ImmutableSortedDictionary<TKey, TValue>.Builder builder))
                return false;
            other = builder.ToImmutable();
            return true;
        }

        private ImmutableSortedDictionary<TKey, TValue> AddRange(
          IEnumerable<KeyValuePair<TKey, TValue>> items,
          bool overwriteOnCollision,
          bool avoidToSortedMap)
        {
            Requires.NotNull<IEnumerable<KeyValuePair<TKey, TValue>>>(items, nameof(items));
            if (this.IsEmpty && !avoidToSortedMap)
                return this.FillFromEmpty(items, overwriteOnCollision);
            ImmutableSortedDictionary<TKey, TValue>.Node root = this._root;
            int count = this._count;
            foreach (KeyValuePair<TKey, TValue> keyValuePair in items)
            {
                bool replacedExistingValue = false;
                bool mutated;
                ImmutableSortedDictionary<TKey, TValue>.Node node = overwriteOnCollision ? root.SetItem(keyValuePair.Key, keyValuePair.Value, this._keyComparer, this._valueComparer, out replacedExistingValue, out mutated) : root.Add(keyValuePair.Key, keyValuePair.Value, this._keyComparer, this._valueComparer, out mutated);
                if (mutated)
                {
                    root = node;
                    if (!replacedExistingValue)
                        ++count;
                }
            }
            return this.Wrap(root, count);
        }

        private ImmutableSortedDictionary<TKey, TValue> Wrap(
          ImmutableSortedDictionary<TKey, TValue>.Node root,
          int adjustedCountIfDifferentRoot)
        {
            if (this._root == root)
                return this;
            return !root.IsEmpty ? new ImmutableSortedDictionary<TKey, TValue>(root, adjustedCountIfDifferentRoot, this._keyComparer, this._valueComparer) : this.Clear();
        }

        private ImmutableSortedDictionary<TKey, TValue> FillFromEmpty(
          IEnumerable<KeyValuePair<TKey, TValue>> items,
          bool overwriteOnCollision)
        {
            Requires.NotNull<IEnumerable<KeyValuePair<TKey, TValue>>>(items, nameof(items));
            ImmutableSortedDictionary<TKey, TValue> other;
            if (ImmutableSortedDictionary<TKey, TValue>.TryCastToImmutableMap(items, out other))
                return other.WithComparers(this.KeyComparer, this.ValueComparer);
            SortedDictionary<TKey, TValue> dictionary1;
            if (items is IDictionary<TKey, TValue> dictionary2)
            {
                dictionary1 = new SortedDictionary<TKey, TValue>(dictionary2, this.KeyComparer);
            }
            else
            {
                dictionary1 = new SortedDictionary<TKey, TValue>(this.KeyComparer);
                foreach (KeyValuePair<TKey, TValue> keyValuePair in items)
                {
                    if (overwriteOnCollision)
                    {
                        dictionary1[keyValuePair.Key] = keyValuePair.Value;
                    }
                    else
                    {
                        TValue x;
                        if (dictionary1.TryGetValue(keyValuePair.Key, out x))
                        {
                            if (!this._valueComparer.Equals(x, keyValuePair.Value))
                                throw new ArgumentException(SR.Format(SR.DuplicateKey, (object)keyValuePair.Key));
                        }
                        else
                            dictionary1.Add(keyValuePair.Key, keyValuePair.Value);
                    }
                }
            }
            return dictionary1.Count == 0 ? this : new ImmutableSortedDictionary<TKey, TValue>(ImmutableSortedDictionary<TKey, TValue>.Node.NodeTreeFromSortedDictionary(dictionary1), dictionary1.Count, this.KeyComparer, this.ValueComparer);
        }


#nullable enable
        /// <summary>Represents a sorted dictionary that mutates with little or no memory allocations and that can produce or build on immutable sorted dictionary instances very efficiently.
        /// 
        /// NuGet package: System.Collections.Immutable (about immutable collections and how to install)</summary>
        /// <typeparam name="TKey" />
        /// <typeparam name="TValue" />
        [DebuggerDisplay("Count = {Count}")]
        [DebuggerTypeProxy(typeof(ImmutableSortedDictionaryBuilderDebuggerProxy<,>))]
        public sealed class Builder :
          IDictionary<TKey, TValue>,
          ICollection<KeyValuePair<TKey, TValue>>,
          IEnumerable<KeyValuePair<TKey, TValue>>,
          IEnumerable,
          IReadOnlyDictionary<TKey, TValue>,
          IReadOnlyCollection<KeyValuePair<TKey, TValue>>,
          IDictionary,
          ICollection
        {

#nullable disable
            private ImmutableSortedDictionary<TKey, TValue>.Node _root = ImmutableSortedDictionary<TKey, TValue>.Node.EmptyNode;
            private IComparer<TKey> _keyComparer = (IComparer<TKey>)Comparer<TKey>.Default;
            private IEqualityComparer<TValue> _valueComparer = (IEqualityComparer<TValue>)EqualityComparer<TValue>.Default;
            private int _count;
            private ImmutableSortedDictionary<TKey, TValue> _immutable;
            private int _version;
            private object _syncRoot;


#nullable enable
            internal Builder(ImmutableSortedDictionary<TKey, TValue> map)
            {
                Requires.NotNull<ImmutableSortedDictionary<TKey, TValue>>(map, nameof(map));
                this._root = map._root;
                this._keyComparer = map.KeyComparer;
                this._valueComparer = map.ValueComparer;
                this._count = map.Count;
                this._immutable = map;
            }

            /// <summary>Returns a collection containing all keys stored in the dictionary. See <see cref="T:System.Collections.Generic.IDictionary`2" />.</summary>
            /// <returns>A collection containing all keys stored in the dictionary.</returns>
            ICollection<TKey> IDictionary<
#nullable disable
            TKey, TValue>.Keys => (ICollection<TKey>)this.Root.Keys.ToArray<TKey>(this.Count);


#nullable enable
            /// <summary>Gets a strongly typed, read-only collection of elements.</summary>
            /// <returns>A strongly typed, read-only collection of elements.</returns>
            public IEnumerable<TKey> Keys => this.Root.Keys;

            /// <summary>Returns a collection containing all values stored in the dictionary. See <see cref="T:System.Collections.Generic.IDictionary`2" />.</summary>
            /// <returns>A collection containing all values stored in the dictionary.</returns>
            ICollection<TValue> IDictionary<
#nullable disable
            TKey, TValue>.Values => (ICollection<TValue>)this.Root.Values.ToArray<TValue>(this.Count);


#nullable enable
            /// <summary>Gets a collection that contains the values of the immutable sorted dictionary.</summary>
            /// <returns>A collection that contains the values of the object that implements the dictionary.</returns>
            public IEnumerable<TValue> Values => this.Root.Values;

            /// <summary>Gets the number of elements in this immutable sorted dictionary.</summary>
            /// <returns>The number of elements in this dictionary.</returns>
            public int Count => this._count;

            /// <summary>Gets a value that indicates whether this instance is read-only.</summary>
            /// <returns>Always <see langword="false" />.</returns>
            bool ICollection<KeyValuePair<
#nullable disable
            TKey, TValue>>.IsReadOnly => false;

            internal int Version => this._version;


#nullable enable
            private ImmutableSortedDictionary<
#nullable disable
            TKey, TValue>.Node Root
            {
                get => this._root;
                set
                {
                    ++this._version;
                    if (this._root == value)
                        return;
                    this._root = value;
                    this._immutable = (ImmutableSortedDictionary<TKey, TValue>)null;
                }
            }


#nullable enable
            /// <summary>Gets or sets the value for a specified key in the immutable sorted dictionary.</summary>
            /// <param name="key">The key to retrieve the value for.</param>
            /// <returns>The value associated with the given key.</returns>
            public TValue this[TKey key]
            {
                get
                {
                    TValue obj;
                    if (this.TryGetValue(key, out obj))
                        return obj;
                    throw new KeyNotFoundException(SR.Format(SR.Arg_KeyNotFoundWithKey, (object)key.ToString()));
                }
                set
                {
                    bool replacedExistingValue;
                    bool mutated;
                    this.Root = this._root.SetItem(key, value, this._keyComparer, this._valueComparer, out replacedExistingValue, out mutated);
                    if (!mutated || replacedExistingValue)
                        return;
                    ++this._count;
                }
            }

            /// <summary>Returns a read-only reference to the value associated with the provided <paramref name="key" />.</summary>
            /// <param name="key">Key of the entry to be looked up.</param>
            /// <exception cref="T:System.Collections.Generic.KeyNotFoundException">The <paramref name="key" /> is not present.</exception>
            /// <returns>A read-only reference to the value associated with the provided <paramref name="key" />.</returns>
            public ref readonly TValue ValueRef(TKey key)
            {
                Requires.NotNullAllowStructs<TKey>(key, nameof(key));
                return ref this._root.ValueRef(key, this._keyComparer);
            }

            /// <summary>Gets a value that indicates whether the <see cref="T:System.Collections.IDictionary" /> object has a fixed size.</summary>
            /// <returns>
            /// <see langword="true" /> if the <see cref="T:System.Collections.IDictionary" /> object has a fixed size; otherwise, <see langword="false" />.</returns>
            bool IDictionary.IsFixedSize => false;

            /// <summary>Gets a value that indicates whether the <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only.</summary>
            /// <returns>
            /// <see langword="true" /> if the <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only; otherwise, <see langword="false" />.</returns>
            bool IDictionary.IsReadOnly => false;

            /// <summary>Gets an <see cref="T:System.Collections.Generic.ICollection`1" /> containing the keys of the <see cref="T:System.Collections.Generic.IDictionary`2" />.</summary>
            /// <returns>An <see cref="T:System.Collections.Generic.ICollection`1" /> containing the keys of the object that implements <see cref="T:System.Collections.Generic.IDictionary`2" />.</returns>
            ICollection IDictionary.Keys => (ICollection)this.Keys.ToArray<TKey>(this.Count);

            /// <summary>Gets an <see cref="T:System.Collections.Generic.ICollection`1" /> containing the values in the <see cref="T:System.Collections.Generic.IDictionary`2" />.</summary>
            /// <returns>An <see cref="T:System.Collections.Generic.ICollection`1" /> containing the values in the object that implements <see cref="T:System.Collections.Generic.IDictionary`2" />.</returns>
            ICollection IDictionary.Values => (ICollection)this.Values.ToArray<TValue>(this.Count);

            /// <summary>Gets an object that can be used to synchronize access to the <see cref="T:System.Collections.ICollection" />.</summary>
            /// <returns>An object that can be used to synchronize access to the <see cref="T:System.Collections.ICollection" />.</returns>
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            object ICollection.SyncRoot
            {
                get
                {
                    if (this._syncRoot == null)
                        Interlocked.CompareExchange<object>(ref this._syncRoot, new object(), (object)null);
                    return this._syncRoot;
                }
            }

            /// <summary>Gets a value that indicates whether access to the <see cref="T:System.Collections.ICollection" /> is synchronized (thread safe).</summary>
            /// <returns>
            /// <see langword="true" /> if access to the <see cref="T:System.Collections.ICollection" /> is synchronized (thread safe); otherwise, <see langword="false" />.</returns>
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            bool ICollection.IsSynchronized => false;

            /// <summary>Gets or sets the key comparer.</summary>
            /// <returns>The key comparer.</returns>
            public IComparer<TKey> KeyComparer
            {
                get => this._keyComparer;
                set
                {
                    Requires.NotNull<IComparer<TKey>>(value, nameof(value));
                    if (value == this._keyComparer)
                        return;
                    ImmutableSortedDictionary<TKey, TValue>.Node node = ImmutableSortedDictionary<TKey, TValue>.Node.EmptyNode;
                    int num = 0;
                    foreach (KeyValuePair<TKey, TValue> keyValuePair in this)
                    {
                        bool mutated;
                        node = node.Add(keyValuePair.Key, keyValuePair.Value, value, this._valueComparer, out mutated);
                        if (mutated)
                            ++num;
                    }
                    this._keyComparer = value;
                    this.Root = node;
                    this._count = num;
                }
            }

            /// <summary>Gets or sets the value comparer.</summary>
            /// <returns>The value comparer.</returns>
            public IEqualityComparer<TValue> ValueComparer
            {
                get => this._valueComparer;
                set
                {
                    Requires.NotNull<IEqualityComparer<TValue>>(value, nameof(value));
                    if (value == this._valueComparer)
                        return;
                    this._valueComparer = value;
                    this._immutable = (ImmutableSortedDictionary<TKey, TValue>)null;
                }
            }


#nullable disable
            /// <summary>Adds an element with the provided key and value to the dictionary object.</summary>
            /// <param name="key">The key of the element to add.</param>
            /// <param name="value">The value of the element to add.</param>
            void IDictionary.Add(object key, object value) => this.Add((TKey)key, (TValue)value);

            /// <summary>Determines whether the dictionary object contains an element with the specified key.</summary>
            /// <param name="key">The key to locate.</param>
            /// <returns>
            /// <see langword="true" /> if the dictionary contains an element with the key; otherwise, <see langword="false" />.</returns>
            bool IDictionary.Contains(object key) => this.ContainsKey((TKey)key);

            /// <summary>Returns an <see cref="T:System.Collections.IDictionaryEnumerator" /> object for the dictionary.</summary>
            /// <returns>An <see cref="T:System.Collections.IDictionaryEnumerator" /> object for the dictionary.</returns>
            IDictionaryEnumerator IDictionary.GetEnumerator() => (IDictionaryEnumerator)new DictionaryEnumerator<TKey, TValue>((IEnumerator<KeyValuePair<TKey, TValue>>)this.GetEnumerator());

            /// <summary>Removes the element with the specified key from the dictionary.</summary>
            /// <param name="key">The key of the element to remove.</param>
            void IDictionary.Remove(object key) => this.Remove((TKey)key);


#nullable enable
            /// <summary>Gets or sets the element with the specified key.</summary>
            /// <param name="key">The key.</param>
            /// <returns>The value associated with the specified key.</returns>
            object? IDictionary.this[
#nullable disable
            object key]
            {
                get => (object)this[(TKey)key];
                set => this[(TKey)key] = (TValue)value;
            }

            /// <summary>Copies the elements of the dictionary to an array, starting at a particular array index.
            /// 
            /// NuGet package: System.Collections.Immutable (about immutable collections and how to install)</summary>
            /// <param name="array">The one-dimensional array that is the destination of the elements copied from the dictionary. The array must have zero-based indexing.</param>
            /// <param name="index">The zero-based index in <paramref name="array" /> at which copying begins.</param>
            void ICollection.CopyTo(Array array, int index) => this.Root.CopyTo(array, index, this.Count);


#nullable enable
            /// <summary>Adds an element that has the specified key and value to the immutable sorted dictionary.</summary>
            /// <param name="key">The key of the element to add.</param>
            /// <param name="value">The value of the element to add.</param>
            public void Add(TKey key, TValue value)
            {
                bool mutated;
                this.Root = this.Root.Add(key, value, this._keyComparer, this._valueComparer, out mutated);
                if (!mutated)
                    return;
                ++this._count;
            }

            /// <summary>Determines whether the immutable sorted dictionary contains an element with the specified key.</summary>
            /// <param name="key">The key to locate in the dictionary.</param>
            /// <returns>
            /// <see langword="true" /> if the dictionary contains an element with the key; otherwise, <see langword="false" />.</returns>
            public bool ContainsKey(TKey key) => this.Root.ContainsKey(key, this._keyComparer);

            /// <summary>Removes the element with the specified key from the immutable sorted dictionary.</summary>
            /// <param name="key">The key of the element to remove.</param>
            /// <returns>
            /// <see langword="true" /> if the element is successfully removed; otherwise, <see langword="false" />. This method also returns <see langword="false" /> if <paramref name="key" /> was not found in the original dictionary.</returns>
            public bool Remove(TKey key)
            {
                bool mutated;
                this.Root = this.Root.Remove(key, this._keyComparer, out mutated);
                if (mutated)
                    --this._count;
                return mutated;
            }

            /// <summary>Gets the value associated with the specified key.</summary>
            /// <param name="key">The key whose value will be retrieved.</param>
            /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, contains the default value for the type of the <paramref name="value" /> parameter. This parameter is passed uninitialized.</param>
            /// <returns>
            /// <see langword="true" /> if the object that implements the dictionary contains an element with the specified key; otherwise, <see langword="false" />.</returns>
            public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value) => this.Root.TryGetValue(key, this._keyComparer, out value);

            /// <summary>Determines whether this dictionary contains a specified key.</summary>
            /// <param name="equalKey">The key to search for.</param>
            /// <param name="actualKey">The matching key located in the dictionary if found, or <c>equalkey</c> if no match is found.</param>
            /// <returns>
            /// <see langword="true" /> if a match for <paramref name="equalKey" /> is found; otherwise, <see langword="false" />.</returns>
            public bool TryGetKey(TKey equalKey, out TKey actualKey)
            {
                Requires.NotNullAllowStructs<TKey>(equalKey, nameof(equalKey));
                return this.Root.TryGetKey(equalKey, this._keyComparer, out actualKey);
            }

            /// <summary>Adds the specified item to the immutable sorted dictionary.</summary>
            /// <param name="item">The object to add to the dictionary.</param>
            public void Add(KeyValuePair<TKey, TValue> item) => this.Add(item.Key, item.Value);

            /// <summary>Removes all items from the immutable sorted dictionary.</summary>
            public void Clear()
            {
                this.Root = ImmutableSortedDictionary<TKey, TValue>.Node.EmptyNode;
                this._count = 0;
            }

            /// <summary>Determines whether the immutable sorted dictionary contains a specific value.</summary>
            /// <param name="item">The object to locate in the dictionary.</param>
            /// <returns>
            /// <see langword="true" /> if <paramref name="item" /> is found in the dictionary; otherwise, <see langword="false" />.</returns>
            public bool Contains(KeyValuePair<TKey, TValue> item) => this.Root.Contains(item, this._keyComparer, this._valueComparer);


#nullable disable
            /// <summary>See <see cref="T:System.Collections.Generic.IDictionary`2" />.</summary>
            /// <param name="array">The one-dimensional array that is the destination of the elements copied from the dictionary. The array must have zero-based indexing.</param>
            /// <param name="arrayIndex">The zero-based index in <paramref name="array" /> at which copying begins.</param>
            void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(
              KeyValuePair<TKey, TValue>[] array,
              int arrayIndex)
            {
                this.Root.CopyTo(array, arrayIndex, this.Count);
            }


#nullable enable
            /// <summary>Removes the first occurrence of a specific object from the immutable sorted dictionary.</summary>
            /// <param name="item">The object to remove from the dictionary.</param>
            /// <returns>
            /// <see langword="true" /> if <paramref name="item" /> was successfully removed from the dictionary; otherwise, <see langword="false" />. This method also returns <see langword="false" /> if <paramref name="item" /> is not found in the dictionary.</returns>
            public bool Remove(KeyValuePair<TKey, TValue> item) => this.Contains(item) && this.Remove(item.Key);

            /// <summary>Returns an enumerator that iterates through the immutable sorted dictionary.</summary>
            /// <returns>An enumerator that can be used to iterate through the dictionary.</returns>
            public ImmutableSortedDictionary<TKey, TValue>.Enumerator GetEnumerator() => this.Root.GetEnumerator(this);


#nullable disable
            /// <summary>See <see cref="T:System.Collections.Generic.IDictionary`2" />.</summary>
            /// <returns>An enumerator that can be used to iterate through the collection.</returns>
            IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() => (IEnumerator<KeyValuePair<TKey, TValue>>)this.GetEnumerator();

            /// <summary>Returns an enumerator that iterates through a collection.</summary>
            /// <returns>An enumerator object that can be used to iterate through the collection.</returns>
            IEnumerator IEnumerable.GetEnumerator() => (IEnumerator)this.GetEnumerator();


#nullable enable
            /// <summary>Determines whether the immutable sorted dictionary contains an element with the specified value.</summary>
            /// <param name="value">The value to locate in the dictionary. The value can be <see langword="null" /> for reference types.</param>
            /// <returns>
            /// <see langword="true" /> if the immutable sorted dictionary contains an element with the specified value; otherwise, <see langword="false" />.</returns>
            public bool ContainsValue(TValue value) => this._root.ContainsValue(value, this._valueComparer);

            /// <summary>Adds a sequence of values to the immutable sorted dictionary.</summary>
            /// <param name="items">The items to add to the dictionary.</param>
            public void AddRange(IEnumerable<KeyValuePair<TKey, TValue>> items)
            {
                Requires.NotNull<IEnumerable<KeyValuePair<TKey, TValue>>>(items, nameof(items));
                foreach (KeyValuePair<TKey, TValue> keyValuePair in items)
                    this.Add(keyValuePair);
            }

            /// <summary>Removes any entries with keys that match those found in the specified sequence from the immutable sorted dictionary.</summary>
            /// <param name="keys">The keys for entries to remove from the dictionary.</param>
            public void RemoveRange(IEnumerable<TKey> keys)
            {
                Requires.NotNull<IEnumerable<TKey>>(keys, nameof(keys));
                foreach (TKey key in keys)
                    this.Remove(key);
            }

            /// <summary>Gets the value for a given key if a matching key exists in the dictionary; otherwise the default value.</summary>
            /// <param name="key">The key to search for.</param>
            /// <returns>The value for the key, or <c>default(TValue)</c> if no matching key was found.</returns>
            public TValue? GetValueOrDefault(TKey key) => this.GetValueOrDefault(key, default(TValue));

            /// <summary>Gets the value for a given key if a matching key exists in the dictionary; otherwise the default value.</summary>
            /// <param name="key">The key to search for.</param>
            /// <param name="defaultValue">The default value to return if no matching key is found in the dictionary.</param>
            /// <returns>The value for the key, or <paramref name="defaultValue" /> if no matching key was found.</returns>
            public TValue GetValueOrDefault(TKey key, TValue defaultValue)
            {
                Requires.NotNullAllowStructs<TKey>(key, nameof(key));
                TValue obj;
                return this.TryGetValue(key, out obj) ? obj : defaultValue;
            }

            /// <summary>Creates an immutable sorted dictionary based on the contents of this instance.</summary>
            /// <returns>An immutable sorted dictionary.</returns>
            public ImmutableSortedDictionary<TKey, TValue> ToImmutable() => this._immutable ?? (this._immutable = ImmutableSortedDictionary<TKey, TValue>.Wrap(this.Root, this._count, this._keyComparer, this._valueComparer));
        }

        /// <summary>Enumerates the contents of a binary tree.
        /// 
        /// NuGet package: System.Collections.Immutable (about immutable collections and how to install)</summary>
        /// <typeparam name="TKey" />
        /// <typeparam name="TValue" />
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public struct Enumerator :
          IEnumerator<KeyValuePair<TKey, TValue>>,
          IDisposable,
          IEnumerator,
          ISecurePooledObjectUser
        {

#nullable disable
            private readonly ImmutableSortedDictionary<TKey, TValue>.Builder _builder;
            private readonly int _poolUserId;
            private ImmutableSortedDictionary<TKey, TValue>.Node _root;
            private SecurePooledObject<Stack<RefAsValueType<ImmutableSortedDictionary<TKey, TValue>.Node>>> _stack;
            private ImmutableSortedDictionary<TKey, TValue>.Node _current;
            private int _enumeratingBuilderVersion;


#nullable enable
            internal Enumerator(
              ImmutableSortedDictionary<
#nullable disable
              TKey, TValue>.Node root,

#nullable enable
              ImmutableSortedDictionary<
#nullable disable
              TKey, TValue>.Builder
#nullable enable
              ? builder = null)
            {
                Requires.NotNull<ImmutableSortedDictionary<TKey, TValue>.Node>(root, nameof(root));
                this._root = root;
                this._builder = builder;
                this._current = (ImmutableSortedDictionary<TKey, TValue>.Node)null;
                this._enumeratingBuilderVersion = builder != null ? builder.Version : -1;
                this._poolUserId = SecureObjectPool.NewId();
                this._stack = (SecurePooledObject<Stack<RefAsValueType<ImmutableSortedDictionary<TKey, TValue>.Node>>>)null;
                if (this._root.IsEmpty)
                    return;
                if (!SecureObjectPool<Stack<RefAsValueType<ImmutableSortedDictionary<TKey, TValue>.Node>>, ImmutableSortedDictionary<TKey, TValue>.Enumerator>.TryTake(this, out this._stack))
                    this._stack = SecureObjectPool<Stack<RefAsValueType<ImmutableSortedDictionary<TKey, TValue>.Node>>, ImmutableSortedDictionary<TKey, TValue>.Enumerator>.PrepNew(this, new Stack<RefAsValueType<ImmutableSortedDictionary<TKey, TValue>.Node>>(root.Height));
                this.PushLeft(this._root);
            }

            /// <summary>Gets the element at the current position of the enumerator.</summary>
            /// <returns>The element at the current position of the enumerator.</returns>
            public KeyValuePair<TKey, TValue> Current
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

            /// <summary>The current element.</summary>
            /// <returns>The element in the collection at the current position of the enumerator.</returns>
            object IEnumerator.Current => (object)this.Current;

            /// <summary>Releases the resources used by the current instance of the <see cref="T:System.Collections.Immutable.ImmutableSortedDictionary`2.Enumerator" /> class.</summary>
            public void Dispose()
            {
                this._root = (ImmutableSortedDictionary<TKey, TValue>.Node)null;
                this._current = (ImmutableSortedDictionary<TKey, TValue>.Node)null;
                Stack<RefAsValueType<ImmutableSortedDictionary<TKey, TValue>.Node>> stack;
                if (this._stack != null && this._stack.TryUse<ImmutableSortedDictionary<TKey, TValue>.Enumerator>(ref this, out stack))
                {
                    stack.ClearFastWhenEmpty<RefAsValueType<ImmutableSortedDictionary<TKey, TValue>.Node>>();
                    SecureObjectPool<Stack<RefAsValueType<ImmutableSortedDictionary<TKey, TValue>.Node>>, ImmutableSortedDictionary<TKey, TValue>.Enumerator>.TryAdd(this, this._stack);
                }
                this._stack = (SecurePooledObject<Stack<RefAsValueType<ImmutableSortedDictionary<TKey, TValue>.Node>>>)null;
            }

            /// <summary>Advances the enumerator to the next element of the immutable sorted dictionary.</summary>
            /// <returns>
            /// <see langword="true" /> if the enumerator was successfully advanced to the next element; <see langword="false" /> if the enumerator has passed the end of the sorted dictionary.</returns>
            public bool MoveNext()
            {
                this.ThrowIfDisposed();
                this.ThrowIfChanged();
                if (this._stack != null)
                {
                    Stack<RefAsValueType<ImmutableSortedDictionary<TKey, TValue>.Node>> refAsValueTypeStack = this._stack.Use<ImmutableSortedDictionary<TKey, TValue>.Enumerator>(ref this);
                    if (refAsValueTypeStack.Count > 0)
                    {
                        ImmutableSortedDictionary<TKey, TValue>.Node node = refAsValueTypeStack.Pop().Value;
                        this._current = node;
                        this.PushLeft(node.Right);
                        return true;
                    }
                }
                this._current = (ImmutableSortedDictionary<TKey, TValue>.Node)null;
                return false;
            }

            /// <summary>Sets the enumerator to its initial position, which is before the first element in the immutable sorted dictionary.</summary>
            public void Reset()
            {
                this.ThrowIfDisposed();
                this._enumeratingBuilderVersion = this._builder != null ? this._builder.Version : -1;
                this._current = (ImmutableSortedDictionary<TKey, TValue>.Node)null;
                if (this._stack == null)
                    return;
                this._stack.Use<ImmutableSortedDictionary<TKey, TValue>.Enumerator>(ref this).ClearFastWhenEmpty<RefAsValueType<ImmutableSortedDictionary<TKey, TValue>.Node>>();
                this.PushLeft(this._root);
            }

            internal void ThrowIfDisposed()
            {
                if (this._root != null && (this._stack == null || this._stack.IsOwned<ImmutableSortedDictionary<TKey, TValue>.Enumerator>(ref this)))
                    return;
                Requires.FailObjectDisposed<ImmutableSortedDictionary<TKey, TValue>.Enumerator>(this);
            }

            private void ThrowIfChanged()
            {
                if (this._builder != null && this._builder.Version != this._enumeratingBuilderVersion)
                    throw new InvalidOperationException(SR.CollectionModifiedDuringEnumeration);
            }


#nullable disable
            private void PushLeft(ImmutableSortedDictionary<TKey, TValue>.Node node)
            {
                Requires.NotNull<ImmutableSortedDictionary<TKey, TValue>.Node>(node, nameof(node));
                Stack<RefAsValueType<ImmutableSortedDictionary<TKey, TValue>.Node>> refAsValueTypeStack = this._stack.Use<ImmutableSortedDictionary<TKey, TValue>.Enumerator>(ref this);
                for (; !node.IsEmpty; node = node.Left)
                    refAsValueTypeStack.Push(new RefAsValueType<ImmutableSortedDictionary<TKey, TValue>.Node>(node));
            }
        }


#nullable enable
        [DebuggerDisplay("{_key} = {_value}")]
        internal sealed class Node :
          IBinaryTree<KeyValuePair<TKey, TValue>>,
          IBinaryTree,
          IEnumerable<KeyValuePair<TKey, TValue>>,
          IEnumerable
        {
            internal static readonly ImmutableSortedDictionary<
#nullable disable
            TKey, TValue>.Node EmptyNode = new ImmutableSortedDictionary<TKey, TValue>.Node();
            private readonly TKey _key;
            private readonly TValue _value;
            private bool _frozen;
            private byte _height;
            private ImmutableSortedDictionary<TKey, TValue>.Node _left;
            private ImmutableSortedDictionary<TKey, TValue>.Node _right;

            private Node() => this._frozen = true;

            private Node(
              TKey key,
              TValue value,
              ImmutableSortedDictionary<TKey, TValue>.Node left,
              ImmutableSortedDictionary<TKey, TValue>.Node right,
              bool frozen = false)
            {
                Requires.NotNullAllowStructs<TKey>(key, nameof(key));
                Requires.NotNull<ImmutableSortedDictionary<TKey, TValue>.Node>(left, nameof(left));
                Requires.NotNull<ImmutableSortedDictionary<TKey, TValue>.Node>(right, nameof(right));
                this._key = key;
                this._value = value;
                this._left = left;
                this._right = right;
                this._height = checked((byte)(1 + (int)Math.Max(left._height, right._height)));
                this._frozen = frozen;
            }

            public bool IsEmpty => this._left == null;


#nullable enable
            IBinaryTree<KeyValuePair<TKey, TValue>>? IBinaryTree<KeyValuePair<
#nullable disable
            TKey, TValue>>.Left => (IBinaryTree<KeyValuePair<TKey, TValue>>)this._left;


#nullable enable
            IBinaryTree<KeyValuePair<TKey, TValue>>? IBinaryTree<KeyValuePair<
#nullable disable
            TKey, TValue>>.Right => (IBinaryTree<KeyValuePair<TKey, TValue>>)this._right;

            public int Height => (int)this._height;


#nullable enable
            public ImmutableSortedDictionary<
#nullable disable
            TKey, TValue>.Node
#nullable enable
            ? Left => this._left;

            IBinaryTree? IBinaryTree.Left => (IBinaryTree)this._left;

            public ImmutableSortedDictionary<
#nullable disable
            TKey, TValue>.Node
#nullable enable
            ? Right => this._right;

            IBinaryTree? IBinaryTree.Right => (IBinaryTree)this._right;

            public KeyValuePair<TKey, TValue> Value => new KeyValuePair<TKey, TValue>(this._key, this._value);

            int IBinaryTree.Count => throw new NotSupportedException();

            internal IEnumerable<TKey> Keys => this.Select<KeyValuePair<TKey, TValue>, TKey>((Func<KeyValuePair<TKey, TValue>, TKey>)(p => p.Key));

            internal IEnumerable<TValue> Values => this.Select<KeyValuePair<TKey, TValue>, TValue>((Func<KeyValuePair<TKey, TValue>, TValue>)(p => p.Value));

            public ImmutableSortedDictionary<
#nullable disable
            TKey, TValue>.Enumerator GetEnumerator() => new ImmutableSortedDictionary<TKey, TValue>.Enumerator(this);

            IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() => (IEnumerator<KeyValuePair<TKey, TValue>>)this.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => (IEnumerator)this.GetEnumerator();


#nullable enable
            internal ImmutableSortedDictionary<
#nullable disable
            TKey, TValue>.Enumerator GetEnumerator(

#nullable enable
              ImmutableSortedDictionary<
#nullable disable
              TKey, TValue>.Builder builder)
            {
                return new ImmutableSortedDictionary<TKey, TValue>.Enumerator(this, builder);
            }


#nullable enable
            internal void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex, int dictionarySize)
            {
                Requires.NotNull<KeyValuePair<TKey, TValue>[]>(array, nameof(array));
                Requires.Range(arrayIndex >= 0, nameof(arrayIndex));
                Requires.Range(array.Length >= arrayIndex + dictionarySize, nameof(arrayIndex));
                foreach (KeyValuePair<TKey, TValue> keyValuePair in this)
                    array[arrayIndex++] = keyValuePair;
            }

            internal void CopyTo(Array array, int arrayIndex, int dictionarySize)
            {
                Requires.NotNull<Array>(array, nameof(array));
                Requires.Range(arrayIndex >= 0, nameof(arrayIndex));
                Requires.Range(array.Length >= arrayIndex + dictionarySize, nameof(arrayIndex));
                foreach (KeyValuePair<TKey, TValue> keyValuePair in this)
                    array.SetValue((object)new DictionaryEntry((object)keyValuePair.Key, (object)keyValuePair.Value), arrayIndex++);
            }

            internal static ImmutableSortedDictionary<
#nullable disable
            TKey, TValue>.Node NodeTreeFromSortedDictionary(
#nullable enable
            SortedDictionary<TKey, TValue> dictionary)
            {
                Requires.NotNull<SortedDictionary<TKey, TValue>>(dictionary, nameof(dictionary));
                IOrderedCollection<KeyValuePair<TKey, TValue>> items = dictionary.AsOrderedCollection<KeyValuePair<TKey, TValue>>();
                return ImmutableSortedDictionary<TKey, TValue>.Node.NodeTreeFromList(items, 0, items.Count);
            }

            internal ImmutableSortedDictionary<
#nullable disable
            TKey, TValue>.Node Add(

#nullable enable
              TKey key,
              TValue value,
              IComparer<TKey> keyComparer,
              IEqualityComparer<TValue> valueComparer,
              out bool mutated)
            {
                Requires.NotNullAllowStructs<TKey>(key, nameof(key));
                Requires.NotNull<IComparer<TKey>>(keyComparer, nameof(keyComparer));
                Requires.NotNull<IEqualityComparer<TValue>>(valueComparer, nameof(valueComparer));
                return this.SetOrAdd(key, value, keyComparer, valueComparer, false, out bool _, out mutated);
            }

            internal ImmutableSortedDictionary<
#nullable disable
            TKey, TValue>.Node SetItem(

#nullable enable
              TKey key,
              TValue value,
              IComparer<TKey> keyComparer,
              IEqualityComparer<TValue> valueComparer,
              out bool replacedExistingValue,
              out bool mutated)
            {
                Requires.NotNullAllowStructs<TKey>(key, nameof(key));
                Requires.NotNull<IComparer<TKey>>(keyComparer, nameof(keyComparer));
                Requires.NotNull<IEqualityComparer<TValue>>(valueComparer, nameof(valueComparer));
                return this.SetOrAdd(key, value, keyComparer, valueComparer, true, out replacedExistingValue, out mutated);
            }

            internal ImmutableSortedDictionary<
#nullable disable
            TKey, TValue>.Node Remove(
#nullable enable
            TKey key, IComparer<TKey> keyComparer, out bool mutated)
            {
                Requires.NotNullAllowStructs<TKey>(key, nameof(key));
                Requires.NotNull<IComparer<TKey>>(keyComparer, nameof(keyComparer));
                return this.RemoveRecursive(key, keyComparer, out mutated);
            }

            internal ref readonly TValue ValueRef(TKey key, IComparer<TKey> keyComparer)
            {
                Requires.NotNullAllowStructs<TKey>(key, nameof(key));
                Requires.NotNull<IComparer<TKey>>(keyComparer, nameof(keyComparer));
                ImmutableSortedDictionary<TKey, TValue>.Node node = this.Search(key, keyComparer);
                if (node.IsEmpty)
                    //  throw new KeyNotFoundException(SR.Format(SR.Arg_KeyNotFoundWithKey, (object)key.ToString()));
                    throw new KeyNotFoundException(key.ToString());
                return ref node._value;
            }

            internal bool TryGetValue(TKey key, IComparer<TKey> keyComparer, [MaybeNullWhen(false)] out TValue value)
            {
                Requires.NotNullAllowStructs<TKey>(key, nameof(key));
                Requires.NotNull<IComparer<TKey>>(keyComparer, nameof(keyComparer));
                ImmutableSortedDictionary<TKey, TValue>.Node node = this.Search(key, keyComparer);
                if (node.IsEmpty)
                {
                    value = default(TValue);
                    return false;
                }
                value = node._value;
                return true;
            }

            internal bool TryGetKey(TKey equalKey, IComparer<TKey> keyComparer, out TKey actualKey)
            {
                Requires.NotNullAllowStructs<TKey>(equalKey, nameof(equalKey));
                Requires.NotNull<IComparer<TKey>>(keyComparer, nameof(keyComparer));
                ImmutableSortedDictionary<TKey, TValue>.Node node = this.Search(equalKey, keyComparer);
                if (node.IsEmpty)
                {
                    actualKey = equalKey;
                    return false;
                }
                actualKey = node._key;
                return true;
            }

            internal bool ContainsKey(TKey key, IComparer<TKey> keyComparer)
            {
                Requires.NotNullAllowStructs<TKey>(key, nameof(key));
                Requires.NotNull<IComparer<TKey>>(keyComparer, nameof(keyComparer));
                return !this.Search(key, keyComparer).IsEmpty;
            }

            internal bool ContainsValue(TValue value, IEqualityComparer<TValue> valueComparer)
            {
                Requires.NotNull<IEqualityComparer<TValue>>(valueComparer, nameof(valueComparer));
                foreach (KeyValuePair<TKey, TValue> keyValuePair in this)
                {
                    if (valueComparer.Equals(value, keyValuePair.Value))
                        return true;
                }
                return false;
            }

            internal bool Contains(
              KeyValuePair<TKey, TValue> pair,
              IComparer<TKey> keyComparer,
              IEqualityComparer<TValue> valueComparer)
            {
                Requires.NotNullAllowStructs<TKey>(pair.Key, "Key");
                Requires.NotNull<IComparer<TKey>>(keyComparer, nameof(keyComparer));
                Requires.NotNull<IEqualityComparer<TValue>>(valueComparer, nameof(valueComparer));
                ImmutableSortedDictionary<TKey, TValue>.Node node = this.Search(pair.Key, keyComparer);
                return !node.IsEmpty && valueComparer.Equals(node._value, pair.Value);
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
            private static ImmutableSortedDictionary<TKey, TValue>.Node RotateLeft(
              ImmutableSortedDictionary<TKey, TValue>.Node tree)
            {
                Requires.NotNull<ImmutableSortedDictionary<TKey, TValue>.Node>(tree, nameof(tree));
                if (tree._right.IsEmpty)
                    return tree;
                ImmutableSortedDictionary<TKey, TValue>.Node right = tree._right;
                return right.Mutate(tree.Mutate(right: right._left));
            }

            private static ImmutableSortedDictionary<TKey, TValue>.Node RotateRight(
              ImmutableSortedDictionary<TKey, TValue>.Node tree)
            {
                Requires.NotNull<ImmutableSortedDictionary<TKey, TValue>.Node>(tree, nameof(tree));
                if (tree._left.IsEmpty)
                    return tree;
                ImmutableSortedDictionary<TKey, TValue>.Node left = tree._left;
                return left.Mutate(right: tree.Mutate(left._right));
            }

            private static ImmutableSortedDictionary<TKey, TValue>.Node DoubleLeft(
              ImmutableSortedDictionary<TKey, TValue>.Node tree)
            {
                Requires.NotNull<ImmutableSortedDictionary<TKey, TValue>.Node>(tree, nameof(tree));
                return tree._right.IsEmpty ? tree : ImmutableSortedDictionary<TKey, TValue>.Node.RotateLeft(tree.Mutate(right: ImmutableSortedDictionary<TKey, TValue>.Node.RotateRight(tree._right)));
            }

            private static ImmutableSortedDictionary<TKey, TValue>.Node DoubleRight(
              ImmutableSortedDictionary<TKey, TValue>.Node tree)
            {
                Requires.NotNull<ImmutableSortedDictionary<TKey, TValue>.Node>(tree, nameof(tree));
                return tree._left.IsEmpty ? tree : ImmutableSortedDictionary<TKey, TValue>.Node.RotateRight(tree.Mutate(ImmutableSortedDictionary<TKey, TValue>.Node.RotateLeft(tree._left)));
            }

            private static int Balance(ImmutableSortedDictionary<TKey, TValue>.Node tree)
            {
                Requires.NotNull<ImmutableSortedDictionary<TKey, TValue>.Node>(tree, nameof(tree));
                return (int)tree._right._height - (int)tree._left._height;
            }

            private static bool IsRightHeavy(ImmutableSortedDictionary<TKey, TValue>.Node tree)
            {
                Requires.NotNull<ImmutableSortedDictionary<TKey, TValue>.Node>(tree, nameof(tree));
                return ImmutableSortedDictionary<TKey, TValue>.Node.Balance(tree) >= 2;
            }

            private static bool IsLeftHeavy(ImmutableSortedDictionary<TKey, TValue>.Node tree)
            {
                Requires.NotNull<ImmutableSortedDictionary<TKey, TValue>.Node>(tree, nameof(tree));
                return ImmutableSortedDictionary<TKey, TValue>.Node.Balance(tree) <= -2;
            }

            private static ImmutableSortedDictionary<TKey, TValue>.Node MakeBalanced(
              ImmutableSortedDictionary<TKey, TValue>.Node tree)
            {
                Requires.NotNull<ImmutableSortedDictionary<TKey, TValue>.Node>(tree, nameof(tree));
                if (ImmutableSortedDictionary<TKey, TValue>.Node.IsRightHeavy(tree))
                    return ImmutableSortedDictionary<TKey, TValue>.Node.Balance(tree._right) >= 0 ? ImmutableSortedDictionary<TKey, TValue>.Node.RotateLeft(tree) : ImmutableSortedDictionary<TKey, TValue>.Node.DoubleLeft(tree);
                if (!ImmutableSortedDictionary<TKey, TValue>.Node.IsLeftHeavy(tree))
                    return tree;
                return ImmutableSortedDictionary<TKey, TValue>.Node.Balance(tree._left) <= 0 ? ImmutableSortedDictionary<TKey, TValue>.Node.RotateRight(tree) : ImmutableSortedDictionary<TKey, TValue>.Node.DoubleRight(tree);
            }

            private static ImmutableSortedDictionary<TKey, TValue>.Node NodeTreeFromList(
              IOrderedCollection<KeyValuePair<TKey, TValue>> items,
              int start,
              int length)
            {
                Requires.NotNull<IOrderedCollection<KeyValuePair<TKey, TValue>>>(items, nameof(items));
                Requires.Range(start >= 0, nameof(start));
                Requires.Range(length >= 0, nameof(length));
                if (length == 0)
                    return ImmutableSortedDictionary<TKey, TValue>.Node.EmptyNode;
                int length1 = (length - 1) / 2;
                int length2 = length - 1 - length1;
                ImmutableSortedDictionary<TKey, TValue>.Node left = ImmutableSortedDictionary<TKey, TValue>.Node.NodeTreeFromList(items, start, length2);
                ImmutableSortedDictionary<TKey, TValue>.Node right = ImmutableSortedDictionary<TKey, TValue>.Node.NodeTreeFromList(items, start + length2 + 1, length1);
                KeyValuePair<TKey, TValue> keyValuePair = items[start + length2];
                return new ImmutableSortedDictionary<TKey, TValue>.Node(keyValuePair.Key, keyValuePair.Value, left, right, true);
            }

            private ImmutableSortedDictionary<TKey, TValue>.Node SetOrAdd(
              TKey key,
              TValue value,
              IComparer<TKey> keyComparer,
              IEqualityComparer<TValue> valueComparer,
              bool overwriteExistingValue,
              out bool replacedExistingValue,
              out bool mutated)
            {
                replacedExistingValue = false;
                if (this.IsEmpty)
                {
                    mutated = true;
                    return new ImmutableSortedDictionary<TKey, TValue>.Node(key, value, this, this);
                }
                ImmutableSortedDictionary<TKey, TValue>.Node tree = this;
                int num = keyComparer.Compare(key, this._key);
                if (num > 0)
                {
                    ImmutableSortedDictionary<TKey, TValue>.Node right = this._right.SetOrAdd(key, value, keyComparer, valueComparer, overwriteExistingValue, out replacedExistingValue, out mutated);
                    if (mutated)
                        tree = this.Mutate(right: right);
                }
                else if (num < 0)
                {
                    ImmutableSortedDictionary<TKey, TValue>.Node left = this._left.SetOrAdd(key, value, keyComparer, valueComparer, overwriteExistingValue, out replacedExistingValue, out mutated);
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
                        throw new ArgumentException(SR.Format(SR.DuplicateKey, (object)key));
                    mutated = true;
                    replacedExistingValue = true;
                    tree = new ImmutableSortedDictionary<TKey, TValue>.Node(key, value, this._left, this._right);
                }
                return !mutated ? tree : ImmutableSortedDictionary<TKey, TValue>.Node.MakeBalanced(tree);
            }

            private ImmutableSortedDictionary<TKey, TValue>.Node RemoveRecursive(
              TKey key,
              IComparer<TKey> keyComparer,
              out bool mutated)
            {
                if (this.IsEmpty)
                {
                    mutated = false;
                    return this;
                }
                ImmutableSortedDictionary<TKey, TValue>.Node tree = this;
                int num = keyComparer.Compare(key, this._key);
                if (num == 0)
                {
                    mutated = true;
                    if (this._right.IsEmpty && this._left.IsEmpty)
                        tree = ImmutableSortedDictionary<TKey, TValue>.Node.EmptyNode;
                    else if (this._right.IsEmpty && !this._left.IsEmpty)
                        tree = this._left;
                    else if (!this._right.IsEmpty && this._left.IsEmpty)
                    {
                        tree = this._right;
                    }
                    else
                    {
                        ImmutableSortedDictionary<TKey, TValue>.Node node = this._right;
                        while (!node._left.IsEmpty)
                            node = node._left;
                        ImmutableSortedDictionary<TKey, TValue>.Node right = this._right.Remove(node._key, keyComparer, out bool _);
                        tree = node.Mutate(this._left, right);
                    }
                }
                else if (num < 0)
                {
                    ImmutableSortedDictionary<TKey, TValue>.Node left = this._left.Remove(key, keyComparer, out mutated);
                    if (mutated)
                        tree = this.Mutate(left);
                }
                else
                {
                    ImmutableSortedDictionary<TKey, TValue>.Node right = this._right.Remove(key, keyComparer, out mutated);
                    if (mutated)
                        tree = this.Mutate(right: right);
                }
                return !tree.IsEmpty ? ImmutableSortedDictionary<TKey, TValue>.Node.MakeBalanced(tree) : tree;
            }

            private ImmutableSortedDictionary<TKey, TValue>.Node Mutate(
              ImmutableSortedDictionary<TKey, TValue>.Node left = null,
              ImmutableSortedDictionary<TKey, TValue>.Node right = null)
            {
                if (this._frozen)
                    return new ImmutableSortedDictionary<TKey, TValue>.Node(this._key, this._value, left ?? this._left, right ?? this._right);
                if (left != null)
                    this._left = left;
                if (right != null)
                    this._right = right;
                this._height = checked((byte)(1 + (int)Math.Max(this._left._height, this._right._height)));
                return this;
            }

            private ImmutableSortedDictionary<TKey, TValue>.Node Search(
              TKey key,
              IComparer<TKey> keyComparer)
            {
                if (this.IsEmpty)
                    return this;
                int num = keyComparer.Compare(key, this._key);
                if (num == 0)
                    return this;
                return num > 0 ? this._right.Search(key, keyComparer) : this._left.Search(key, keyComparer);
            }
        }
    }
}
