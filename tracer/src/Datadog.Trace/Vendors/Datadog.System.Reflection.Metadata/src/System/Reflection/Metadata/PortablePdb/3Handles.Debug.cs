// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.LocalVariableHandle
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
using System;

namespace Datadog.System.Reflection.Metadata
{
  public readonly struct LocalVariableHandle : IEquatable<LocalVariableHandle>
  {
    private const uint tokenType = 855638016;
    private const byte tokenTypeSmall = 51;
    private readonly int _rowId;

    private LocalVariableHandle(int rowId) => this._rowId = rowId;

    internal static LocalVariableHandle FromRowId(int rowId) => new LocalVariableHandle(rowId);

    public static implicit operator Handle(LocalVariableHandle handle) => new Handle((byte) 51, handle._rowId);

    public static implicit operator EntityHandle(LocalVariableHandle handle) => new EntityHandle((uint) (855638016UL | (ulong) handle._rowId));

    public static explicit operator LocalVariableHandle(Handle handle)
    {
      if (handle.VType != (byte) 51)
        Throw.InvalidCast();
      return new LocalVariableHandle(handle.RowId);
    }

    public static explicit operator LocalVariableHandle(EntityHandle handle)
    {
      if (handle.VType != 855638016U)
        Throw.InvalidCast();
      return new LocalVariableHandle(handle.RowId);
    }

    public bool IsNil => this.RowId == 0;

    internal int RowId => this._rowId;

    public static bool operator ==(LocalVariableHandle left, LocalVariableHandle right) => left._rowId == right._rowId;

    public override bool Equals(object? obj) => obj is LocalVariableHandle localVariableHandle && localVariableHandle._rowId == this._rowId;

    public bool Equals(LocalVariableHandle other) => this._rowId == other._rowId;

    public override int GetHashCode() => this._rowId.GetHashCode();

    public static bool operator !=(LocalVariableHandle left, LocalVariableHandle right) => left._rowId != right._rowId;
  }
}
