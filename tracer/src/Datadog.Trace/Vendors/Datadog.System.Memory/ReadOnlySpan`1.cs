﻿// Decompiled with JetBrains decompiler
// Type: System.ReadOnlySpan`1
// Assembly: System.Memory, Version=4.0.1.2, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51
// MVID: 805945F3-27B0-47AD-B8F6-389D9D8F82C3
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Memory.4.5.5\lib\net461\System.Memory.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Memory.4.5.5\lib\net461\System.Memory.xml

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Datadog.System.Runtime.CompilerServices.Unsafe;

namespace Datadog.System
{
    /// <typeparam name="T"></typeparam>
    [DebuggerTypeProxy(typeof (SpanDebugView<>))]
  [DebuggerDisplay("{ToString(),raw}")]
  [DebuggerTypeProxy(typeof (SpanDebugView<>))]
  [DebuggerDisplay("{ToString(),raw}")]
  public readonly ref struct ReadOnlySpan<T>
  {
    private readonly System.Pinnable<T> _pinnable;
    private readonly IntPtr _byteOffset;
    private readonly int _length;

    /// <returns></returns>
    public int Length => this._length;

    /// <returns></returns>
    public bool IsEmpty => this._length == 0;

    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public static bool operator !=(ReadOnlySpan<T> left, ReadOnlySpan<T> right) => !(left == right);

    /// <param name="obj"></param>
    /// <returns></returns>
    [Obsolete("Equals() on ReadOnlySpan will always throw an exception. Use == instead.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override bool Equals(object obj) => throw new NotSupportedException(SR.NotSupported_CannotCallEqualsOnSpan);

    /// <returns></returns>
    [Obsolete("GetHashCode() on ReadOnlySpan will always throw an exception.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override int GetHashCode() => throw new NotSupportedException(SR.NotSupported_CannotCallGetHashCodeOnSpan);

    public static implicit operator ReadOnlySpan<T>(T[] array) => new ReadOnlySpan<T>(array);

    public static implicit operator ReadOnlySpan<T>(ArraySegment<T> segment) => new ReadOnlySpan<T>(segment.Array, segment.Offset, segment.Count);

    /// <returns></returns>
    public static ReadOnlySpan<T> Empty => new ReadOnlySpan<T>();

    public ReadOnlySpan<T>.Enumerator GetEnumerator() => new ReadOnlySpan<T>.Enumerator(this);

    /// <param name="array"></param>
    [MethodImpl((MethodImplOptions) 256)]
    public unsafe ReadOnlySpan(T[] array)
    {
      if (array == null)
      {
        // todo: fix *(ReadOnlySpan<T>*) ref this = new ReadOnlySpan<T>();
      }
      else
      {
        this._length = array.Length;
        this._pinnable = Unsafe.As<System.Pinnable<T>>((object) array);
        this._byteOffset = SpanHelpers.PerTypeValues<T>.ArrayAdjustment;
      }
    }

    /// <param name="array"></param>
    /// <param name="start"></param>
    /// <param name="length"></param>
    [MethodImpl((MethodImplOptions) 256)]
    public unsafe ReadOnlySpan(T[] array, int start, int length)
    {
      if (array == null)
      {
        if (start != 0 || length != 0)
          ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start);
        // todo: fix *(ReadOnlySpan<T>*) ref this = new ReadOnlySpan<T>();
      }
      else
      {
        if ((uint) start > (uint) array.Length || (uint) length > (uint) (array.Length - start))
          ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start);
        this._length = length;
        this._pinnable = Unsafe.As<System.Pinnable<T>>((object) array);
        this._byteOffset = SpanHelpers.PerTypeValues<T>.ArrayAdjustment.Add<T>(start);
      }
    }

    /// <param name="pointer"></param>
    /// <param name="length"></param>
    [CLSCompliant(false)]
    [MethodImpl((MethodImplOptions) 256)]
    public unsafe ReadOnlySpan(void* pointer, int length)
    {
      if (SpanHelpers.IsReferenceOrContainsReferences<T>())
        ThrowHelper.ThrowArgumentException_InvalidTypeWithPointersNotSupported(typeof (T));
      if (length < 0)
        ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start);
      this._length = length;
      this._pinnable = (System.Pinnable<T>) null;
      this._byteOffset = new IntPtr(pointer);
    }

    [MethodImpl((MethodImplOptions) 256)]
    internal ReadOnlySpan(System.Pinnable<T> pinnable, IntPtr byteOffset, int length)
    {
      this._length = length;
      this._pinnable = pinnable;
      this._byteOffset = byteOffset;
    }

    /// <param name="index"></param>
    /// <returns></returns>
    public readonly unsafe ref readonly T this[int index]
    {
      [MethodImpl((MethodImplOptions) 256)] get
      {
        if ((uint) index >= (uint) this._length)
          ThrowHelper.ThrowIndexOutOfRangeException();
        return ref (this._pinnable == null ? ref Unsafe.Add<T>(ref Unsafe.AsRef<T>(this._byteOffset.ToPointer()), index) : ref Unsafe.Add<T>(ref Unsafe.AddByteOffset<T>(ref this._pinnable.Data, this._byteOffset), index));
      }
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public unsafe ref readonly T GetPinnableReference()
    {
      if (this._length == 0)
        return ref Unsafe.AsRef<T>((void*) null);
      return ref (this._pinnable == null ? ref Unsafe.AsRef<T>(this._byteOffset.ToPointer()) : ref Unsafe.AddByteOffset<T>(ref this._pinnable.Data, this._byteOffset));
    }

    /// <param name="destination"></param>
    public void CopyTo(Span<T> destination)
    {
      if (this.TryCopyTo(destination))
        return;
      ThrowHelper.ThrowArgumentException_DestinationTooShort();
    }

    /// <param name="destination"></param>
    /// <returns></returns>
    public bool TryCopyTo(Span<T> destination)
    {
      int length1 = this._length;
      int length2 = destination.Length;
      if (length1 == 0)
        return true;
      if ((uint) length1 > (uint) length2)
        return false;
      ref T local = ref this.DangerousGetPinnableReference();
      SpanHelpers.CopyTo<T>(ref destination.DangerousGetPinnableReference(), length2, ref local, length1);
      return true;
    }

    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public static bool operator ==(ReadOnlySpan<T> left, ReadOnlySpan<T> right) => left._length == right._length && Unsafe.AreSame<T>(ref left.DangerousGetPinnableReference(), ref right.DangerousGetPinnableReference());

    public override unsafe string ToString()
    {
      if (!(typeof (T) == typeof (char)))
        return string.Format("System.ReadOnlySpan<{0}>[{1}]", (object) typeof (T).Name, (object) this._length);
      if (this._byteOffset == MemoryExtensions.StringAdjustment && Unsafe.As<object>((object) this._pinnable) is string str && this._length == str.Length)
        return str;
      fixed (char* chPtr = &Unsafe.As<T, char>(ref this.DangerousGetPinnableReference()))
        return new string(chPtr, 0, this._length);
    }

    /// <param name="start"></param>
    /// <returns></returns>
    [MethodImpl((MethodImplOptions) 256)]
    public ReadOnlySpan<T> Slice(int start)
    {
      if ((uint) start > (uint) this._length)
        ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start);
      return new ReadOnlySpan<T>(this._pinnable, this._byteOffset.Add<T>(start), this._length - start);
    }

    /// <param name="start"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [MethodImpl((MethodImplOptions) 256)]
    public ReadOnlySpan<T> Slice(int start, int length)
    {
      if ((uint) start > (uint) this._length || (uint) length > (uint) (this._length - start))
        ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start);
      return new ReadOnlySpan<T>(this._pinnable, this._byteOffset.Add<T>(start), length);
    }

    /// <returns></returns>
    public T[] ToArray()
    {
      if (this._length == 0)
        return SpanHelpers.PerTypeValues<T>.EmptyArray;
      T[] destination = new T[this._length];
      this.CopyTo((Span<T>) destination);
      return destination;
    }

    /// <returns></returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl((MethodImplOptions) 256)]
    internal unsafe ref T DangerousGetPinnableReference() => ref (this._pinnable == null ? ref Unsafe.AsRef<T>(this._byteOffset.ToPointer()) : ref Unsafe.AddByteOffset<T>(ref this._pinnable.Data, this._byteOffset));

    internal System.Pinnable<T> Pinnable => this._pinnable;

    internal IntPtr ByteOffset => this._byteOffset;

    public ref struct Enumerator
    {
      private readonly ReadOnlySpan<T> _span;
      private int _index;

      [MethodImpl((MethodImplOptions) 256)]
      internal Enumerator(ReadOnlySpan<T> span)
      {
        this._span = span;
        this._index = -1;
      }

      [MethodImpl((MethodImplOptions) 256)]
      public bool MoveNext()
      {
        int num = this._index + 1;
        if (num >= this._span.Length)
          return false;
        this._index = num;
        return true;
      }

      public readonly ref readonly T Current
      {
        [MethodImpl((MethodImplOptions) 256)] get => ref this._span[this._index];
      }
    }
  }
}
