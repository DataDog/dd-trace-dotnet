﻿// Decompiled with JetBrains decompiler
// Type: System.Buffers.ReadOnlySequence`1
// Assembly: System.Memory, Version=4.0.1.2, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51
// MVID: 805945F3-27B0-47AD-B8F6-389D9D8F82C3
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Memory.4.5.5\lib\net461\System.Memory.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Memory.4.5.5\lib\net461\System.Memory.xml

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Datadog.System.Runtime.CompilerServices.Unsafe;
using Datadog.System.Runtime.InteropServices;

namespace Datadog.System.Buffers
{
    [DebuggerTypeProxy(typeof(ReadOnlySequenceDebugView<>))]
    [DebuggerDisplay("{ToString(),raw}")]
    public readonly struct ReadOnlySequence<T>
    {
        private readonly SequencePosition _sequenceStart;
        private readonly SequencePosition _sequenceEnd;
        public static readonly ReadOnlySequence<T> Empty = new ReadOnlySequence<T>(SpanHelpers.PerTypeValues<T>.EmptyArray);

        public long Length => this.GetLength();

        public bool IsEmpty => this.Length == 0L;

        public bool IsSingleSegment
        {
            [MethodImpl((MethodImplOptions)256)]
            get => this._sequenceStart.GetObject() == this._sequenceEnd.GetObject();
        }

        public ReadOnlyMemory<T> First => this.GetFirstBuffer();

        public SequencePosition Start => this._sequenceStart;

        public SequencePosition End => this._sequenceEnd;

        [MethodImpl((MethodImplOptions)256)]
        private ReadOnlySequence(
          object startSegment,
          int startIndexAndFlags,
          object endSegment,
          int endIndexAndFlags)
        {
            this._sequenceStart = new SequencePosition(startSegment, startIndexAndFlags);
            this._sequenceEnd = new SequencePosition(endSegment, endIndexAndFlags);
        }

        public ReadOnlySequence(
          ReadOnlySequenceSegment<T> startSegment,
          int startIndex,
          ReadOnlySequenceSegment<T> endSegment,
          int endIndex)
        {
            if (startSegment != null && endSegment != null && (startSegment == endSegment || startSegment.RunningIndex <= endSegment.RunningIndex))
            {
                ReadOnlyMemory<T> memory = startSegment.Memory;
                if ((uint)memory.Length >= (uint)startIndex)
                {
                    memory = endSegment.Memory;
                    if ((uint)memory.Length >= (uint)endIndex && (startSegment != endSegment || endIndex >= startIndex))
                        goto label_4;
                }
            }
            ThrowHelper.ThrowArgumentValidationException<T>(startSegment, startIndex, endSegment);
        label_4:
            this._sequenceStart = new SequencePosition((object)startSegment, ReadOnlySequence.SegmentToSequenceStart(startIndex));
            this._sequenceEnd = new SequencePosition((object)endSegment, ReadOnlySequence.SegmentToSequenceEnd(endIndex));
        }

        public ReadOnlySequence(T[] array)
        {
            if (array == null)
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.array);
            this._sequenceStart = new SequencePosition((object)array, ReadOnlySequence.ArrayToSequenceStart(0));
            this._sequenceEnd = new SequencePosition((object)array, ReadOnlySequence.ArrayToSequenceEnd(array.Length));
        }

        public ReadOnlySequence(T[] array, int start, int length)
        {
            if (array == null || (uint)start > (uint)array.Length || (uint)length > (uint)(array.Length - start))
                ThrowHelper.ThrowArgumentValidationException((Array)array, start);
            this._sequenceStart = new SequencePosition((object)array, ReadOnlySequence.ArrayToSequenceStart(start));
            this._sequenceEnd = new SequencePosition((object)array, ReadOnlySequence.ArrayToSequenceEnd(start + length));
        }

