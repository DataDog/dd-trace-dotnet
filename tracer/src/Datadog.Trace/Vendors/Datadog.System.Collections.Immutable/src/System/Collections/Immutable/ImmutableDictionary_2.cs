﻿// Decompiled with JetBrains decompiler
// Type: System.Collections.Immutable.ImmutableDictionary`2
// Assembly: System.Collections.Immutable, Version=7.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 5F9FF90F-0D16-4469-A104-76829D3705E2
// Assembly location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.dll
// XML documentation location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.xml

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Datadog.System.Collections.Generic;
using Datadog.System.Diagnostics.CodeAnalysis;


#nullable enable
namespace Datadog.System.Collections.Immutable
{
    /// <summary>Represents an immutable, unordered collection of keys and values.
    /// 
    /// NuGet package: System.Collections.Immutable (about immutable collections and how to install)</summary>
    /// <typeparam name="TKey">The type of the keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of the values in the dictionary.</typeparam>
    [DebuggerDisplay("Count = {Count}")]
  [DebuggerTypeProxy(typeof (ImmutableDictionaryDebuggerProxy<,>))]
  public sealed class ImmutableDictionary<TKey, TValue> : 
    IImmutableDictionary<TKey, TValue>,
    IReadOnlyDictionary<TKey, TValue>,
    IReadOnlyCollection<KeyValuePair<TKey, TValue>>,
    IEnumerable<KeyValuePair<TKey, TValue>>,
    IEnumerable,
    IImmutableDictionaryInternal<TKey, TValue>,
    IHashKeyCollection<TKey>,
    IDictionary<TKey, TValue>,
    ICollection<KeyValuePair<TKey, TValue>>,
    IDictionary,
    ICollection
    where TKey : notnull
  {
    /// <summary>Gets an empty immutable dictionary.</summary>
    public static readonly ImmutableDictionary<TKey, TValue> Empty = new ImmutableDictionary<TKey, TValue>();

    #nullable disable
    private static readonly Action<KeyValuePair<int, ImmutableDictionary<TKey, TValue>.HashBucket>> s_FreezeBucketAction = (Action<KeyValuePair<int, ImmutableDictionary<TKey, TValue>.HashBucket>>) (kv => kv.Value.Freeze());
    private readonly int _count;
    private readonly SortedInt32KeyNode<ImmutableDictionary<TKey, TValue>.HashBucket> _root;
    private readonly ImmutableDictionary<TKey, TValue>.Comparers _comparers;

    private ImmutableDictionary(
      SortedInt32KeyNode<ImmutableDictionary<TKey, TValue>.HashBucket> root,
      ImmutableDictionary<TKey, TValue>.Comparers comparers,
      int count)
      : this(Requires.NotNullPassthrough<ImmutableDictionary<TKey, TValue>.Comparers>(comparers, nameof (comparers)))
    {
      Requires.NotNull<SortedInt32KeyNode<ImmutableDictionary<TKey, TValue>.HashBucket>>(root, nameof (root));
      root.Freeze(ImmutableDictionary<TKey, TValue>.s_FreezeBucketAction);
      this._root = root;
      this._count = count;
    }

    private ImmutableDictionary(
      ImmutableDictionary<TKey, TValue>.Comparers comparers = null)
    {
      this._comparers = comparers ?? ImmutableDictionary<TKey, TValue>.Comparers.Get((IEqualityComparer<TKey>) EqualityComparer<TKey>.Default, (IEqualityComparer<TValue>) EqualityComparer<TValue>.Default);
      this._root = SortedInt32KeyNode<ImmutableDictionary<TKey, TValue>.HashBucket>.EmptyNode;
    }


    #nullable enable
    /// <summary>Retrieves an empty immutable dictionary that has the same ordering and key/value comparison rules as this dictionary instance.</summary>
    /// <returns>An empty dictionary with equivalent ordering and key/value comparison rules.</returns>
    public ImmutableDictionary<TKey, TValue> Clear() => !this.IsEmpty ? ImmutableDictionary<TKey, TValue>.EmptyWithComparers(this._comparers) : this;

    /// <summary>Gets the number of key/value pairs in the immutable dictionary.</summary>
    /// <returns>The number of key/value pairs in the dictionary.</returns>
    public int Count => this._count;

    /// <summary>Gets a value that indicates whether this instance of the immutable dictionary is empty.</summary>
    /// <returns>
    /// <see langword="true" /> if this instance is empty; otherwise, <see langword="false" />.</returns>
    public bool IsEmpty => this.Count == 0;

    /// <summary>Gets the key comparer for the immutable dictionary.</summary>
    /// <returns>The key comparer.</returns>
    public IEqualityComparer<TKey> KeyComparer => this._comparers.KeyComparer;

    /// <summary>Gets the value comparer used to determine whether values are equal.</summary>
    /// <returns>The value comparer used to determine whether values are equal.</returns>
    public IEqualityComparer<TValue> ValueComparer => this._comparers.ValueComparer;

    /// <summary>Gets the keys in the immutable dictionary.</summary>
    /// <returns>The keys in the immutable dictionary.</returns>
    public IEnumerable<TKey> Keys
    {
      get
      {
        foreach (KeyValuePair<int, ImmutableDictionary<TKey, TValue>.HashBucket> keyValuePair1 in this._root)
        {
          foreach (KeyValuePair<TKey, TValue> keyValuePair2 in keyValuePair1.Value)
            yield return keyValuePair2.Key;
        }
      }
    }

    /// <summary>Gets the values in the immutable dictionary.</summary>
    /// <returns>The values in the immutable dictionary.</returns>
    public IEnumerable<TValue> Values
    {
      get
      {
        foreach (KeyValuePair<int, ImmutableDictionary<TKey, TValue>.HashBucket> keyValuePair1 in this._root)
        {
          foreach (KeyValuePair<TKey, TValue> keyValuePair2 in keyValuePair1.Value)
            yield return keyValuePair2.Value;
        }
      }
    }


    #nullable disable
    /// <summary>Retrieves an empty dictionary that has the same ordering and key-value comparison rules as this dictionary instance.</summary>
    /// <returns>The immutable dictionary instance.</returns>
    IImmutableDictionary<TKey, TValue> IImmutableDictionary<TKey, TValue>.Clear() => (IImmutableDictionary<TKey, TValue>) this.Clear();


    #nullable enable
    /// <summary>Gets the keys.</summary>
    /// <returns>A collection containing the keys.</returns>
    ICollection<TKey> IDictionary<
    #nullable disable
    TKey, TValue>.Keys => (ICollection<TKey>) new KeysCollectionAccessor<TKey, TValue>((IImmutableDictionary<TKey, TValue>) this);


    #nullable enable
    /// <summary>Gets the values.</summary>
    /// <returns>A collection containing the values.</returns>
    ICollection<TValue> IDictionary<
    #nullable disable
    TKey, TValue>.Values => (ICollection<TValue>) new ValuesCollectionAccessor<TKey, TValue>((IImmutableDictionary<TKey, TValue>) this);


    #nullable enable
    private ImmutableDictionary<
    #nullable disable
    TKey, TValue>.MutationInput Origin => new ImmutableDictionary<TKey, TValue>.MutationInput(this);


    #nullable enable
    /// <summary>Gets the <paramref name="TValue" /> associated with the specified key.</summary>
    /// <param name="key">The type of the key.</param>
    /// <returns>The value associated with the specified key. If no results are found, the operation throws an exception.</returns>
    public TValue this[TKey key]
    {
      get
      {
        Requires.NotNullAllowStructs<TKey>(key, nameof (key));
        TValue obj;
        if (this.TryGetValue(key, out obj))
          return obj;
        throw new KeyNotFoundException( key.ToString());
        //throw new KeyNotFoundException(SR.Format(SR.Arg_KeyNotFoundWithKey, (object) key.ToString()));
      }
    }

    /// <summary>Gets or sets the <typeparamref name="TValue" /> with the specified key.</summary>
    /// <param name="key">The type of the key.</param>
    /// <returns>An object of type <typeparamref name="TValue" /> associated with the <paramref name="key" />.</returns>
    TValue IDictionary<
    #nullable disable
    TKey, TValue>.this[TKey key]
    {
      get => this[key];
      set => throw new NotSupportedException();
    }

    /// <summary>Gets a value indicating whether the <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only.</summary>
    /// <returns>
    /// <see langword="true" /> if the <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only; otherwise, <see langword="false" />.</returns>
    bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => true;


    #nullable enable
    /// <summary>Creates an immutable dictionary with the same contents as this dictionary that can be efficiently mutated across multiple operations by using standard mutable interfaces.</summary>
    /// <returns>A collection with the same contents as this dictionary that can be efficiently mutated across multiple operations by using standard mutable interfaces.</returns>
    public ImmutableDictionary<
    #nullable disable
    TKey, TValue>.Builder ToBuilder() => new ImmutableDictionary<TKey, TValue>.Builder(this);


    #nullable enable
    /// <summary>Adds an element with the specified key and value to the immutable dictionary.</summary>
    /// <param name="key">The key of the element to add.</param>
    /// <param name="value">The value of the element to add.</param>
    /// <exception cref="T:System.ArgumentException">The given key already exists in the dictionary but has a different value.</exception>
    /// <returns>A new immutable dictionary that contains the additional key/value pair.</returns>
    public ImmutableDictionary<TKey, TValue> Add(TKey key, TValue value)
    {
      Requires.NotNullAllowStructs<TKey>(key, nameof (key));
      return ImmutableDictionary<TKey, TValue>.Add(key, value, ImmutableDictionary<TKey, TValue>.KeyCollisionBehavior.ThrowIfValueDifferent, this.Origin).Finalize(this);
    }

    /// <summary>Adds the specified key/value pairs to the immutable dictionary.</summary>
    /// <param name="pairs">The key/value pairs to add.</param>
    /// <exception cref="T:System.ArgumentException">One of the given keys already exists in the dictionary but has a different value.</exception>
    /// <returns>A new immutable dictionary that contains the additional key/value pairs.</returns>
    public ImmutableDictionary<TKey, TValue> AddRange(IEnumerable<KeyValuePair<TKey, TValue>> pairs)
    {
      Requires.NotNull<IEnumerable<KeyValuePair<TKey, TValue>>>(pairs, nameof (pairs));
      return this.AddRange(pairs, false);
    }

    /// <summary>Sets the specified key and value in the immutable dictionary, possibly overwriting an existing value for the key.</summary>
    /// <param name="key">The key of the entry to add.</param>
    /// <param name="value">The key value to set.</param>
    /// <returns>A new immutable dictionary that contains the specified key/value pair.</returns>
    public ImmutableDictionary<TKey, TValue> SetItem(TKey key, TValue value)
    {
      Requires.NotNullAllowStructs<TKey>(key, nameof (key));
      return ImmutableDictionary<TKey, TValue>.Add(key, value, ImmutableDictionary<TKey, TValue>.KeyCollisionBehavior.SetValue, this.Origin).Finalize(this);
    }

    /// <summary>Sets the specified key/value pairs in the immutable dictionary, possibly overwriting existing values for the keys.</summary>
    /// <param name="items">The key/value pairs to set in the dictionary. If any of the keys already exist in the dictionary, this method will overwrite their previous values.</param>
    /// <returns>A new immutable dictionary that contains the specified key/value pairs.</returns>
    public ImmutableDictionary<TKey, TValue> SetItems(IEnumerable<KeyValuePair<TKey, TValue>> items)
    {
      Requires.NotNull<IEnumerable<KeyValuePair<TKey, TValue>>>(items, nameof (items));
      return ImmutableDictionary<TKey, TValue>.AddRange(items, this.Origin, ImmutableDictionary<TKey, TValue>.KeyCollisionBehavior.SetValue).Finalize(this);
    }

    /// <summary>Removes the element with the specified key from the immutable dictionary.</summary>
    /// <param name="key">The key of the element to remove.</param>
    /// <returns>A new immutable dictionary with the specified element removed; or this instance if the specified key cannot be found in the dictionary.</returns>
    public ImmutableDictionary<TKey, TValue> Remove(TKey key)
    {
      Requires.NotNullAllowStructs<TKey>(key, nameof (key));
      return ImmutableDictionary<TKey, TValue>.Remove(key, this.Origin).Finalize(this);
    }

    /// <summary>Removes the elements with the specified keys from the immutable dictionary.</summary>
    /// <param name="keys">The keys of the elements to remove.</param>
    /// <returns>A new immutable dictionary with the specified keys removed; or this instance if the specified keys cannot be found in the dictionary.</returns>
    public ImmutableDictionary<TKey, TValue> RemoveRange(IEnumerable<TKey> keys)
    {
      Requires.NotNull<IEnumerable<TKey>>(keys, nameof (keys));
      int count = this._count;
      SortedInt32KeyNode<ImmutableDictionary<TKey, TValue>.HashBucket> root = this._root;
      foreach (TKey key in keys)
      {
        int hashCode = this.KeyComparer.GetHashCode(key);
        ImmutableDictionary<TKey, TValue>.HashBucket hashBucket;
        if (root.TryGetValue(hashCode, out hashBucket))
        {
          ImmutableDictionary<TKey, TValue>.OperationResult result;
          ImmutableDictionary<TKey, TValue>.HashBucket newBucket = hashBucket.Remove(key, this._comparers.KeyOnlyComparer, out result);
          root = ImmutableDictionary<TKey, TValue>.UpdateRoot(root, hashCode, newBucket, this._comparers.HashBucketEqualityComparer);
          if (result == ImmutableDictionary<TKey, TValue>.OperationResult.SizeChanged)
            --count;
        }
      }
      return this.Wrap(root, count);
    }

    /// <summary>Determines whether the immutable dictionary contains an element with the specified key.</summary>
    /// <param name="key">The key to locate.</param>
    /// <returns>
    /// <see langword="true" /> if the immutable dictionary contains an element with the specified key; otherwise, <see langword="false" />.</returns>
    public bool ContainsKey(TKey key)
    {
      Requires.NotNullAllowStructs<TKey>(key, nameof (key));
      return ImmutableDictionary<TKey, TValue>.ContainsKey(key, this.Origin);
    }

    /// <summary>Determines whether this immutable dictionary contains the specified key/value pair.</summary>
    /// <param name="pair">The key/value pair to locate.</param>
    /// <returns>
    /// <see langword="true" /> if the specified key/value pair is found in the dictionary; otherwise, <see langword="false" />.</returns>
    public bool Contains(KeyValuePair<TKey, TValue> pair) => ImmutableDictionary<TKey, TValue>.Contains(pair, this.Origin);

    /// <summary>Gets the value associated with the specified key.</summary>
    /// <param name="key">The key whose value will be retrieved.</param>
    /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, contains the default value for the type of the <paramref name="value" /> parameter. This parameter is passed uninitialized.</param>
    /// <exception cref="T:System.ArgumentNullException">
    /// <paramref name="key" /> is null.</exception>
    /// <returns>
    /// <see langword="true" /> if the object that implements the dictionary contains an element with the specified key; otherwise, <see langword="false" />.</returns>
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
      Requires.NotNullAllowStructs<TKey>(key, nameof (key));
      return ImmutableDictionary<TKey, TValue>.TryGetValue(key, this.Origin, out value);
    }

