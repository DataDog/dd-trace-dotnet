// Decompiled with JetBrains decompiler
// Type: System.Collections.Immutable.KeysOrValuesCollectionAccessor`3
// Assembly: System.Collections.Immutable, Version=7.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 5F9FF90F-0D16-4469-A104-76829D3705E2
// Assembly location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.dll
// XML documentation location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.xml

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;


#nullable enable
namespace Datadog.System.Collections.Immutable
{
    internal abstract class KeysOrValuesCollectionAccessor<TKey, TValue, T> : 
    ICollection<T>,
    IEnumerable<T>,
    IEnumerable,
    ICollection
    where TKey : notnull
  {

    #nullable disable
    private readonly IImmutableDictionary<TKey, TValue> _dictionary;
    private readonly IEnumerable<T> _keysOrValues;


    #nullable enable
    protected KeysOrValuesCollectionAccessor(
      IImmutableDictionary<TKey, TValue> dictionary,
      IEnumerable<T> keysOrValues)
    {
      Requires.NotNull<IImmutableDictionary<TKey, TValue>>(dictionary, nameof (dictionary));
      Requires.NotNull<IEnumerable<T>>(keysOrValues, nameof (keysOrValues));
      this._dictionary = dictionary;
      this._keysOrValues = keysOrValues;
    }

    public bool IsReadOnly => true;

    public int Count => this._dictionary.Count;

    protected IImmutableDictionary<TKey, TValue> Dictionary => this._dictionary;

    public void Add(T item) => throw new NotSupportedException();

    public void Clear() => throw new NotSupportedException();

    public abstract bool Contains(T item);

    public void CopyTo(T[] array, int arrayIndex)
    {
      Requires.NotNull<T[]>(array, nameof (array));
      Requires.Range(arrayIndex >= 0, nameof (arrayIndex));
      Requires.Range(array.Length >= arrayIndex + this.Count, nameof (arrayIndex));
      foreach (T obj in this)
        array[arrayIndex++] = obj;
    }

    public bool Remove(T item) => throw new NotSupportedException();

    public IEnumerator<T> GetEnumerator() => this._keysOrValues.GetEnumerator();


    #nullable disable
    IEnumerator IEnumerable.GetEnumerator() => (IEnumerator) this.GetEnumerator();

    void ICollection.CopyTo(Array array, int arrayIndex)
    {
      Requires.NotNull<Array>(array, nameof (array));
      Requires.Range(arrayIndex >= 0, nameof (arrayIndex));
      Requires.Range(array.Length >= arrayIndex + this.Count, nameof (arrayIndex));
      foreach (T obj in this)
        array.SetValue((object) obj, arrayIndex++);
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    bool ICollection.IsSynchronized => true;


    #nullable enable
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    object ICollection.SyncRoot => (object) this;
  }
}
