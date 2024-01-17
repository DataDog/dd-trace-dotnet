﻿// Decompiled with JetBrains decompiler
// Type: System.ReadOnlyMemory`1
// Assembly: System.Memory, Version=4.0.1.2, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51
// MVID: 805945F3-27B0-47AD-B8F6-389D9D8F82C3
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Memory.4.5.5\lib\net461\System.Memory.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Memory.4.5.5\lib\net461\System.Memory.xml

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Datadog.System.Buffers;
using Datadog.System.Runtime.CompilerServices.Unsafe;
using Datadog.System.Runtime.InteropServices;

namespace Datadog.System
{
    [DebuggerTypeProxy(typeof (MemoryDebugView<>))]
  [DebuggerDisplay("{ToString(),raw}")]
  public readonly struct ReadOnlyMemory<T>
  {
    private readonly object _object;
    private readonly int _index;
    private readonly int _length;
    internal const int RemoveFlagsBitMask = 2147483647;

    [MethodImpl((MethodImplOptions) 256)]
    public unsafe ReadOnlyMemory(T[] array)
    {
      if (array == null)
      {
        // todo: fix *(ReadOnlyMemory<T>*) ref this = new ReadOnlyMemory<T>();
      }
      else
      {
        this._object = (object) array;
        this._index = 0;
        this._length = array.Length;
      }
    }

    [MethodImpl((MethodImplOptions) 256)]
    public unsafe ReadOnlyMemory(T[] array, int start, int length)
    {
      if (array == null)
      {
        if (start != 0 || length != 0)
          ThrowHelper.ThrowArgumentOutOfRangeException();
        // todo: fix *(ReadOnlyMemory<T>*) ref this = new ReadOnlyMemory<T>();
      }
      else
      {
        if ((uint) start > (uint) array.Length || (uint) length > (uint) (array.Length - start))
          ThrowHelper.ThrowArgumentOutOfRangeException();
        this._object = (object) array;
        this._index = start;
        this._length = length;
      }
    }

    [MethodImpl((MethodImplOptions) 256)]
    internal ReadOnlyMemory(object obj, int start, int length)
    {
      this._object = obj;
      this._index = start;
      this._length = length;
    }

    public static implicit operator ReadOnlyMemory<T>(T[] array) => new ReadOnlyMemory<T>(array);

    public static implicit operator ReadOnlyMemory<T>(ArraySegment<T> segment) => new ReadOnlyMemory<T>(segment.Array, segment.Offset, segment.Count);

    public static ReadOnlyMemory<T> Empty => new ReadOnlyMemory<T>();

    public int Length => this._length & int.MaxValue;

    public bool IsEmpty => (this._length & int.MaxValue) == 0;

    public override string ToString()
    {
      if (!(typeof (T) == typeof (char)))
        return string.Format("System.ReadOnlyMemory<{0}>[{1}]", (object) typeof (T).Name, (object) (this._length & int.MaxValue));
      return !(this._object is string str) ? this.Span.ToString() : str.Substring(this._index, this._length & int.MaxValue);
    }

    [MethodImpl((MethodImplOptions) 256)]
    public ReadOnlyMemory<T> Slice(int start)
    {
      int length = this._length;
      int num = length & int.MaxValue;
      if ((uint) start > (uint) num)
        ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start);
      return new ReadOnlyMemory<T>(this._object, this._index + start, length - start);
    }

    [MethodImpl((MethodImplOptions) 256)]
    public ReadOnlyMemory<T> Slice(int start, int length)
    {
      int length1 = this._length;
      int num = this._length & int.MaxValue;
      if ((uint) start > (uint) num || (uint) length > (uint) (num - start))
        ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start);
      return new ReadOnlyMemory<T>(this._object, this._index + start, length | length1 & int.MinValue);
    }

    public ReadOnlySpan<T> Span
    {
      [MethodImpl((MethodImplOptions) 256)] get
      {
        if (this._index < 0)
          return (ReadOnlySpan<T>) ((MemoryManager<T>) this._object).GetSpan().Slice(this._index & int.MaxValue, this._length);
        if (typeof (T) == typeof (char) && this._object is string o)
          return new ReadOnlySpan<T>(Unsafe.As<Pinnable<T>>((object) o), MemoryExtensions.StringAdjustment, o.Length).Slice(this._index, this._length);
        return this._object != null ? new ReadOnlySpan<T>((T[]) this._object, this._index, this._length & int.MaxValue) : new ReadOnlySpan<T>();
      }
    }

    public void CopyTo(Memory<T> destination) => this.Span.CopyTo(destination.Span);

    public bool TryCopyTo(Memory<T> destination) => this.Span.TryCopyTo(destination.Span);

    public unsafe MemoryHandle Pin()
    {
      if (this._index < 0)
        return ((MemoryManager<T>) this._object).Pin(this._index & int.MaxValue);
      if (typeof (T) == typeof (char) && this._object is string str)
      {
        GCHandle handle = GCHandle.Alloc((object) str, GCHandleType.Pinned);
        return new MemoryHandle(Unsafe.Add<T>((void*) handle.AddrOfPinnedObject(), this._index), handle);
      }
      if (!(this._object is T[] objArray))
        return new MemoryHandle();
      if (this._length < 0)
        return new MemoryHandle(Unsafe.Add<T>(Unsafe.AsPointer<T>(ref MemoryMarshal.GetReference<T>((System.Span<T>) objArray)), this._index));
      GCHandle handle1 = GCHandle.Alloc((object) objArray, GCHandleType.Pinned);
      return new MemoryHandle(Unsafe.Add<T>((void*) handle1.AddrOfPinnedObject(), this._index), handle1);
    }

    public T[] ToArray() => this.Span.ToArray();

    [EditorBrowsable(EditorBrowsableState.Never)]
    public override bool Equals(object obj)
    {
      switch (obj)
      {
        case ReadOnlyMemory<T> other1:
          return this.Equals(other1);
        case Memory<T> other2:
          return this.Equals((ReadOnlyMemory<T>) other2);
        default:
          return false;
      }
    }

    public bool Equals(ReadOnlyMemory<T> other) => this._object == other._object && this._index == other._index && this._length == other._length;

    [EditorBrowsable(EditorBrowsableState.Never)]
    public override int GetHashCode()
    {
      if (this._object == null)
        return 0;
      int hashCode1 = this._object.GetHashCode();
      int num = this._index;
      int hashCode2 = num.GetHashCode();
      num = this._length;
      int hashCode3 = num.GetHashCode();
      return ReadOnlyMemory<T>.CombineHashCodes(hashCode1, hashCode2, hashCode3);
    }

    private static int CombineHashCodes(int left, int right) => (left << 5) + left ^ right;

    private static int CombineHashCodes(int h1, int h2, int h3) => ReadOnlyMemory<T>.CombineHashCodes(ReadOnlyMemory<T>.CombineHashCodes(h1, h2), h3);

    [MethodImpl((MethodImplOptions) 256)]
    internal object GetObjectStartLength(out int start, out int length)
    {
      start = this._index;
      length = this._length;
      return this._object;
    }
  }
}
