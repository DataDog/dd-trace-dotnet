//------------------------------------------------------------------------------
// <auto-generated />
// This file was automatically generated by the UpdateVendors tool.
//------------------------------------------------------------------------------
#pragma warning disable CS0618, CS0649, CS1574, CS1580, CS1581, CS1584, CS1591, CS1573, CS8018, SYSLIB0011, SYSLIB0032
#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618, CS8620, CS8714, CS8762, CS8765, CS8766, CS8767, CS8768, CS8769, CS8612, CS8629, CS8774
// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.ImportScopeHandle
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72

using System;
using System.Diagnostics.CodeAnalysis;


#nullable enable
namespace Datadog.Trace.VendoredMicrosoftCode.System.Reflection.Metadata
{
  internal readonly struct ImportScopeHandle : IEquatable<ImportScopeHandle>
  {
    private const uint tokenType = 889192448;
    private const byte tokenTypeSmall = 53;
    private readonly int _rowId;

    private ImportScopeHandle(int rowId) => this._rowId = rowId;

    internal static ImportScopeHandle FromRowId(int rowId) => new ImportScopeHandle(rowId);

    public static implicit operator Handle(ImportScopeHandle handle) => new Handle((byte) 53, handle._rowId);

    public static implicit operator EntityHandle(ImportScopeHandle handle) => new EntityHandle((uint) (889192448UL | (ulong) handle._rowId));

    public static explicit operator ImportScopeHandle(Handle handle)
    {
      if (handle.VType != (byte) 53)
        Throw.InvalidCast();
      return new ImportScopeHandle(handle.RowId);
    }

    public static explicit operator ImportScopeHandle(EntityHandle handle)
    {
      if (handle.VType != 889192448U)
        Throw.InvalidCast();
      return new ImportScopeHandle(handle.RowId);
    }

    public bool IsNil => this.RowId == 0;

    internal int RowId => this._rowId;

    public static bool operator ==(ImportScopeHandle left, ImportScopeHandle right) => left._rowId == right._rowId;

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is ImportScopeHandle importScopeHandle && importScopeHandle._rowId == this._rowId;

    public bool Equals(ImportScopeHandle other) => this._rowId == other._rowId;

    public override int GetHashCode() => this._rowId.GetHashCode();

    public static bool operator !=(ImportScopeHandle left, ImportScopeHandle right) => left._rowId != right._rowId;
  }
}
