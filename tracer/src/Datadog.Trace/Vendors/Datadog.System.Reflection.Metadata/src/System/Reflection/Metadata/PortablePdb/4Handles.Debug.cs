// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.LocalConstantHandle
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using Datadog.System.Diagnostics.CodeAnalysis;


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
  public readonly struct LocalConstantHandle : IEquatable<LocalConstantHandle>
  {
    private const uint tokenType = 872415232;
    private const byte tokenTypeSmall = 52;
    private readonly int _rowId;

    private LocalConstantHandle(int rowId) => this._rowId = rowId;

    internal static LocalConstantHandle FromRowId(int rowId) => new LocalConstantHandle(rowId);

    public static implicit operator Handle(LocalConstantHandle handle) => new Handle((byte) 52, handle._rowId);

    public static implicit operator EntityHandle(LocalConstantHandle handle) => new EntityHandle((uint) (872415232UL | (ulong) handle._rowId));

    public static explicit operator LocalConstantHandle(Handle handle)
    {
      if (handle.VType != (byte) 52)
        Throw.InvalidCast();
      return new LocalConstantHandle(handle.RowId);
    }

    public static explicit operator LocalConstantHandle(EntityHandle handle)
    {
      if (handle.VType != 872415232U)
        Throw.InvalidCast();
      return new LocalConstantHandle(handle.RowId);
    }

    public bool IsNil => this.RowId == 0;

    internal int RowId => this._rowId;

    public static bool operator ==(LocalConstantHandle left, LocalConstantHandle right) => left._rowId == right._rowId;

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is LocalConstantHandle localConstantHandle && localConstantHandle._rowId == this._rowId;

    public bool Equals(LocalConstantHandle other) => this._rowId == other._rowId;

    public override int GetHashCode() => this._rowId.GetHashCode();

    public static bool operator !=(LocalConstantHandle left, LocalConstantHandle right) => left._rowId != right._rowId;
  }
}
