// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.LocalScopeHandleCollection
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
    public readonly struct LocalScopeHandleCollection : 
    IReadOnlyCollection<LocalScopeHandle>,
    IEnumerable<LocalScopeHandle>,
    IEnumerable
  {

    #nullable disable
    private readonly MetadataReader _reader;
    private readonly int _firstRowId;
    private readonly int _lastRowId;


    #nullable enable
    internal LocalScopeHandleCollection(MetadataReader reader, int methodDefinitionRowId)
    {
      this._reader = reader;
      if (methodDefinitionRowId == 0)
      {
        this._firstRowId = 1;
        this._lastRowId = reader.LocalScopeTable.NumberOfRows;
      }
      else
        reader.LocalScopeTable.GetLocalScopeRange(methodDefinitionRowId, out this._firstRowId, out this._lastRowId);
    }

    public int Count => this._lastRowId - this._firstRowId + 1;

    public LocalScopeHandleCollection.Enumerator GetEnumerator() => new LocalScopeHandleCollection.Enumerator(this._reader, this._firstRowId, this._lastRowId);


    #nullable disable
    IEnumerator<LocalScopeHandle> IEnumerable<LocalScopeHandle>.GetEnumerator() => (IEnumerator<LocalScopeHandle>) this.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => (IEnumerator) this.GetEnumerator();


    #nullable enable
    public struct Enumerator : IEnumerator<LocalScopeHandle>, IDisposable, IEnumerator
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
        this._lastRowId = lastRowId;
        this._currentRowId = firstRowId - 1;
      }

      public LocalScopeHandle Current => LocalScopeHandle.FromRowId((int) ((long) this._currentRowId & 16777215L));

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

    public struct ChildrenEnumerator : IEnumerator<LocalScopeHandle>, IDisposable, IEnumerator
    {

      #nullable disable
      private readonly MetadataReader _reader;
      private readonly int _parentEndOffset;
      private readonly int _parentRowId;
      private readonly MethodDefinitionHandle _parentMethodRowId;
      private int _currentRowId;
      private const int EnumEnded = 16777216;


      #nullable enable
      internal ChildrenEnumerator(MetadataReader reader, int parentRowId)
      {
        this._reader = reader;
        this._parentEndOffset = reader.LocalScopeTable.GetEndOffset(parentRowId);
        this._parentMethodRowId = reader.LocalScopeTable.GetMethod(parentRowId);
        this._currentRowId = 0;
        this._parentRowId = parentRowId;
      }

      public LocalScopeHandle Current => LocalScopeHandle.FromRowId((int) ((long) this._currentRowId & 16777215L));

      public bool MoveNext()
      {
        int currentRowId = this._currentRowId;
        int num;
        int rowId;
        switch (currentRowId)
        {
          case 0:
            num = -1;
            rowId = this._parentRowId + 1;
            break;
          case 16777216:
            return false;
          default:
            num = this._reader.LocalScopeTable.GetEndOffset(currentRowId);
            rowId = currentRowId + 1;
            break;
        }
        for (int numberOfRows = this._reader.LocalScopeTable.NumberOfRows; rowId <= numberOfRows && !(this._parentMethodRowId != this._reader.LocalScopeTable.GetMethod(rowId)); ++rowId)
        {
          int endOffset = this._reader.LocalScopeTable.GetEndOffset(rowId);
          if (endOffset > num)
          {
            if (endOffset > this._parentEndOffset)
            {
              this._currentRowId = 16777216;
              return false;
            }
            this._currentRowId = rowId;
            return true;
          }
        }
        this._currentRowId = 16777216;
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
