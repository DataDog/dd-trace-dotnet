﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.PropertyDefinitionHandle
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
using System;

namespace Datadog.System.Reflection.Metadata
{
  public readonly struct PropertyDefinitionHandle : IEquatable<PropertyDefinitionHandle>
  {
    private const uint tokenType = 385875968;
    private const byte tokenTypeSmall = 23;
    private readonly int _rowId;

    private PropertyDefinitionHandle(int rowId) => this._rowId = rowId;

    internal static PropertyDefinitionHandle FromRowId(int rowId) => new PropertyDefinitionHandle(rowId);

    public static implicit operator Handle(PropertyDefinitionHandle handle) => new Handle((byte) 23, handle._rowId);

    public static implicit operator EntityHandle(PropertyDefinitionHandle handle) => new EntityHandle((uint) (385875968UL | (ulong) handle._rowId));

    public static explicit operator PropertyDefinitionHandle(Handle handle)
    {
      if (handle.VType != (byte) 23)
        Throw.InvalidCast();
      return new PropertyDefinitionHandle(handle.RowId);
    }

    public static explicit operator PropertyDefinitionHandle(EntityHandle handle)
    {
      if (handle.VType != 385875968U)
        Throw.InvalidCast();
      return new PropertyDefinitionHandle(handle.RowId);
    }

    public bool IsNil => this.RowId == 0;

    internal int RowId => this._rowId;

    public static bool operator ==(PropertyDefinitionHandle left, PropertyDefinitionHandle right) => left._rowId == right._rowId;

    public override bool Equals(object? obj) => obj is PropertyDefinitionHandle definitionHandle && definitionHandle._rowId == this._rowId;

    public bool Equals(PropertyDefinitionHandle other) => this._rowId == other._rowId;

    public override int GetHashCode() => this._rowId.GetHashCode();

    public static bool operator !=(PropertyDefinitionHandle left, PropertyDefinitionHandle right) => left._rowId != right._rowId;
  }
}