    /// <summary>Determines whether this dictionary contains a specified key.</summary>
    /// <param name="equalKey">The key to search for.</param>
    /// <param name="actualKey">The matching key located in the dictionary if found, or <c>equalkey</c> if no match is found.</param>
    /// <returns>
    /// <see langword="true" /> if a match for <paramref name="equalKey" /> is found; otherwise, <see langword="false" />.</returns>
    public bool TryGetKey(TKey equalKey, out TKey actualKey)
    {
      Requires.NotNullAllowStructs<TKey>(equalKey, nameof (equalKey));
      return ImmutableDictionary<TKey, TValue>.TryGetKey(equalKey, this.Origin, out actualKey);
    }

    /// <summary>Gets an instance of the immutable dictionary that uses the specified key and value comparers.</summary>
    /// <param name="keyComparer">The key comparer to use.</param>
    /// <param name="valueComparer">The value comparer to use.</param>
    /// <returns>An instance of the immutable dictionary that uses the given comparers.</returns>
    public ImmutableDictionary<TKey, TValue> WithComparers(
      IEqualityComparer<TKey>? keyComparer,
      IEqualityComparer<TValue>? valueComparer)
    {
      if (keyComparer == null)
        keyComparer = (IEqualityComparer<TKey>) EqualityComparer<TKey>.Default;
      if (valueComparer == null)
        valueComparer = (IEqualityComparer<TValue>) EqualityComparer<TValue>.Default;
      if (this.KeyComparer != keyComparer)
        return new ImmutableDictionary<TKey, TValue>(ImmutableDictionary<TKey, TValue>.Comparers.Get(keyComparer, valueComparer)).AddRange((IEnumerable<KeyValuePair<TKey, TValue>>) this, true);
      return this.ValueComparer == valueComparer ? this : new ImmutableDictionary<TKey, TValue>(this._root, this._comparers.WithValueComparer(valueComparer), this._count);
    }

    /// <summary>Gets an instance of the immutable dictionary that uses the specified key comparer.</summary>
    /// <param name="keyComparer">The key comparer to use.</param>
    /// <returns>An instance of the immutable dictionary that uses the given comparer.</returns>
    public ImmutableDictionary<TKey, TValue> WithComparers(IEqualityComparer<TKey>? keyComparer) => this.WithComparers(keyComparer, this._comparers.ValueComparer);

    /// <summary>Determines whether the immutable dictionary contains an element with the specified value.</summary>
    /// <param name="value">The value to locate. The value can be <see langword="null" /> for reference types.</param>
    /// <returns>
    /// <see langword="true" /> if the dictionary contains an element with the specified value; otherwise, <see langword="false" />.</returns>
    public bool ContainsValue(TValue value)
    {
      foreach (KeyValuePair<TKey, TValue> keyValuePair in this)
      {
        if (this.ValueComparer.Equals(value, keyValuePair.Value))
          return true;
      }
      return false;
    }

    /// <summary>Returns an enumerator that iterates through the immutable dictionary.</summary>
    /// <returns>An enumerator that can be used to iterate through the dictionary.</returns>
    public ImmutableDictionary<
    #nullable disable
    TKey, TValue>.Enumerator GetEnumerator() => new ImmutableDictionary<TKey, TValue>.Enumerator(this._root);

    /// <summary>See the <see cref="T:System.Collections.Immutable.IImmutableDictionary`2" /> interface.</summary>
    /// <param name="key">Key of the entry to be added.</param>
    /// <param name="value">Value of the entry to be added.</param>
    /// <returns>A new immutable dictionary that contains the additional key/value pair.</returns>
    IImmutableDictionary<TKey, TValue> IImmutableDictionary<TKey, TValue>.Add(
      TKey key,
      TValue value)
    {
      return (IImmutableDictionary<TKey, TValue>) this.Add(key, value);
    }

    /// <summary>See the <see cref="T:System.Collections.Immutable.IImmutableDictionary`2" /> interface.</summary>
    /// <param name="key">Key of entry to be added.</param>
    /// <param name="value">Value of the entry to be added.</param>
    /// <returns>A new immutable dictionary that contains the specified key/value pair.</returns>
    IImmutableDictionary<TKey, TValue> IImmutableDictionary<TKey, TValue>.SetItem(
      TKey key,
      TValue value)
    {
      return (IImmutableDictionary<TKey, TValue>) this.SetItem(key, value);
    }