        public ReadOnlySequence(ReadOnlyMemory<T> memory)
        {
            MemoryManager<T> manager;
            int start1;
            int length;
            if (MemoryMarshal.TryGetMemoryManager<T, MemoryManager<T>>(memory, out manager, out start1, out length))
            {
                this._sequenceStart = new SequencePosition((object)manager, ReadOnlySequence.MemoryManagerToSequenceStart(start1));
                this._sequenceEnd = new SequencePosition((object)manager, ReadOnlySequence.MemoryManagerToSequenceEnd(start1 + length));
            }
            else
            {
                ArraySegment<T> segment;
                if (MemoryMarshal.TryGetArray<T>(memory, out segment))
                {
                    T[] array = segment.Array;
                    int offset = segment.Offset;
                    this._sequenceStart = new SequencePosition((object)array, ReadOnlySequence.ArrayToSequenceStart(offset));
                    this._sequenceEnd = new SequencePosition((object)array, ReadOnlySequence.ArrayToSequenceEnd(offset + segment.Count));
                }
                else if (typeof(T) == typeof(char))
                {
                    string text;
                    int start2;
                    if (!MemoryMarshal.TryGetString((ReadOnlyMemory<char>)(ValueType)memory, out text, out start2, out length))
                        ThrowHelper.ThrowInvalidOperationException();
                    this._sequenceStart = new SequencePosition((object)text, ReadOnlySequence.StringToSequenceStart(start2));
                    this._sequenceEnd = new SequencePosition((object)text, ReadOnlySequence.StringToSequenceEnd(start2 + length));
                }
                else
                {
                    ThrowHelper.ThrowInvalidOperationException();
                    this._sequenceStart = new SequencePosition();
                    this._sequenceEnd = new SequencePosition();
                }
            }
        }

        public ReadOnlySequence<T> Slice(long start, long length)
        {
            if (start < 0L || length < 0L)
                ThrowHelper.ThrowStartOrEndArgumentValidationException(start);
            int index1 = ReadOnlySequence<T>.GetIndex(in this._sequenceStart);
            int index2 = ReadOnlySequence<T>.GetIndex(in this._sequenceEnd);
            object obj1 = this._sequenceStart.GetObject();
            object endObject = this._sequenceEnd.GetObject();
            SequencePosition sequencePosition;
            SequencePosition end;
            if (obj1 != endObject)
            {
                ReadOnlySequenceSegment<T> startSegment = (ReadOnlySequenceSegment<T>)obj1;
                int num1 = startSegment.Memory.Length - index1;
                if ((long)num1 > start)
                {
                    int num2 = index1 + (int)start;
                    sequencePosition = new SequencePosition(obj1, num2);
                    end = ReadOnlySequence<T>.GetEndPosition(startSegment, obj1, num2, endObject, index2, length);
                }
                else
                {
                    if (num1 < 0)
                        ThrowHelper.ThrowArgumentOutOfRangeException_PositionOutOfRange();
                    sequencePosition = ReadOnlySequence<T>.SeekMultiSegment(startSegment.Next, endObject, index2, start - (long)num1, ExceptionArgument.start);
                    int index3 = ReadOnlySequence<T>.GetIndex(in sequencePosition);
                    object obj2 = sequencePosition.GetObject();
                    if (obj2 != endObject)
                    {
                        end = ReadOnlySequence<T>.GetEndPosition((ReadOnlySequenceSegment<T>)obj2, obj2, index3, endObject, index2, length);
                    }
                    else
                    {
                        if ((long)(index2 - index3) < length)
                            ThrowHelper.ThrowStartOrEndArgumentValidationException(0L);
                        end = new SequencePosition(obj2, index3 + (int)length);
                    }
                }
            }
            else
            {
                if ((long)(index2 - index1) < start)
                    ThrowHelper.ThrowStartOrEndArgumentValidationException(-1L);
                int integer = index1 + (int)start;
                sequencePosition = new SequencePosition(obj1, integer);
                if ((long)(index2 - integer) < length)
                    ThrowHelper.ThrowStartOrEndArgumentValidationException(0L);
                end = new SequencePosition(obj1, integer + (int)length);
            }
            return this.SliceImpl(in sequencePosition, in end);
        }

