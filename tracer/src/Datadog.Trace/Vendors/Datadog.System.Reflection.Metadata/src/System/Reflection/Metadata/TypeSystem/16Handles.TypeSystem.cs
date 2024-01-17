﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.GenericParameterHandle
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
using System;

namespace Datadog.System.Reflection.Metadata
{
  public readonly struct GenericParameterHandle : IEquatable<GenericParameterHandle>
  {
    private const uint tokenType = 704643072;
    private const byte tokenTypeSmall = 42;
    private readonly int _rowId;

    private GenericParameterHandle(int rowId) => this._rowId = rowId;

    internal static GenericParameterHandle FromRowId(int rowId) => new GenericParameterHandle(rowId);

    public static implicit operator Handle(GenericParameterHandle handle) => new Handle((byte) 42, handle._rowId);

    public static implicit operator EntityHandle(GenericParameterHandle handle) => new EntityHandle((uint) (704643072UL | (ulong) handle._rowId));

    public static explicit operator GenericParameterHandle(Handle handle)
    {
      if (handle.VType != (byte) 42)
        Throw.InvalidCast();
      return new GenericParameterHandle(handle.RowId);
    }

    public static explicit operator GenericParameterHandle(EntityHandle handle)
    {
      if (handle.VType != 704643072U)
        Throw.InvalidCast();
      return new GenericParameterHandle(handle.RowId);
    }

    public bool IsNil => this.RowId == 0;

    internal int RowId => this._rowId;

    public static bool operator ==(GenericParameterHandle left, GenericParameterHandle right) => left._rowId == right._rowId;

    public override bool Equals(object? obj) => obj is GenericParameterHandle genericParameterHandle && genericParameterHandle._rowId == this._rowId;

    public bool Equals(GenericParameterHandle other) => this._rowId == other._rowId;

    public override int GetHashCode() => this._rowId.GetHashCode();

    public static bool operator !=(GenericParameterHandle left, GenericParameterHandle right) => left._rowId != right._rowId;
  }
}
