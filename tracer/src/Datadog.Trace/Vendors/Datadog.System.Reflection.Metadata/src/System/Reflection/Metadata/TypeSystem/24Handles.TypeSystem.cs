// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.AssemblyFileHandle
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
using System;

namespace Datadog.System.Reflection.Metadata
{
  public readonly struct AssemblyFileHandle : IEquatable<AssemblyFileHandle>
  {
    private const uint tokenType = 637534208;
    private const byte tokenTypeSmall = 38;
    private readonly int _rowId;

    private AssemblyFileHandle(int rowId) => this._rowId = rowId;

    internal static AssemblyFileHandle FromRowId(int rowId) => new AssemblyFileHandle(rowId);

    public static implicit operator Handle(AssemblyFileHandle handle) => new Handle((byte) 38, handle._rowId);

    public static implicit operator EntityHandle(AssemblyFileHandle handle) => new EntityHandle((uint) (637534208UL | (ulong) handle._rowId));

    public static explicit operator AssemblyFileHandle(Handle handle)
    {
      if (handle.VType != (byte) 38)
        Throw.InvalidCast();
      return new AssemblyFileHandle(handle.RowId);
    }

    public static explicit operator AssemblyFileHandle(EntityHandle handle)
    {
      if (handle.VType != 637534208U)
        Throw.InvalidCast();
      return new AssemblyFileHandle(handle.RowId);
    }

    public bool IsNil => this.RowId == 0;

    internal int RowId => this._rowId;

    public static bool operator ==(AssemblyFileHandle left, AssemblyFileHandle right) => left._rowId == right._rowId;

    public override bool Equals(object? obj) => obj is AssemblyFileHandle assemblyFileHandle && assemblyFileHandle._rowId == this._rowId;

    public bool Equals(AssemblyFileHandle other) => this._rowId == other._rowId;

    public override int GetHashCode() => this._rowId.GetHashCode();

    public static bool operator !=(AssemblyFileHandle left, AssemblyFileHandle right) => left._rowId != right._rowId;
  }
}
