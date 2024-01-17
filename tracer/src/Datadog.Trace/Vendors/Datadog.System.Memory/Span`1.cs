﻿// Decompiled with JetBrains decompiler
// Type: System.Span`1
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
    [DebuggerTypeProxy(typeof(SpanDebugView<>))]
    [DebuggerDisplay("{ToString(),raw}")]
    [DebuggerTypeProxy(typeof(SpanDebugView<>))]
    [DebuggerDisplay("{ToString(),raw}")]
    public readonly ref struct Span<T>
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
        public static bool operator !=(Span<T> left, Span<T> right) => !(left == right);

        /// <param name="obj"></param>
        /// <returns></returns>
        [Obsolete("Equals() on Span will always throw an exception. Use == instead.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override bool Equals(object obj) => throw new NotSupportedException(SR.NotSupported_CannotCallEqualsOnSpan);

        /// <returns></returns>
        [Obsolete("GetHashCode() on Span will always throw an exception.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override int GetHashCode() => throw new NotSupportedException(SR.NotSupported_CannotCallGetHashCodeOnSpan);

        public static implicit operator Span<T>(T[] array) => new Span<T>(array);

        public static implicit operator Span<T>(ArraySegment<T> segment) => new Span<T>(segment.Array, segment.Offset, segment.Count);

        /// <returns></returns>
        public static Span<T> Empty => new Span<T>();

        public Span<T>.Enumerator GetEnumerator() => new Span<T>.Enumerator(this);

        /// <param name="array"></param>
        [MethodImpl((MethodImplOptions)256)]
        public unsafe Span(T[] array)
        {
            if (array == null)
            {
                // todo: fix *(Span<T>*) ref this = new Span<T>();
            }
            else
            {
                if ((object)default(T) == null && array.GetType() != typeof(T[]))
                    ThrowHelper.ThrowArrayTypeMismatchException();
                this._length = array.Length;
                this._pinnable = Unsafe.As<System.Pinnable<T>>((object)array);
                this._byteOffset = SpanHelpers.PerTypeValues<T>.ArrayAdjustment;
            }
        }

        [MethodImpl((MethodImplOptions)256)]
        internal static Span<T> Create(T[] array, int start)
        {
            if (array == null)
            {
                if (start != 0)
                    ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start);
                return new Span<T>();
            }
            if ((object)default(T) == null && array.GetType() != typeof(T[]))
                ThrowHelper.ThrowArrayTypeMismatchException();
            if ((uint)start > (uint)array.Length)
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start);
            IntPtr byteOffset = SpanHelpers.PerTypeValues<T>.ArrayAdjustment.Add<T>(start);
            int length = array.Length - start;
            return new Span<T>(Unsafe.As<System.Pinnable<T>>((object)array), byteOffset, length);
        }

        /// <param name="array"></param>
        /// <param name="start"></param>
        /// <param name="length"></param>
        [MethodImpl((MethodImplOptions)256)]
        public unsafe Span(T[] array, int start, int length)
        {
            if (array == null)
            {
                if (start != 0 || length != 0)
                    ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start);
                // todo: fix *(Span<T>*) ref this = new Span<T>();
            }
            else
            {
                if ((object)default(T) == null && array.GetType() != typeof(T[]))
                    ThrowHelper.ThrowArrayTypeMismatchException();
                if ((uint)start > (uint)array.Length || (uint)length > (uint)(array.Length - start))
                    ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start);
                this._length = length;
                this._pinnable = Unsafe.As<System.Pinnable<T>>((object)array);
                this._byteOffset = SpanHelpers.PerTypeValues<T>.ArrayAdjustment.Add<T>(start);
            }
        }

        /// <param name="pointer"></param>
        /// <param name="length"></param>
        [CLSCompliant(false)]
        [MethodImpl((MethodImplOptions)256)]
        public unsafe Span(void* pointer, int length)
        {
            if (SpanHelpers.IsReferenceOrContainsReferences<T>())
                ThrowHelper.ThrowArgumentException_InvalidTypeWithPointersNotSupported(typeof(T));
            if (length < 0)
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start);
            this._length = length;
            this._pinnable = (System.Pinnable<T>)null;
            this._byteOffset = new IntPtr(pointer);
        }

        [MethodImpl((MethodImplOptions)256)]
        internal Span(System.Pinnable<T> pinnable, IntPtr byteOffset, int length)
        {
            this._length = length;
            this._pinnable = pinnable;
            this._byteOffset = byteOffset;
        }

        /// <param name="index"></param>
        /// <returns></returns>
        public unsafe ref T this[int index]
        {
            [MethodImpl((MethodImplOptions)256)]
            get
            {
                if ((uint)index >= (uint)this._length)
                    ThrowHelper.ThrowIndexOutOfRangeException();
                return ref (this._pinnable == null ? ref Unsafe.Add<T>(ref Unsafe.AsRef<T>(this._byteOffset.ToPointer()), index) : ref Unsafe.Add<T>(ref Unsafe.AddByteOffset<T>(ref this._pinnable.Data, this._byteOffset), index));
            }
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public unsafe ref T GetPinnableReference()
        {
            if (this._length == 0)
                return ref Unsafe.AsRef<T>((void*)null);
            return ref (this._pinnable == null ? ref Unsafe.AsRef<T>(this._byteOffset.ToPointer()) : ref Unsafe.AddByteOffset<T>(ref this._pinnable.Data, this._byteOffset));
        }

        public unsafe void Clear()
        {
            int length = this._length;
            if (length == 0)
                return;
            UIntPtr byteLength = (UIntPtr)((ulong)(uint)length * (ulong)Unsafe.SizeOf<T>());
            if ((Unsafe.SizeOf<T>() & sizeof(IntPtr) - 1) != 0)
            {
                if (this._pinnable == null)
                    SpanHelpers.ClearLessThanPointerSized((byte*)this._byteOffset.ToPointer(), byteLength);
                else
                    SpanHelpers.ClearLessThanPointerSized(ref Unsafe.As<T, byte>(ref Unsafe.AddByteOffset<T>(ref this._pinnable.Data, this._byteOffset)), byteLength);
            }
            else if (SpanHelpers.IsReferenceOrContainsReferences<T>())
            {
                UIntPtr pointerSizeLength = (UIntPtr)(ulong)(length * Unsafe.SizeOf<T>() / sizeof(IntPtr));
                SpanHelpers.ClearPointerSizedWithReferences(ref Unsafe.As<T, IntPtr>(ref this.DangerousGetPinnableReference()), pointerSizeLength);
            }
            else
                SpanHelpers.ClearPointerSizedWithoutReferences(ref Unsafe.As<T, byte>(ref this.DangerousGetPinnableReference()), byteLength);
        }

        /// <param name="value"></param>
        public unsafe void Fill(T value)
        {
            int length = this._length;
            if (length == 0)
                return;
            if (Unsafe.SizeOf<T>() == 1)
            {
                byte num = Unsafe.As<T, byte>(ref value);
                if (this._pinnable == null)
                    Unsafe.InitBlockUnaligned(this._byteOffset.ToPointer(), num, (uint)length);
                else
                    Unsafe.InitBlockUnaligned(ref Unsafe.As<T, byte>(ref Unsafe.AddByteOffset<T>(ref this._pinnable.Data, this._byteOffset)), num, (uint)length);
            }
            else
            {
                ref T local = ref this.DangerousGetPinnableReference();
                int elementOffset;
                for (elementOffset = 0; elementOffset < (length & -8); elementOffset += 8)
                {
                    Unsafe.Add<T>(ref local, elementOffset) = value;
                    Unsafe.Add<T>(ref local, elementOffset + 1) = value;
                    Unsafe.Add<T>(ref local, elementOffset + 2) = value;
                    Unsafe.Add<T>(ref local, elementOffset + 3) = value;
                    Unsafe.Add<T>(ref local, elementOffset + 4) = value;
                    Unsafe.Add<T>(ref local, elementOffset + 5) = value;
                    Unsafe.Add<T>(ref local, elementOffset + 6) = value;
                    Unsafe.Add<T>(ref local, elementOffset + 7) = value;
                }
                if (elementOffset < (length & -4))
                {
                    Unsafe.Add<T>(ref local, elementOffset) = value;
                    Unsafe.Add<T>(ref local, elementOffset + 1) = value;
                    Unsafe.Add<T>(ref local, elementOffset + 2) = value;
                    Unsafe.Add<T>(ref local, elementOffset + 3) = value;
                    elementOffset += 4;
                }
                for (; elementOffset < length; ++elementOffset)
                    Unsafe.Add<T>(ref local, elementOffset) = value;
            }
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
            int length2 = destination._length;
            if (length1 == 0)
                return true;
            if ((uint)length1 > (uint)length2)
                return false;
            ref T local = ref this.DangerousGetPinnableReference();
            SpanHelpers.CopyTo<T>(ref destination.DangerousGetPinnableReference(), length2, ref local, length1);
            return true;
        }

        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static bool operator ==(Span<T> left, Span<T> right) => left._length == right._length && Unsafe.AreSame<T>(ref left.DangerousGetPinnableReference(), ref right.DangerousGetPinnableReference());

        public static implicit operator ReadOnlySpan<T>(Span<T> span) => new ReadOnlySpan<T>(span._pinnable, span._byteOffset, span._length);

        public override unsafe string ToString()
        {
            if (!(typeof(T) == typeof(char)))
                return string.Format("System.Span<{0}>[{1}]", (object)typeof(T).Name, (object)this._length);
            fixed (char* chPtr = &Unsafe.As<T, char>(ref this.DangerousGetPinnableReference()))
                return new string(chPtr, 0, this._length);
        }

        /// <param name="start"></param>
        /// <returns></returns>
        [MethodImpl((MethodImplOptions)256)]
        public Span<T> Slice(int start)
        {
            if ((uint)start > (uint)this._length)
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start);
            return new Span<T>(this._pinnable, this._byteOffset.Add<T>(start), this._length - start);
        }

        /// <param name="start"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        [MethodImpl((MethodImplOptions)256)]
        public Span<T> Slice(int start, int length)
        {
            if ((uint)start > (uint)this._length || (uint)length > (uint)(this._length - start))
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start);
            return new Span<T>(this._pinnable, this._byteOffset.Add<T>(start), length);
        }

        /// <returns></returns>
        public T[] ToArray()
        {
            if (this._length == 0)
                return SpanHelpers.PerTypeValues<T>.EmptyArray;
            T[] destination = new T[this._length];
            this.CopyTo((Span<T>)destination);
            return destination;
        }

        /// <returns></returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl((MethodImplOptions)256)]
        internal unsafe ref T DangerousGetPinnableReference() => ref (this._pinnable == null ? ref Unsafe.AsRef<T>(this._byteOffset.ToPointer()) : ref Unsafe.AddByteOffset<T>(ref this._pinnable.Data, this._byteOffset));

        internal System.Pinnable<T> Pinnable => this._pinnable;

        internal IntPtr ByteOffset => this._byteOffset;

        public ref struct Enumerator
        {
            private readonly Span<T> _span;
            private int _index;

            [MethodImpl((MethodImplOptions)256)]
            internal Enumerator(Span<T> span)
            {
                this._span = span;
                this._index = -1;
            }

            [MethodImpl((MethodImplOptions)256)]
            public bool MoveNext()
            {
                int num = this._index + 1;
                if (num >= this._span.Length)
                    return false;
                this._index = num;
                return true;
            }

            public ref T Current
            {
                [MethodImpl((MethodImplOptions)256)]
                get => ref this._span[this._index];
            }
        }
    }
}
