// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.TypeReferenceHandle
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
using System;

namespace Datadog.System.Reflection.Metadata
{
  public readonly struct TypeReferenceHandle : IEquatable<TypeReferenceHandle>
  {
    private const uint tokenType = 16777216;
    private const byte tokenTypeSmall = 1;
    private readonly int _rowId;

    private TypeReferenceHandle(int rowId) => this._rowId = rowId;

    internal static TypeReferenceHandle FromRowId(int rowId) => new TypeReferenceHandle(rowId);

    public static implicit operator Handle(TypeReferenceHandle handle) => new Handle((byte) 1, handle._rowId);

    public static implicit operator EntityHandle(TypeReferenceHandle handle) => new EntityHandle((uint) (16777216UL | (ulong) handle._rowId));

    public static explicit operator TypeReferenceHandle(Handle handle)
    {
      if (handle.VType != (byte) 1)
        Throw.InvalidCast();
      return new TypeReferenceHandle(handle.RowId);
    }

    public static explicit operator TypeReferenceHandle(EntityHandle handle)
    {
      if (handle.VType != 16777216U)
        Throw.InvalidCast();
      return new TypeReferenceHandle(handle.RowId);
    }

    public bool IsNil => this.RowId == 0;

    internal int RowId => this._rowId;

    public static bool operator ==(TypeReferenceHandle left, TypeReferenceHandle right) => left._rowId == right._rowId;

    public override bool Equals(object? obj) => obj is TypeReferenceHandle typeReferenceHandle && typeReferenceHandle._rowId == this._rowId;

    public bool Equals(TypeReferenceHandle other) => this._rowId == other._rowId;

    public override int GetHashCode() => this._rowId.GetHashCode();

    public static bool operator !=(TypeReferenceHandle left, TypeReferenceHandle right) => left._rowId != right._rowId;
  }
}
