// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.AssemblyDefinitionHandle
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
using System;

namespace Datadog.System.Reflection.Metadata
{
  public readonly struct AssemblyDefinitionHandle : IEquatable<AssemblyDefinitionHandle>
  {
    private const uint tokenType = 536870912;
    private const byte tokenTypeSmall = 32;
    private readonly int _rowId;

    internal AssemblyDefinitionHandle(int rowId) => this._rowId = rowId;

    internal static AssemblyDefinitionHandle FromRowId(int rowId) => new AssemblyDefinitionHandle(rowId);

    public static implicit operator Handle(AssemblyDefinitionHandle handle) => new Handle((byte) 32, handle._rowId);

    public static implicit operator EntityHandle(AssemblyDefinitionHandle handle) => new EntityHandle((uint) (536870912UL | (ulong) handle._rowId));

    public static explicit operator AssemblyDefinitionHandle(Handle handle)
    {
      if (handle.VType != (byte) 32)
        Throw.InvalidCast();
      return new AssemblyDefinitionHandle(handle.RowId);
    }

    public static explicit operator AssemblyDefinitionHandle(EntityHandle handle)
    {
      if (handle.VType != 536870912U)
        Throw.InvalidCast();
      return new AssemblyDefinitionHandle(handle.RowId);
    }

    public bool IsNil => this.RowId == 0;

    internal int RowId => this._rowId;

    public static bool operator ==(AssemblyDefinitionHandle left, AssemblyDefinitionHandle right) => left._rowId == right._rowId;

    public override bool Equals(object? obj) => obj is AssemblyDefinitionHandle definitionHandle && definitionHandle._rowId == this._rowId;

    public bool Equals(AssemblyDefinitionHandle other) => this._rowId == other._rowId;

    public override int GetHashCode() => this._rowId.GetHashCode();

    public static bool operator !=(AssemblyDefinitionHandle left, AssemblyDefinitionHandle right) => left._rowId != right._rowId;
  }
}
