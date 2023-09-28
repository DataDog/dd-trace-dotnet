// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.TypeSpecificationHandle
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
using System;

namespace Datadog.System.Reflection.Metadata
{
  public readonly struct TypeSpecificationHandle : IEquatable<TypeSpecificationHandle>
  {
    private const uint tokenType = 452984832;
    private const byte tokenTypeSmall = 27;
    private readonly int _rowId;

    private TypeSpecificationHandle(int rowId) => this._rowId = rowId;

    internal static TypeSpecificationHandle FromRowId(int rowId) => new TypeSpecificationHandle(rowId);

    public static implicit operator Handle(TypeSpecificationHandle handle) => new Handle((byte) 27, handle._rowId);

    public static implicit operator EntityHandle(TypeSpecificationHandle handle) => new EntityHandle((uint) (452984832UL | (ulong) handle._rowId));

    public static explicit operator TypeSpecificationHandle(Handle handle)
    {
      if (handle.VType != (byte) 27)
        Throw.InvalidCast();
      return new TypeSpecificationHandle(handle.RowId);
    }

    public static explicit operator TypeSpecificationHandle(EntityHandle handle)
    {
      if (handle.VType != 452984832U)
        Throw.InvalidCast();
      return new TypeSpecificationHandle(handle.RowId);
    }

    public bool IsNil => this.RowId == 0;

    internal int RowId => this._rowId;

    public static bool operator ==(TypeSpecificationHandle left, TypeSpecificationHandle right) => left._rowId == right._rowId;

    public override bool Equals(object? obj) => obj is TypeSpecificationHandle specificationHandle && specificationHandle._rowId == this._rowId;

    public bool Equals(TypeSpecificationHandle other) => this._rowId == other._rowId;

    public override int GetHashCode() => this._rowId.GetHashCode();

    public static bool operator !=(TypeSpecificationHandle left, TypeSpecificationHandle right) => left._rowId != right._rowId;
  }
}
