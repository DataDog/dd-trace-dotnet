// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.TypeDefinitionHandle
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
using System;

namespace Datadog.System.Reflection.Metadata
{
  public readonly struct TypeDefinitionHandle : IEquatable<TypeDefinitionHandle>
  {
    private const uint tokenType = 33554432;
    private const byte tokenTypeSmall = 2;
    private readonly int _rowId;

    private TypeDefinitionHandle(int rowId) => this._rowId = rowId;

    internal static TypeDefinitionHandle FromRowId(int rowId) => new TypeDefinitionHandle(rowId);

    public static implicit operator Handle(TypeDefinitionHandle handle) => new Handle((byte) 2, handle._rowId);

    public static implicit operator EntityHandle(TypeDefinitionHandle handle) => new EntityHandle((uint) (33554432UL | (ulong) handle._rowId));

    public static explicit operator TypeDefinitionHandle(Handle handle)
    {
      if (handle.VType != (byte) 2)
        Throw.InvalidCast();
      return new TypeDefinitionHandle(handle.RowId);
    }

    public static explicit operator TypeDefinitionHandle(EntityHandle handle)
    {
      if (handle.VType != 33554432U)
        Throw.InvalidCast();
      return new TypeDefinitionHandle(handle.RowId);
    }

    public bool IsNil => this.RowId == 0;

    internal int RowId => this._rowId;

    public static bool operator ==(TypeDefinitionHandle left, TypeDefinitionHandle right) => left._rowId == right._rowId;

    public override bool Equals(object? obj) => obj is TypeDefinitionHandle definitionHandle && definitionHandle._rowId == this._rowId;

    public bool Equals(TypeDefinitionHandle other) => this._rowId == other._rowId;

    public override int GetHashCode() => this._rowId.GetHashCode();

    public static bool operator !=(TypeDefinitionHandle left, TypeDefinitionHandle right) => left._rowId != right._rowId;
  }
}
