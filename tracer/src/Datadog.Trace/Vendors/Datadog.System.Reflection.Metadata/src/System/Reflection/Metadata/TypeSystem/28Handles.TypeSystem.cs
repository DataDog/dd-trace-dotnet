// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.BlobHandle
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using Datadog.System.Diagnostics.CodeAnalysis;


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
  public readonly struct BlobHandle : IEquatable<BlobHandle>
  {
    private readonly uint _value;
    internal const int TemplateParameterOffset_AttributeUsageTarget = 2;

    private BlobHandle(uint value) => this._value = value;

    internal static BlobHandle FromOffset(int heapOffset) => new BlobHandle((uint) heapOffset);

    internal static BlobHandle FromVirtualIndex(
      BlobHandle.VirtualIndex virtualIndex,
      ushort virtualValue)
    {
      return new BlobHandle((uint) ((BlobHandle.VirtualIndex) (int.MinValue | (int) virtualValue << 8) | virtualIndex));
    }

    internal unsafe void SubstituteTemplateParameters(byte[] blob)
    {
      fixed (byte* numPtr = &blob[2])
        *(int*) numPtr = (int) this.VirtualValue;
    }

    public static implicit operator Handle(BlobHandle handle) => new Handle((byte) ((handle._value & 2147483648U) >> 24 | 113U), (int) handle._value & 536870911);

    public static explicit operator BlobHandle(Handle handle)
    {
      if (((int) handle.VType & (int) sbyte.MaxValue) != 113)
        Throw.InvalidCast();
      return new BlobHandle((uint) (((int) handle.VType & 128) << 24 | handle.Offset));
    }

    internal uint RawValue => this._value;

    public bool IsNil => this._value == 0U;

    internal int GetHeapOffset() => (int) this._value;

    internal BlobHandle.VirtualIndex GetVirtualIndex() => (BlobHandle.VirtualIndex) (this._value & (uint) byte.MaxValue);

    internal bool IsVirtual => (this._value & 2147483648U) > 0U;

    private ushort VirtualValue => (ushort) (this._value >> 8);

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is BlobHandle other && this.Equals(other);

    public bool Equals(BlobHandle other) => (int) this._value == (int) other._value;

    public override int GetHashCode() => (int) this._value;

    public static bool operator ==(BlobHandle left, BlobHandle right) => left.Equals(right);

    public static bool operator !=(BlobHandle left, BlobHandle right) => !left.Equals(right);

    internal enum VirtualIndex : byte
    {
      Nil,
      ContractPublicKeyToken,
      ContractPublicKey,
      AttributeUsage_AllowSingle,
      AttributeUsage_AllowMultiple,
      Count,
    }
  }
}
