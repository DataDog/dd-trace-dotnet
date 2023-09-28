// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.FieldDefinitionHandle
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
using System;

namespace Datadog.System.Reflection.Metadata
{
  public readonly struct FieldDefinitionHandle : IEquatable<FieldDefinitionHandle>
  {
    private const uint tokenType = 67108864;
    private const byte tokenTypeSmall = 4;
    private readonly int _rowId;

    private FieldDefinitionHandle(int rowId) => this._rowId = rowId;

    internal static FieldDefinitionHandle FromRowId(int rowId) => new FieldDefinitionHandle(rowId);

    public static implicit operator Handle(FieldDefinitionHandle handle) => new Handle((byte) 4, handle._rowId);

    public static implicit operator EntityHandle(FieldDefinitionHandle handle) => new EntityHandle((uint) (67108864UL | (ulong) handle._rowId));

    public static explicit operator FieldDefinitionHandle(Handle handle)
    {
      if (handle.VType != (byte) 4)
        Throw.InvalidCast();
      return new FieldDefinitionHandle(handle.RowId);
    }

    public static explicit operator FieldDefinitionHandle(EntityHandle handle)
    {
      if (handle.VType != 67108864U)
        Throw.InvalidCast();
      return new FieldDefinitionHandle(handle.RowId);
    }

    public bool IsNil => this.RowId == 0;

    internal int RowId => this._rowId;

    public static bool operator ==(FieldDefinitionHandle left, FieldDefinitionHandle right) => left._rowId == right._rowId;

    public override bool Equals(object? obj) => obj is FieldDefinitionHandle definitionHandle && definitionHandle._rowId == this._rowId;

    public bool Equals(FieldDefinitionHandle other) => this._rowId == other._rowId;

    public override int GetHashCode() => this._rowId.GetHashCode();

    public static bool operator !=(FieldDefinitionHandle left, FieldDefinitionHandle right) => left._rowId != right._rowId;
  }
}
