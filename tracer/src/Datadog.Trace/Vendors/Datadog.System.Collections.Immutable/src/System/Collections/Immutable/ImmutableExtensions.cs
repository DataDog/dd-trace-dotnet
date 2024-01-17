﻿// Decompiled with JetBrains decompiler
// Type: System.Collections.Immutable.ImmutableExtensions
// Assembly: System.Collections.Immutable, Version=7.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 5F9FF90F-0D16-4469-A104-76829D3705E2
// Assembly location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.dll
// XML documentation location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.xml

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Datadog.System.Linq;


#nullable enable
namespace Datadog.System.Collections.Immutable
{
    internal static class ImmutableExtensions
  {
    internal static bool IsValueType<T>()
    {
      if ((object) default (T) != null)
        return true;
      Type type = typeof (T);
      return type.IsConstructedGenericType && type.GetGenericTypeDefinition() == typeof (Nullable<>);
    }

    internal static IOrderedCollection<T> AsOrderedCollection<T>(this IEnumerable<T> sequence)
    {
      Requires.NotNull<IEnumerable<T>>(sequence, nameof (sequence));
      switch (sequence)
      {
        case IOrderedCollection<T> orderedCollection:
          return orderedCollection;
        case IList<T> collection:
          return (IOrderedCollection<T>) new ImmutableExtensions.ListOfTWrapper<T>(collection);
        default:
          return (IOrderedCollection<T>) new ImmutableExtensions.FallbackWrapper<T>(sequence);
      }
    }

    internal static void ClearFastWhenEmpty<T>(this Stack<T> stack)
    {
      if (stack.Count <= 0)
        return;
      stack.Clear();
    }

    internal static DisposableEnumeratorAdapter<T, TEnumerator> GetEnumerableDisposable<T, TEnumerator>(
      this IEnumerable<T> enumerable)
      where TEnumerator : struct, IStrongEnumerator<T>, IEnumerator<T>
    {
      Requires.NotNull<IEnumerable<T>>(enumerable, nameof (enumerable));
      return enumerable is IStrongEnumerable<T, TEnumerator> strongEnumerable ? new DisposableEnumeratorAdapter<T, TEnumerator>(strongEnumerable.GetEnumerator()) : new DisposableEnumeratorAdapter<T, TEnumerator>(enumerable.GetEnumerator());
    }

    internal static bool TryGetCount<T>(this IEnumerable<T> sequence, out int count) => sequence.TryGetCount<T>(out count);

    internal static bool TryGetCount<T>(this IEnumerable sequence, out int count)
    {
      switch (sequence)
      {
        case ICollection collection:
          count = collection.Count;
          return true;
        case ICollection<T> objs1:
          count = objs1.Count;
          return true;
        case IReadOnlyCollection<T> objs2:
          count = objs2.Count;
          return true;
        default:
          count = 0;
          return false;
      }
    }

    internal static int GetCount<T>(ref IEnumerable<T> sequence)
    {
      int count;
      if (!sequence.TryGetCount<T>(out count))
      {
        List<T> list = sequence.ToList<T>();
        count = list.Count;
        sequence = (IEnumerable<T>) list;
      }
      return count;
    }

    internal static bool TryCopyTo<T>(this IEnumerable<T> sequence, T[] array, int arrayIndex)
    {
      if (sequence is IList<T>)
      {
        if (sequence is List<T> objList)
        {
          objList.CopyTo(array, arrayIndex);
          return true;
        }
        if (sequence.GetType() == typeof (T[]))
        {
          T[] sourceArray = (T[]) sequence;
          Array.Copy((Array) sourceArray, 0, (Array) array, arrayIndex, sourceArray.Length);
          return true;
        }
        if (sequence is ImmutableArray<T> immutableArray)
        {
          Array.Copy((Array) immutableArray.array, 0, (Array) array, arrayIndex, immutableArray.Length);
          return true;
        }
      }
      return false;
    }

    internal static T[] ToArray<T>(this IEnumerable<T> sequence, int count)
    {
      Requires.NotNull<IEnumerable<T>>(sequence, nameof (sequence));
      Requires.Range(count >= 0, nameof (count));
      if (count == 0)
        return ImmutableArray<T>.Empty.array;
      T[] array = new T[count];
      if (!sequence.TryCopyTo<T>(array, 0))
      {
        int num = 0;
        foreach (T obj in sequence)
        {
          Requires.Argument(num < count);
          array[num++] = obj;
        }
        Requires.Argument(num == count);
      }
      return array;
    }


    #nullable disable
    private sealed class ListOfTWrapper<T> : IOrderedCollection<T>, IEnumerable<T>, IEnumerable
    {
      private readonly IList<T> _collection;

      internal ListOfTWrapper(IList<T> collection)
      {
        Requires.NotNull<IList<T>>(collection, nameof (collection));
        this._collection = collection;
      }

      public int Count => this._collection.Count;

      public T this[int index] => this._collection[index];

      public IEnumerator<T> GetEnumerator() => this._collection.GetEnumerator();

      IEnumerator IEnumerable.GetEnumerator() => (IEnumerator) this.GetEnumerator();
    }

    private sealed class FallbackWrapper<T> : IOrderedCollection<T>, IEnumerable<T>, IEnumerable
    {
      private readonly IEnumerable<T> _sequence;
      private IList<T> _collection;

      internal FallbackWrapper(IEnumerable<T> sequence)
      {
        Requires.NotNull<IEnumerable<T>>(sequence, nameof (sequence));
        this._sequence = sequence;
      }

      public int Count
      {
        get
        {
          if (this._collection == null)
          {
            int count;
            if (this._sequence.TryGetCount<T>(out count))
              return count;
            this._collection = (IList<T>) this._sequence.ToArray<T>();
          }
          return this._collection.Count;
        }
      }

      public T this[int index]
      {
        get
        {
          if (this._collection == null)
            this._collection = (IList<T>) this._sequence.ToArray<T>();
          return this._collection[index];
        }
      }

      public IEnumerator<T> GetEnumerator() => this._sequence.GetEnumerator();

      IEnumerator IEnumerable.GetEnumerator() => (IEnumerator) this.GetEnumerator();
    }
  }
}
