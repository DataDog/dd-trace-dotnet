﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.UserStringHandle
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
using System;

namespace Datadog.System.Reflection.Metadata
{
  /// <summary>#UserString heap handle.</summary>
  /// <remarks>The handle is 32-bit wide.</remarks>
  public readonly struct UserStringHandle : IEquatable<UserStringHandle>
  {
    private readonly int _offset;

    private UserStringHandle(int offset) => this._offset = offset;

    internal static UserStringHandle FromOffset(int heapOffset) => new UserStringHandle(heapOffset);

    public static implicit operator Handle(UserStringHandle handle) => new Handle((byte) 112, handle._offset);

    public static explicit operator UserStringHandle(Handle handle)
    {
      if (handle.VType != (byte) 112)
        Throw.InvalidCast();
      return new UserStringHandle(handle.Offset);
    }

    public bool IsNil => this._offset == 0;

    internal int GetHeapOffset() => this._offset;

    public static bool operator ==(UserStringHandle left, UserStringHandle right) => left._offset == right._offset;

    public override bool Equals(object? obj) => obj is UserStringHandle userStringHandle && userStringHandle._offset == this._offset;

    public bool Equals(UserStringHandle other) => this._offset == other._offset;

    public override int GetHashCode() => this._offset.GetHashCode();

    public static bool operator !=(UserStringHandle left, UserStringHandle right) => left._offset != right._offset;
  }
}
