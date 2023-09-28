// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.MethodSpecificationHandle
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
using System;

namespace Datadog.System.Reflection.Metadata
{
  public readonly struct MethodSpecificationHandle : IEquatable<MethodSpecificationHandle>
  {
    private const uint tokenType = 721420288;
    private const byte tokenTypeSmall = 43;
    private readonly int _rowId;

    private MethodSpecificationHandle(int rowId) => this._rowId = rowId;

    internal static MethodSpecificationHandle FromRowId(int rowId) => new MethodSpecificationHandle(rowId);

    public static implicit operator Handle(MethodSpecificationHandle handle) => new Handle((byte) 43, handle._rowId);

    public static implicit operator EntityHandle(MethodSpecificationHandle handle) => new EntityHandle((uint) (721420288UL | (ulong) handle._rowId));

    public static explicit operator MethodSpecificationHandle(Handle handle)
    {
      if (handle.VType != (byte) 43)
        Throw.InvalidCast();
      return new MethodSpecificationHandle(handle.RowId);
    }

    public static explicit operator MethodSpecificationHandle(EntityHandle handle)
    {
      if (handle.VType != 721420288U)
        Throw.InvalidCast();
      return new MethodSpecificationHandle(handle.RowId);
    }

    public bool IsNil => this.RowId == 0;

    internal int RowId => this._rowId;

    public static bool operator ==(MethodSpecificationHandle left, MethodSpecificationHandle right) => left._rowId == right._rowId;

    public override bool Equals(object? obj) => obj is MethodSpecificationHandle specificationHandle && specificationHandle._rowId == this._rowId;

    public bool Equals(MethodSpecificationHandle other) => this._rowId == other._rowId;

    public override int GetHashCode() => this._rowId.GetHashCode();

    public static bool operator !=(MethodSpecificationHandle left, MethodSpecificationHandle right) => left._rowId != right._rowId;
  }
}
