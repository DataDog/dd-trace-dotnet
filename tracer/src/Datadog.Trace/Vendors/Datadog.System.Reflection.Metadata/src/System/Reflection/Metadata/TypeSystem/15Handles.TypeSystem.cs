// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.ParameterHandle
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
using System;

namespace Datadog.System.Reflection.Metadata
{
  public readonly struct ParameterHandle : IEquatable<ParameterHandle>
  {
    private const uint tokenType = 134217728;
    private const byte tokenTypeSmall = 8;
    private readonly int _rowId;

    private ParameterHandle(int rowId) => this._rowId = rowId;

    internal static ParameterHandle FromRowId(int rowId) => new ParameterHandle(rowId);

    public static implicit operator Handle(ParameterHandle handle) => new Handle((byte) 8, handle._rowId);

    public static implicit operator EntityHandle(ParameterHandle handle) => new EntityHandle((uint) (134217728UL | (ulong) handle._rowId));

    public static explicit operator ParameterHandle(Handle handle)
    {
      if (handle.VType != (byte) 8)
        Throw.InvalidCast();
      return new ParameterHandle(handle.RowId);
    }

    public static explicit operator ParameterHandle(EntityHandle handle)
    {
      if (handle.VType != 134217728U)
        Throw.InvalidCast();
      return new ParameterHandle(handle.RowId);
    }

    public bool IsNil => this.RowId == 0;

    internal int RowId => this._rowId;

    public static bool operator ==(ParameterHandle left, ParameterHandle right) => left._rowId == right._rowId;

    public override bool Equals(object? obj) => obj is ParameterHandle parameterHandle && parameterHandle._rowId == this._rowId;

    public bool Equals(ParameterHandle other) => this._rowId == other._rowId;

    public override int GetHashCode() => this._rowId.GetHashCode();

    public static bool operator !=(ParameterHandle left, ParameterHandle right) => left._rowId != right._rowId;
  }
}
