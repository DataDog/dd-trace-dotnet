// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.GenericParameterConstraintHandle
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
using System;

namespace Datadog.System.Reflection.Metadata
{
  public readonly struct GenericParameterConstraintHandle : 
    IEquatable<GenericParameterConstraintHandle>
  {
    private const uint tokenType = 738197504;
    private const byte tokenTypeSmall = 44;
    private readonly int _rowId;

    private GenericParameterConstraintHandle(int rowId) => this._rowId = rowId;

    internal static GenericParameterConstraintHandle FromRowId(int rowId) => new GenericParameterConstraintHandle(rowId);

    public static implicit operator Handle(GenericParameterConstraintHandle handle) => new Handle((byte) 44, handle._rowId);

    public static implicit operator EntityHandle(GenericParameterConstraintHandle handle) => new EntityHandle((uint) (738197504UL | (ulong) handle._rowId));

    public static explicit operator GenericParameterConstraintHandle(Handle handle)
    {
      if (handle.VType != (byte) 44)
        Throw.InvalidCast();
      return new GenericParameterConstraintHandle(handle.RowId);
    }

    public static explicit operator GenericParameterConstraintHandle(EntityHandle handle)
    {
      if (handle.VType != 738197504U)
        Throw.InvalidCast();
      return new GenericParameterConstraintHandle(handle.RowId);
    }

    public bool IsNil => this.RowId == 0;

    internal int RowId => this._rowId;

    public static bool operator ==(
      GenericParameterConstraintHandle left,
      GenericParameterConstraintHandle right)
    {
      return left._rowId == right._rowId;
    }

    public override bool Equals(object? obj) => obj is GenericParameterConstraintHandle constraintHandle && constraintHandle._rowId == this._rowId;

    public bool Equals(GenericParameterConstraintHandle other) => this._rowId == other._rowId;

    public override int GetHashCode() => this._rowId.GetHashCode();

    public static bool operator !=(
      GenericParameterConstraintHandle left,
      GenericParameterConstraintHandle right)
    {
      return left._rowId != right._rowId;
    }
  }
}
