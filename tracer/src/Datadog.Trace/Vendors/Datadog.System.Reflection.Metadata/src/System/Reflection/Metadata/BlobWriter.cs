﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.BlobWriter
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using System.IO;
using System.Runtime.InteropServices;
using Datadog.System.Collections.Immutable;
using Datadog.System.Reflection.Internal;


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
    public struct BlobWriter
  {

    #nullable disable
    private readonly byte[] _buffer;
    private readonly int _start;
    private readonly int _end;
    private int _position;

    public BlobWriter(int size)
      : this(new byte[size])
    {
    }


    #nullable enable
    public BlobWriter(byte[] buffer)
      : this(buffer, 0, buffer.Length)
    {
    }

    public BlobWriter(Blob blob)
      : this(blob.Buffer, blob.Start, blob.Length)
    {
    }

    public BlobWriter(byte[] buffer, int start, int count)
    {
      this._buffer = buffer;
      this._start = start;
      this._position = start;
      this._end = start + count;
    }

    internal bool IsDefault => this._buffer == null;

    /// <summary>
    /// Compares the current content of this writer with another one.
    /// </summary>
    public bool ContentEquals(BlobWriter other) => this.Length == other.Length && ByteSequenceComparer.Equals(this._buffer, this._start, other._buffer, other._start, this.Length);

    public int Offset
    {
      get => this._position - this._start;
      set
      {
        if (value < 0 || this._start > this._end - value)
          Throw.ValueArgumentOutOfRange();
        this._position = this._start + value;
      }
    }

    public int Length => this._end - this._start;

    public int RemainingBytes => this._end - this._position;

    public Blob Blob => new Blob(this._buffer, this._start, this.Length);

    public byte[] ToArray() => this.ToArray(0, this.Offset);

    /// <exception cref="T:System.ArgumentOutOfRangeException">Range specified by <paramref name="start" /> and <paramref name="byteCount" /> falls outside of the bounds of the buffer content.</exception>
    public byte[] ToArray(int start, int byteCount)
    {
      BlobUtilities.ValidateRange(this.Length, start, byteCount, nameof (byteCount));
      byte[] destinationArray = new byte[byteCount];
      Array.Copy((Array) this._buffer, this._start + start, (Array) destinationArray, 0, byteCount);
      return destinationArray;
    }

    public ImmutableArray<byte> ToImmutableArray() => this.ToImmutableArray(0, this.Offset);

    /// <exception cref="T:System.ArgumentOutOfRangeException">Range specified by <paramref name="start" /> and <paramref name="byteCount" /> falls outside of the bounds of the buffer content.</exception>
    public ImmutableArray<byte> ToImmutableArray(int start, int byteCount)
    {
      byte[] array = this.ToArray(start, byteCount);
      return ImmutableByteArrayInterop.DangerousCreateFromUnderlyingArray(ref array);
    }

    private int Advance(int value)
    {
      int position = this._position;
      if (position > this._end - value)
        Throw.OutOfBounds();
      this._position = position + value;
      return position;
    }

    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="byteCount" /> is negative.</exception>
    public unsafe void WriteBytes(byte value, int byteCount)
    {
      if (byteCount < 0)
        Throw.ArgumentOutOfRange(nameof (byteCount));
      int num = this.Advance(byteCount);
      fixed (byte* numPtr1 = this._buffer)
      {
        byte* numPtr2 = numPtr1 + num;
        for (int index = 0; index < byteCount; ++index)
          numPtr2[index] = value;
      }
    }

    /// <exception cref="T:System.ArgumentNullException"><paramref name="buffer" /> is null.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="byteCount" /> is negative.</exception>
    public unsafe void WriteBytes(byte* buffer, int byteCount)
    {
      if ((IntPtr) buffer == IntPtr.Zero)
        Throw.ArgumentNull(nameof (buffer));
      if (byteCount < 0)
        Throw.ArgumentOutOfRange(nameof (byteCount));
      this.WriteBytesUnchecked(buffer, byteCount);
    }


    #nullable disable
    private unsafe void WriteBytesUnchecked(byte* buffer, int byteCount)
    {
      int startIndex = this.Advance(byteCount);
      Marshal.Copy((IntPtr) (void*) buffer, this._buffer, startIndex, byteCount);
    }


    #nullable enable
    /// <exception cref="T:System.ArgumentNullException"><paramref name="source" /> is null.</exception>
    public void WriteBytes(BlobBuilder source)
    {
      if (source == null)
        Throw.ArgumentNull(nameof (source));
      source.WriteContentTo(ref this);
    }

    /// <exception cref="T:System.ArgumentNullException"><paramref name="source" /> is null.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="byteCount" /> is negative.</exception>
    public int WriteBytes(Stream source, int byteCount)
    {
      if (source == null)
        Throw.ArgumentNull(nameof (source));
      if (byteCount < 0)
        Throw.ArgumentOutOfRange(nameof (byteCount));
      int offset = this.Advance(byteCount);
      int num = source.TryReadAll(this._buffer, offset, byteCount);
      this._position = offset + num;
      return num;
    }

    /// <exception cref="T:System.ArgumentNullException"><paramref name="buffer" /> is null.</exception>
    public void WriteBytes(ImmutableArray<byte> buffer) => this.WriteBytes(buffer, 0, buffer.IsDefault ? 0 : buffer.Length);

    /// <exception cref="T:System.ArgumentNullException"><paramref name="buffer" /> is null.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">Range specified by <paramref name="start" /> and <paramref name="byteCount" /> falls outside of the bounds of the <paramref name="buffer" />.</exception>
    public void WriteBytes(ImmutableArray<byte> buffer, int start, int byteCount) => this.WriteBytes(ImmutableByteArrayInterop.DangerousGetUnderlyingArray(buffer), start, byteCount);

    /// <exception cref="T:System.ArgumentNullException"><paramref name="buffer" /> is null.</exception>
    public void WriteBytes(byte[] buffer) => this.WriteBytes(buffer, 0, buffer != null ? buffer.Length : 0);

    /// <exception cref="T:System.ArgumentNullException"><paramref name="buffer" /> is null.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">Range specified by <paramref name="start" /> and <paramref name="byteCount" /> falls outside of the bounds of the <paramref name="buffer" />.</exception>
    public unsafe void WriteBytes(byte[] buffer, int start, int byteCount)
    {
      if (buffer == null)
        Throw.ArgumentNull(nameof (buffer));
      BlobUtilities.ValidateRange(buffer.Length, start, byteCount, nameof (byteCount));
      if (buffer.Length == 0)
        return;
      fixed (byte* numPtr = &buffer[0])
        this.WriteBytes(numPtr + start, byteCount);
    }

    public void PadTo(int offset) => this.WriteBytes((byte) 0, offset - this.Offset);

    public void Align(int alignment)
    {
      int offset = this.Offset;
      this.WriteBytes((byte) 0, BitArithmetic.Align(offset, alignment) - offset);
    }

    public void WriteBoolean(bool value) => this.WriteByte(value ? (byte) 1 : (byte) 0);

    public void WriteByte(byte value) => this._buffer[this.Advance(1)] = value;

    public void WriteSByte(sbyte value) => this.WriteByte((byte) value);

    public void WriteDouble(double value) => this._buffer.WriteDouble(this.Advance(8), value);

    public void WriteSingle(float value) => this._buffer.WriteSingle(this.Advance(4), value);

    public void WriteInt16(short value) => this.WriteUInt16((ushort) value);

    public void WriteUInt16(ushort value) => this._buffer.WriteUInt16(this.Advance(2), value);

    public void WriteInt16BE(short value) => this.WriteUInt16BE((ushort) value);

    public void WriteUInt16BE(ushort value) => this._buffer.WriteUInt16BE(this.Advance(2), value);

    public void WriteInt32BE(int value) => this.WriteUInt32BE((uint) value);

    public void WriteUInt32BE(uint value) => this._buffer.WriteUInt32BE(this.Advance(4), value);

    public void WriteInt32(int value) => this.WriteUInt32((uint) value);

    public void WriteUInt32(uint value) => this._buffer.WriteUInt32(this.Advance(4), value);

    public void WriteInt64(long value) => this.WriteUInt64((ulong) value);

    public void WriteUInt64(ulong value) => this._buffer.WriteUInt64(this.Advance(8), value);

    public void WriteDecimal(Decimal value) => this._buffer.WriteDecimal(this.Advance(13), value);

    public void WriteGuid(Guid value) => this._buffer.WriteGuid(this.Advance(16), value);

    public void WriteDateTime(DateTime value) => this.WriteInt64(value.Ticks);

    /// <summary>
    /// Writes a reference to a heap (heap offset) or a table (row number).
    /// </summary>
    /// <param name="reference">Heap offset or table row number.</param>
    /// <param name="isSmall">True to encode the reference as 16-bit integer, false to encode as 32-bit integer.</param>
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
    public unsafe void WriteUTF16(char[] value)
    {
      if (value == null)
        Throw.ArgumentNull(nameof (value));
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
    public unsafe void WriteUTF16(string value)
    {
      if (value == null)
        Throw.ArgumentNull(nameof (value));
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
    public void WriteSerializedString(string? str)
    {
      if (str == null)
        this.WriteByte(byte.MaxValue);
      else
        this.WriteUTF8(str, 0, str.Length, true, true);
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
    /// <exception cref="T:System.ArgumentNullException"><paramref name="value" /> is null.</exception>
    public void WriteUTF8(string value, bool allowUnpairedSurrogates)
    {
      if (value == null)
        Throw.ArgumentNull(nameof (value));
      this.WriteUTF8(value, 0, value.Length, allowUnpairedSurrogates, false);
    }


    #nullable disable
    private unsafe void WriteUTF8(
      string str,
      int start,
      int length,
      bool allowUnpairedSurrogates,
      bool prependSize)
    {
      fixed (char* chPtr1 = str)
      {
        char* chPtr2 = chPtr1 + start;
        int utF8ByteCount = BlobUtilities.GetUTF8ByteCount(chPtr2, length);
        if (prependSize)
          this.WriteCompressedInteger(utF8ByteCount);
        this._buffer.WriteUTF8(this.Advance(utF8ByteCount), chPtr2, length, utF8ByteCount, allowUnpairedSurrogates);
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
    public void WriteCompressedSignedInteger(int value) => BlobWriterImpl.WriteCompressedSignedInteger(ref this, value);

    /// <summary>
    /// Implements compressed unsigned integer encoding as defined by ECMA-335-II chapter 23.2: Blobs and signatures.
    /// </summary>
    /// <remarks>
    /// If the value lies between 0 (0x00) and 127 (0x7F), inclusive,
    /// encode as a one-byte integer (bit 7 is clear, value held in bits 6 through 0).
    /// 
    /// If the value lies between 28 (0x80) and 214 - 1 (0x3FFF), inclusive,
    /// encode as a 2-byte integer with bit 15 set, bit 14 clear(value held in bits 13 through 0).
    /// 
    /// Otherwise, encode as a 4-byte integer, with bit 31 set, bit 30 set, bit 29 clear (value held in bits 28 through 0).
    /// </remarks>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="value" /> can't be represented as a compressed unsigned integer.</exception>
    public void WriteCompressedInteger(int value) => BlobWriterImpl.WriteCompressedInteger(ref this, (uint) value);


    #nullable enable
    /// <summary>
    /// Writes a constant value (see ECMA-335 Partition II section 22.9) at the current position.
    /// </summary>
    /// <exception cref="T:System.ArgumentException"><paramref name="value" /> is not of a constant type.</exception>
    public void WriteConstant(object? value) => BlobWriterImpl.WriteConstant(ref this, value);

    public void Clear() => this._position = this._start;
  }
}