        public ReadOnlySequence<T> Slice(long start, SequencePosition end)
        {
            if (start < 0L)
                ThrowHelper.ThrowStartOrEndArgumentValidationException(start);
            uint index1 = (uint)ReadOnlySequence<T>.GetIndex(in end);
            object endObject = end.GetObject();
            uint index2 = (uint)ReadOnlySequence<T>.GetIndex(in this._sequenceStart);
            object @object = this._sequenceStart.GetObject();
            uint index3 = (uint)ReadOnlySequence<T>.GetIndex(in this._sequenceEnd);
            object obj = this._sequenceEnd.GetObject();
            if (@object == obj)
            {
                if (!ReadOnlySequence<T>.InRange(index1, index2, index3))
                    ThrowHelper.ThrowArgumentOutOfRangeException_PositionOutOfRange();
                if ((long)(index1 - index2) < start)
                    ThrowHelper.ThrowStartOrEndArgumentValidationException(-1L);
            }
            else
            {
                ReadOnlySequenceSegment<T> onlySequenceSegment = (ReadOnlySequenceSegment<T>)@object;
                ulong start1 = (ulong)onlySequenceSegment.RunningIndex + (ulong)index2;
                ulong num1 = (ulong)((ReadOnlySequenceSegment<T>)endObject).RunningIndex + (ulong)index1;
                if (!ReadOnlySequence<T>.InRange(num1, start1, (ulong)((ReadOnlySequenceSegment<T>)obj).RunningIndex + (ulong)index3))
                    ThrowHelper.ThrowArgumentOutOfRangeException_PositionOutOfRange();
                if (start1 + (ulong)start > num1)
                    ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start);
                int num2 = onlySequenceSegment.Memory.Length - (int)index2;
                if ((long)num2 <= start)
                {
                    if (num2 < 0)
                        ThrowHelper.ThrowArgumentOutOfRangeException_PositionOutOfRange();
                    return this.SliceImpl(ReadOnlySequence<T>.SeekMultiSegment(onlySequenceSegment.Next, endObject, (int)index1, start - (long)num2, ExceptionArgument.start), in end);
                }
            }
            return this.SliceImpl(new SequencePosition(@object, (int)index2 + (int)start), in end);
        }

        public ReadOnlySequence<T> Slice(SequencePosition start, long length)
        {
            uint index1 = (uint)ReadOnlySequence<T>.GetIndex(in start);
            object @object = start.GetObject();
            uint index2 = (uint)ReadOnlySequence<T>.GetIndex(in this._sequenceStart);
            object obj = this._sequenceStart.GetObject();
            uint index3 = (uint)ReadOnlySequence<T>.GetIndex(in this._sequenceEnd);
            object endObject = this._sequenceEnd.GetObject();
            if (obj == endObject)
            {
                if (!ReadOnlySequence<T>.InRange(index1, index2, index3))
                    ThrowHelper.ThrowArgumentOutOfRangeException_PositionOutOfRange();
                if (length < 0L)
                    ThrowHelper.ThrowStartOrEndArgumentValidationException(0L);
                if ((long)(index3 - index1) < length)
                    ThrowHelper.ThrowStartOrEndArgumentValidationException(0L);
            }
            else
            {
                ReadOnlySequenceSegment<T> onlySequenceSegment = (ReadOnlySequenceSegment<T>)@object;
                ulong num1 = (ulong)onlySequenceSegment.RunningIndex + (ulong)index1;
                ulong start1 = (ulong)((ReadOnlySequenceSegment<T>)obj).RunningIndex + (ulong)index2;
                ulong end1 = (ulong)((ReadOnlySequenceSegment<T>)endObject).RunningIndex + (ulong)index3;
                if (!ReadOnlySequence<T>.InRange(num1, start1, end1))
                    ThrowHelper.ThrowArgumentOutOfRangeException_PositionOutOfRange();
                if (length < 0L)
                    ThrowHelper.ThrowStartOrEndArgumentValidationException(0L);
                if (num1 + (ulong)length > end1)
                    ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.length);
                int num2 = onlySequenceSegment.Memory.Length - (int)index1;
                if ((long)num2 < length)
                {
                    if (num2 < 0)
                        ThrowHelper.ThrowArgumentOutOfRangeException_PositionOutOfRange();
                    SequencePosition end2 = ReadOnlySequence<T>.SeekMultiSegment(onlySequenceSegment.Next, endObject, (int)index3, length - (long)num2, ExceptionArgument.length);
                    return this.SliceImpl(in start, in end2);
                }
            }
            return this.SliceImpl(in start, new SequencePosition(@object, (int)index1 + (int)length));
        }

        public ReadOnlySequence<T> Slice(int start, int length) => this.Slice((long)start, (long)length);

        public ReadOnlySequence<T> Slice(int start, SequencePosition end) => this.Slice((long)start, end);

        public ReadOnlySequence<T> Slice(SequencePosition start, int length) => this.Slice(start, (long)length);

        [MethodImpl((MethodImplOptions)256)]
        public ReadOnlySequence<T> Slice(SequencePosition start, SequencePosition end)
        {
            this.BoundsCheck((uint)ReadOnlySequence<T>.GetIndex(in start), start.GetObject(), (uint)ReadOnlySequence<T>.GetIndex(in end), end.GetObject());
            return this.SliceImpl(in start, in end);
        }

        [MethodImpl((MethodImplOptions)256)]
        public ReadOnlySequence<T> Slice(SequencePosition start)
        {
            this.BoundsCheck(in start);
            return this.SliceImpl(in start, in this._sequenceEnd);
        }

        public ReadOnlySequence<T> Slice(long start)
        {
            if (start < 0L)
                ThrowHelper.ThrowStartOrEndArgumentValidationException(start);
            return start == 0L ? this : this.SliceImpl(this.Seek(in this._sequenceStart, in this._sequenceEnd, start, ExceptionArgument.start), in this._sequenceEnd);
        }

        public override string ToString()
        {
            if (typeof(T) == typeof(char))
            {
                ReadOnlySequence<T> source = this;
                ReadOnlySequence<char> sequence = Unsafe.As<ReadOnlySequence<T>, ReadOnlySequence<char>>(ref source);
                string text;
                int start;
                int length;
                if (SequenceMarshal.TryGetString(sequence, out text, out start, out length))
                    return text.Substring(start, length);
                if (this.Length < (long)int.MaxValue)
                    return new string(sequence.ToArray<char>());
            }
            return string.Format("System.Buffers.ReadOnlySequence<{0}>[{1}]", (object)typeof(T).Name, (object)this.Length);
        }

        public ReadOnlySequence<T>.Enumerator GetEnumerator() => new ReadOnlySequence<T>.Enumerator(in this);

        public SequencePosition GetPosition(long offset) => this.GetPosition(offset, this._sequenceStart);

        public SequencePosition GetPosition(long offset, SequencePosition origin)
        {
            if (offset < 0L)
                ThrowHelper.ThrowArgumentOutOfRangeException_OffsetOutOfRange();
            return this.Seek(in origin, in this._sequenceEnd, offset, ExceptionArgument.offset);
        }

        public bool TryGet(ref SequencePosition position, out ReadOnlyMemory<T> memory, bool advance = true)
        {
            SequencePosition next;
            bool buffer = this.TryGetBuffer(in position, out memory, out next);
            if (advance)
                position = next;
            return buffer;
        }

        [MethodImpl((MethodImplOptions)256)]
        internal bool TryGetBuffer(
          in SequencePosition position,
          out ReadOnlyMemory<T> memory,
          out SequencePosition next)
        {
            object obj1 = position.GetObject();
            next = new SequencePosition();
            if (obj1 == null)
            {
                memory = new ReadOnlyMemory<T>();
                return false;
            }
            ReadOnlySequence<T>.SequenceType sequenceType = this.GetSequenceType();
            object obj2 = this._sequenceEnd.GetObject();
            int index1 = ReadOnlySequence<T>.GetIndex(in position);
            int index2 = ReadOnlySequence<T>.GetIndex(in this._sequenceEnd);
            if (sequenceType == ReadOnlySequence<T>.SequenceType.MultiSegment)
            {
                ReadOnlySequenceSegment<T> onlySequenceSegment = (ReadOnlySequenceSegment<T>)obj1;
                if (onlySequenceSegment != obj2)
                {
                    ReadOnlySequenceSegment<T> next1 = onlySequenceSegment.Next;
                    if (next1 == null)
                        ThrowHelper.ThrowInvalidOperationException_EndPositionNotReached();
                    next = new SequencePosition((object)next1, 0);
                    memory = onlySequenceSegment.Memory.Slice(index1);
                }
                else
                    memory = onlySequenceSegment.Memory.Slice(index1, index2 - index1);
            }
            else
            {
                if (obj1 != obj2)
                    ThrowHelper.ThrowInvalidOperationException_EndPositionNotReached();
                memory = sequenceType != ReadOnlySequence<T>.SequenceType.Array ? (!(typeof(T) == typeof(char)) || sequenceType != ReadOnlySequence<T>.SequenceType.String ? (ReadOnlyMemory<T>)((MemoryManager<T>)obj1).Memory.Slice(index1, index2 - index1) : (ReadOnlyMemory<T>)(ValueType)((string)obj1).AsMemory(index1, index2 - index1)) : new ReadOnlyMemory<T>((T[])obj1, index1, index2 - index1);
            }
            return true;
        }

        [MethodImpl((MethodImplOptions)256)]
        private ReadOnlyMemory<T> GetFirstBuffer()
        {
            object obj = this._sequenceStart.GetObject();
            if (obj == null)
                return new ReadOnlyMemory<T>();
            int integer1 = this._sequenceStart.GetInteger();
            int integer2 = this._sequenceEnd.GetInteger();
            bool flag = obj != this._sequenceEnd.GetObject();
            if (integer1 >= 0)
            {
                if (integer2 >= 0)
                {
                    ReadOnlyMemory<T> memory = ((ReadOnlySequenceSegment<T>)obj).Memory;
                    return flag ? memory.Slice(integer1) : memory.Slice(integer1, integer2 - integer1);
                }
                if (flag)
                    ThrowHelper.ThrowInvalidOperationException_EndPositionNotReached();
                return new ReadOnlyMemory<T>((T[])obj, integer1, (integer2 & int.MaxValue) - integer1);
            }
            if (flag)
                ThrowHelper.ThrowInvalidOperationException_EndPositionNotReached();
            if (typeof(T) == typeof(char) && integer2 < 0)
                return (ReadOnlyMemory<T>)(ValueType)((string)obj).AsMemory(integer1 & int.MaxValue, integer2 - integer1);
            int start = integer1 & int.MaxValue;
            return (ReadOnlyMemory<T>)((MemoryManager<T>)obj).Memory.Slice(start, integer2 - start);
        }

        [MethodImpl((MethodImplOptions)256)]
        private SequencePosition Seek(
          in SequencePosition start,
          in SequencePosition end,
          long offset,
          ExceptionArgument argument)
        {
            int index1 = ReadOnlySequence<T>.GetIndex(in start);
            int index2 = ReadOnlySequence<T>.GetIndex(in end);
            object @object = start.GetObject();
            object endObject = end.GetObject();
            if (@object != endObject)
            {
                ReadOnlySequenceSegment<T> onlySequenceSegment = (ReadOnlySequenceSegment<T>)@object;
                int num = onlySequenceSegment.Memory.Length - index1;
                if ((long)num <= offset)
                {
                    if (num < 0)
                        ThrowHelper.ThrowArgumentOutOfRangeException_PositionOutOfRange();
                    return ReadOnlySequence<T>.SeekMultiSegment(onlySequenceSegment.Next, endObject, index2, offset - (long)num, argument);
                }
            }
            else if ((long)(index2 - index1) < offset)
                ThrowHelper.ThrowArgumentOutOfRangeException(argument);
            return new SequencePosition(@object, index1 + (int)offset);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static SequencePosition SeekMultiSegment(
          ReadOnlySequenceSegment<T> currentSegment,
          object endObject,
          int endIndex,
          long offset,
          ExceptionArgument argument)
        {
            for (; currentSegment != null && currentSegment != endObject; currentSegment = currentSegment.Next)
            {
                int length = currentSegment.Memory.Length;
                if ((long)length <= offset)
                    offset -= (long)length;
                else
                    goto label_6;
            }
            if (currentSegment == null || (long)endIndex < offset)
                ThrowHelper.ThrowArgumentOutOfRangeException(argument);
            label_6:
            return new SequencePosition((object)currentSegment, (int)offset);
        }

        private void BoundsCheck(in SequencePosition position)
        {
            uint index1 = (uint)ReadOnlySequence<T>.GetIndex(in position);
            uint index2 = (uint)ReadOnlySequence<T>.GetIndex(in this._sequenceStart);
            uint index3 = (uint)ReadOnlySequence<T>.GetIndex(in this._sequenceEnd);
            object obj1 = this._sequenceStart.GetObject();
            object obj2 = this._sequenceEnd.GetObject();
            if (obj1 == obj2)
            {
                if (ReadOnlySequence<T>.InRange(index1, index2, index3))
                    return;
                ThrowHelper.ThrowArgumentOutOfRangeException_PositionOutOfRange();
            }
            else
            {
                ulong start = (ulong)((ReadOnlySequenceSegment<T>)obj1).RunningIndex + (ulong)index2;
                if (ReadOnlySequence<T>.InRange((ulong)((ReadOnlySequenceSegment<T>)position.GetObject()).RunningIndex + (ulong)index1, start, (ulong)((ReadOnlySequenceSegment<T>)obj2).RunningIndex + (ulong)index3))
                    return;
                ThrowHelper.ThrowArgumentOutOfRangeException_PositionOutOfRange();
            }
        }

        private void BoundsCheck(
          uint sliceStartIndex,
          object sliceStartObject,
          uint sliceEndIndex,
          object sliceEndObject)
        {
            uint index1 = (uint)ReadOnlySequence<T>.GetIndex(in this._sequenceStart);
            uint index2 = (uint)ReadOnlySequence<T>.GetIndex(in this._sequenceEnd);
            object obj1 = this._sequenceStart.GetObject();
            object obj2 = this._sequenceEnd.GetObject();
            if (obj1 == obj2)
            {
                if (sliceStartObject == sliceEndObject && sliceStartObject == obj1 && sliceStartIndex <= sliceEndIndex && sliceStartIndex >= index1 && sliceEndIndex <= index2)
                    return;
                ThrowHelper.ThrowArgumentOutOfRangeException_PositionOutOfRange();
            }
            else
            {
                ulong num1 = (ulong)((ReadOnlySequenceSegment<T>)sliceStartObject).RunningIndex + (ulong)sliceStartIndex;
                ulong num2 = (ulong)((ReadOnlySequenceSegment<T>)sliceEndObject).RunningIndex + (ulong)sliceEndIndex;
                if (num1 > num2)
                    ThrowHelper.ThrowArgumentOutOfRangeException_PositionOutOfRange();
                if (num1 >= (ulong)((ReadOnlySequenceSegment<T>)obj1).RunningIndex + (ulong)index1 && num2 <= (ulong)((ReadOnlySequenceSegment<T>)obj2).RunningIndex + (ulong)index2)
                    return;
                ThrowHelper.ThrowArgumentOutOfRangeException_PositionOutOfRange();
            }
        }

        private static SequencePosition GetEndPosition(
          ReadOnlySequenceSegment<T> startSegment,
          object startObject,
          int startIndex,
          object endObject,
          int endIndex,
          long length)
        {
            int num = startSegment.Memory.Length - startIndex;
            if ((long)num > length)
                return new SequencePosition(startObject, startIndex + (int)length);
            if (num < 0)
                ThrowHelper.ThrowArgumentOutOfRangeException_PositionOutOfRange();
            return ReadOnlySequence<T>.SeekMultiSegment(startSegment.Next, endObject, endIndex, length - (long)num, ExceptionArgument.length);
        }

        [MethodImpl((MethodImplOptions)256)]
        private ReadOnlySequence<T>.SequenceType GetSequenceType()
        {
            return (SequenceType)(-(2 * (this._sequenceStart.GetInteger() >> 31) + (this._sequenceEnd.GetInteger() >> 31)));
        }

        [MethodImpl((MethodImplOptions)256)]
        private static int GetIndex(in SequencePosition position) => position.GetInteger() & int.MaxValue;

        [MethodImpl((MethodImplOptions)256)]
        private ReadOnlySequence<T> SliceImpl(in SequencePosition start, in SequencePosition end) => new ReadOnlySequence<T>(start.GetObject(), ReadOnlySequence<T>.GetIndex(in start) | this._sequenceStart.GetInteger() & int.MinValue, end.GetObject(), ReadOnlySequence<T>.GetIndex(in end) | this._sequenceEnd.GetInteger() & int.MinValue);

        [MethodImpl((MethodImplOptions)256)]
        private long GetLength()
        {
            int index1 = ReadOnlySequence<T>.GetIndex(in this._sequenceStart);
            int index2 = ReadOnlySequence<T>.GetIndex(in this._sequenceEnd);
            object obj1 = this._sequenceStart.GetObject();
            object obj2 = this._sequenceEnd.GetObject();
            if (obj1 == obj2)
                return (long)(index2 - index1);
            ReadOnlySequenceSegment<T> onlySequenceSegment = (ReadOnlySequenceSegment<T>)obj1;
            return ((ReadOnlySequenceSegment<T>)obj2).RunningIndex + (long)index2 - (onlySequenceSegment.RunningIndex + (long)index1);
        }

        internal bool TryGetReadOnlySequenceSegment(
          out ReadOnlySequenceSegment<T> startSegment,
          out int startIndex,
          out ReadOnlySequenceSegment<T> endSegment,
          out int endIndex)
        {
            object obj = this._sequenceStart.GetObject();
            if (obj == null || this.GetSequenceType() != ReadOnlySequence<T>.SequenceType.MultiSegment)
            {
                startSegment = (ReadOnlySequenceSegment<T>)null;
                startIndex = 0;
                endSegment = (ReadOnlySequenceSegment<T>)null;
                endIndex = 0;
                return false;
            }
            startSegment = (ReadOnlySequenceSegment<T>)obj;
            startIndex = ReadOnlySequence<T>.GetIndex(in this._sequenceStart);
            endSegment = (ReadOnlySequenceSegment<T>)this._sequenceEnd.GetObject();
            endIndex = ReadOnlySequence<T>.GetIndex(in this._sequenceEnd);
            return true;
        }

        internal bool TryGetArray(out ArraySegment<T> segment)
        {
            if (this.GetSequenceType() != ReadOnlySequence<T>.SequenceType.Array)
            {
                segment = new ArraySegment<T>();
                return false;
            }
            int index = ReadOnlySequence<T>.GetIndex(in this._sequenceStart);
            segment = new ArraySegment<T>((T[])this._sequenceStart.GetObject(), index, ReadOnlySequence<T>.GetIndex(in this._sequenceEnd) - index);
            return true;
        }

        internal bool TryGetString(out string text, out int start, out int length)
        {
            if (typeof(T) != typeof(char) || this.GetSequenceType() != ReadOnlySequence<T>.SequenceType.String)
            {
                start = 0;
                length = 0;
                text = (string)null;
                return false;
            }
            start = ReadOnlySequence<T>.GetIndex(in this._sequenceStart);
            length = ReadOnlySequence<T>.GetIndex(in this._sequenceEnd) - start;
            text = (string)this._sequenceStart.GetObject();
            return true;
        }

        private static bool InRange(uint value, uint start, uint end) => value - start <= end - start;

        private static bool InRange(ulong value, ulong start, ulong end) => value - start <= end - start;

        public struct Enumerator
        {
            private readonly ReadOnlySequence<T> _sequence;
            private SequencePosition _next;
            private ReadOnlyMemory<T> _currentMemory;

            public Enumerator(in ReadOnlySequence<T> sequence)
            {
                this._currentMemory = new ReadOnlyMemory<T>();
                this._next = sequence.Start;
                this._sequence = sequence;
            }

            public ReadOnlyMemory<T> Current => this._currentMemory;

            public bool MoveNext() => this._next.GetObject() != null && this._sequence.TryGet(ref this._next, out this._currentMemory);
        }

        private enum SequenceType
        {
            MultiSegment,
            Array,
            MemoryManager,
            String,
            Empty,
        }
    }
}
