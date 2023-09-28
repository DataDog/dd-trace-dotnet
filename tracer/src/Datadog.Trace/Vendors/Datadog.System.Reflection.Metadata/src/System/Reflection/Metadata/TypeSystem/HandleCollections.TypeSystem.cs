// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.GenericParameterHandleCollection
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using System.Collections;
using System.Collections.Generic;


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
    /// <summary>
    /// Represents generic type parameters of a method or type.
    /// </summary>
    public readonly struct GenericParameterHandleCollection : 
    IReadOnlyList<GenericParameterHandle>,
    IReadOnlyCollection<GenericParameterHandle>,
    IEnumerable<GenericParameterHandle>,
    IEnumerable
  {
    private readonly int _firstRowId;
    private readonly ushort _count;

    internal GenericParameterHandleCollection(int firstRowId, ushort count)
    {
      this._firstRowId = firstRowId;
      this._count = count;
    }

    public int Count => (int) this._count;

    public GenericParameterHandle this[int index]
    {
      get
      {
        if (index < 0 || index >= (int) this._count)
          Throw.IndexOutOfRange();
        return GenericParameterHandle.FromRowId(this._firstRowId + index);
      }
    }

    public GenericParameterHandleCollection.Enumerator GetEnumerator() => new GenericParameterHandleCollection.Enumerator(this._firstRowId, this._firstRowId + (int) this._count - 1);


    #nullable disable
    IEnumerator<GenericParameterHandle> IEnumerable<GenericParameterHandle>.GetEnumerator() => (IEnumerator<GenericParameterHandle>) this.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => (IEnumerator) this.GetEnumerator();


    #nullable enable
    public struct Enumerator : IEnumerator<GenericParameterHandle>, IDisposable, IEnumerator
    {
      private readonly int _lastRowId;
      private int _currentRowId;
      private const int EnumEnded = 16777216;

      internal Enumerator(int firstRowId, int lastRowId)
      {
        this._currentRowId = firstRowId - 1;
        this._lastRowId = lastRowId;
      }

      public GenericParameterHandle Current => GenericParameterHandle.FromRowId((int) ((long) this._currentRowId & 16777215L));

      public bool MoveNext()
      {
        if (this._currentRowId >= this._lastRowId)
        {
          this._currentRowId = 16777216;
          return false;
        }
        ++this._currentRowId;
        return true;
      }

      object IEnumerator.Current => (object) this.Current;

      void IEnumerator.Reset() => throw new NotSupportedException();

      void IDisposable.Dispose()
      {
      }
    }
  }
}
