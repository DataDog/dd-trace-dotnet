﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.EventDefinitionHandle
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
using System;

namespace Datadog.System.Reflection.Metadata
{
  public readonly struct EventDefinitionHandle : IEquatable<EventDefinitionHandle>
  {
    private const uint tokenType = 335544320;
    private const byte tokenTypeSmall = 20;
    private readonly int _rowId;

    private EventDefinitionHandle(int rowId) => this._rowId = rowId;

    internal static EventDefinitionHandle FromRowId(int rowId) => new EventDefinitionHandle(rowId);

    public static implicit operator Handle(EventDefinitionHandle handle) => new Handle((byte) 20, handle._rowId);

    public static implicit operator EntityHandle(EventDefinitionHandle handle) => new EntityHandle((uint) (335544320UL | (ulong) handle._rowId));

    public static explicit operator EventDefinitionHandle(Handle handle)
    {
      if (handle.VType != (byte) 20)
        Throw.InvalidCast();
      return new EventDefinitionHandle(handle.RowId);
    }

    public static explicit operator EventDefinitionHandle(EntityHandle handle)
    {
      if (handle.VType != 335544320U)
        Throw.InvalidCast();
      return new EventDefinitionHandle(handle.RowId);
    }

    public bool IsNil => this.RowId == 0;

    internal int RowId => this._rowId;

    public static bool operator ==(EventDefinitionHandle left, EventDefinitionHandle right) => left._rowId == right._rowId;

    public override bool Equals(object? obj) => obj is EventDefinitionHandle definitionHandle && definitionHandle._rowId == this._rowId;

    public bool Equals(EventDefinitionHandle other) => this._rowId == other._rowId;

    public override int GetHashCode() => this._rowId.GetHashCode();

    public static bool operator !=(EventDefinitionHandle left, EventDefinitionHandle right) => left._rowId != right._rowId;
  }
}
