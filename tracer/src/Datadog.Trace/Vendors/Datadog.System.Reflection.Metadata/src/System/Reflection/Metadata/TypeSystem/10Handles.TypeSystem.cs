// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.MemberReferenceHandle
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
using System;

namespace Datadog.System.Reflection.Metadata
{
  public readonly struct MemberReferenceHandle : IEquatable<MemberReferenceHandle>
  {
    private const uint tokenType = 167772160;
    private const byte tokenTypeSmall = 10;
    private readonly int _rowId;

    private MemberReferenceHandle(int rowId) => this._rowId = rowId;

    internal static MemberReferenceHandle FromRowId(int rowId) => new MemberReferenceHandle(rowId);

    public static implicit operator Handle(MemberReferenceHandle handle) => new Handle((byte) 10, handle._rowId);

    public static implicit operator EntityHandle(MemberReferenceHandle handle) => new EntityHandle((uint) (167772160UL | (ulong) handle._rowId));

    public static explicit operator MemberReferenceHandle(Handle handle)
    {
      if (handle.VType != (byte) 10)
        Throw.InvalidCast();
      return new MemberReferenceHandle(handle.RowId);
    }

    public static explicit operator MemberReferenceHandle(EntityHandle handle)
    {
      if (handle.VType != 167772160U)
        Throw.InvalidCast();
      return new MemberReferenceHandle(handle.RowId);
    }

    public bool IsNil => this.RowId == 0;

    internal int RowId => this._rowId;

    public static bool operator ==(MemberReferenceHandle left, MemberReferenceHandle right) => left._rowId == right._rowId;

    public override bool Equals(object? obj) => obj is MemberReferenceHandle memberReferenceHandle && memberReferenceHandle._rowId == this._rowId;

    public bool Equals(MemberReferenceHandle other) => this._rowId == other._rowId;

    public override int GetHashCode() => this._rowId.GetHashCode();

    public static bool operator !=(MemberReferenceHandle left, MemberReferenceHandle right) => left._rowId != right._rowId;
  }
}