    /// <summary>Applies a given set of key-value pairs to an immutable dictionary, replacing any conflicting keys in the resulting dictionary.</summary>
    /// <param name="items">The key-value pairs to set on the map. Any keys that conflict with existing keys will replace the previous values.</param>
    /// <returns>A copy of the immutable dictionary with updated key-value pairs.</returns>
    IImmutableDictionary<TKey, TValue> IImmutableDictionary<TKey, TValue>.SetItems(
      IEnumerable<KeyValuePair<TKey, TValue>> items)
    {
      return (IImmutableDictionary<TKey, TValue>) this.SetItems(items);
    }

    /// <summary>See the <see cref="T:System.Collections.Immutable.IImmutableDictionary`2" /> interface.</summary>
    /// <param name="pairs">Sequence of key/value pairs to be added to the dictionary.</param>
    /// <returns>A new immutable dictionary that contains the additional key/value pairs.</returns>
    IImmutableDictionary<TKey, TValue> IImmutableDictionary<TKey, TValue>.AddRange(
      IEnumerable<KeyValuePair<TKey, TValue>> pairs)
    {
      return (IImmutableDictionary<TKey, TValue>) this.AddRange(pairs);
    }

    /// <summary>See the <see cref="T:System.Collections.Immutable.IImmutableDictionary`2" /> interface.</summary>
    /// <param name="keys">Sequence of keys to be removed.</param>
    /// <returns>A new immutable dictionary with the specified keys removed; or this instance if the specified keys cannot be found in the dictionary.</returns>
    IImmutableDictionary<TKey, TValue> IImmutableDictionary<TKey, TValue>.RemoveRange(
      IEnumerable<TKey> keys)
    {
      return (IImmutableDictionary<TKey, TValue>) this.RemoveRange(keys);
    }

    /// <summary>See the <see cref="T:System.Collections.Immutable.IImmutableDictionary`2" /> interface.</summary>
    /// <param name="key">Key of the entry to be removed.</param>
    /// <returns>A new immutable dictionary with the specified element removed; or this instance if the specified key cannot be found in the dictionary.</returns>
    IImmutableDictionary<TKey, TValue> IImmutableDictionary<TKey, TValue>.Remove(TKey key) => (IImmutableDictionary<TKey, TValue>) this.Remove(key);

    /// <summary>Adds an element with the provided key and value to the immutable dictionary.</summary>
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
      Requires.NotNull<KeyValuePair<TKey, TValue>[]>(array, nameof (array));
      Requires.Range(arrayIndex >= 0, nameof (arrayIndex));
      Requires.Range(array.Length >= arrayIndex + this.Count, nameof (arrayIndex));
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
    ICollection IDictionary.Keys => (ICollection) new KeysCollectionAccessor<TKey, TValue>((IImmutableDictionary<TKey, TValue>) this);

    /// <summary>Gets an <see cref="T:System.Collections.Generic.ICollection`1" /> containing the values in the <see cref="T:System.Collections.Generic.IDictionary`2" />.</summary>
    /// <returns>An <see cref="T:System.Collections.Generic.ICollection`1" /> containing the values in the object that implements <see cref="T:System.Collections.Generic.IDictionary`2" />.</returns>
    ICollection IDictionary.Values => (ICollection) new ValuesCollectionAccessor<TKey, TValue>((IImmutableDictionary<TKey, TValue>) this);

    internal SortedInt32KeyNode<ImmutableDictionary<
    #nullable disable
    TKey, TValue>.HashBucket> Root => this._root;

    /// <summary>Adds an element with the provided key and value to the immutable dictionary object.</summary>
    /// <param name="key">The object to use as the key of the element to add.</param>
    /// <param name="value">The object to use as the value of the element to add.</param>
    void IDictionary.Add(object key, object value) => throw new NotSupportedException();

    /// <summary>Determines whether the immutable dictionary object contains an element with the specified key.</summary>
    /// <param name="key">The key to locate in the dictionary object.</param>
    /// <returns>
    /// <see langword="true" /> if the dictionary contains an element with the key; otherwise, <see langword="false" />.</returns>
    bool IDictionary.Contains(object key) => this.ContainsKey((TKey) key);

    /// <summary>Returns an <see cref="T:System.Collections.IDictionaryEnumerator" /> object for the immutable dictionary object.</summary>
    /// <returns>An enumerator object for the dictionary object.</returns>
    IDictionaryEnumerator IDictionary.GetEnumerator() => (IDictionaryEnumerator) new DictionaryEnumerator<TKey, TValue>((IEnumerator<KeyValuePair<TKey, TValue>>) this.GetEnumerator());

    /// <summary>Removes the element with the specified key from the immutable dictionary object.</summary>
    /// <param name="key">The key of the element to remove.</param>
    void IDictionary.Remove(object key) => throw new NotSupportedException();


    #nullable enable
    /// <summary>Gets or sets the element with the specified key.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The value stored under the specified key.</returns>
    object? IDictionary.this[
    #nullable disable
    object key]
    {
      get => (object) this[(TKey) key];
      set => throw new NotSupportedException();
    }

    /// <summary>Clears this instance.</summary>
    /// <exception cref="T:System.NotSupportedException">The dictionary object is read-only.</exception>
    void IDictionary.Clear() => throw new NotSupportedException();

    /// <summary>Copies the elements of the dictionary to an array, starting at a particular array index.</summary>
    /// <param name="array">The one-dimensional array that is the destination of the elements copied from the dictionary. The array must have zero-based indexing.</param>
    /// <param name="arrayIndex">The zero-based index in <paramref name="array" /> at which copying begins.</param>
    void ICollection.CopyTo(Array array, int arrayIndex)
    {
      Requires.NotNull<Array>(array, nameof (array));
      Requires.Range(arrayIndex >= 0, nameof (arrayIndex));
      Requires.Range(array.Length >= arrayIndex + this.Count, nameof (arrayIndex));
      foreach (KeyValuePair<TKey, TValue> keyValuePair in this)
        array.SetValue((object) new DictionaryEntry((object) keyValuePair.Key, (object) keyValuePair.Value), arrayIndex++);
    }


    #nullable enable
    /// <summary>Gets an object that can be used to synchronize access to the <see cref="T:System.Collections.ICollection" />.</summary>
    /// <returns>An object that can be used to synchronize access to the <see cref="T:System.Collections.ICollection" />.</returns>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    object ICollection.SyncRoot => (object) this;

    /// <summary>Gets a value indicating whether access to the <see cref="T:System.Collections.ICollection" /> is synchronized (thread safe).</summary>
    /// <returns>
    /// <see langword="true" /> if access to the <see cref="T:System.Collections.ICollection" /> is synchronized (thread safe); otherwise, <see langword="false" />.</returns>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    bool ICollection.IsSynchronized => true;


    #nullable disable
    /// <summary>Returns an enumerator that iterates through the collection.</summary>
    /// <returns>An enumerator that can be used to iterate through the collection.</returns>
    IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() => !this.IsEmpty ? (IEnumerator<KeyValuePair<TKey, TValue>>) this.GetEnumerator() : Enumerable.Empty<KeyValuePair<TKey, TValue>>().GetEnumerator();

    /// <summary>Returns an enumerator that iterates through a collection.</summary>
    /// <returns>An enumerator object that can be used to iterate through the collection.</returns>
    IEnumerator IEnumerable.GetEnumerator() => (IEnumerator) this.GetEnumerator();

    private static ImmutableDictionary<TKey, TValue> EmptyWithComparers(
      ImmutableDictionary<TKey, TValue>.Comparers comparers)
    {
      Requires.NotNull<ImmutableDictionary<TKey, TValue>.Comparers>(comparers, nameof (comparers));
      return ImmutableDictionary<TKey, TValue>.Empty._comparers != comparers ? new ImmutableDictionary<TKey, TValue>(comparers) : ImmutableDictionary<TKey, TValue>.Empty;
    }

    private static bool TryCastToImmutableMap(
      IEnumerable<KeyValuePair<TKey, TValue>> sequence,
      [NotNullWhen(true)] out ImmutableDictionary<TKey, TValue> other)
    {
      other = sequence as ImmutableDictionary<TKey, TValue>;
      if (other != null)
        return true;
      if (!(sequence is ImmutableDictionary<TKey, TValue>.Builder builder))
        return false;
      other = builder.ToImmutable();
      return true;
    }

    private static bool ContainsKey(
      TKey key,
      ImmutableDictionary<TKey, TValue>.MutationInput origin)
    {
      int hashCode = origin.KeyComparer.GetHashCode(key);
      ImmutableDictionary<TKey, TValue>.HashBucket hashBucket;
      return origin.Root.TryGetValue(hashCode, out hashBucket) && hashBucket.TryGetValue(key, origin.Comparers, out TValue _);
    }

    private static bool Contains(
      KeyValuePair<TKey, TValue> keyValuePair,
      ImmutableDictionary<TKey, TValue>.MutationInput origin)
    {
      int hashCode = origin.KeyComparer.GetHashCode(keyValuePair.Key);
      ImmutableDictionary<TKey, TValue>.HashBucket hashBucket;
      TValue x;
      return origin.Root.TryGetValue(hashCode, out hashBucket) && hashBucket.TryGetValue(keyValuePair.Key, origin.Comparers, out x) && origin.ValueComparer.Equals(x, keyValuePair.Value);
    }

    private static bool TryGetValue(
      TKey key,
      ImmutableDictionary<TKey, TValue>.MutationInput origin,
      [MaybeNullWhen(false)] out TValue value)
    {
      int hashCode = origin.KeyComparer.GetHashCode(key);
      ImmutableDictionary<TKey, TValue>.HashBucket hashBucket;
      if (origin.Root.TryGetValue(hashCode, out hashBucket))
        return hashBucket.TryGetValue(key, origin.Comparers, out value);
      value = default (TValue);
      return false;
    }

