// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.GuidHandle
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using Datadog.System.Diagnostics.CodeAnalysis;


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
  public readonly struct GuidHandle : IEquatable<GuidHandle>
  {
    private readonly int _index;

    private GuidHandle(int index) => this._index = index;

    internal static GuidHandle FromIndex(int heapIndex) => new GuidHandle(heapIndex);

    public static implicit operator Handle(GuidHandle handle) => new Handle((byte) 114, handle._index);

    public static explicit operator GuidHandle(Handle handle)
    {
      if (handle.VType != (byte) 114)
        Throw.InvalidCast();
      return new GuidHandle(handle.Offset);
    }

    public bool IsNil => this._index == 0;

    internal int Index => this._index;

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is GuidHandle other && this.Equals(other);

    public bool Equals(GuidHandle other) => this._index == other._index;

    public override int GetHashCode() => this._index;

    public static bool operator ==(GuidHandle left, GuidHandle right) => left.Equals(right);

    public static bool operator !=(GuidHandle left, GuidHandle right) => !left.Equals(right);
  }
}
