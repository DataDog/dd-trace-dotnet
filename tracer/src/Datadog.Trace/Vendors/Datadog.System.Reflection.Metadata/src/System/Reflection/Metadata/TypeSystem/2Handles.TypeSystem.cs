﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.InterfaceImplementationHandle
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
using System;

namespace Datadog.System.Reflection.Metadata
{
  public readonly struct InterfaceImplementationHandle : IEquatable<InterfaceImplementationHandle>
  {
    private const uint tokenType = 150994944;
    private const byte tokenTypeSmall = 9;
    private readonly int _rowId;

    internal InterfaceImplementationHandle(int rowId) => this._rowId = rowId;

    internal static InterfaceImplementationHandle FromRowId(int rowId) => new InterfaceImplementationHandle(rowId);

    public static implicit operator Handle(InterfaceImplementationHandle handle) => new Handle((byte) 9, handle._rowId);

    public static implicit operator EntityHandle(InterfaceImplementationHandle handle) => new EntityHandle((uint) (150994944UL | (ulong) handle._rowId));

    public static explicit operator InterfaceImplementationHandle(Handle handle)
    {
      if (handle.VType != (byte) 9)
        Throw.InvalidCast();
      return new InterfaceImplementationHandle(handle.RowId);
    }

    public static explicit operator InterfaceImplementationHandle(EntityHandle handle)
    {
      if (handle.VType != 150994944U)
        Throw.InvalidCast();
      return new InterfaceImplementationHandle(handle.RowId);
    }

    public bool IsNil => this.RowId == 0;

    internal int RowId => this._rowId;

    public static bool operator ==(
      InterfaceImplementationHandle left,
      InterfaceImplementationHandle right)
    {
      return left._rowId == right._rowId;
    }

    public override bool Equals(object? obj) => obj is InterfaceImplementationHandle implementationHandle && implementationHandle._rowId == this._rowId;

    public bool Equals(InterfaceImplementationHandle other) => this._rowId == other._rowId;

    public override int GetHashCode() => this._rowId.GetHashCode();

    public static bool operator !=(
      InterfaceImplementationHandle left,
      InterfaceImplementationHandle right)
    {
      return left._rowId != right._rowId;
    }
  }
}
