﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.BlobBuilder
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Datadog.System.Collections.Immutable;
using Datadog.System.Reflection.Internal;


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
    [DebuggerDisplay("{GetDebuggerDisplay(),nq}")]
  public class BlobBuilder
  {
    internal const int DefaultChunkSize = 256;
    internal const int MinChunkSize = 16;

    #nullable disable
    private BlobBuilder _nextOrPrevious;
    private int _previousLengthOrFrozenSuffixLengthDelta;
    private byte[] _buffer;
    private uint _length;
    private const uint IsFrozenMask = 2147483648;


    #nullable enable
    private BlobBuilder FirstChunk => this._nextOrPrevious._nextOrPrevious;

    private bool IsHead => ((int) this._length & int.MinValue) == 0;

    private int Length => (int) this._length & int.MaxValue;

    private uint FrozenLength => this._length | 2147483648U;

    public BlobBuilder(int capacity = 256)
    {
      if (capacity < 0)
        Throw.ArgumentOutOfRange(nameof (capacity));
      this._nextOrPrevious = this;
      this._buffer = new byte[Math.Max(16, capacity)];
    }

    protected virtual BlobBuilder AllocateChunk(int minimalSize) => new BlobBuilder(Math.Max(this._buffer.Length, minimalSize));

    protected virtual void FreeChunk()
    {
    }

    public void Clear()
    {
      if (!this.IsHead)
        Throw.InvalidOperationBuilderAlreadyLinked();
      BlobBuilder firstChunk = this.FirstChunk;
      if (firstChunk != this)
      {
        byte[] buffer = firstChunk._buffer;
        firstChunk._length = this.FrozenLength;
        firstChunk._buffer = this._buffer;
        this._buffer = buffer;
      }
      foreach (BlobBuilder chunk in this.GetChunks())
      {
        if (chunk != this)
        {
          chunk.ClearChunk();
          chunk.FreeChunk();
        }
      }
      this.ClearChunk();
    }

    protected void Free()
    {
      this.Clear();
      this.FreeChunk();
    }

    internal void ClearChunk()
    {
      this._length = 0U;
      this._previousLengthOrFrozenSuffixLengthDelta = 0;
      this._nextOrPrevious = this;
    }

    [Conditional("DEBUG")]
    private void CheckInvariants()
    {
      if (!this.IsHead)
        return;
      int num = 0;
      foreach (BlobBuilder chunk in this.GetChunks())
        num += chunk.Length;
    }

    public int Count => this._previousLengthOrFrozenSuffixLengthDelta + this.Length;

    private int PreviousLength
    {
      get => this._previousLengthOrFrozenSuffixLengthDelta;
      set => this._previousLengthOrFrozenSuffixLengthDelta = value;
    }

    protected int FreeBytes => this._buffer.Length - this.Length;

    protected internal int ChunkCapacity => this._buffer.Length;

    internal BlobBuilder.Chunks GetChunks()
    {
      if (!this.IsHead)
        Throw.InvalidOperationBuilderAlreadyLinked();
      return new BlobBuilder.Chunks(this);
    }

    /// <summary>
    /// Returns a sequence of all blobs that represent the content of the builder.
    /// </summary>
    /// <exception cref="T:System.InvalidOperationException">Content is not available, the builder has been linked with another one.</exception>
    public BlobBuilder.Blobs GetBlobs()
    {
      if (!this.IsHead)
        Throw.InvalidOperationBuilderAlreadyLinked();
      return new BlobBuilder.Blobs(this);
    }

    /// <summary>
    /// Compares the current content of this writer with another one.
    /// </summary>
    /// <exception cref="T:System.InvalidOperationException">Content is not available, the builder has been linked with another one.</exception>
    public bool ContentEquals(BlobBuilder other)
    {
      if (!this.IsHead)
        Throw.InvalidOperationBuilderAlreadyLinked();
      if (this == other)
        return true;
      if (other == null)
        return false;
      if (!other.IsHead)
        Throw.InvalidOperationBuilderAlreadyLinked();
      if (this.Count != other.Count)
        return false;
      BlobBuilder.Chunks chunks1 = this.GetChunks();
      BlobBuilder.Chunks chunks2 = other.GetChunks();
      int leftStart = 0;
      int rightStart = 0;
      bool flag1 = chunks1.MoveNext();
      bool flag2 = chunks2.MoveNext();
      while (flag1 & flag2)
      {
        BlobBuilder current1 = chunks1.Current;
        BlobBuilder current2 = chunks2.Current;
        int length = Math.Min(current1.Length - leftStart, current2.Length - rightStart);
        if (!ByteSequenceComparer.Equals(current1._buffer, leftStart, current2._buffer, rightStart, length))
          return false;
        leftStart += length;
        rightStart += length;
        if (leftStart == current1.Length)
        {
          flag1 = chunks1.MoveNext();
          leftStart = 0;
        }
        if (rightStart == current2.Length)
        {
          flag2 = chunks2.MoveNext();
          rightStart = 0;
        }
      }
      return flag1 == flag2;
    }

    /// <exception cref="T:System.InvalidOperationException">Content is not available, the builder has been linked with another one.</exception>
    public byte[] ToArray() => this.ToArray(0, this.Count);

    /// <exception cref="T:System.ArgumentOutOfRangeException">Range specified by <paramref name="start" /> and <paramref name="byteCount" /> falls outside of the bounds of the buffer content.</exception>
    /// <exception cref="T:System.InvalidOperationException">Content is not available, the builder has been linked with another one.</exception>
    public byte[] ToArray(int start, int byteCount)
    {
      BlobUtilities.ValidateRange(this.Count, start, byteCount, nameof (byteCount));
      byte[] destinationArray = new byte[byteCount];
      int num1 = 0;
      int num2 = start;
      int val1 = start + byteCount;
      foreach (BlobBuilder chunk in this.GetChunks())
      {
        int val2 = num1 + chunk.Length;
        if (val2 > num2)
        {
          int length = Math.Min(val1, val2) - num2;
          Array.Copy((Array) chunk._buffer, num2 - num1, (Array) destinationArray, num2 - start, length);
          num2 += length;
          if (num2 == val1)
            break;
        }
        num1 = val2;
      }
      return destinationArray;
    }

    /// <exception cref="T:System.InvalidOperationException">Content is not available, the builder has been linked with another one.</exception>
    public ImmutableArray<byte> ToImmutableArray() => this.ToImmutableArray(0, this.Count);

    /// <exception cref="T:System.ArgumentOutOfRangeException">Range specified by <paramref name="start" /> and <paramref name="byteCount" /> falls outside of the bounds of the buffer content.</exception>
    /// <exception cref="T:System.InvalidOperationException">Content is not available, the builder has been linked with another one.</exception>
    public ImmutableArray<byte> ToImmutableArray(int start, int byteCount)
    {
      byte[] array = this.ToArray(start, byteCount);
      return ImmutableByteArrayInterop.DangerousCreateFromUnderlyingArray(ref array);
    }

    /// <exception cref="T:System.ArgumentNullException"><paramref name="destination" /> is null.</exception>
    /// <exception cref="T:System.InvalidOperationException">Content is not available, the builder has been linked with another one.</exception>
    public void WriteContentTo(Stream destination)
    {
      if (destination == null)
        Throw.ArgumentNull(nameof (destination));
      foreach (BlobBuilder chunk in this.GetChunks())
        destination.Write(chunk._buffer, 0, chunk.Length);
    }

    /// <exception cref="T:System.ArgumentNullException"><paramref name="destination" /> is default(<see cref="T:System.Reflection.Metadata.BlobWriter" />).</exception>
    /// <exception cref="T:System.InvalidOperationException">Content is not available, the builder has been linked with another one.</exception>
    public void WriteContentTo(ref BlobWriter destination)
    {
      if (destination.IsDefault)
        Throw.ArgumentNull(nameof (destination));
      foreach (BlobBuilder chunk in this.GetChunks())
        destination.WriteBytes(chunk._buffer, 0, chunk.Length);
    }

    /// <exception cref="T:System.ArgumentNullException"><paramref name="destination" /> is null.</exception>
    /// <exception cref="T:System.InvalidOperationException">Content is not available, the builder has been linked with another one.</exception>
    public void WriteContentTo(BlobBuilder destination)
    {
      if (destination == null)
        Throw.ArgumentNull(nameof (destination));
      foreach (BlobBuilder chunk in this.GetChunks())
        destination.WriteBytes(chunk._buffer, 0, chunk.Length);
    }

    /// <exception cref="T:System.ArgumentNullException"><paramref name="prefix" /> is null.</exception>
    /// <exception cref="T:System.InvalidOperationException">Builder is not writable, it has been linked with another one.</exception>
    public void LinkPrefix(BlobBuilder prefix)
    {
      if (prefix == null)
        Throw.ArgumentNull(nameof (prefix));
      if (!prefix.IsHead || !this.IsHead)
        Throw.InvalidOperationBuilderAlreadyLinked();
      if (prefix.Count == 0)
        return;
      this.PreviousLength += prefix.Count;
      prefix._length = prefix.FrozenLength;
      BlobBuilder firstChunk1 = this.FirstChunk;
      BlobBuilder firstChunk2 = prefix.FirstChunk;
      BlobBuilder nextOrPrevious1 = this._nextOrPrevious;
      BlobBuilder nextOrPrevious2 = prefix._nextOrPrevious;
      this._nextOrPrevious = nextOrPrevious1 != this ? nextOrPrevious1 : prefix;
      prefix._nextOrPrevious = firstChunk1 != this ? firstChunk1 : (firstChunk2 != prefix ? firstChunk2 : prefix);
      if (nextOrPrevious1 != this)
        nextOrPrevious1._nextOrPrevious = firstChunk2 != prefix ? firstChunk2 : prefix;
      if (nextOrPrevious2 == prefix)
        return;
      nextOrPrevious2._nextOrPrevious = prefix;
    }

    /// <exception cref="T:System.ArgumentNullException"><paramref name="suffix" /> is null.</exception>
    /// <exception cref="T:System.InvalidOperationException">Builder is not writable, it has been linked with another one.</exception>
    public void LinkSuffix(BlobBuilder suffix)
    {
      if (suffix == null)
        Throw.ArgumentNull(nameof (suffix));
      if (!this.IsHead || !suffix.IsHead)
        Throw.InvalidOperationBuilderAlreadyLinked();
      if (suffix.Count == 0)
        return;
      bool flag = this.Count == 0;
      byte[] buffer = suffix._buffer;
      uint length1 = suffix._length;
      int previousLength = suffix.PreviousLength;
      int length2 = suffix.Length;
      suffix._buffer = this._buffer;
      suffix._length = this.FrozenLength;
      this._buffer = buffer;
      this._length = length1;
      this.PreviousLength += suffix.Length + previousLength;
      suffix._previousLengthOrFrozenSuffixLengthDelta = previousLength + length2 - suffix.Length;
      if (flag)
        return;
      BlobBuilder firstChunk1 = this.FirstChunk;
      BlobBuilder firstChunk2 = suffix.FirstChunk;
      BlobBuilder nextOrPrevious1 = this._nextOrPrevious;
      BlobBuilder nextOrPrevious2 = suffix._nextOrPrevious;
      this._nextOrPrevious = nextOrPrevious2;
      suffix._nextOrPrevious = firstChunk2 != suffix ? firstChunk2 : (firstChunk1 != this ? firstChunk1 : suffix);
      if (nextOrPrevious1 != this)
        nextOrPrevious1._nextOrPrevious = suffix;
      if (nextOrPrevious2 == suffix)
        return;
      nextOrPrevious2._nextOrPrevious = firstChunk1 != this ? firstChunk1 : suffix;
    }

    private void AddLength(int value) => this._length += (uint) value;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Expand(int newLength)
    {
      if (!this.IsHead)
        Throw.InvalidOperationBuilderAlreadyLinked();
      BlobBuilder blobBuilder = this.AllocateChunk(Math.Max(newLength, 16));
      if (blobBuilder.ChunkCapacity < newLength)
        throw new InvalidOperationException(SR.Format(SR.ReturnedBuilderSizeTooSmall, (object) this.GetType(), (object) "AllocateChunk"));
      byte[] buffer = blobBuilder._buffer;
      if (this._length == 0U)
      {
        blobBuilder._buffer = this._buffer;
        this._buffer = buffer;
      }
      else
      {
        BlobBuilder nextOrPrevious = this._nextOrPrevious;
        BlobBuilder firstChunk = this.FirstChunk;
        if (nextOrPrevious == this)
        {
          this._nextOrPrevious = blobBuilder;
        }
        else
        {
          blobBuilder._nextOrPrevious = firstChunk;
          nextOrPrevious._nextOrPrevious = blobBuilder;
          this._nextOrPrevious = blobBuilder;
        }
        blobBuilder._buffer = this._buffer;
        blobBuilder._length = this.FrozenLength;
        blobBuilder._previousLengthOrFrozenSuffixLengthDelta = this.PreviousLength;
        this._buffer = buffer;
        this.PreviousLength += this.Length;
        this._length = 0U;
      }
    }

    /// <summary>Reserves a contiguous block of bytes.</summary>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="byteCount" /> is negative.</exception>
    /// <exception cref="T:System.InvalidOperationException">Builder is not writable, it has been linked with another one.</exception>
    public Blob ReserveBytes(int byteCount)
    {
      if (byteCount < 0)
        Throw.ArgumentOutOfRange(nameof (byteCount));
      return new Blob(this._buffer, this.ReserveBytesImpl(byteCount), byteCount);
    }

    private int ReserveBytesImpl(int byteCount)
    {
      uint num = this._length;
      if ((long) num > (long) (this._buffer.Length - byteCount))
      {
        this.Expand(byteCount);
        num = 0U;
      }
      this._length = num + (uint) byteCount;
      return (int) num;
    }

    private int ReserveBytesPrimitive(int byteCount) => this.ReserveBytesImpl(byteCount);

    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="byteCount" /> is negative.</exception>
    /// <exception cref="T:System.InvalidOperationException">Builder is not writable, it has been linked with another one.</exception>
    public void WriteBytes(byte value, int byteCount)
    {
      if (byteCount < 0)
        Throw.ArgumentOutOfRange(nameof (byteCount));
      if (!this.IsHead)
        Throw.InvalidOperationBuilderAlreadyLinked();
      int byteCount1 = Math.Min(this.FreeBytes, byteCount);
      this._buffer.WriteBytes(this.Length, value, byteCount1);
      this.AddLength(byteCount1);
      int num = byteCount - byteCount1;
      if (num <= 0)
        return;
      this.Expand(num);
      this._buffer.WriteBytes(0, value, num);
      this.AddLength(num);
    }

    /// <exception cref="T:System.ArgumentNullException"><paramref name="buffer" /> is null.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="byteCount" /> is negative.</exception>
    /// <exception cref="T:System.InvalidOperationException">Builder is not writable, it has been linked with another one.</exception>
    public unsafe void WriteBytes(byte* buffer, int byteCount)
    {
      if ((IntPtr) buffer == IntPtr.Zero)
        Throw.ArgumentNull(nameof (buffer));
      if (byteCount < 0)
        Throw.ArgumentOutOfRange(nameof (byteCount));
      if (!this.IsHead)
        Throw.InvalidOperationBuilderAlreadyLinked();
      this.WriteBytesUnchecked(buffer, byteCount);
    }


    #nullable disable
    private unsafe void WriteBytesUnchecked(byte* buffer, int byteCount)
    {
      int length = Math.Min(this.FreeBytes, byteCount);
      Marshal.Copy((IntPtr) (void*) buffer, this._buffer, this.Length, length);
      this.AddLength(length);
      int num = byteCount - length;
      if (num <= 0)
        return;
      this.Expand(num);
      Marshal.Copy((IntPtr) (void*) (buffer + length), this._buffer, 0, num);
      this.AddLength(num);
    }


    #nullable enable
    /// <exception cref="T:System.ArgumentNullException"><paramref name="source" /> is null.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="byteCount" /> is negative.</exception>
    /// <exception cref="T:System.InvalidOperationException">Builder is not writable, it has been linked with another one.</exception>
    /// <returns>Bytes successfully written from the <paramref name="source" />.</returns>
    public int TryWriteBytes(Stream source, int byteCount)
    {
      if (source == null)
        Throw.ArgumentNull(nameof (source));
      if (byteCount < 0)
        throw new ArgumentOutOfRangeException(nameof (byteCount));
      if (byteCount == 0)
        return 0;
      int num1 = 0;
      int count = Math.Min(this.FreeBytes, byteCount);
      if (count > 0)
      {
        num1 = source.TryReadAll(this._buffer, this.Length, count);
        this.AddLength(num1);
        if (num1 != count)
          return num1;
      }
      int num2 = byteCount - count;
      if (num2 > 0)
      {
        this.Expand(num2);
        int num3 = source.TryReadAll(this._buffer, 0, num2);
        this.AddLength(num3);
        num1 = num3 + count;
      }
      return num1;
    }

    /// <exception cref="T:System.ArgumentNullException"><paramref name="buffer" /> is null.</exception>
    /// <exception cref="T:System.InvalidOperationException">Builder is not writable, it has been linked with another one.</exception>
    public void WriteBytes(ImmutableArray<byte> buffer) => this.WriteBytes(buffer, 0, buffer.IsDefault ? 0 : buffer.Length);

    /// <exception cref="T:System.ArgumentNullException"><paramref name="buffer" /> is null.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">Range specified by <paramref name="start" /> and <paramref name="byteCount" /> falls outside of the bounds of the <paramref name="buffer" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">Builder is not writable, it has been linked with another one.</exception>
    public void WriteBytes(ImmutableArray<byte> buffer, int start, int byteCount) => this.WriteBytes(ImmutableByteArrayInterop.DangerousGetUnderlyingArray(buffer), start, byteCount);

    /// <exception cref="T:System.ArgumentNullException"><paramref name="buffer" /> is null.</exception>
    /// <exception cref="T:System.InvalidOperationException">Builder is not writable, it has been linked with another one.</exception>
    public void WriteBytes(byte[] buffer) => this.WriteBytes(buffer, 0, buffer != null ? buffer.Length : 0);

    /// <exception cref="T:System.ArgumentNullException"><paramref name="buffer" /> is null.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">Range specified by <paramref name="start" /> and <paramref name="byteCount" /> falls outside of the bounds of the <paramref name="buffer" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">Builder is not writable, it has been linked with another one.</exception>
    public unsafe void WriteBytes(byte[] buffer, int start, int byteCount)
    {
      if (buffer == null)
        Throw.ArgumentNull(nameof (buffer));
      BlobUtilities.ValidateRange(buffer.Length, start, byteCount, nameof (byteCount));
      if (!this.IsHead)
        Throw.InvalidOperationBuilderAlreadyLinked();
      if (buffer.Length == 0)
        return;
      fixed (byte* numPtr = &buffer[0])
        this.WriteBytesUnchecked(numPtr + start, byteCount);
    }

    /// <exception cref="T:System.InvalidOperationException">Builder is not writable, it has been linked with another one.</exception>
    public void PadTo(int position) => this.WriteBytes((byte) 0, position - this.Count);

    /// <exception cref="T:System.InvalidOperationException">Builder is not writable, it has been linked with another one.</exception>
    public void Align(int alignment)
    {
      int count = this.Count;
      this.WriteBytes((byte) 0, BitArithmetic.Align(count, alignment) - count);
    }

    /// <exception cref="T:System.InvalidOperationException">Builder is not writable, it has been linked with another one.</exception>
    public void WriteBoolean(bool value) => this.WriteByte(value ? (byte) 1 : (byte) 0);

    /// <exception cref="T:System.InvalidOperationException">Builder is not writable, it has been linked with another one.</exception>
    public void WriteByte(byte value) => this._buffer.WriteByte(this.ReserveBytesPrimitive(1), value);

    /// <exception cref="T:System.InvalidOperationException">Builder is not writable, it has been linked with another one.</exception>
    public void WriteSByte(sbyte value) => this.WriteByte((byte) value);

    /// <exception cref="T:System.InvalidOperationException">Builder is not writable, it has been linked with another one.</exception>
    public void WriteDouble(double value) => this._buffer.WriteDouble(this.ReserveBytesPrimitive(8), value);

    /// <exception cref="T:System.InvalidOperationException">Builder is not writable, it has been linked with another one.</exception>
    public void WriteSingle(float value) => this._buffer.WriteSingle(this.ReserveBytesPrimitive(4), value);

    /// <exception cref="T:System.InvalidOperationException">Builder is not writable, it has been linked with another one.</exception>
    public void WriteInt16(short value) => this.WriteUInt16((ushort) value);

    /// <exception cref="T:System.InvalidOperationException">Builder is not writable, it has been linked with another one.</exception>
    public void WriteUInt16(ushort value) => this._buffer.WriteUInt16(this.ReserveBytesPrimitive(2), value);

    /// <exception cref="T:System.InvalidOperationException">Builder is not writable, it has been linked with another one.</exception>
    public void WriteInt16BE(short value) => this.WriteUInt16BE((ushort) value);

    /// <exception cref="T:System.InvalidOperationException">Builder is not writable, it has been linked with another one.</exception>
    public void WriteUInt16BE(ushort value) => this._buffer.WriteUInt16BE(this.ReserveBytesPrimitive(2), value);

    /// <exception cref="T:System.InvalidOperationException">Builder is not writable, it has been linked with another one.</exception>
    public void WriteInt32BE(int value) => this.WriteUInt32BE((uint) value);

    /// <exception cref="T:System.InvalidOperationException">Builder is not writable, it has been linked with another one.</exception>
    public void WriteUInt32BE(uint value) => this._buffer.WriteUInt32BE(this.ReserveBytesPrimitive(4), value);

    /// <exception cref="T:System.InvalidOperationException">Builder is not writable, it has been linked with another one.</exception>
    public void WriteInt32(int value) => this.WriteUInt32((uint) value);

    /// <exception cref="T:System.InvalidOperationException">Builder is not writable, it has been linked with another one.</exception>
    public void WriteUInt32(uint value) => this._buffer.WriteUInt32(this.ReserveBytesPrimitive(4), value);

    /// <exception cref="T:System.InvalidOperationException">Builder is not writable, it has been linked with another one.</exception>
    public void WriteInt64(long value) => this.WriteUInt64((ulong) value);

    /// <exception cref="T:System.InvalidOperationException">Builder is not writable, it has been linked with another one.</exception>
    public void WriteUInt64(ulong value) => this._buffer.WriteUInt64(this.ReserveBytesPrimitive(8), value);

    /// <exception cref="T:System.InvalidOperationException">Builder is not writable, it has been linked with another one.</exception>
    public void WriteDecimal(Decimal value) => this._buffer.WriteDecimal(this.ReserveBytesPrimitive(13), value);

    /// <exception cref="T:System.InvalidOperationException">Builder is not writable, it has been linked with another one.</exception>
    public void WriteGuid(Guid value) => this._buffer.WriteGuid(this.ReserveBytesPrimitive(16), value);

    /// <exception cref="T:System.InvalidOperationException">Builder is not writable, it has been linked with another one.</exception>
    public void WriteDateTime(DateTime value) => this.WriteInt64(value.Ticks);

    /// <summary>
    /// Writes a reference to a heap (heap offset) or a table (row number).
    /// </summary>
    /// <param name="reference">Heap offset or table row number.</param>
    /// <param name="isSmall">True to encode the reference as 16-bit integer, false to encode as 32-bit integer.</param>
    /// <exception cref="T:System.InvalidOperationException">Builder is not writable, it has been linked with another one.</exception>
    public void WriteReference(int reference, bool isSmall)
    {
      if (isSmall)
        this.WriteUInt16((ushort) reference);
      else
        this.WriteInt32(reference);
    }

    /// <summary>
    /// Writes UTF16 (little-endian) encoded string at the current position.
    /// </summary>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="value" /> is null.</exception>
    /// <exception cref="T:System.InvalidOperationException">Builder is not writable, it has been linked with another one.</exception>
    public unsafe void WriteUTF16(char[] value)
    {
      if (value == null)
        Throw.ArgumentNull(nameof (value));
      if (!this.IsHead)
        Throw.InvalidOperationBuilderAlreadyLinked();
      if (value.Length == 0)
        return;
      if (BitConverter.IsLittleEndian)
      {
        fixed (char* buffer = &value[0])
          this.WriteBytesUnchecked((byte*) buffer, value.Length * 2);
      }
      else
      {
        for (int index = 0; index < value.Length; ++index)
          this.WriteUInt16((ushort) value[index]);
      }
    }

    /// <summary>
    /// Writes UTF16 (little-endian) encoded string at the current position.
    /// </summary>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="value" /> is null.</exception>
    /// <exception cref="T:System.InvalidOperationException">Builder is not writable, it has been linked with another one.</exception>
    public unsafe void WriteUTF16(string value)
    {
      if (value == null)
        Throw.ArgumentNull(nameof (value));
      if (!this.IsHead)
        Throw.InvalidOperationBuilderAlreadyLinked();
      if (BitConverter.IsLittleEndian)
      {
        fixed (char* buffer = value)
          this.WriteBytesUnchecked((byte*) buffer, value.Length * 2);
      }
      else
      {
        for (int index = 0; index < value.Length; ++index)
          this.WriteUInt16((ushort) value[index]);
      }
    }

    /// <summary>
    /// Writes string in SerString format (see ECMA-335-II 23.3 Custom attributes).
    /// </summary>
    /// <remarks>
    /// The string is UTF8 encoded and prefixed by the its size in bytes.
    /// Null string is represented as a single byte 0xFF.
    /// </remarks>
    /// <exception cref="T:System.InvalidOperationException">Builder is not writable, it has been linked with another one.</exception>
    public void WriteSerializedString(string? value)
    {
      if (value == null)
        this.WriteByte(byte.MaxValue);
      else
        this.WriteUTF8(value, 0, value.Length, true, true);
    }

    /// <summary>
    /// Writes string in User String (#US) heap format (see ECMA-335-II 24.2.4 #US and #Blob heaps):
    /// </summary>
    /// <remarks>
    /// The string is UTF16 encoded and prefixed by the its size in bytes.
    /// 
    /// This final byte holds the value 1 if and only if any UTF16 character within the string has any bit set in its top byte,
    /// or its low byte is any of the following: 0x01-0x08, 0x0E-0x1F, 0x27, 0x2D, 0x7F. Otherwise, it holds 0.
    /// The 1 signifies Unicode characters that require handling beyond that normally provided for 8-bit encoding sets.
    /// </remarks>
    /// <exception cref="T:System.InvalidOperationException">Builder is not writable, it has been linked with another one.</exception>
    public void WriteUserString(string value)
    {
      if (value == null)
        Throw.ArgumentNull(nameof (value));
      this.WriteCompressedInteger(BlobUtilities.GetUserStringByteLength(value.Length));
      this.WriteUTF16(value);
      this.WriteByte(BlobUtilities.GetUserStringTrailingByte(value));
    }

    /// <summary>Writes UTF8 encoded string at the current position.</summary>
    /// <param name="value">Constant value.</param>
    /// <param name="allowUnpairedSurrogates">
    /// True to encode unpaired surrogates as specified, otherwise replace them with U+FFFD character.
    /// </param>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="value" /> is null.</exception>
    /// <exception cref="T:System.InvalidOperationException">Builder is not writable, it has been linked with another one.</exception>
    public void WriteUTF8(string value, bool allowUnpairedSurrogates = true)
    {
      if (value == null)
        Throw.ArgumentNull(nameof (value));
      this.WriteUTF8(value, 0, value.Length, allowUnpairedSurrogates, false);
    }

    internal unsafe void WriteUTF8(
      string str,
      int start,
      int length,
      bool allowUnpairedSurrogates,
      bool prependSize)
    {
      if (!this.IsHead)
        Throw.InvalidOperationBuilderAlreadyLinked();
      fixed (char* chPtr1 = str)
      {
        char* chPtr2 = chPtr1 + start;
        int byteLimit = this.FreeBytes - (prependSize ? 4 : 0);
        char* remainder;
        int utF8ByteCount1 = BlobUtilities.GetUTF8ByteCount(chPtr2, length, byteLimit, out remainder);
        int charCount1 = (int) (remainder - chPtr2);
        int charCount2 = length - charCount1;
        int utF8ByteCount2 = BlobUtilities.GetUTF8ByteCount(remainder, charCount2);
        if (prependSize)
          this.WriteCompressedInteger(utF8ByteCount1 + utF8ByteCount2);
        this._buffer.WriteUTF8(this.Length, chPtr2, charCount1, utF8ByteCount1, allowUnpairedSurrogates);
        this.AddLength(utF8ByteCount1);
        if (utF8ByteCount2 > 0)
        {
          this.Expand(utF8ByteCount2);
          this._buffer.WriteUTF8(0, remainder, charCount2, utF8ByteCount2, allowUnpairedSurrogates);
          this.AddLength(utF8ByteCount2);
        }
      }
    }

    /// <summary>
    /// Implements compressed signed integer encoding as defined by ECMA-335-II chapter 23.2: Blobs and signatures.
    /// </summary>
    /// <remarks>
    /// If the value lies between -64 (0xFFFFFFC0) and 63 (0x3F), inclusive, encode as a one-byte integer:
    /// bit 7 clear, value bits 5 through 0 held in bits 6 through 1, sign bit (value bit 31) in bit 0.
    /// 
    /// If the value lies between -8192 (0xFFFFE000) and 8191 (0x1FFF), inclusive, encode as a two-byte integer:
    /// 15 set, bit 14 clear, value bits 12 through 0 held in bits 13 through 1, sign bit(value bit 31) in bit 0.
    /// 
    /// If the value lies between -268435456 (0xF000000) and 268435455 (0x0FFFFFFF), inclusive, encode as a four-byte integer:
    /// 31 set, 30 set, bit 29 clear, value bits 27 through 0 held in bits 28 through 1, sign bit(value bit 31) in bit 0.
    /// </remarks>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="value" /> can't be represented as a compressed signed integer.</exception>
    /// <exception cref="T:System.InvalidOperationException">Builder is not writable, it has been linked with another one.</exception>
    public void WriteCompressedSignedInteger(int value) => BlobWriterImpl.WriteCompressedSignedInteger(this, value);

    /// <summary>
    /// Implements compressed unsigned integer encoding as defined by ECMA-335-II chapter 23.2: Blobs and signatures.
    /// </summary>
    /// <remarks>
    /// If the value lies between 0 (0x00) and 127 (0x7F), inclusive,
    /// encode as a one-byte integer (bit 7 is clear, value held in bits 6 through 0).
    /// 
    /// If the value lies between 28 (0x80) and 214 - 1 (0x3FFF), inclusive,
    /// encode as a 2-byte integer with bit 15 set, bit 14 clear (value held in bits 13 through 0).
    /// 
    /// Otherwise, encode as a 4-byte integer, with bit 31 set, bit 30 set, bit 29 clear (value held in bits 28 through 0).
    /// </remarks>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="value" /> can't be represented as a compressed unsigned integer.</exception>
    /// <exception cref="T:System.InvalidOperationException">Builder is not writable, it has been linked with another one.</exception>
    public void WriteCompressedInteger(int value) => BlobWriterImpl.WriteCompressedInteger(this, (uint) value);

    /// <summary>
    /// Writes a constant value (see ECMA-335 Partition II section 22.9) at the current position.
    /// </summary>
    /// <exception cref="T:System.ArgumentException"><paramref name="value" /> is not of a constant type.</exception>
    /// <exception cref="T:System.InvalidOperationException">Builder is not writable, it has been linked with another one.</exception>
    public void WriteConstant(object? value) => BlobWriterImpl.WriteConstant(this, value);

    internal string GetDebuggerDisplay() => !this.IsHead ? "<" + BlobBuilder.Display(this._buffer, this.Length) + ">" : string.Join("->", this.GetChunks().Select<BlobBuilder, string>((Func<BlobBuilder, string>) (chunk => "[" + BlobBuilder.Display(chunk._buffer, chunk.Length) + "]")));


    #nullable disable
    private static string Display(byte[] bytes, int length) => length > 64 ? BitConverter.ToString(bytes, 0, 32) + "-...-" + BitConverter.ToString(bytes, length - 32, 32) : BitConverter.ToString(bytes, 0, length);


    #nullable enable
    internal struct Chunks : 
      IEnumerable<BlobBuilder>,
      IEnumerable,
      IEnumerator<BlobBuilder>,
      IDisposable,
      IEnumerator
    {

      #nullable disable
      private readonly BlobBuilder _head;
      private BlobBuilder _next;
      private BlobBuilder _currentOpt;


      #nullable enable
      internal Chunks(BlobBuilder builder)
      {
        this._head = builder;
        this._next = builder.FirstChunk;
        this._currentOpt = (BlobBuilder) null;
      }

      object IEnumerator.Current => (object) this.Current;

      public BlobBuilder Current => this._currentOpt;

      public bool MoveNext()
      {
        if (this._currentOpt == this._head)
          return false;
        if (this._currentOpt == this._head._nextOrPrevious)
        {
          this._currentOpt = this._head;
          return true;
        }
        this._currentOpt = this._next;
        this._next = this._next._nextOrPrevious;
        return true;
      }

      public void Reset()
      {
        this._currentOpt = (BlobBuilder) null;
        this._next = this._head.FirstChunk;
      }

      void IDisposable.Dispose()
      {
      }

      public BlobBuilder.Chunks GetEnumerator() => this;


      #nullable disable
      IEnumerator<BlobBuilder> IEnumerable<BlobBuilder>.GetEnumerator() => (IEnumerator<BlobBuilder>) this.GetEnumerator();

      IEnumerator IEnumerable.GetEnumerator() => (IEnumerator) this.GetEnumerator();
    }


    #nullable enable
    public struct Blobs : IEnumerable<Blob>, IEnumerable, IEnumerator<Blob>, IDisposable, IEnumerator
    {
      private BlobBuilder.Chunks _chunks;

      internal Blobs(BlobBuilder builder) => this._chunks = new BlobBuilder.Chunks(builder);

      object IEnumerator.Current => (object) this.Current;

      public Blob Current
      {
        get
        {
          BlobBuilder current = this._chunks.Current;
          return current != null ? new Blob(current._buffer, 0, current.Length) : new Blob();
        }
      }

      public bool MoveNext() => this._chunks.MoveNext();

      public void Reset() => this._chunks.Reset();

      void IDisposable.Dispose()
      {
      }

      public BlobBuilder.Blobs GetEnumerator() => this;


      #nullable disable
      IEnumerator<Blob> IEnumerable<Blob>.GetEnumerator() => (IEnumerator<Blob>) this.GetEnumerator();

      IEnumerator IEnumerable.GetEnumerator() => (IEnumerator) this.GetEnumerator();
    }
  }
}
