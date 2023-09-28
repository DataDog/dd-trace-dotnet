// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.DeclarativeSecurityAttributeHandle
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
using System;

namespace Datadog.System.Reflection.Metadata
{
  public readonly struct DeclarativeSecurityAttributeHandle : 
    IEquatable<DeclarativeSecurityAttributeHandle>
  {
    private const uint tokenType = 234881024;
    private const byte tokenTypeSmall = 14;
    private readonly int _rowId;

    private DeclarativeSecurityAttributeHandle(int rowId) => this._rowId = rowId;

    internal static DeclarativeSecurityAttributeHandle FromRowId(int rowId) => new DeclarativeSecurityAttributeHandle(rowId);

    public static implicit operator Handle(DeclarativeSecurityAttributeHandle handle) => new Handle((byte) 14, handle._rowId);

    public static implicit operator EntityHandle(DeclarativeSecurityAttributeHandle handle) => new EntityHandle((uint) (234881024UL | (ulong) handle._rowId));

    public static explicit operator DeclarativeSecurityAttributeHandle(Handle handle)
    {
      if (handle.VType != (byte) 14)
        Throw.InvalidCast();
      return new DeclarativeSecurityAttributeHandle(handle.RowId);
    }

    public static explicit operator DeclarativeSecurityAttributeHandle(EntityHandle handle)
    {
      if (handle.VType != 234881024U)
        Throw.InvalidCast();
      return new DeclarativeSecurityAttributeHandle(handle.RowId);
    }

    public bool IsNil => this._rowId == 0;

    internal int RowId => this._rowId;

    public static bool operator ==(
      DeclarativeSecurityAttributeHandle left,
      DeclarativeSecurityAttributeHandle right)
    {
      return left._rowId == right._rowId;
    }

    public override bool Equals(object? obj) => obj is DeclarativeSecurityAttributeHandle securityAttributeHandle && securityAttributeHandle._rowId == this._rowId;

    public bool Equals(DeclarativeSecurityAttributeHandle other) => this._rowId == other._rowId;

    public override int GetHashCode() => this._rowId.GetHashCode();

    public static bool operator !=(
      DeclarativeSecurityAttributeHandle left,
      DeclarativeSecurityAttributeHandle right)
    {
      return left._rowId != right._rowId;
    }
  }
}
