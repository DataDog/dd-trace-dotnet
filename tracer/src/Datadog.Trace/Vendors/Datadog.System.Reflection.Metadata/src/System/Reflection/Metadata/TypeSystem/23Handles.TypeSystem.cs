// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.ManifestResourceHandle
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
using System;

namespace Datadog.System.Reflection.Metadata
{
  public readonly struct ManifestResourceHandle : IEquatable<ManifestResourceHandle>
  {
    private const uint tokenType = 671088640;
    private const byte tokenTypeSmall = 40;
    private readonly int _rowId;

    private ManifestResourceHandle(int rowId) => this._rowId = rowId;

    internal static ManifestResourceHandle FromRowId(int rowId) => new ManifestResourceHandle(rowId);

    public static implicit operator Handle(ManifestResourceHandle handle) => new Handle((byte) 40, handle._rowId);

    public static implicit operator EntityHandle(ManifestResourceHandle handle) => new EntityHandle((uint) (671088640UL | (ulong) handle._rowId));

    public static explicit operator ManifestResourceHandle(Handle handle)
    {
      if (handle.VType != (byte) 40)
        Throw.InvalidCast();
      return new ManifestResourceHandle(handle.RowId);
    }

    public static explicit operator ManifestResourceHandle(EntityHandle handle)
    {
      if (handle.VType != 671088640U)
        Throw.InvalidCast();
      return new ManifestResourceHandle(handle.RowId);
    }

    public bool IsNil => this.RowId == 0;

    internal int RowId => this._rowId;

    public static bool operator ==(ManifestResourceHandle left, ManifestResourceHandle right) => left._rowId == right._rowId;

    public override bool Equals(object? obj) => obj is ManifestResourceHandle manifestResourceHandle && manifestResourceHandle._rowId == this._rowId;

    public bool Equals(ManifestResourceHandle other) => this._rowId == other._rowId;

    public override int GetHashCode() => this._rowId.GetHashCode();

    public static bool operator !=(ManifestResourceHandle left, ManifestResourceHandle right) => left._rowId != right._rowId;
  }
}