    private static bool TryGetKey(
      TKey equalKey,
      ImmutableDictionary<TKey, TValue>.MutationInput origin,
      out TKey actualKey)
    {
      int hashCode = origin.KeyComparer.GetHashCode(equalKey);
      ImmutableDictionary<TKey, TValue>.HashBucket hashBucket;
      if (origin.Root.TryGetValue(hashCode, out hashBucket))
        return hashBucket.TryGetKey(equalKey, origin.Comparers, out actualKey);
      actualKey = equalKey;
      return false;
    }

    private static ImmutableDictionary<TKey, TValue>.MutationResult Add(
      TKey key,
      TValue value,
      ImmutableDictionary<TKey, TValue>.KeyCollisionBehavior behavior,
      ImmutableDictionary<TKey, TValue>.MutationInput origin)
    {
      Requires.NotNullAllowStructs<TKey>(key, nameof (key));
      int hashCode = origin.KeyComparer.GetHashCode(key);
      ImmutableDictionary<TKey, TValue>.OperationResult result;
      ImmutableDictionary<TKey, TValue>.HashBucket newBucket = origin.Root.GetValueOrDefault(hashCode).Add(key, value, origin.KeyOnlyComparer, origin.ValueComparer, behavior, out result);
      return result == ImmutableDictionary<TKey, TValue>.OperationResult.NoChangeRequired ? new ImmutableDictionary<TKey, TValue>.MutationResult(origin) : new ImmutableDictionary<TKey, TValue>.MutationResult(ImmutableDictionary<TKey, TValue>.UpdateRoot(origin.Root, hashCode, newBucket, origin.HashBucketComparer), result == ImmutableDictionary<TKey, TValue>.OperationResult.SizeChanged ? 1 : 0);
    }

    private static ImmutableDictionary<TKey, TValue>.MutationResult AddRange(
      IEnumerable<KeyValuePair<TKey, TValue>> items,
      ImmutableDictionary<TKey, TValue>.MutationInput origin,
      ImmutableDictionary<TKey, TValue>.KeyCollisionBehavior collisionBehavior = ImmutableDictionary<TKey, TValue>.KeyCollisionBehavior.ThrowIfValueDifferent)
    {
      Requires.NotNull<IEnumerable<KeyValuePair<TKey, TValue>>>(items, nameof (items));
      int countAdjustment = 0;
      SortedInt32KeyNode<ImmutableDictionary<TKey, TValue>.HashBucket> root = origin.Root;
      foreach (KeyValuePair<TKey, TValue> keyValuePair in items)
      {
        Requires.NotNullAllowStructs<TKey>(keyValuePair.Key, "Key");
        int hashCode = origin.KeyComparer.GetHashCode(keyValuePair.Key);
        ImmutableDictionary<TKey, TValue>.OperationResult result;
        ImmutableDictionary<TKey, TValue>.HashBucket newBucket = root.GetValueOrDefault(hashCode).Add(keyValuePair.Key, keyValuePair.Value, origin.KeyOnlyComparer, origin.ValueComparer, collisionBehavior, out result);
        root = ImmutableDictionary<TKey, TValue>.UpdateRoot(root, hashCode, newBucket, origin.HashBucketComparer);
        if (result == ImmutableDictionary<TKey, TValue>.OperationResult.SizeChanged)
          ++countAdjustment;
      }
      return new ImmutableDictionary<TKey, TValue>.MutationResult(root, countAdjustment);
    }

    private static ImmutableDictionary<TKey, TValue>.MutationResult Remove(
      TKey key,
      ImmutableDictionary<TKey, TValue>.MutationInput origin)
    {
      int hashCode = origin.KeyComparer.GetHashCode(key);
      ImmutableDictionary<TKey, TValue>.HashBucket hashBucket;
      ImmutableDictionary<TKey, TValue>.OperationResult result;
      return origin.Root.TryGetValue(hashCode, out hashBucket) ? new ImmutableDictionary<TKey, TValue>.MutationResult(ImmutableDictionary<TKey, TValue>.UpdateRoot(origin.Root, hashCode, hashBucket.Remove(key, origin.KeyOnlyComparer, out result), origin.HashBucketComparer), result == ImmutableDictionary<TKey, TValue>.OperationResult.SizeChanged ? -1 : 0) : new ImmutableDictionary<TKey, TValue>.MutationResult(origin);
    }

    private static SortedInt32KeyNode<ImmutableDictionary<TKey, TValue>.HashBucket> UpdateRoot(
      SortedInt32KeyNode<ImmutableDictionary<TKey, TValue>.HashBucket> root,
      int hashCode,
      ImmutableDictionary<TKey, TValue>.HashBucket newBucket,
      IEqualityComparer<ImmutableDictionary<TKey, TValue>.HashBucket> hashBucketComparer)
    {
      bool flag;
      return newBucket.IsEmpty ? root.Remove(hashCode, out flag) : root.SetItem(hashCode, newBucket, hashBucketComparer, out flag, out bool _);
    }

    private static ImmutableDictionary<TKey, TValue> Wrap(
      SortedInt32KeyNode<ImmutableDictionary<TKey, TValue>.HashBucket> root,
      ImmutableDictionary<TKey, TValue>.Comparers comparers,
      int count)
    {
      Requires.NotNull<SortedInt32KeyNode<ImmutableDictionary<TKey, TValue>.HashBucket>>(root, nameof (root));
      Requires.NotNull<ImmutableDictionary<TKey, TValue>.Comparers>(comparers, nameof (comparers));
      Requires.Range(count >= 0, nameof (count));
      return new ImmutableDictionary<TKey, TValue>(root, comparers, count);
    }

    private ImmutableDictionary<TKey, TValue> Wrap(
      SortedInt32KeyNode<ImmutableDictionary<TKey, TValue>.HashBucket> root,
      int adjustedCountIfDifferentRoot)
    {
      if (root == null)
        return this.Clear();
      if (this._root == root)
        return this;
      return !root.IsEmpty ? new ImmutableDictionary<TKey, TValue>(root, this._comparers, adjustedCountIfDifferentRoot) : this.Clear();
    }

    private ImmutableDictionary<TKey, TValue> AddRange(
      IEnumerable<KeyValuePair<TKey, TValue>> pairs,
      bool avoidToHashMap)
    {
      Requires.NotNull<IEnumerable<KeyValuePair<TKey, TValue>>>(pairs, nameof (pairs));
      ImmutableDictionary<TKey, TValue> other;
      return this.IsEmpty && !avoidToHashMap && ImmutableDictionary<TKey, TValue>.TryCastToImmutableMap(pairs, out other) ? other.WithComparers(this.KeyComparer, this.ValueComparer) : ImmutableDictionary<TKey, TValue>.AddRange(pairs, this.Origin).Finalize(this);
    }


    #nullable enable
    /// <summary>Represents a hash map that mutates with little or no memory allocations and that can produce or build on immutable hash map instances very efficiently.
    /// 
    /// NuGet package: System.Collections.Immutable (about immutable collections and how to install)</summary>
    /// <typeparam name="TKey" />
    /// <typeparam name="TValue" />
    [DebuggerDisplay("Count = {Count}")]
    [DebuggerTypeProxy(typeof (ImmutableDictionaryBuilderDebuggerProxy<,>))]
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
      private SortedInt32KeyNode<ImmutableDictionary<TKey, TValue>.HashBucket> _root = SortedInt32KeyNode<ImmutableDictionary<TKey, TValue>.HashBucket>.EmptyNode;
      private ImmutableDictionary<TKey, TValue>.Comparers _comparers;
      private int _count;
      private ImmutableDictionary<TKey, TValue> _immutable;
      private int _version;
      private object _syncRoot;


      #nullable enable
      internal Builder(ImmutableDictionary<TKey, TValue> map)
      {
        Requires.NotNull<ImmutableDictionary<TKey, TValue>>(map, nameof (map));
        this._root = map._root;
        this._count = map._count;
        this._comparers = map._comparers;
        this._immutable = map;
      }

      /// <summary>Gets or sets the key comparer.</summary>
      /// <returns>The key comparer.</returns>
      public IEqualityComparer<TKey> KeyComparer
      {
        get => this._comparers.KeyComparer;
        set
        {
          Requires.NotNull<IEqualityComparer<TKey>>(value, nameof (value));
          if (value == this.KeyComparer)
            return;
          ImmutableDictionary<TKey, TValue>.Comparers comparers = ImmutableDictionary<TKey, TValue>.Comparers.Get(value, this.ValueComparer);
          ImmutableDictionary<TKey, TValue>.MutationResult mutationResult = ImmutableDictionary<TKey, TValue>.AddRange((IEnumerable<KeyValuePair<TKey, TValue>>) this, new ImmutableDictionary<TKey, TValue>.MutationInput(SortedInt32KeyNode<ImmutableDictionary<TKey, TValue>.HashBucket>.EmptyNode, comparers));
          this._immutable = (ImmutableDictionary<TKey, TValue>) null;
          this._comparers = comparers;
          this._count = mutationResult.CountAdjustment;
          this.Root = mutationResult.Root;
        }
      }

      /// <summary>Gets or sets the value comparer.</summary>
      /// <returns>The value comparer.</returns>
      public IEqualityComparer<TValue> ValueComparer
      {
        get => this._comparers.ValueComparer;
        set
        {
          Requires.NotNull<IEqualityComparer<TValue>>(value, nameof (value));
          if (value == this.ValueComparer)
            return;
          this._comparers = this._comparers.WithValueComparer(value);
          this._immutable = (ImmutableDictionary<TKey, TValue>) null;
        }
      }

      /// <summary>Gets the number of elements contained in the immutable dictionary.</summary>
      /// <returns>The number of elements contained in the immutable dictionary.</returns>
      public int Count => this._count;

      /// <summary>Gets a value that indicates whether the collection is read-only.</summary>
      /// <returns>
      /// <see langword="true" /> if the collection is read-only; otherwise, <see langword="false" />.</returns>
      bool ICollection<KeyValuePair<
      #nullable disable
      TKey, TValue>>.IsReadOnly => false;


