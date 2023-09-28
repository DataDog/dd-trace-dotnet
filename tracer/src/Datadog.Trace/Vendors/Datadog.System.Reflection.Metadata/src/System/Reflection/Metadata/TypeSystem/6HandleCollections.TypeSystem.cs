// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.PropertyDefinitionHandleCollection
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
    public readonly struct PropertyDefinitionHandleCollection : 
    IReadOnlyCollection<PropertyDefinitionHandle>,
    IEnumerable<PropertyDefinitionHandle>,
    IEnumerable
  {

    #nullable disable
    private readonly MetadataReader _reader;
    private readonly int _firstRowId;
    private readonly int _lastRowId;


    #nullable enable
    internal PropertyDefinitionHandleCollection(MetadataReader reader)
    {
      this._reader = reader;
      this._firstRowId = 1;
      this._lastRowId = reader.PropertyTable.NumberOfRows;
    }

    internal PropertyDefinitionHandleCollection(
      MetadataReader reader,
      TypeDefinitionHandle containingType)
    {
      this._reader = reader;
      reader.GetPropertyRange(containingType, out this._firstRowId, out this._lastRowId);
    }

    public int Count => this._lastRowId - this._firstRowId + 1;

    public PropertyDefinitionHandleCollection.Enumerator GetEnumerator() => new PropertyDefinitionHandleCollection.Enumerator(this._reader, this._firstRowId, this._lastRowId);


    #nullable disable
    IEnumerator<PropertyDefinitionHandle> IEnumerable<PropertyDefinitionHandle>.GetEnumerator() => (IEnumerator<PropertyDefinitionHandle>) this.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => (IEnumerator) this.GetEnumerator();


    #nullable enable
    public struct Enumerator : IEnumerator<PropertyDefinitionHandle>, IDisposable, IEnumerator
    {

      #nullable disable
      private readonly MetadataReader _reader;
      private readonly int _lastRowId;
      private int _currentRowId;
      private const int EnumEnded = 16777216;


      #nullable enable
      internal Enumerator(MetadataReader reader, int firstRowId, int lastRowId)
      {
        this._reader = reader;
        this._currentRowId = firstRowId - 1;
        this._lastRowId = lastRowId;
      }

      public PropertyDefinitionHandle Current => this._reader.UsePropertyPtrTable ? this.GetCurrentPropertyIndirect() : PropertyDefinitionHandle.FromRowId((int) ((long) this._currentRowId & 16777215L));

      private PropertyDefinitionHandle GetCurrentPropertyIndirect() => this._reader.PropertyPtrTable.GetPropertyFor(this._currentRowId & 16777215);

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
