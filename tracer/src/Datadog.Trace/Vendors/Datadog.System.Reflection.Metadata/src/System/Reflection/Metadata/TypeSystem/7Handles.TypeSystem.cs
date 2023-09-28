// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.ExportedTypeHandle
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
using System;

namespace Datadog.System.Reflection.Metadata
{
  public readonly struct ExportedTypeHandle : IEquatable<ExportedTypeHandle>
  {
    private const uint tokenType = 654311424;
    private const byte tokenTypeSmall = 39;
    private readonly int _rowId;

    private ExportedTypeHandle(int rowId) => this._rowId = rowId;

    internal static ExportedTypeHandle FromRowId(int rowId) => new ExportedTypeHandle(rowId);

    public static implicit operator Handle(ExportedTypeHandle handle) => new Handle((byte) 39, handle._rowId);

    public static implicit operator EntityHandle(ExportedTypeHandle handle) => new EntityHandle((uint) (654311424UL | (ulong) handle._rowId));

    public static explicit operator ExportedTypeHandle(Handle handle)
    {
      if (handle.VType != (byte) 39)
        Throw.InvalidCast();
      return new ExportedTypeHandle(handle.RowId);
    }

    public static explicit operator ExportedTypeHandle(EntityHandle handle)
    {
      if (handle.VType != 654311424U)
        Throw.InvalidCast();
      return new ExportedTypeHandle(handle.RowId);
    }

    public bool IsNil => this.RowId == 0;

    internal int RowId => this._rowId;

    public static bool operator ==(ExportedTypeHandle left, ExportedTypeHandle right) => left._rowId == right._rowId;

    public override bool Equals(object? obj) => obj is ExportedTypeHandle exportedTypeHandle && exportedTypeHandle._rowId == this._rowId;

    public bool Equals(ExportedTypeHandle other) => this._rowId == other._rowId;

    public override int GetHashCode() => this._rowId.GetHashCode();

    public static bool operator !=(ExportedTypeHandle left, ExportedTypeHandle right) => left._rowId != right._rowId;
  }
}