      #nullable enable
      /// <summary>Gets a collection that contains the keys of the immutable dictionary.</summary>
      /// <returns>A collection that contains the keys of the object that implements the immutable dictionary.</returns>
      public IEnumerable<TKey> Keys
      {
        get
        {
          foreach (KeyValuePair<TKey, TValue> keyValuePair in this)
            yield return keyValuePair.Key;
        }
      }

      /// <summary>Gets a collection containing the keys of the generic dictionary.</summary>
      /// <returns>A collection containing the keys of the object that implements the generic dictionary.</returns>
      ICollection<TKey> IDictionary<
      #nullable disable
      TKey, TValue>.Keys => (ICollection<TKey>) this.Keys.ToArray<TKey>(this.Count);


      #nullable enable
      /// <summary>Gets a collection that contains the values of the immutable dictionary.</summary>
      /// <returns>A collection that contains the values of the object that implements the dictionary.</returns>
      public IEnumerable<TValue> Values
      {
        get
        {
          foreach (KeyValuePair<TKey, TValue> keyValuePair in this)
            yield return keyValuePair.Value;
        }
      }

      /// <summary>Gets a collection containing the values in the generic dictionary.</summary>
      /// <returns>A collection containing the values in the object that implements the generic dictionary.</returns>
      ICollection<TValue> IDictionary<
      #nullable disable
      TKey, TValue>.Values => (ICollection<TValue>) this.Values.ToArray<TValue>(this.Count);

      /// <summary>Gets a value that indicates whether the <see cref="T:System.Collections.IDictionary" /> object has a fixed size.</summary>
      /// <returns>
      /// <see langword="true" /> if the <see cref="T:System.Collections.IDictionary" /> object has a fixed size; otherwise, <see langword="false" />.</returns>
      bool IDictionary.IsFixedSize => false;

      /// <summary>Gets a value that indicates whether the <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only.</summary>
      /// <returns>
      /// <see langword="true" /> if the <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only; otherwise, <see langword="false" />.</returns>
      bool IDictionary.IsReadOnly => false;


      #nullable enable
      /// <summary>Gets an <see cref="T:System.Collections.Generic.ICollection`1" /> containing the keys of the <see cref="T:System.Collections.Generic.IDictionary`2" />.</summary>
      /// <returns>An <see cref="T:System.Collections.Generic.ICollection`1" /> containing the keys of the object that implements <see cref="T:System.Collections.Generic.IDictionary`2" />.</returns>
      ICollection IDictionary.Keys => (ICollection) this.Keys.ToArray<TKey>(this.Count);

      /// <summary>Gets an <see cref="T:System.Collections.Generic.ICollection`1" /> containing the values in the <see cref="T:System.Collections.Generic.IDictionary`2" />.</summary>
      /// <returns>An <see cref="T:System.Collections.Generic.ICollection`1" /> containing the values in the object that implements <see cref="T:System.Collections.Generic.IDictionary`2" />.</returns>
      ICollection IDictionary.Values => (ICollection) this.Values.ToArray<TValue>(this.Count);

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

      /// <summary>Gets a value that indicates whether access to the <see cref="T:System.Collections.ICollection" /> is synchronized (thread safe).</summary>
      /// <returns>
      /// <see langword="true" /> if access to the <see cref="T:System.Collections.ICollection" /> is synchronized (thread safe); otherwise, <see langword="false" />.</returns>
      [DebuggerBrowsable(DebuggerBrowsableState.Never)]
      bool ICollection.IsSynchronized => false;


      #nullable disable
      /// <summary>Adds an element with the provided key and value to the dictionary object.</summary>
      /// <param name="key">The key of the element to add.</param>
      /// <param name="value">The value of the element to add.</param>
      void IDictionary.Add(object key, object value) => this.Add((TKey) key, (TValue) value);

      /// <summary>Determines whether the dictionary object contains an element with the specified key.</summary>
      /// <param name="key">The key to locate.</param>
      /// <returns>
      /// <see langword="true" /> if the dictionary contains an element with the key; otherwise, <see langword="false" />.</returns>
      bool IDictionary.Contains(object key) => this.ContainsKey((TKey) key);

      /// <summary>Returns an <see cref="T:System.Collections.IDictionaryEnumerator" /> object for the dictionary.</summary>
      /// <exception cref="T:System.NotImplementedException" />
      /// <returns>An <see cref="T:System.Collections.IDictionaryEnumerator" /> object for the dictionary.</returns>
      IDictionaryEnumerator IDictionary.GetEnumerator() => (IDictionaryEnumerator) new DictionaryEnumerator<TKey, TValue>((IEnumerator<KeyValuePair<TKey, TValue>>) this.GetEnumerator());

      /// <summary>Removes the element with the specified key from the dictionary.</summary>
      /// <param name="key">The key of the element to remove.</param>
      void IDictionary.Remove(object key) => this.Remove((TKey) key);


      #nullable enable
      /// <summary>Gets or sets the element with the specified key.</summary>
      /// <param name="key">The key.</param>
      /// <returns>Value stored under specified key.</returns>
      object? IDictionary.this[
      #nullable disable
      object key]
      {
        get => (object) this[(TKey) key];
        set => this[(TKey) key] = (TValue) value;
      }

      /// <summary>Copies the elements of the dictionary to an array of type <see cref="T:System.Collections.Generic.KeyValuePair`2" />, starting at the specified array index.</summary>
      /// <param name="array">The one-dimensional array of type <see cref="T:System.Collections.Generic.KeyValuePair`2" /> that is the destination of the elements copied from the dictionary. The array must have zero-based indexing.</param>
      /// <param name="arrayIndex">The zero-based index in <paramref name="array" /> at which copying begins.</param>
      void ICollection.CopyTo(Array array, int arrayIndex)
      {
        Requires.NotNull<Array>(array, nameof (array));
        Requires.Range(arrayIndex >= 0, nameof (arrayIndex));
        Requires.Range(array.Length >= arrayIndex + this.Count, nameof (arrayIndex));
        foreach (KeyValuePair<TKey, TValue> keyValuePair in this)
          array.SetValue((object) new DictionaryEntry((object) keyValuePair.Key, (object) keyValuePair.Value), arrayIndex++);
      }

      internal int Version => this._version;


      #nullable enable
      private ImmutableDictionary<
      #nullable disable
      TKey, TValue>.MutationInput Origin => new ImmutableDictionary<TKey, TValue>.MutationInput(this.Root, this._comparers);


      #nullable enable
      private SortedInt32KeyNode<ImmutableDictionary<
      #nullable disable
      TKey, TValue>.HashBucket> Root
      {
        get => this._root;
        set
        {
          ++this._version;
          if (this._root == value)
            return;
          this._root = value;
          this._immutable = (ImmutableDictionary<TKey, TValue>) null;
        }
      }


      #nullable enable
      /// <summary>Gets or sets the element with the specified key.</summary>
      /// <param name="key">The element to get or set.</param>
      /// <exception cref="T:System.ArgumentNullException">
      /// <paramref name="key" /> is <see langword="null" />.</exception>
      /// <exception cref="T:System.Collections.Generic.KeyNotFoundException">The property is being retrieved, and <paramref name="key" /> is not found.</exception>
      /// <exception cref="T:System.NotSupportedException">The property is being set, and the <see cref="T:System.Collections.Generic.IDictionary`2" /> is read-only.</exception>
      /// <returns>The element that has the specified key.</returns>
      public TValue this[TKey key]
      {
        get
        {
          TValue obj;
          if (this.TryGetValue(key, out obj))
            return obj;
          throw new KeyNotFoundException( key.ToString());
        }
        set => this.Apply(ImmutableDictionary<TKey, TValue>.Add(key, value, ImmutableDictionary<TKey, TValue>.KeyCollisionBehavior.SetValue, this.Origin));
      }

      /// <summary>Adds a sequence of values to this collection.</summary>
      /// <param name="items">The items to add to this collection.</param>
      public void AddRange(IEnumerable<KeyValuePair<TKey, TValue>> items) => this.Apply(ImmutableDictionary<TKey, TValue>.AddRange(items, this.Origin));

      /// <summary>Removes any entries with keys that match those found in the specified sequence from the immutable dictionary.</summary>
      /// <param name="keys">The keys for entries to remove from the dictionary.</param>
      public void RemoveRange(IEnumerable<TKey> keys)
      {
        Requires.NotNull<IEnumerable<TKey>>(keys, nameof (keys));
        foreach (TKey key in keys)
          this.Remove(key);
      }

      /// <summary>Returns an enumerator that iterates through the immutable dictionary.</summary>
      /// <returns>An enumerator that can be used to iterate through the collection.</returns>
      public ImmutableDictionary<
      #nullable disable
      TKey, TValue>.Enumerator GetEnumerator() => new ImmutableDictionary<TKey, TValue>.Enumerator(this._root, this);


      #nullable enable
      /// <summary>Gets the value for a given key if a matching key exists in the dictionary.</summary>
      /// <param name="key">The key to search for.</param>
      /// <returns>The value for the key, or <c>default(TValue)</c> if no matching key was found.</returns>
      public TValue? GetValueOrDefault(TKey key) => this.GetValueOrDefault(key, default (TValue));

      /// <summary>Gets the value for a given key if a matching key exists in the dictionary.</summary>
      /// <param name="key">The key to search for.</param>
      /// <param name="defaultValue">The default value to return if no matching key is found in the dictionary.</param>
      /// <returns>The value for the key, or <paramref name="defaultValue" /> if no matching key was found.</returns>
      public TValue GetValueOrDefault(TKey key, TValue defaultValue)
      {
        Requires.NotNullAllowStructs<TKey>(key, nameof (key));
        TValue obj;
        return this.TryGetValue(key, out obj) ? obj : defaultValue;
      }

      /// <summary>Creates an immutable dictionary based on the contents of this instance.</summary>
      /// <returns>An immutable dictionary.</returns>
      public ImmutableDictionary<TKey, TValue> ToImmutable() => this._immutable ?? (this._immutable = ImmutableDictionary<TKey, TValue>.Wrap(this._root, this._comparers, this._count));

