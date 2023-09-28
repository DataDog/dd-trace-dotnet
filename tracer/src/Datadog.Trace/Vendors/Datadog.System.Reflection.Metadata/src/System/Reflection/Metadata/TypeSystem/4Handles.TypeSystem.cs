// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.MethodImplementationHandle
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
using System;

namespace Datadog.System.Reflection.Metadata
{
  public readonly struct MethodImplementationHandle : IEquatable<MethodImplementationHandle>
  {
    private const uint tokenType = 419430400;
    private const byte tokenTypeSmall = 25;
    private readonly int _rowId;

    private MethodImplementationHandle(int rowId) => this._rowId = rowId;

    internal static MethodImplementationHandle FromRowId(int rowId) => new MethodImplementationHandle(rowId);

    public static implicit operator Handle(MethodImplementationHandle handle) => new Handle((byte) 25, handle._rowId);

    public static implicit operator EntityHandle(MethodImplementationHandle handle) => new EntityHandle((uint) (419430400UL | (ulong) handle._rowId));

    public static explicit operator MethodImplementationHandle(Handle handle)
    {
      if (handle.VType != (byte) 25)
        Throw.InvalidCast();
      return new MethodImplementationHandle(handle.RowId);
    }

    public static explicit operator MethodImplementationHandle(EntityHandle handle)
    {
      if (handle.VType != 419430400U)
        Throw.InvalidCast();
      return new MethodImplementationHandle(handle.RowId);
    }

    public bool IsNil => this.RowId == 0;

    internal int RowId => this._rowId;

    public static bool operator ==(
      MethodImplementationHandle left,
      MethodImplementationHandle right)
    {
      return left._rowId == right._rowId;
    }

    public override bool Equals(object? obj) => obj is MethodImplementationHandle implementationHandle && implementationHandle._rowId == this._rowId;

    public bool Equals(MethodImplementationHandle other) => this._rowId == other._rowId;

    public override int GetHashCode() => this._rowId.GetHashCode();

    public static bool operator !=(
      MethodImplementationHandle left,
      MethodImplementationHandle right)
    {
      return left._rowId != right._rowId;
    }
  }
}
