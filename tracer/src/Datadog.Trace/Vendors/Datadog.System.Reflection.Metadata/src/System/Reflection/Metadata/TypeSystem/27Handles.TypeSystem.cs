// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.NamespaceDefinitionHandle
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using Datadog.System.Diagnostics.CodeAnalysis;
using Datadog.System.Reflection.Metadata.Ecma335;


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
  /// <summary>A handle that represents a namespace Datadog.definition.</summary>
  public readonly struct NamespaceDefinitionHandle : IEquatable<NamespaceDefinitionHandle>
  {
    private readonly uint _value;

    private NamespaceDefinitionHandle(uint value) => this._value = value;

    internal static NamespaceDefinitionHandle FromFullNameOffset(int stringHeapOffset) => new NamespaceDefinitionHandle((uint) stringHeapOffset);

    internal static NamespaceDefinitionHandle FromVirtualIndex(uint virtualIndex)
    {
      if (!HeapHandleType.IsValidHeapOffset(virtualIndex))
        Throw.TooManySubnamespaces();
      return new NamespaceDefinitionHandle(2147483648U | virtualIndex);
    }

    public static implicit operator Handle(NamespaceDefinitionHandle handle) => new Handle((byte) ((handle._value & 2147483648U) >> 24 | 124U), (int) handle._value & 536870911);

    public static explicit operator NamespaceDefinitionHandle(Handle handle)
    {
      if (((int) handle.VType & (int) sbyte.MaxValue) != 124)
        Throw.InvalidCast();
      return new NamespaceDefinitionHandle((uint) (((int) handle.VType & 128) << 24 | handle.Offset));
    }

    public bool IsNil => this._value == 0U;

    internal bool IsVirtual => (this._value & 2147483648U) > 0U;

    internal int GetHeapOffset() => (int) this._value & 536870911;

    internal bool HasFullName => !this.IsVirtual;

    internal StringHandle GetFullName() => StringHandle.FromOffset(this.GetHeapOffset());

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is NamespaceDefinitionHandle other && this.Equals(other);

    public bool Equals(NamespaceDefinitionHandle other) => (int) this._value == (int) other._value;

    public override int GetHashCode() => (int) this._value;

    public static bool operator ==(NamespaceDefinitionHandle left, NamespaceDefinitionHandle right) => left.Equals(right);

    public static bool operator !=(NamespaceDefinitionHandle left, NamespaceDefinitionHandle right) => !left.Equals(right);
  }
}
