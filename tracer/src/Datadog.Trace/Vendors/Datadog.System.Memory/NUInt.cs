// Decompiled with JetBrains decompiler
// Type: System.NUInt
// Assembly: System.Memory, Version=4.0.1.2, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51
// MVID: 805945F3-27B0-47AD-B8F6-389D9D8F82C3
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Memory.4.5.5\lib\net461\System.Memory.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Memory.4.5.5\lib\net461\System.Memory.xml

using System;
using System.Runtime.CompilerServices;

namespace Datadog.System
{
    internal struct NUInt
  {
    private readonly unsafe void* _value;

    private unsafe NUInt(uint value) => this._value = (void*) value;

    private unsafe NUInt(ulong value) => this._value = (void*) value;

    public static implicit operator NUInt(uint value) => new NUInt(value);

    public static unsafe implicit operator IntPtr(NUInt value) => (IntPtr) value._value;

    public static explicit operator NUInt(int value) => new NUInt((uint) value);

    public static unsafe explicit operator void*(NUInt value) => value._value;

    [MethodImpl((MethodImplOptions) 256)]
    public static unsafe NUInt operator *(NUInt left, NUInt right) => sizeof (IntPtr) != 4 ? new NUInt((ulong) left._value * (ulong) right._value) : new NUInt((uint) left._value * (uint) right._value);
  }
}
