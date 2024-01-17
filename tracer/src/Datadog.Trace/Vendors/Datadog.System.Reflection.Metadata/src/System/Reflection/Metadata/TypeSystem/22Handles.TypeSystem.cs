﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.ConstantHandle
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
using System;

namespace Datadog.System.Reflection.Metadata
{
  public readonly struct ConstantHandle : IEquatable<ConstantHandle>
  {
    private const uint tokenType = 184549376;
    private const byte tokenTypeSmall = 11;
    private readonly int _rowId;

    private ConstantHandle(int rowId) => this._rowId = rowId;

    internal static ConstantHandle FromRowId(int rowId) => new ConstantHandle(rowId);

    public static implicit operator Handle(ConstantHandle handle) => new Handle((byte) 11, handle._rowId);

    public static implicit operator EntityHandle(ConstantHandle handle) => new EntityHandle((uint) (184549376UL | (ulong) handle._rowId));

    public static explicit operator ConstantHandle(Handle handle)
    {
      if (handle.VType != (byte) 11)
        Throw.InvalidCast();
      return new ConstantHandle(handle.RowId);
    }

    public static explicit operator ConstantHandle(EntityHandle handle)
    {
      if (handle.VType != 184549376U)
        Throw.InvalidCast();
      return new ConstantHandle(handle.RowId);
    }

    public bool IsNil => this.RowId == 0;

    internal int RowId => this._rowId;

    public static bool operator ==(ConstantHandle left, ConstantHandle right) => left._rowId == right._rowId;

    public override bool Equals(object? obj) => obj is ConstantHandle constantHandle && constantHandle._rowId == this._rowId;

    public bool Equals(ConstantHandle other) => this._rowId == other._rowId;

    public override int GetHashCode() => this._rowId.GetHashCode();

    public static bool operator !=(ConstantHandle left, ConstantHandle right) => left._rowId != right._rowId;
  }
}
