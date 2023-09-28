// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.MethodImplementationHandleCollection
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
    public readonly struct MethodImplementationHandleCollection : 
    IReadOnlyCollection<MethodImplementationHandle>,
    IEnumerable<MethodImplementationHandle>,
    IEnumerable
  {
    private readonly int _firstRowId;
    private readonly int _lastRowId;

    internal MethodImplementationHandleCollection(
      MetadataReader reader,
      TypeDefinitionHandle containingType)
    {
      if (containingType.IsNil)
      {
        this._firstRowId = 1;
        this._lastRowId = reader.MethodImplTable.NumberOfRows;
      }
      else
        reader.MethodImplTable.GetMethodImplRange(containingType, out this._firstRowId, out this._lastRowId);
    }

    public int Count => this._lastRowId - this._firstRowId + 1;

    public MethodImplementationHandleCollection.Enumerator GetEnumerator() => new MethodImplementationHandleCollection.Enumerator(this._firstRowId, this._lastRowId);


    #nullable disable
    IEnumerator<MethodImplementationHandle> IEnumerable<MethodImplementationHandle>.GetEnumerator() => (IEnumerator<MethodImplementationHandle>) this.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => (IEnumerator) this.GetEnumerator();


    #nullable enable
    public struct Enumerator : IEnumerator<MethodImplementationHandle>, IDisposable, IEnumerator
    {
      private readonly int _lastRowId;
      private int _currentRowId;
      private const int EnumEnded = 16777216;

      internal Enumerator(int firstRowId, int lastRowId)
      {
        this._currentRowId = firstRowId - 1;
        this._lastRowId = lastRowId;
      }

      public MethodImplementationHandle Current => MethodImplementationHandle.FromRowId((int) ((long) this._currentRowId & 16777215L));

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
