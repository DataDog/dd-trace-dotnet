// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.SequencePoint
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using System.Diagnostics;
using Datadog.System.Diagnostics.CodeAnalysis;
using Datadog.System.Reflection.Internal;


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
    [DebuggerDisplay("{GetDebuggerDisplay(),nq}")]
  public readonly struct SequencePoint : IEquatable<SequencePoint>
  {
    public const int HiddenLine = 16707566;

    public DocumentHandle Document { get; }

    public int Offset { get; }

    public int StartLine { get; }

    public int EndLine { get; }

    public int StartColumn { get; }

    public int EndColumn { get; }

    internal SequencePoint(DocumentHandle document, int offset)
    {
      this.Document = document;
      this.Offset = offset;
      this.StartLine = 16707566;
      this.StartColumn = 0;
      this.EndLine = 16707566;
      this.EndColumn = 0;
    }

    internal SequencePoint(
      DocumentHandle document,
      int offset,
      int startLine,
      ushort startColumn,
      int endLine,
      ushort endColumn)
    {
      this.Document = document;
      this.Offset = offset;
      this.StartLine = startLine;
      this.StartColumn = (int) startColumn;
      this.EndLine = endLine;
      this.EndColumn = (int) endColumn;
    }

    public override int GetHashCode() => Hash.Combine(this.Document.RowId, Hash.Combine(this.Offset, Hash.Combine(this.StartLine, Hash.Combine(this.StartColumn, Hash.Combine(this.EndLine, this.EndColumn)))));

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is SequencePoint other && this.Equals(other);

    public bool Equals(SequencePoint other) => this.Document == other.Document && this.Offset == other.Offset && this.StartLine == other.StartLine && this.StartColumn == other.StartColumn && this.EndLine == other.EndLine && this.EndColumn == other.EndColumn;

    public bool IsHidden => this.StartLine == 16707566;


    #nullable disable
    private string GetDebuggerDisplay()
    {
      if (this.IsHidden)
        return "<hidden>";
      return string.Format("{0}: ({1}, {2}) - ({3}, {4})", (object) this.Offset, (object) this.StartLine, (object) this.StartColumn, (object) this.EndLine, (object) this.EndColumn);
    }
  }
}
