﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.StandaloneSignatureHandle
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
using System;

namespace Datadog.System.Reflection.Metadata
{
  public readonly struct StandaloneSignatureHandle : IEquatable<StandaloneSignatureHandle>
  {
    private const uint tokenType = 285212672;
    private const byte tokenTypeSmall = 17;
    private readonly int _rowId;

    private StandaloneSignatureHandle(int rowId) => this._rowId = rowId;

    internal static StandaloneSignatureHandle FromRowId(int rowId) => new StandaloneSignatureHandle(rowId);

    public static implicit operator Handle(StandaloneSignatureHandle handle) => new Handle((byte) 17, handle._rowId);

    public static implicit operator EntityHandle(StandaloneSignatureHandle handle) => new EntityHandle((uint) (285212672UL | (ulong) handle._rowId));

    public static explicit operator StandaloneSignatureHandle(Handle handle)
    {
      if (handle.VType != (byte) 17)
        Throw.InvalidCast();
      return new StandaloneSignatureHandle(handle.RowId);
    }

    public static explicit operator StandaloneSignatureHandle(EntityHandle handle)
    {
      if (handle.VType != 285212672U)
        Throw.InvalidCast();
      return new StandaloneSignatureHandle(handle.RowId);
    }

    public bool IsNil => this.RowId == 0;

    internal int RowId => this._rowId;

    public static bool operator ==(StandaloneSignatureHandle left, StandaloneSignatureHandle right) => left._rowId == right._rowId;

    public override bool Equals(object? obj) => obj is StandaloneSignatureHandle standaloneSignatureHandle && standaloneSignatureHandle._rowId == this._rowId;

    public bool Equals(StandaloneSignatureHandle other) => this._rowId == other._rowId;

    public override int GetHashCode() => this._rowId.GetHashCode();

    public static bool operator !=(StandaloneSignatureHandle left, StandaloneSignatureHandle right) => left._rowId != right._rowId;
  }
}
