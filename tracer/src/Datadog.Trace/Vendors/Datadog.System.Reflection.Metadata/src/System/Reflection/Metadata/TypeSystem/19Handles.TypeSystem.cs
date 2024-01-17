﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.AssemblyReferenceHandle
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
using System;
using Datadog.System.Reflection.Metadata.Ecma335;

namespace Datadog.System.Reflection.Metadata
{
  public readonly struct AssemblyReferenceHandle : IEquatable<AssemblyReferenceHandle>
  {
    private const uint tokenType = 587202560;
    private const byte tokenTypeSmall = 35;
    private readonly uint _value;

    private AssemblyReferenceHandle(uint value) => this._value = value;

    internal static AssemblyReferenceHandle FromRowId(int rowId) => new AssemblyReferenceHandle((uint) rowId);

    internal static AssemblyReferenceHandle FromVirtualIndex(
      AssemblyReferenceHandle.VirtualIndex virtualIndex)
    {
      return new AssemblyReferenceHandle((uint) TokenTypeIds.VirtualBit | (uint)virtualIndex);
    }

    public static implicit operator Handle(AssemblyReferenceHandle handle) => Handle.FromVToken(handle.VToken);

    public static implicit operator EntityHandle(AssemblyReferenceHandle handle) => new EntityHandle(handle.VToken);

    public static explicit operator AssemblyReferenceHandle(Handle handle)
    {
      if (handle.Type != 35U)
        Throw.InvalidCast();
      return new AssemblyReferenceHandle(handle.SpecificEntityHandleValue);
    }

    public static explicit operator AssemblyReferenceHandle(EntityHandle handle)
    {
      if (handle.Type != 587202560U)
        Throw.InvalidCast();
      return new AssemblyReferenceHandle(handle.SpecificHandleValue);
    }

    internal uint Value => this._value;

    private uint VToken => this._value | 587202560U;

    public bool IsNil => this._value == 0U;

    internal bool IsVirtual => (this._value & 2147483648U) > 0U;

    internal int RowId => (int) this._value & 16777215;

    public static bool operator ==(AssemblyReferenceHandle left, AssemblyReferenceHandle right) => (int) left._value == (int) right._value;

    public override bool Equals(object? obj) => obj is AssemblyReferenceHandle assemblyReferenceHandle && (int) assemblyReferenceHandle._value == (int) this._value;

    public bool Equals(AssemblyReferenceHandle other) => (int) this._value == (int) other._value;

    public override int GetHashCode() => this._value.GetHashCode();

    public static bool operator !=(AssemblyReferenceHandle left, AssemblyReferenceHandle right) => (int) left._value != (int) right._value;

    internal enum VirtualIndex
    {
      System_Runtime,
      System_Runtime_InteropServices_WindowsRuntime,
      System_ObjectModel,
      System_Runtime_WindowsRuntime,
      System_Runtime_WindowsRuntime_UI_Xaml,
      System_Numerics_Vectors,
      Count,
    }
  }
}
