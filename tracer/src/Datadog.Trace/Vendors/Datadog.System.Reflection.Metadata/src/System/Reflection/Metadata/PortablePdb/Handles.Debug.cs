// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.DocumentHandle
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
using System;

namespace Datadog.System.Reflection.Metadata
{
  public readonly struct DocumentHandle : IEquatable<DocumentHandle>
  {
    private const uint tokenType = 805306368;
    private const byte tokenTypeSmall = 48;
    private readonly int _rowId;

    private DocumentHandle(int rowId) => this._rowId = rowId;

    internal static DocumentHandle FromRowId(int rowId) => new DocumentHandle(rowId);

    public static implicit operator Handle(DocumentHandle handle) => new Handle((byte) 48, handle._rowId);

    public static implicit operator EntityHandle(DocumentHandle handle) => new EntityHandle((uint) (805306368UL | (ulong) handle._rowId));

    public static explicit operator DocumentHandle(Handle handle)
    {
      if (handle.VType != (byte) 48)
        Throw.InvalidCast();
      return new DocumentHandle(handle.RowId);
    }

    public static explicit operator DocumentHandle(EntityHandle handle)
    {
      if (handle.VType != 805306368U)
        Throw.InvalidCast();
      return new DocumentHandle(handle.RowId);
    }

    public bool IsNil => this.RowId == 0;

    internal int RowId => this._rowId;

    public static bool operator ==(DocumentHandle left, DocumentHandle right) => left._rowId == right._rowId;

    public override bool Equals(object? obj) => obj is DocumentHandle documentHandle && documentHandle._rowId == this._rowId;

    public bool Equals(DocumentHandle other) => this._rowId == other._rowId;

    public override int GetHashCode() => this._rowId.GetHashCode();

    public static bool operator !=(DocumentHandle left, DocumentHandle right) => left._rowId != right._rowId;
  }
}