      /// <summary>Adds an element that has the specified key and value to the immutable dictionary.</summary>
      /// <param name="key">The key of the element to add.</param>
      /// <param name="value">The value of the element to add.</param>
      /// <exception cref="T:System.ArgumentNullException">
      /// <paramref name="key" /> is null.</exception>
      /// <exception cref="T:System.ArgumentException">An element with the same key already exists in the dictionary.</exception>
      /// <exception cref="T:System.NotSupportedException">The dictionary is read-only.</exception>
      public void Add(TKey key, TValue value) => this.Apply(ImmutableDictionary<TKey, TValue>.Add(key, value, ImmutableDictionary<TKey, TValue>.KeyCollisionBehavior.ThrowIfValueDifferent, this.Origin));

      /// <summary>Determines whether the immutable dictionary contains an element that has the specified key.</summary>
      /// <param name="key">The key to locate in the dictionary.</param>
      /// <exception cref="T:System.ArgumentNullException">
      /// <paramref name="key" /> is null.</exception>
      /// <returns>
      /// <see langword="true" /> if the dictionary contains an element with the key; otherwise, <see langword="false" />.</returns>
      public bool ContainsKey(TKey key) => ImmutableDictionary<TKey, TValue>.ContainsKey(key, this.Origin);

      /// <summary>Determines whether the immutable dictionary contains an element that has the specified value.</summary>
      /// <param name="value">The value to locate in the immutable dictionary. The value can be <see langword="null" /> for reference types.</param>
      /// <returns>
      /// <see langword="true" /> if the dictionary contains an element with the specified value; otherwise, <see langword="false" />.</returns>
      public bool ContainsValue(TValue value)
      {
        foreach (KeyValuePair<TKey, TValue> keyValuePair in this)
        {
          if (this.ValueComparer.Equals(value, keyValuePair.Value))
            return true;
        }
        return false;
      }

      /// <summary>Removes the element with the specified key from the immutable dictionary.</summary>
      /// <param name="key">The key of the element to remove.</param>
      /// <exception cref="T:System.ArgumentNullException">
      /// <paramref name="key" /> is null.</exception>
      /// <exception cref="T:System.NotSupportedException">The dictionary is read-only.</exception>
      /// <returns>
      /// <see langword="true" /> if the element is successfully removed; otherwise, <see langword="false" />.  This method also returns <see langword="false" /> if <paramref name="key" /> was not found in the dictionary.</returns>
      public bool Remove(TKey key) => this.Apply(ImmutableDictionary<TKey, TValue>.Remove(key, this.Origin));

