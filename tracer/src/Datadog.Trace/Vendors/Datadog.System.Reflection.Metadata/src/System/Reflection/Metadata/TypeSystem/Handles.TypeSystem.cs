// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.ModuleDefinitionHandle
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
using System;

namespace Datadog.System.Reflection.Metadata
{
  public readonly struct ModuleDefinitionHandle : IEquatable<ModuleDefinitionHandle>
  {
    private const uint tokenType = 0;
    private const byte tokenTypeSmall = 0;
    private readonly int _rowId;

    internal ModuleDefinitionHandle(int rowId) => this._rowId = rowId;

    internal static ModuleDefinitionHandle FromRowId(int rowId) => new ModuleDefinitionHandle(rowId);

    public static implicit operator Handle(ModuleDefinitionHandle handle) => new Handle((byte) 0, handle._rowId);

    public static implicit operator EntityHandle(ModuleDefinitionHandle handle) => new EntityHandle((uint) (0UL | (ulong) handle._rowId));

    public static explicit operator ModuleDefinitionHandle(Handle handle)
    {
      if (handle.VType != (byte) 0)
        Throw.InvalidCast();
      return new ModuleDefinitionHandle(handle.RowId);
    }

    public static explicit operator ModuleDefinitionHandle(EntityHandle handle)
    {
      if (handle.VType != 0U)
        Throw.InvalidCast();
      return new ModuleDefinitionHandle(handle.RowId);
    }

    public bool IsNil => this.RowId == 0;

    internal int RowId => this._rowId;

    public static bool operator ==(ModuleDefinitionHandle left, ModuleDefinitionHandle right) => left._rowId == right._rowId;

    public override bool Equals(object? obj) => obj is ModuleDefinitionHandle definitionHandle && definitionHandle._rowId == this._rowId;

    public bool Equals(ModuleDefinitionHandle other) => this._rowId == other._rowId;

    public override int GetHashCode() => this._rowId.GetHashCode();

    public static bool operator !=(ModuleDefinitionHandle left, ModuleDefinitionHandle right) => left._rowId != right._rowId;
  }
}
