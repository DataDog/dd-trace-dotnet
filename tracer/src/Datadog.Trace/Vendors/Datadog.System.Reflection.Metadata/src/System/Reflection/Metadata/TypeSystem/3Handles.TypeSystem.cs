﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.MethodDefinitionHandle
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
using System;

namespace Datadog.System.Reflection.Metadata
{
  public readonly struct MethodDefinitionHandle : IEquatable<MethodDefinitionHandle>
  {
    private const uint tokenType = 100663296;
    private const byte tokenTypeSmall = 6;
    private readonly int _rowId;

    private MethodDefinitionHandle(int rowId) => this._rowId = rowId;

    internal static MethodDefinitionHandle FromRowId(int rowId) => new MethodDefinitionHandle(rowId);

    public static implicit operator Handle(MethodDefinitionHandle handle) => new Handle((byte) 6, handle._rowId);

    public static implicit operator EntityHandle(MethodDefinitionHandle handle) => new EntityHandle((uint) (100663296UL | (ulong) handle._rowId));

    public static explicit operator MethodDefinitionHandle(Handle handle)
    {
      if (handle.VType != (byte) 6)
        Throw.InvalidCast();
      return new MethodDefinitionHandle(handle.RowId);
    }

    public static explicit operator MethodDefinitionHandle(EntityHandle handle)
    {
      if (handle.VType != 100663296U)
        Throw.InvalidCast();
      return new MethodDefinitionHandle(handle.RowId);
    }

    public bool IsNil => this.RowId == 0;

    internal int RowId => this._rowId;

    public static bool operator ==(MethodDefinitionHandle left, MethodDefinitionHandle right) => left._rowId == right._rowId;

    public override bool Equals(object? obj) => obj is MethodDefinitionHandle definitionHandle && definitionHandle._rowId == this._rowId;

    public bool Equals(MethodDefinitionHandle other) => this._rowId == other._rowId;

    public override int GetHashCode() => this._rowId.GetHashCode();

    public static bool operator !=(MethodDefinitionHandle left, MethodDefinitionHandle right) => left._rowId != right._rowId;

    /// <summary>
    /// Returns a handle to <see cref="T:System.Reflection.Metadata.MethodDebugInformation" /> corresponding to this handle.
    /// </summary>
    /// <remarks>
    /// The resulting handle is only valid within the context of a <see cref="T:System.Reflection.Metadata.MetadataReader" /> open on the Portable PDB blob,
    /// which in case of standalone PDB file is a different reader than the one containing this method definition.
    /// </remarks>
    public MethodDebugInformationHandle ToDebugInformationHandle() => MethodDebugInformationHandle.FromRowId(this._rowId);
  }
}
