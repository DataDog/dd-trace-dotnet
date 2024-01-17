﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.MethodDebugInformationHandle
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
using System;

namespace Datadog.System.Reflection.Metadata
{
  public readonly struct MethodDebugInformationHandle : IEquatable<MethodDebugInformationHandle>
  {
    private const uint tokenType = 822083584;
    private const byte tokenTypeSmall = 49;
    private readonly int _rowId;

    private MethodDebugInformationHandle(int rowId) => this._rowId = rowId;

    internal static MethodDebugInformationHandle FromRowId(int rowId) => new MethodDebugInformationHandle(rowId);

    public static implicit operator Handle(MethodDebugInformationHandle handle) => new Handle((byte) 49, handle._rowId);

    public static implicit operator EntityHandle(MethodDebugInformationHandle handle) => new EntityHandle((uint) (822083584UL | (ulong) handle._rowId));

    public static explicit operator MethodDebugInformationHandle(Handle handle)
    {
      if (handle.VType != (byte) 49)
        Throw.InvalidCast();
      return new MethodDebugInformationHandle(handle.RowId);
    }

    public static explicit operator MethodDebugInformationHandle(EntityHandle handle)
    {
      if (handle.VType != 822083584U)
        Throw.InvalidCast();
      return new MethodDebugInformationHandle(handle.RowId);
    }

    public bool IsNil => this.RowId == 0;

    internal int RowId => this._rowId;

    public static bool operator ==(
      MethodDebugInformationHandle left,
      MethodDebugInformationHandle right)
    {
      return left._rowId == right._rowId;
    }

    public override bool Equals(object? obj) => obj is MethodDebugInformationHandle informationHandle && informationHandle._rowId == this._rowId;

    public bool Equals(MethodDebugInformationHandle other) => this._rowId == other._rowId;

    public override int GetHashCode() => this._rowId.GetHashCode();

    public static bool operator !=(
      MethodDebugInformationHandle left,
      MethodDebugInformationHandle right)
    {
      return left._rowId != right._rowId;
    }

    /// <summary>
    /// Returns a handle to <see cref="T:System.Reflection.Metadata.MethodDefinition" /> corresponding to this handle.
    /// </summary>
    /// <remarks>
    /// The resulting handle is only valid within the context of a <see cref="T:System.Reflection.Metadata.MetadataReader" /> open on the type system metadata blob,
    /// which in case of standalone PDB file is a different reader than the one containing this method debug information.
    /// </remarks>
    public MethodDefinitionHandle ToDefinitionHandle() => MethodDefinitionHandle.FromRowId(this._rowId);
  }
}
