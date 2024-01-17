﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.BlobReader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Datadog.System.Reflection.Internal;


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
    [DebuggerDisplay("{GetDebuggerDisplay(),nq}")]
  public struct BlobReader
  {
    internal const int InvalidCompressedInteger = 2147483647;
    private readonly MemoryBlock _block;

    #nullable disable
    private readonly unsafe byte* _endPointer;
    private unsafe byte* _currentPointer;
    private static readonly uint[] s_corEncodeTokenArray = new uint[4]
    {
      33554432U,
      16777216U,
      452984832U,
      0U
    };


    #nullable enable
    /// <summary>Creates a reader of the specified memory block.</summary>
    /// <param name="buffer">Pointer to the start of the memory block.</param>
    /// <param name="length">Length in bytes of the memory block.</param>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="buffer" /> is null and <paramref name="length" /> is greater than zero.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="length" /> is negative.</exception>
    /// <exception cref="T:System.PlatformNotSupportedException">The current platform is not little-endian.</exception>
    public unsafe BlobReader(byte* buffer, int length)
      : this(MemoryBlock.CreateChecked(buffer, length))
    {
    }

    internal unsafe BlobReader(MemoryBlock block)
    {
      this._block = block;
      this._currentPointer = block.Pointer;
      this._endPointer = block.Pointer + block.Length;
    }

    internal unsafe string GetDebuggerDisplay()
    {
      if ((IntPtr) this._block.Pointer == IntPtr.Zero)
        return "<null>";
      int displayedBytes;
      string debuggerDisplay = this._block.GetDebuggerDisplay(out displayedBytes);
      return this.Offset >= displayedBytes ? (displayedBytes != this._block.Length ? debuggerDisplay + "*..." : debuggerDisplay + "*") : debuggerDisplay.Insert(this.Offset * 3, "*");
    }

    /// <summary>
    /// Pointer to the byte at the start of the underlying memory block.
    /// </summary>
    public unsafe byte* StartPointer => this._block.Pointer;

    /// <summary>
    /// Pointer to the byte at the current position of the reader.
    /// </summary>
    public unsafe byte* CurrentPointer => this._currentPointer;

    /// <summary>The total length of the underlying memory block.</summary>
    public int Length => this._block.Length;

    /// <summary>
    /// Gets or sets the offset from start of the blob to the current position.
    /// </summary>
    /// <exception cref="T:System.BadImageFormatException">Offset is set outside the bounds of underlying reader.</exception>
    public unsafe int Offset
    {
      get => (int) (this._currentPointer - this._block.Pointer);
      set
      {
        if ((uint) value > (uint) this._block.Length)
          Throw.OutOfBounds();
        this._currentPointer = this._block.Pointer + value;
      }
    }

    /// <summary>
    /// Bytes remaining from current position to end of underlying memory block.
    /// </summary>
    public unsafe int RemainingBytes => (int) (this._endPointer - this._currentPointer);

    /// <summary>
    /// Repositions the reader to the start of the underlying memory block.
    /// </summary>
    public unsafe void Reset() => this._currentPointer = this._block.Pointer;

    /// <summary>
    /// Repositions the reader forward by the number of bytes required to satisfy the given alignment.
    /// </summary>
    public void Align(byte alignment)
    {
      if (this.TryAlign(alignment))
        return;
      Throw.OutOfBounds();
    }

    internal unsafe bool TryAlign(byte alignment)
    {
      int num1 = this.Offset & (int) alignment - 1;
      if (num1 != 0)
      {
        int num2 = (int) alignment - num1;
        if (num2 > this.RemainingBytes)
          return false;
        this._currentPointer += num2;
      }
      return true;
    }

    internal unsafe MemoryBlock GetMemoryBlockAt(int offset, int length)
    {
      this.CheckBounds(offset, length);
      return new MemoryBlock(this._currentPointer + offset, length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void CheckBounds(int offset, int byteCount)
    {
      if ((ulong) (uint) offset + (ulong) (uint) byteCount <= (ulong) (this._endPointer - this._currentPointer))
        return;
      Throw.OutOfBounds();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void CheckBounds(int byteCount)
    {
      if ((long) (uint) byteCount <= this._endPointer - this._currentPointer)
        return;
      Throw.OutOfBounds();
    }


    #nullable disable
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe byte* GetCurrentPointerAndAdvance(int length)
    {
      byte* currentPointer = this._currentPointer;
      if ((uint) length > (uint) (this._endPointer - currentPointer))
        Throw.OutOfBounds();
      this._currentPointer = currentPointer + length;
      return currentPointer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe byte* GetCurrentPointerAndAdvance1()
    {
      byte* currentPointer = this._currentPointer;
      if (currentPointer == this._endPointer)
        Throw.OutOfBounds();
      this._currentPointer = currentPointer + 1;
      return currentPointer;
    }

    public bool ReadBoolean() => this.ReadByte() > (byte) 0;

    public unsafe sbyte ReadSByte() => (sbyte) *this.GetCurrentPointerAndAdvance1();

    public unsafe byte ReadByte() => *this.GetCurrentPointerAndAdvance1();

    public unsafe char ReadChar()
    {
      byte* pointerAndAdvance = this.GetCurrentPointerAndAdvance(2);
      return (char) ((uint) *pointerAndAdvance + ((uint) pointerAndAdvance[1] << 8));
    }

    public unsafe short ReadInt16()
    {
      byte* pointerAndAdvance = this.GetCurrentPointerAndAdvance(2);
      return (short) ((int) *pointerAndAdvance + ((int) pointerAndAdvance[1] << 8));
    }

    public unsafe ushort ReadUInt16()
    {
      byte* pointerAndAdvance = this.GetCurrentPointerAndAdvance(2);
      return (ushort) ((uint) *pointerAndAdvance + ((uint) pointerAndAdvance[1] << 8));
    }

    public unsafe int ReadInt32()
    {
      byte* pointerAndAdvance = this.GetCurrentPointerAndAdvance(4);
      return (int) *pointerAndAdvance + ((int) pointerAndAdvance[1] << 8) + ((int) pointerAndAdvance[2] << 16) + ((int) pointerAndAdvance[3] << 24);
    }

    public unsafe uint ReadUInt32()
    {
      byte* pointerAndAdvance = this.GetCurrentPointerAndAdvance(4);
      return (uint) ((int) *pointerAndAdvance + ((int) pointerAndAdvance[1] << 8) + ((int) pointerAndAdvance[2] << 16) + ((int) pointerAndAdvance[3] << 24));
    }

    public unsafe long ReadInt64()
    {
      byte* pointerAndAdvance = this.GetCurrentPointerAndAdvance(8);
      return (long) (uint) ((int) *pointerAndAdvance + ((int) pointerAndAdvance[1] << 8) + ((int) pointerAndAdvance[2] << 16) + ((int) pointerAndAdvance[3] << 24)) + ((long) (uint) ((int) pointerAndAdvance[4] + ((int) pointerAndAdvance[5] << 8) + ((int) pointerAndAdvance[6] << 16) + ((int) pointerAndAdvance[7] << 24)) << 32);
    }

    public ulong ReadUInt64() => (ulong) this.ReadInt64();

    public unsafe float ReadSingle() => *(float*) this.ReadInt32();

    public unsafe double ReadDouble() => *(double*) this.ReadInt64();

    public unsafe Guid ReadGuid()
    {
      byte* pointerAndAdvance = this.GetCurrentPointerAndAdvance(16);
      return BitConverter.IsLittleEndian ? *(Guid*) pointerAndAdvance : new Guid((int) *pointerAndAdvance | (int) pointerAndAdvance[1] << 8 | (int) pointerAndAdvance[2] << 16 | (int) pointerAndAdvance[3] << 24, (short) ((int) pointerAndAdvance[4] | (int) pointerAndAdvance[5] << 8), (short) ((int) pointerAndAdvance[6] | (int) pointerAndAdvance[7] << 8), pointerAndAdvance[8], pointerAndAdvance[9], pointerAndAdvance[10], pointerAndAdvance[11], pointerAndAdvance[12], pointerAndAdvance[13], pointerAndAdvance[14], pointerAndAdvance[15]);
    }

    /// <summary>
    /// Reads <see cref="T:System.Decimal" /> number.
    /// </summary>
    /// <remarks>
    /// Decimal number is encoded in 13 bytes as follows:
    /// - byte 0: highest bit indicates sign (1 for negative, 0 for non-negative); the remaining 7 bits encode scale
    /// - bytes 1..12: 96-bit unsigned integer in little endian encoding.
    /// </remarks>
    /// <exception cref="T:System.BadImageFormatException">The data at the current position was not a valid <see cref="T:System.Decimal" /> number.</exception>
    public unsafe Decimal ReadDecimal()
    {
      byte* pointerAndAdvance = this.GetCurrentPointerAndAdvance(13);
      byte scale = (byte) ((uint) *pointerAndAdvance & (uint) sbyte.MaxValue);
      return scale <= (byte) 28 ? new Decimal((int) pointerAndAdvance[1] | (int) pointerAndAdvance[2] << 8 | (int) pointerAndAdvance[3] << 16 | (int) pointerAndAdvance[4] << 24, (int) pointerAndAdvance[5] | (int) pointerAndAdvance[6] << 8 | (int) pointerAndAdvance[7] << 16 | (int) pointerAndAdvance[8] << 24, (int) pointerAndAdvance[9] | (int) pointerAndAdvance[10] << 8 | (int) pointerAndAdvance[11] << 16 | (int) pointerAndAdvance[12] << 24, ((uint) *pointerAndAdvance & 128U) > 0U, scale) : throw new BadImageFormatException(SR.ValueTooLarge);
    }

    public DateTime ReadDateTime() => new DateTime(this.ReadInt64());

    public SignatureHeader ReadSignatureHeader() => new SignatureHeader(this.ReadByte());

    /// <summary>
    /// Finds specified byte in the blob following the current position.
    /// </summary>
    /// <returns>
    /// Index relative to the current position, or -1 if the byte is not found in the blob following the current position.
    /// </returns>
    /// <remarks>Doesn't change the current position.</remarks>
    public int IndexOf(byte value)
    {
      int offset = this.Offset;
      int num = this._block.IndexOfUnchecked(value, offset);
      return num < 0 ? -1 : num - offset;
    }


    #nullable enable
    /// <summary>
    /// Reads UTF8 encoded string starting at the current position.
    /// </summary>
    /// <param name="byteCount">The number of bytes to read.</param>
    /// <returns>The string.</returns>
    /// <exception cref="T:System.BadImageFormatException"><paramref name="byteCount" /> bytes not available.</exception>
    public unsafe string ReadUTF8(int byteCount)
    {
      string str = this._block.PeekUtf8(this.Offset, byteCount);
      this._currentPointer += byteCount;
      return str;
    }

    /// <summary>
    /// Reads UTF16 (little-endian) encoded string starting at the current position.
    /// </summary>
    /// <param name="byteCount">The number of bytes to read.</param>
    /// <returns>The string.</returns>
    /// <exception cref="T:System.BadImageFormatException"><paramref name="byteCount" /> bytes not available.</exception>
    public unsafe string ReadUTF16(int byteCount)
    {
      string str = this._block.PeekUtf16(this.Offset, byteCount);
      this._currentPointer += byteCount;
      return str;
    }

    /// <summary>Reads bytes starting at the current position.</summary>
    /// <param name="byteCount">The number of bytes to read.</param>
    /// <returns>The byte array.</returns>
    /// <exception cref="T:System.BadImageFormatException"><paramref name="byteCount" /> bytes not available.</exception>
    public unsafe byte[] ReadBytes(int byteCount)
    {
      byte[] numArray = this._block.PeekBytes(this.Offset, byteCount);
      this._currentPointer += byteCount;
      return numArray;
    }

    /// <summary>
    /// Reads bytes starting at the current position in to the given buffer at the given offset;
    /// </summary>
    /// <param name="byteCount">The number of bytes to read.</param>
    /// <param name="buffer">The destination buffer the bytes read will be written.</param>
    /// <param name="bufferOffset">The offset in the destination buffer where the bytes read will be written.</param>
    /// <exception cref="T:System.BadImageFormatException"><paramref name="byteCount" /> bytes not available.</exception>
    public unsafe void ReadBytes(int byteCount, byte[] buffer, int bufferOffset) => Marshal.Copy((IntPtr) (void*) this.GetCurrentPointerAndAdvance(byteCount), buffer, bufferOffset, byteCount);

    internal unsafe string ReadUtf8NullTerminated()
    {
      int numberOfBytesRead;
      string str = this._block.PeekUtf8NullTerminated(this.Offset, (byte[]) null, MetadataStringDecoder.DefaultUTF8, out numberOfBytesRead);
      this._currentPointer += numberOfBytesRead;
      return str;
    }

    private unsafe int ReadCompressedIntegerOrInvalid()
    {
      int numberOfBytesRead;
      int num = this._block.PeekCompressedInteger(this.Offset, out numberOfBytesRead);
      this._currentPointer += numberOfBytesRead;
      return num;
    }

    /// <summary>
    /// Reads an unsigned compressed integer value.
    /// See Metadata Specification section II.23.2: Blobs and signatures.
    /// </summary>
    /// <param name="value">The value of the compressed integer that was read.</param>
    /// <returns>true if the value was read successfully. false if the data at the current position was not a valid compressed integer.</returns>
    public bool TryReadCompressedInteger(out int value)
    {
      value = this.ReadCompressedIntegerOrInvalid();
      return value != int.MaxValue;
    }

    /// <summary>
    /// Reads an unsigned compressed integer value.
    /// See Metadata Specification section II.23.2: Blobs and signatures.
    /// </summary>
    /// <returns>The value of the compressed integer that was read.</returns>
    /// <exception cref="T:System.BadImageFormatException">The data at the current position was not a valid compressed integer.</exception>
    public int ReadCompressedInteger()
    {
      int num;
      if (!this.TryReadCompressedInteger(out num))
        Throw.InvalidCompressedInteger();
      return num;
    }

    /// <summary>
    /// Reads a signed compressed integer value.
    /// See Metadata Specification section II.23.2: Blobs and signatures.
    /// </summary>
    /// <param name="value">The value of the compressed integer that was read.</param>
    /// <returns>true if the value was read successfully. false if the data at the current position was not a valid compressed integer.</returns>
    public unsafe bool TryReadCompressedSignedInteger(out int value)
    {
      int numberOfBytesRead;
      value = this._block.PeekCompressedInteger(this.Offset, out numberOfBytesRead);
      if (value == int.MaxValue)
        return false;
      bool flag = (value & 1) != 0;
      value >>= 1;
      if (flag)
      {
        switch (numberOfBytesRead)
        {
          case 1:
            value |= -64;
            break;
          case 2:
            value |= -8192;
            break;
          default:
            value |= -268435456;
            break;
        }
      }
      this._currentPointer += numberOfBytesRead;
      return true;
    }

    /// <summary>
    /// Reads a signed compressed integer value.
    /// See Metadata Specification section II.23.2: Blobs and signatures.
    /// </summary>
    /// <returns>The value of the compressed integer that was read.</returns>
    /// <exception cref="T:System.BadImageFormatException">The data at the current position was not a valid compressed integer.</exception>
    public int ReadCompressedSignedInteger()
    {
      int num;
      if (!this.TryReadCompressedSignedInteger(out num))
        Throw.InvalidCompressedInteger();
      return num;
    }

    /// <summary>
    /// Reads type code encoded in a serialized custom attribute value.
    /// </summary>
    /// <returns><see cref="F:System.Reflection.Metadata.SerializationTypeCode.Invalid" /> if the encoding is invalid.</returns>
    public SerializationTypeCode ReadSerializationTypeCode()
    {
      int num = this.ReadCompressedIntegerOrInvalid();
      return num > (int) byte.MaxValue ? SerializationTypeCode.Invalid : (SerializationTypeCode) num;
    }

    /// <summary>Reads type code encoded in a signature.</summary>
    /// <returns><see cref="F:System.Reflection.Metadata.SignatureTypeCode.Invalid" /> if the encoding is invalid.</returns>
    public SignatureTypeCode ReadSignatureTypeCode()
    {
      int num = this.ReadCompressedIntegerOrInvalid();
      switch (num)
      {
        case 17:
        case 18:
          return SignatureTypeCode.TypeHandle;
        default:
          return num > (int) byte.MaxValue ? SignatureTypeCode.Invalid : (SignatureTypeCode) num;
      }
    }

    /// <summary>
    /// Reads a string encoded as a compressed integer containing its length followed by
    /// its contents in UTF8. Null strings are encoded as a single 0xFF byte.
    /// </summary>
    /// <remarks>Defined as a 'SerString' in the ECMA CLI specification.</remarks>
    /// <returns>String value or null.</returns>
    /// <exception cref="T:System.BadImageFormatException">If the encoding is invalid.</exception>
    public string? ReadSerializedString()
    {
      int byteCount;
      if (this.TryReadCompressedInteger(out byteCount))
        return this.ReadUTF8(byteCount);
      if (this.ReadByte() != byte.MaxValue)
        Throw.InvalidSerializedString();
      return (string) null;
    }

    /// <summary>
    /// Reads a type handle encoded in a signature as TypeDefOrRefOrSpecEncoded (see ECMA-335 II.23.2.8).
    /// </summary>
    /// <returns>The handle or nil if the encoding is invalid.</returns>
    public EntityHandle ReadTypeHandle()
    {
      uint num = (uint) this.ReadCompressedIntegerOrInvalid();
      uint corEncodeToken = BlobReader.s_corEncodeTokenArray[(int) num & 3];
      return num == (uint) int.MaxValue || corEncodeToken == 0U ? new EntityHandle() : new EntityHandle(corEncodeToken | num >> 2);
    }

    /// <summary>
    /// Reads a #Blob heap handle encoded as a compressed integer.
    /// </summary>
    /// <remarks>
    /// Blobs that contain references to other blobs are used in Portable PDB format, for example <see cref="P:System.Reflection.Metadata.Document.Name" />.
    /// </remarks>
    public BlobHandle ReadBlobHandle() => BlobHandle.FromOffset(this.ReadCompressedInteger());

    /// <summary>
    /// Reads a constant value (see ECMA-335 Partition II section 22.9) from the current position.
    /// </summary>
    /// <exception cref="T:System.BadImageFormatException">Error while reading from the blob.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="typeCode" /> is not a valid <see cref="T:System.Reflection.Metadata.ConstantTypeCode" />.</exception>
    /// <returns>
    /// Boxed constant value. To avoid allocating the object use Read* methods directly.
    /// Constants of type <see cref="F:System.Reflection.Metadata.ConstantTypeCode.String" /> are encoded as UTF16 strings, use <see cref="M:System.Reflection.Metadata.BlobReader.ReadUTF16(System.Int32)" /> to read them.
    /// </returns>
    public object? ReadConstant(ConstantTypeCode typeCode)
    {
      switch (typeCode)
      {
        case ConstantTypeCode.Boolean:
          return (object) this.ReadBoolean();
        case ConstantTypeCode.Char:
          return (object) this.ReadChar();
        case ConstantTypeCode.SByte:
          return (object) this.ReadSByte();
        case ConstantTypeCode.Byte:
          return (object) this.ReadByte();
        case ConstantTypeCode.Int16:
          return (object) this.ReadInt16();
        case ConstantTypeCode.UInt16:
          return (object) this.ReadUInt16();
        case ConstantTypeCode.Int32:
          return (object) this.ReadInt32();
        case ConstantTypeCode.UInt32:
          return (object) this.ReadUInt32();
        case ConstantTypeCode.Int64:
          return (object) this.ReadInt64();
        case ConstantTypeCode.UInt64:
          return (object) this.ReadUInt64();
        case ConstantTypeCode.Single:
          return (object) this.ReadSingle();
        case ConstantTypeCode.Double:
          return (object) this.ReadDouble();
        case ConstantTypeCode.String:
          return (object) this.ReadUTF16(this.RemainingBytes);
        case ConstantTypeCode.NullReference:
          if (this.ReadUInt32() != 0U)
            throw new BadImageFormatException(SR.InvalidConstantValue);
          return (object) null;
        default:
          throw new ArgumentOutOfRangeException(nameof (typeCode));
      }
    }
  }
}
