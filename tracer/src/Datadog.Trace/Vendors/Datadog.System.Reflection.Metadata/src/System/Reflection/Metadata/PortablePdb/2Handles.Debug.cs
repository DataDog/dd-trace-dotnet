// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.LocalScopeHandle
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
using System;

namespace Datadog.System.Reflection.Metadata
{
  public readonly struct LocalScopeHandle : IEquatable<LocalScopeHandle>
  {
    private const uint tokenType = 838860800;
    private const byte tokenTypeSmall = 50;
    private readonly int _rowId;

    private LocalScopeHandle(int rowId) => this._rowId = rowId;

    internal static LocalScopeHandle FromRowId(int rowId) => new LocalScopeHandle(rowId);

    public static implicit operator Handle(LocalScopeHandle handle) => new Handle((byte) 50, handle._rowId);

    public static implicit operator EntityHandle(LocalScopeHandle handle) => new EntityHandle((uint) (838860800UL | (ulong) handle._rowId));

    public static explicit operator LocalScopeHandle(Handle handle)
    {
      if (handle.VType != (byte) 50)
        Throw.InvalidCast();
      return new LocalScopeHandle(handle.RowId);
    }

    public static explicit operator LocalScopeHandle(EntityHandle handle)
    {
      if (handle.VType != 838860800U)
        Throw.InvalidCast();
      return new LocalScopeHandle(handle.RowId);
    }

    public bool IsNil => this.RowId == 0;

    internal int RowId => this._rowId;

    public static bool operator ==(LocalScopeHandle left, LocalScopeHandle right) => left._rowId == right._rowId;

    public override bool Equals(object? obj) => obj is LocalScopeHandle localScopeHandle && localScopeHandle._rowId == this._rowId;

    public bool Equals(LocalScopeHandle other) => this._rowId == other._rowId;

    public override int GetHashCode() => this._rowId.GetHashCode();

    public static bool operator !=(LocalScopeHandle left, LocalScopeHandle right) => left._rowId != right._rowId;
  }
}
