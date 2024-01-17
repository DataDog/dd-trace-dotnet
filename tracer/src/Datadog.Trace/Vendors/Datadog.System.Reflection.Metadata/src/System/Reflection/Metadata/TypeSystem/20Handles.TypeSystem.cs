﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.CustomAttributeHandle
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
using System;

namespace Datadog.System.Reflection.Metadata
{
  public readonly struct CustomAttributeHandle : IEquatable<CustomAttributeHandle>
  {
    private const uint tokenType = 201326592;
    private const byte tokenTypeSmall = 12;
    private readonly int _rowId;

    private CustomAttributeHandle(int rowId) => this._rowId = rowId;

    internal static CustomAttributeHandle FromRowId(int rowId) => new CustomAttributeHandle(rowId);

    public static implicit operator Handle(CustomAttributeHandle handle) => new Handle((byte) 12, handle._rowId);

    public static implicit operator EntityHandle(CustomAttributeHandle handle) => new EntityHandle((uint) (201326592UL | (ulong) handle._rowId));

    public static explicit operator CustomAttributeHandle(Handle handle)
    {
      if (handle.VType != (byte) 12)
        Throw.InvalidCast();
      return new CustomAttributeHandle(handle.RowId);
    }

    public static explicit operator CustomAttributeHandle(EntityHandle handle)
    {
      if (handle.VType != 201326592U)
        Throw.InvalidCast();
      return new CustomAttributeHandle(handle.RowId);
    }

    public bool IsNil => this._rowId == 0;

    internal int RowId => this._rowId;

    public static bool operator ==(CustomAttributeHandle left, CustomAttributeHandle right) => left._rowId == right._rowId;

    public override bool Equals(object? obj) => obj is CustomAttributeHandle customAttributeHandle && customAttributeHandle._rowId == this._rowId;

    public bool Equals(CustomAttributeHandle other) => this._rowId == other._rowId;

    public override int GetHashCode() => this._rowId.GetHashCode();

    public static bool operator !=(CustomAttributeHandle left, CustomAttributeHandle right) => left._rowId != right._rowId;
  }
}
