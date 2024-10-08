//------------------------------------------------------------------------------
// <auto-generated />
// This file was automatically generated by the UpdateVendors tool.
//------------------------------------------------------------------------------
#pragma warning disable CS0618, CS0649, CS1574, CS1580, CS1581, CS1584, CS1591, CS1573, CS8018, SYSLIB0011, SYSLIB0032
#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618, CS8620, CS8714, CS8762, CS8765, CS8766, CS8767, CS8768, CS8769, CS8612, CS8629, CS8774
// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.CustomDebugInformationHandle
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72

using System;
using System.Diagnostics.CodeAnalysis;


#nullable enable
namespace Datadog.Trace.VendoredMicrosoftCode.System.Reflection.Metadata
{
  internal readonly struct CustomDebugInformationHandle : IEquatable<CustomDebugInformationHandle>
  {
    private const uint tokenType = 922746880;
    private const byte tokenTypeSmall = 55;
    private readonly int _rowId;

    private CustomDebugInformationHandle(int rowId) => this._rowId = rowId;

    internal static CustomDebugInformationHandle FromRowId(int rowId) => new CustomDebugInformationHandle(rowId);

    public static implicit operator Handle(CustomDebugInformationHandle handle) => new Handle((byte) 55, handle._rowId);

    public static implicit operator EntityHandle(CustomDebugInformationHandle handle) => new EntityHandle((uint) (922746880UL | (ulong) handle._rowId));

    public static explicit operator CustomDebugInformationHandle(Handle handle)
    {
      if (handle.VType != (byte) 55)
        Throw.InvalidCast();
      return new CustomDebugInformationHandle(handle.RowId);
    }

    public static explicit operator CustomDebugInformationHandle(EntityHandle handle)
    {
      if (handle.VType != 922746880U)
        Throw.InvalidCast();
      return new CustomDebugInformationHandle(handle.RowId);
    }

    public bool IsNil => this.RowId == 0;

    internal int RowId => this._rowId;

    public static bool operator ==(
      CustomDebugInformationHandle left,
      CustomDebugInformationHandle right)
    {
      return left._rowId == right._rowId;
    }

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is CustomDebugInformationHandle informationHandle && informationHandle._rowId == this._rowId;

    public bool Equals(CustomDebugInformationHandle other) => this._rowId == other._rowId;

    public override int GetHashCode() => this._rowId.GetHashCode();

    public static bool operator !=(
      CustomDebugInformationHandle left,
      CustomDebugInformationHandle right)
    {
      return left._rowId != right._rowId;
    }
  }
}
