﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.SequencePointCollection
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using System.Collections;
using System.Collections.Generic;
using Datadog.System.Reflection.Internal;
using Datadog.System.Reflection.Metadata.Ecma335;


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
    public readonly struct SequencePointCollection : IEnumerable<SequencePoint>, IEnumerable
  {
    private readonly MemoryBlock _block;
    private readonly DocumentHandle _document;

    internal SequencePointCollection(MemoryBlock block, DocumentHandle document)
    {
      this._block = block;
      this._document = document;
    }

    public SequencePointCollection.Enumerator GetEnumerator() => new SequencePointCollection.Enumerator(this._block, this._document);


    #nullable disable
    IEnumerator IEnumerable.GetEnumerator() => (IEnumerator) this.GetEnumerator();

    IEnumerator<SequencePoint> IEnumerable<SequencePoint>.GetEnumerator() => (IEnumerator<SequencePoint>) this.GetEnumerator();


    #nullable enable
    public struct Enumerator : IEnumerator<SequencePoint>, IDisposable, IEnumerator
    {
      private BlobReader _reader;
      private SequencePoint _current;
      private int _previousNonHiddenStartLine;
      private ushort _previousNonHiddenStartColumn;

      internal Enumerator(MemoryBlock block, DocumentHandle document)
      {
        this._reader = new BlobReader(block);
        this._current = new SequencePoint(document, -1);
        this._previousNonHiddenStartLine = -1;
        this._previousNonHiddenStartColumn = (ushort) 0;
      }

      public bool MoveNext()
      {
        if (this._reader.RemainingBytes == 0)
          return false;
        DocumentHandle document = this._current.Document;
        int offset;
        if (this._reader.Offset == 0)
        {
          this._reader.ReadCompressedInteger();
          if (document.IsNil)
            document = this.ReadDocumentHandle();
          offset = this._reader.ReadCompressedInteger();
        }
        else
        {
          int delta;
          while ((delta = this._reader.ReadCompressedInteger()) == 0)
            document = this.ReadDocumentHandle();
          offset = SequencePointCollection.Enumerator.AddOffsets(this._current.Offset, delta);
        }
        int deltaLines;
        int deltaColumns;
        this.ReadDeltaLinesAndColumns(out deltaLines, out deltaColumns);
        if (deltaLines == 0 && deltaColumns == 0)
        {
          this._current = new SequencePoint(document, offset);
          return true;
        }
        int startLine;
        ushort startColumn;
        if (this._previousNonHiddenStartLine < 0)
        {
          startLine = this.ReadLine();
          startColumn = this.ReadColumn();
        }
        else
        {
          startLine = SequencePointCollection.Enumerator.AddLines(this._previousNonHiddenStartLine, this._reader.ReadCompressedSignedInteger());
          startColumn = SequencePointCollection.Enumerator.AddColumns(this._previousNonHiddenStartColumn, this._reader.ReadCompressedSignedInteger());
        }
        this._previousNonHiddenStartLine = startLine;
        this._previousNonHiddenStartColumn = startColumn;
        this._current = new SequencePoint(document, offset, startLine, startColumn, SequencePointCollection.Enumerator.AddLines(startLine, deltaLines), SequencePointCollection.Enumerator.AddColumns(startColumn, deltaColumns));
        return true;
      }


      #nullable disable
      private void ReadDeltaLinesAndColumns(out int deltaLines, out int deltaColumns)
      {
        deltaLines = this._reader.ReadCompressedInteger();
        deltaColumns = deltaLines == 0 ? this._reader.ReadCompressedInteger() : this._reader.ReadCompressedSignedInteger();
      }

      private int ReadLine() => this._reader.ReadCompressedInteger();

      private ushort ReadColumn()
      {
        int num = this._reader.ReadCompressedInteger();
        if (num > (int) ushort.MaxValue)
          Throw.SequencePointValueOutOfRange();
        return (ushort) num;
      }

      private static int AddOffsets(int value, int delta)
      {
        int num = value + delta;
        if (num < 0)
          Throw.SequencePointValueOutOfRange();
        return num;
      }

      private static int AddLines(int value, int delta)
      {
        int num = value + delta;
        if (num < 0 || num >= 16707566)
          Throw.SequencePointValueOutOfRange();
        return num;
      }

      private static ushort AddColumns(ushort value, int delta)
      {
        int num = (int) value + delta;
        if (num < 0 || num >= (int) ushort.MaxValue)
          Throw.SequencePointValueOutOfRange();
        return (ushort) num;
      }

      private DocumentHandle ReadDocumentHandle()
      {
        int rowId = this._reader.ReadCompressedInteger();
        if (rowId == 0 || !TokenTypeIds.IsValidRowId(rowId))
          Throw.InvalidHandle();
        return DocumentHandle.FromRowId(rowId);
      }

      public SequencePoint Current => this._current;


      #nullable enable
      object IEnumerator.Current => (object) this._current;

      public void Reset()
      {
        this._reader.Reset();
        this._current = new SequencePoint();
      }

      void IDisposable.Dispose()
      {
      }
    }
  }
}