      /// <summary>Returns the value associated with the specified key.</summary>
      /// <param name="key">The key whose value will be retrieved.</param>
      /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, returns the default value for the type of the <paramref name="value" /> parameter. This parameter is passed uninitialized.</param>
      /// <exception cref="T:System.ArgumentNullException">
      /// <paramref name="key" /> is null.</exception>
      /// <returns>
      /// <see langword="true" /> if the object that implements the immutable dictionary contains an element with the specified key; otherwise, <see langword="false" />.</returns>
      public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value) => ImmutableDictionary<TKey, TValue>.TryGetValue(key, this.Origin, out value);

      /// <summary>Determines whether this dictionary contains a specified key.</summary>
      /// <param name="equalKey">The key to search for.</param>
      /// <param name="actualKey">The matching key located in the dictionary if found, or <c>equalkey</c> if no match is found.</param>
      /// <returns>
      /// <see langword="true" /> if a match for <paramref name="equalKey" /> is found; otherwise, <see langword="false" />.</returns>
      public bool TryGetKey(TKey equalKey, out TKey actualKey) => ImmutableDictionary<TKey, TValue>.TryGetKey(equalKey, this.Origin, out actualKey);

      /// <summary>Adds the specified item to the immutable dictionary.</summary>
      /// <param name="item">The object to add to the dictionary.</param>
      /// <exception cref="T:System.NotSupportedException">The dictionary is read-only.</exception>
      public void Add(KeyValuePair<TKey, TValue> item) => this.Add(item.Key, item.Value);

      /// <summary>Removes all items from the immutable dictionary.</summary>
      /// <exception cref="T:System.NotSupportedException">The dictionary is read-only.</exception>
      public void Clear()
      {
        this.Root = SortedInt32KeyNode<ImmutableDictionary<TKey, TValue>.HashBucket>.EmptyNode;
        this._count = 0;
      }

      /// <summary>Determines whether the immutable dictionary contains a specific value.</summary>
      /// <param name="item">The object to locate in the dictionary.</param>
      /// <returns>
      /// <see langword="true" /> if <paramref name="item" /> is found in the dictionary; otherwise, <see langword="false" />.</returns>
      public bool Contains(KeyValuePair<TKey, TValue> item) => ImmutableDictionary<TKey, TValue>.Contains(item, this.Origin);


      #nullable disable
      /// <summary>Copies the elements of the dictionary to an array of type <see cref="T:System.Collections.Generic.KeyValuePair`2" />, starting at the specified array index.</summary>
      /// <param name="array">The one-dimensional array that is the destination of the elements copied from the dictionary. The array must have zero-based indexing.</param>
      /// <param name="arrayIndex">The zero-based index in <paramref name="array" /> at which copying begins.</param>
      void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(
        KeyValuePair<TKey, TValue>[] array,
        int arrayIndex)
      {
        Requires.NotNull<KeyValuePair<TKey, TValue>[]>(array, nameof (array));
        foreach (KeyValuePair<TKey, TValue> keyValuePair in this)
          array[arrayIndex++] = keyValuePair;
      }


      #nullable enable
      /// <summary>Removes the first occurrence of a specific object from the immutable dictionary.</summary>
      /// <param name="item">The object to remove from the dictionary.</param>
      /// <exception cref="T:System.NotSupportedException">The dictionary is read-only.</exception>
      /// <returns>
      /// <see langword="true" /> if <paramref name="item" /> was successfully removed from the dictionary; otherwise, <see langword="false" />. This method also returns false if <paramref name="item" /> is not found in the dictionary.</returns>
      public bool Remove(KeyValuePair<TKey, TValue> item) => this.Contains(item) && this.Remove(item.Key);


      #nullable disable
      /// <summary>Returns an enumerator that iterates through the collection.</summary>
      /// <returns>An enumerator that can be used to iterate through the collection.</returns>
      IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() => (IEnumerator<KeyValuePair<TKey, TValue>>) this.GetEnumerator();

      /// <summary>Returns an enumerator that iterates through a collection.</summary>
      /// <returns>An enumerator object that can be used to iterate through the collection.</returns>
      IEnumerator IEnumerable.GetEnumerator() => (IEnumerator) this.GetEnumerator();

      private bool Apply(
        ImmutableDictionary<TKey, TValue>.MutationResult result)
      {
        this.Root = result.Root;
        this._count += result.CountAdjustment;
        return result.CountAdjustment != 0;
      }
    }


    #nullable enable
    internal sealed class Comparers : 
      IEqualityComparer<ImmutableDictionary<
      #nullable disable
      TKey, TValue>.HashBucket>,
      IEqualityComparer<KeyValuePair<
      #nullable enable
      TKey, TValue>>
    {
      internal static readonly ImmutableDictionary<
      #nullable disable
      TKey, TValue>.Comparers Default = new ImmutableDictionary<TKey, TValue>.Comparers((IEqualityComparer<TKey>) EqualityComparer<TKey>.Default, (IEqualityComparer<TValue>) EqualityComparer<TValue>.Default);
      private readonly IEqualityComparer<TKey> _keyComparer;
      private readonly IEqualityComparer<TValue> _valueComparer;


      #nullable enable
      internal Comparers(
        IEqualityComparer<TKey> keyComparer,
        IEqualityComparer<TValue> valueComparer)
      {
        Requires.NotNull<IEqualityComparer<TKey>>(keyComparer, nameof (keyComparer));
        Requires.NotNull<IEqualityComparer<TValue>>(valueComparer, nameof (valueComparer));
        this._keyComparer = keyComparer;
        this._valueComparer = valueComparer;
      }

      internal IEqualityComparer<TKey> KeyComparer => this._keyComparer;

      internal IEqualityComparer<KeyValuePair<TKey, TValue>> KeyOnlyComparer => (IEqualityComparer<KeyValuePair<TKey, TValue>>) this;

      internal IEqualityComparer<TValue> ValueComparer => this._valueComparer;

      internal IEqualityComparer<ImmutableDictionary<
      #nullable disable
      TKey, TValue>.HashBucket> HashBucketEqualityComparer => (IEqualityComparer<ImmutableDictionary<TKey, TValue>.HashBucket>) this;


      #nullable enable
      public bool Equals(
        ImmutableDictionary<
        #nullable disable
        TKey, TValue>.HashBucket x,
        ImmutableDictionary<TKey, TValue>.HashBucket y)
      {
        if (x.AdditionalElements == y.AdditionalElements)
        {
          IEqualityComparer<TKey> keyComparer = this.KeyComparer;
          KeyValuePair<TKey, TValue> firstValue = x.FirstValue;
          TKey key1 = firstValue.Key;
          firstValue = y.FirstValue;
          TKey key2 = firstValue.Key;
          if (keyComparer.Equals(key1, key2))
          {
            IEqualityComparer<TValue> valueComparer = this.ValueComparer;
            firstValue = x.FirstValue;
            TValue x1 = firstValue.Value;
            firstValue = y.FirstValue;
            TValue y1 = firstValue.Value;
            return valueComparer.Equals(x1, y1);
          }
        }
        return false;
      }


      #nullable enable
      public int GetHashCode(ImmutableDictionary<
      #nullable disable
      TKey, TValue>.HashBucket obj) => this.KeyComparer.GetHashCode(obj.FirstValue.Key);

      bool IEqualityComparer<KeyValuePair<TKey, TValue>>.Equals(
        KeyValuePair<TKey, TValue> x,
        KeyValuePair<TKey, TValue> y)
      {
        return this._keyComparer.Equals(x.Key, y.Key);
      }

      int IEqualityComparer<KeyValuePair<TKey, TValue>>.GetHashCode(KeyValuePair<TKey, TValue> obj) => this._keyComparer.GetHashCode(obj.Key);


      #nullable enable
      internal static ImmutableDictionary<
      #nullable disable
      TKey, TValue>.Comparers Get(

        #nullable enable
        IEqualityComparer<TKey> keyComparer,
        IEqualityComparer<TValue> valueComparer)
      {
        Requires.NotNull<IEqualityComparer<TKey>>(keyComparer, nameof (keyComparer));
        Requires.NotNull<IEqualityComparer<TValue>>(valueComparer, nameof (valueComparer));
        return keyComparer != ImmutableDictionary<TKey, TValue>.Comparers.Default.KeyComparer || valueComparer != ImmutableDictionary<TKey, TValue>.Comparers.Default.ValueComparer ? new ImmutableDictionary<TKey, TValue>.Comparers(keyComparer, valueComparer) : ImmutableDictionary<TKey, TValue>.Comparers.Default;
      }

      internal ImmutableDictionary<
      #nullable disable
      TKey, TValue>.Comparers WithValueComparer(
      #nullable enable
      IEqualityComparer<TValue> valueComparer)
      {
        Requires.NotNull<IEqualityComparer<TValue>>(valueComparer, nameof (valueComparer));
        return this._valueComparer != valueComparer ? ImmutableDictionary<TKey, TValue>.Comparers.Get(this.KeyComparer, valueComparer) : this;
      }
    }

    /// <summary>Enumerates the contents of the immutable dictionary without allocating any memory.
    /// 
    /// NuGet package: System.Collections.Immutable (about immutable collections and how to install)</summary>
    /// <typeparam name="TKey" />
    /// <typeparam name="TValue" />
    public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>, IDisposable, IEnumerator
    {

      #nullable disable
      private readonly ImmutableDictionary<TKey, TValue>.Builder _builder;
      private SortedInt32KeyNode<ImmutableDictionary<TKey, TValue>.HashBucket>.Enumerator _mapEnumerator;
      private ImmutableDictionary<TKey, TValue>.HashBucket.Enumerator _bucketEnumerator;
      private int _enumeratingBuilderVersion;


      #nullable enable
      internal Enumerator(
        SortedInt32KeyNode<ImmutableDictionary<
        #nullable disable
        TKey, TValue>.HashBucket> root,

        #nullable enable
        ImmutableDictionary<
        #nullable disable
        TKey, TValue>.Builder
        #nullable enable
        ? builder = null)
      {
        this._builder = builder;
        this._mapEnumerator = new SortedInt32KeyNode<ImmutableDictionary<TKey, TValue>.HashBucket>.Enumerator(root);
        this._bucketEnumerator = new ImmutableDictionary<TKey, TValue>.HashBucket.Enumerator();
        this._enumeratingBuilderVersion = builder != null ? builder.Version : -1;
      }

      /// <summary>Gets the element at the current position of the enumerator.</summary>
      /// <returns>The element in the dictionary at the current position of the enumerator.</returns>
      public KeyValuePair<TKey, TValue> Current
      {
        get
        {
          this._mapEnumerator.ThrowIfDisposed();
          return this._bucketEnumerator.Current;
        }
      }

      /// <summary>Gets the current element.</summary>
      /// <returns>Current element in enumeration.</returns>
      object IEnumerator.Current => (object) this.Current;

      /// <summary>Advances the enumerator to the next element of the immutable dictionary.</summary>
      /// <exception cref="T:System.InvalidOperationException">The dictionary was modified after the enumerator was created.</exception>
      /// <returns>
      /// <see langword="true" /> if the enumerator was successfully advanced to the next element; <see langword="false" /> if the enumerator has passed the end of the dictionary.</returns>
      public bool MoveNext()
      {
        this.ThrowIfChanged();
        if (this._bucketEnumerator.MoveNext())
          return true;
        if (!this._mapEnumerator.MoveNext())
          return false;
        this._bucketEnumerator = new ImmutableDictionary<TKey, TValue>.HashBucket.Enumerator(this._mapEnumerator.Current.Value);
        return this._bucketEnumerator.MoveNext();
      }

      /// <summary>Sets the enumerator to its initial position, which is before the first element in the dictionary.</summary>
      /// <exception cref="T:System.InvalidOperationException">The dictionary was modified after the enumerator was created.</exception>
      public void Reset()
      {
        this._enumeratingBuilderVersion = this._builder != null ? this._builder.Version : -1;
        this._mapEnumerator.Reset();
        this._bucketEnumerator.Dispose();
        this._bucketEnumerator = new ImmutableDictionary<TKey, TValue>.HashBucket.Enumerator();
      }

      /// <summary>Releases the resources used by the current instance of the <see cref="T:System.Collections.Immutable.ImmutableDictionary`2.Enumerator" /> class.</summary>
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

    internal readonly struct HashBucket : IEnumerable<KeyValuePair<TKey, TValue>>, IEnumerable
    {

      #nullable disable
      private readonly KeyValuePair<TKey, TValue> _firstValue;
      private readonly ImmutableList<KeyValuePair<TKey, TValue>>.Node _additionalElements;

      private HashBucket(
        KeyValuePair<TKey, TValue> firstElement,
        ImmutableList<KeyValuePair<TKey, TValue>>.Node additionalElements = null)
      {
        this._firstValue = firstElement;
        this._additionalElements = additionalElements ?? ImmutableList<KeyValuePair<TKey, TValue>>.Node.EmptyNode;
      }

      internal bool IsEmpty => this._additionalElements == null;


      #nullable enable
      internal KeyValuePair<TKey, TValue> FirstValue
      {
        get
        {
          if (this.IsEmpty)
            throw new InvalidOperationException();
          return this._firstValue;
        }
      }

      internal ImmutableList<KeyValuePair<TKey, TValue>>.Node AdditionalElements => this._additionalElements;

      public ImmutableDictionary<
      #nullable disable
      TKey, TValue>.HashBucket.Enumerator GetEnumerator() => new ImmutableDictionary<TKey, TValue>.HashBucket.Enumerator(this);

      IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() => (IEnumerator<KeyValuePair<TKey, TValue>>) this.GetEnumerator();

      IEnumerator IEnumerable.GetEnumerator() => (IEnumerator) this.GetEnumerator();


      #nullable enable
      public override bool Equals(object? obj) => throw new NotSupportedException();

      public override int GetHashCode() => throw new NotSupportedException();

      internal ImmutableDictionary<
      #nullable disable
      TKey, TValue>.HashBucket Add(

        #nullable enable
        TKey key,
        TValue value,
        IEqualityComparer<KeyValuePair<TKey, TValue>> keyOnlyComparer,
        IEqualityComparer<TValue> valueComparer,
        ImmutableDictionary<
        #nullable disable
        TKey, TValue>.KeyCollisionBehavior behavior,
        out ImmutableDictionary<TKey, TValue>.OperationResult result)
      {
        KeyValuePair<TKey, TValue> keyValuePair = new KeyValuePair<TKey, TValue>(key, value);
        if (this.IsEmpty)
        {
          result = ImmutableDictionary<TKey, TValue>.OperationResult.SizeChanged;
          return new ImmutableDictionary<TKey, TValue>.HashBucket(keyValuePair);
        }
        if (keyOnlyComparer.Equals(keyValuePair, this._firstValue))
        {
          switch (behavior)
          {
            case ImmutableDictionary<TKey, TValue>.KeyCollisionBehavior.SetValue:
              result = ImmutableDictionary<TKey, TValue>.OperationResult.AppliedWithoutSizeChange;
              return new ImmutableDictionary<TKey, TValue>.HashBucket(keyValuePair, this._additionalElements);
            case ImmutableDictionary<TKey, TValue>.KeyCollisionBehavior.Skip:
              result = ImmutableDictionary<TKey, TValue>.OperationResult.NoChangeRequired;
              return this;
            case ImmutableDictionary<TKey, TValue>.KeyCollisionBehavior.ThrowIfValueDifferent:
              if (!valueComparer.Equals(this._firstValue.Value, value))
                throw new ArgumentException();
              result = ImmutableDictionary<TKey, TValue>.OperationResult.NoChangeRequired;
              return this;
            case ImmutableDictionary<TKey, TValue>.KeyCollisionBehavior.ThrowAlways:
              throw new ArgumentException();
            default:
              throw new InvalidOperationException();
          }
        }
        else
        {
          int index = this._additionalElements.IndexOf(keyValuePair, keyOnlyComparer);
          if (index < 0)
          {
            result = ImmutableDictionary<TKey, TValue>.OperationResult.SizeChanged;
            return new ImmutableDictionary<TKey, TValue>.HashBucket(this._firstValue, this._additionalElements.Add(keyValuePair));
          }
          switch (behavior)
          {
            case ImmutableDictionary<TKey, TValue>.KeyCollisionBehavior.SetValue:
              result = ImmutableDictionary<TKey, TValue>.OperationResult.AppliedWithoutSizeChange;
              return new ImmutableDictionary<TKey, TValue>.HashBucket(this._firstValue, this._additionalElements.ReplaceAt(index, keyValuePair));
            case ImmutableDictionary<TKey, TValue>.KeyCollisionBehavior.Skip:
              result = ImmutableDictionary<TKey, TValue>.OperationResult.NoChangeRequired;
              return this;
            case ImmutableDictionary<TKey, TValue>.KeyCollisionBehavior.ThrowIfValueDifferent:
              ref readonly KeyValuePair<TKey, TValue> local = ref this._additionalElements.ItemRef(index);
              if (!valueComparer.Equals(local.Value, value))
                throw new ArgumentException();
              result = ImmutableDictionary<TKey, TValue>.OperationResult.NoChangeRequired;
              return this;
            case ImmutableDictionary<TKey, TValue>.KeyCollisionBehavior.ThrowAlways:
              throw new ArgumentException();
            default:
              throw new InvalidOperationException();
          }
        }
      }


      #nullable enable
      internal ImmutableDictionary<
      #nullable disable
      TKey, TValue>.HashBucket Remove(

        #nullable enable
        TKey key,
        IEqualityComparer<KeyValuePair<TKey, TValue>> keyOnlyComparer,
        out ImmutableDictionary<
        #nullable disable
        TKey, TValue>.OperationResult result)
      {
        if (this.IsEmpty)
        {
          result = ImmutableDictionary<TKey, TValue>.OperationResult.NoChangeRequired;
          return this;
        }
        KeyValuePair<TKey, TValue> y = new KeyValuePair<TKey, TValue>(key, default (TValue));
        if (keyOnlyComparer.Equals(this._firstValue, y))
        {
          if (this._additionalElements.IsEmpty)
          {
            result = ImmutableDictionary<TKey, TValue>.OperationResult.SizeChanged;
            return new ImmutableDictionary<TKey, TValue>.HashBucket();
          }
          int count = this._additionalElements.Left.Count;
          result = ImmutableDictionary<TKey, TValue>.OperationResult.SizeChanged;
          return new ImmutableDictionary<TKey, TValue>.HashBucket(this._additionalElements.Key, this._additionalElements.RemoveAt(count));
        }
        int index = this._additionalElements.IndexOf(y, keyOnlyComparer);
        if (index < 0)
        {
          result = ImmutableDictionary<TKey, TValue>.OperationResult.NoChangeRequired;
          return this;
        }
        result = ImmutableDictionary<TKey, TValue>.OperationResult.SizeChanged;
        return new ImmutableDictionary<TKey, TValue>.HashBucket(this._firstValue, this._additionalElements.RemoveAt(index));
      }


      #nullable enable
      internal bool TryGetValue(
        TKey key,
        ImmutableDictionary<
        #nullable disable
        TKey, TValue>.Comparers comparers,
        [MaybeNullWhen(false)] out 
        #nullable enable
        TValue value)
      {
        if (this.IsEmpty)
        {
          value = default (TValue);
          return false;
        }
        if (comparers.KeyComparer.Equals(this._firstValue.Key, key))
        {
          value = this._firstValue.Value;
          return true;
        }
        int index = this._additionalElements.IndexOf(new KeyValuePair<TKey, TValue>(key, default (TValue)), comparers.KeyOnlyComparer);
        if (index < 0)
        {
          value = default (TValue);
          return false;
        }
        value = this._additionalElements.ItemRef(index).Value;
        return true;
      }

      internal bool TryGetKey(
        TKey equalKey,
        ImmutableDictionary<
        #nullable disable
        TKey, TValue>.Comparers comparers,
        out 
        #nullable enable
        TKey actualKey)
      {
        if (this.IsEmpty)
        {
          actualKey = equalKey;
          return false;
        }
        IEqualityComparer<TKey> keyComparer = comparers.KeyComparer;
        KeyValuePair<TKey, TValue> keyValuePair = this._firstValue;
        TKey key1 = keyValuePair.Key;
        TKey y = equalKey;
        if (keyComparer.Equals(key1, y))
        {
            actualKey = default;
            ref TKey local = ref actualKey;
          keyValuePair = this._firstValue;
          TKey key2 = keyValuePair.Key;
          local = key2;
          return true;
        }
        int index = this._additionalElements.IndexOf(new KeyValuePair<TKey, TValue>(equalKey, default (TValue)), comparers.KeyOnlyComparer);
        if (index < 0)
        {
          actualKey = equalKey;
          return false;
        }

        actualKey = default;
        ref TKey local1 = ref actualKey;
        keyValuePair = this._additionalElements.ItemRef(index);
        TKey key3 = keyValuePair.Key;
        local1 = key3;
        return true;
      }

      internal void Freeze() => this._additionalElements?.Freeze();

      internal struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>, IDisposable, IEnumerator
      {

        #nullable disable
        private readonly ImmutableDictionary<TKey, TValue>.HashBucket _bucket;
        private ImmutableDictionary<TKey, TValue>.HashBucket.Enumerator.Position _currentPosition;
        private ImmutableList<KeyValuePair<TKey, TValue>>.Enumerator _additionalEnumerator;


        #nullable enable
        internal Enumerator(
          ImmutableDictionary<
          #nullable disable
          TKey, TValue>.HashBucket bucket)
        {
          this._bucket = bucket;
          this._currentPosition = ImmutableDictionary<TKey, TValue>.HashBucket.Enumerator.Position.BeforeFirst;
          this._additionalEnumerator = new ImmutableList<KeyValuePair<TKey, TValue>>.Enumerator();
        }


        #nullable enable
        object IEnumerator.Current => (object) this.Current;

        public KeyValuePair<TKey, TValue> Current
        {
          get
          {
            switch (this._currentPosition)
            {
              case ImmutableDictionary<TKey, TValue>.HashBucket.Enumerator.Position.First:
                return this._bucket._firstValue;
              case ImmutableDictionary<TKey, TValue>.HashBucket.Enumerator.Position.Additional:
                return this._additionalEnumerator.Current;
              default:
                throw new InvalidOperationException();
            }
          }
        }

        public bool MoveNext()
        {
          if (this._bucket.IsEmpty)
          {
            this._currentPosition = ImmutableDictionary<TKey, TValue>.HashBucket.Enumerator.Position.End;
            return false;
          }
          switch (this._currentPosition)
          {
            case ImmutableDictionary<TKey, TValue>.HashBucket.Enumerator.Position.BeforeFirst:
              this._currentPosition = ImmutableDictionary<TKey, TValue>.HashBucket.Enumerator.Position.First;
              return true;
            case ImmutableDictionary<TKey, TValue>.HashBucket.Enumerator.Position.First:
              if (this._bucket._additionalElements.IsEmpty)
              {
                this._currentPosition = ImmutableDictionary<TKey, TValue>.HashBucket.Enumerator.Position.End;
                return false;
              }
              this._currentPosition = ImmutableDictionary<TKey, TValue>.HashBucket.Enumerator.Position.Additional;
              this._additionalEnumerator = new ImmutableList<KeyValuePair<TKey, TValue>>.Enumerator(this._bucket._additionalElements);
              return this._additionalEnumerator.MoveNext();
            case ImmutableDictionary<TKey, TValue>.HashBucket.Enumerator.Position.Additional:
              return this._additionalEnumerator.MoveNext();
            case ImmutableDictionary<TKey, TValue>.HashBucket.Enumerator.Position.End:
              return false;
            default:
              throw new InvalidOperationException();
          }
        }

        public void Reset()
        {
          this._additionalEnumerator.Dispose();
          this._currentPosition = ImmutableDictionary<TKey, TValue>.HashBucket.Enumerator.Position.BeforeFirst;
        }

        public void Dispose() => this._additionalEnumerator.Dispose();


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
      private readonly SortedInt32KeyNode<ImmutableDictionary<TKey, TValue>.HashBucket> _root;
      private readonly ImmutableDictionary<TKey, TValue>.Comparers _comparers;

      internal MutationInput(
        SortedInt32KeyNode<ImmutableDictionary<TKey, TValue>.HashBucket> root,
        ImmutableDictionary<TKey, TValue>.Comparers comparers)
      {
        this._root = root;
        this._comparers = comparers;
      }

      internal MutationInput(ImmutableDictionary<TKey, TValue> map)
      {
        this._root = map._root;
        this._comparers = map._comparers;
      }

      internal SortedInt32KeyNode<ImmutableDictionary<TKey, TValue>.HashBucket> Root => this._root;

      internal ImmutableDictionary<TKey, TValue>.Comparers Comparers => this._comparers;

      internal IEqualityComparer<TKey> KeyComparer => this._comparers.KeyComparer;

      internal IEqualityComparer<KeyValuePair<TKey, TValue>> KeyOnlyComparer => this._comparers.KeyOnlyComparer;

      internal IEqualityComparer<TValue> ValueComparer => this._comparers.ValueComparer;

      internal IEqualityComparer<ImmutableDictionary<TKey, TValue>.HashBucket> HashBucketComparer => this._comparers.HashBucketEqualityComparer;
    }

    private readonly struct MutationResult
    {
      private readonly SortedInt32KeyNode<ImmutableDictionary<TKey, TValue>.HashBucket> _root;
      private readonly int _countAdjustment;

      internal MutationResult(
        ImmutableDictionary<TKey, TValue>.MutationInput unchangedInput)
      {
        this._root = unchangedInput.Root;
        this._countAdjustment = 0;
      }

      internal MutationResult(
        SortedInt32KeyNode<ImmutableDictionary<TKey, TValue>.HashBucket> root,
        int countAdjustment)
      {
        Requires.NotNull<SortedInt32KeyNode<ImmutableDictionary<TKey, TValue>.HashBucket>>(root, nameof (root));
        this._root = root;
        this._countAdjustment = countAdjustment;
      }

      internal SortedInt32KeyNode<ImmutableDictionary<TKey, TValue>.HashBucket> Root => this._root;

      internal int CountAdjustment => this._countAdjustment;

      internal ImmutableDictionary<TKey, TValue> Finalize(ImmutableDictionary<TKey, TValue> priorMap)
      {
        Requires.NotNull<ImmutableDictionary<TKey, TValue>>(priorMap, nameof (priorMap));
        return priorMap.Wrap(this.Root, priorMap._count + this.CountAdjustment);
      }
    }


    #nullable enable
    internal enum KeyCollisionBehavior
    {
      SetValue,
      Skip,
      ThrowIfValueDifferent,
      ThrowAlways,
    }

    internal enum OperationResult
    {
      AppliedWithoutSizeChange,
      SizeChanged,
      NoChangeRequired,
    }
  }
}
