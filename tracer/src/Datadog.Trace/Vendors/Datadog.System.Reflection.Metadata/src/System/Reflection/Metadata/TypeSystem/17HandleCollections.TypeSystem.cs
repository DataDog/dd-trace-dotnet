// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.AssemblyReferenceHandleCollection
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
    /// <summary>Collection of assembly references.</summary>
    public readonly struct AssemblyReferenceHandleCollection : 
    IReadOnlyCollection<AssemblyReferenceHandle>,
    IEnumerable<AssemblyReferenceHandle>,
    IEnumerable
  {

    #nullable disable
    private readonly MetadataReader _reader;


    #nullable enable
    internal AssemblyReferenceHandleCollection(MetadataReader reader) => this._reader = reader;

    public int Count => this._reader.AssemblyRefTable.NumberOfNonVirtualRows + this._reader.AssemblyRefTable.NumberOfVirtualRows;

    public AssemblyReferenceHandleCollection.Enumerator GetEnumerator() => new AssemblyReferenceHandleCollection.Enumerator(this._reader);


    #nullable disable
    IEnumerator<AssemblyReferenceHandle> IEnumerable<AssemblyReferenceHandle>.GetEnumerator() => (IEnumerator<AssemblyReferenceHandle>) this.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => (IEnumerator) this.GetEnumerator();


    #nullable enable
    public struct Enumerator : IEnumerator<AssemblyReferenceHandle>, IDisposable, IEnumerator
    {

      #nullable disable
      private readonly MetadataReader _reader;
      private int _currentRowId;
      private const int EnumEnded = 16777216;
      private int _virtualRowId;


      #nullable enable
      internal Enumerator(MetadataReader reader)
      {
        this._reader = reader;
        this._currentRowId = 0;
        this._virtualRowId = -1;
      }

      public AssemblyReferenceHandle Current
      {
        get
        {
          if (this._virtualRowId < 0)
            return AssemblyReferenceHandle.FromRowId((int) ((long) this._currentRowId & 16777215L));
          return this._virtualRowId == 16777216 ? new AssemblyReferenceHandle() : AssemblyReferenceHandle.FromVirtualIndex((AssemblyReferenceHandle.VirtualIndex) this._virtualRowId);
        }
      }

      public bool MoveNext()
      {
        if (this._currentRowId < this._reader.AssemblyRefTable.NumberOfNonVirtualRows)
        {
          ++this._currentRowId;
          return true;
        }
        if (this._virtualRowId < this._reader.AssemblyRefTable.NumberOfVirtualRows - 1)
        {
          ++this._virtualRowId;
          return true;
        }
        this._currentRowId = 16777216;
        this._virtualRowId = 16777216;
        return false;
      }

      object IEnumerator.Current => (object) this.Current;

      void IEnumerator.Reset() => throw new NotSupportedException();

      void IDisposable.Dispose()
      {
      }
    }
  }
}
