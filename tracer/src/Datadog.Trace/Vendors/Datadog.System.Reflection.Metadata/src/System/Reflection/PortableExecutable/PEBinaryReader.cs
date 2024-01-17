﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.PortableExecutable.PEBinaryReader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System.IO;
using System.Text;


#nullable enable
namespace Datadog.System.Reflection.PortableExecutable
{
    /// <summary>
    /// Simple BinaryReader wrapper to:
    /// 
    ///  1) throw BadImageFormat instead of EndOfStream or ArgumentOutOfRange.
    ///  2) limit reads to a subset of the base stream.
    /// 
    /// Only methods that are needed to read PE headers are implemented.
    /// </summary>
    internal readonly struct PEBinaryReader
  {
    private readonly long _startOffset;
    private readonly long _maxOffset;

    #nullable disable
    private readonly BinaryReader _reader;


    #nullable enable
    public PEBinaryReader(Stream stream, int size)
    {
      this._startOffset = stream.Position;
      this._maxOffset = this._startOffset + (long) size;
      this._reader = new BinaryReader(stream, Encoding.UTF8, true);
    }

    public int CurrentOffset => (int) (this._reader.BaseStream.Position - this._startOffset);

    public void Seek(int offset)
    {
      this.CheckBounds(this._startOffset, offset);
      this._reader.BaseStream.Seek((long) offset, SeekOrigin.Begin);
    }

    public byte[] ReadBytes(int count)
    {
      this.CheckBounds(this._reader.BaseStream.Position, count);
      return this._reader.ReadBytes(count);
    }

    public byte ReadByte()
    {
      this.CheckBounds(1U);
      return this._reader.ReadByte();
    }

    public short ReadInt16()
    {
      this.CheckBounds(2U);
      return this._reader.ReadInt16();
    }

    public ushort ReadUInt16()
    {
      this.CheckBounds(2U);
      return this._reader.ReadUInt16();
    }

    public int ReadInt32()
    {
      this.CheckBounds(4U);
      return this._reader.ReadInt32();
    }

    public uint ReadUInt32()
    {
      this.CheckBounds(4U);
      return this._reader.ReadUInt32();
    }

    public ulong ReadUInt64()
    {
      this.CheckBounds(8U);
      return this._reader.ReadUInt64();
    }

    /// <summary>
    /// Reads a fixed-length byte block as a null-padded UTF8-encoded string.
    /// The padding is not included in the returned string.
    /// 
    /// Note that it is legal for UTF8 strings to contain NUL; if NUL occurs
    /// between non-NUL codepoints, it is not considered to be padding and
    /// is included in the result.
    /// </summary>
    public string ReadNullPaddedUTF8(int byteCount)
    {
      byte[] bytes = this.ReadBytes(byteCount);
      int count = 0;
      for (int length = bytes.Length; length > 0; --length)
      {
        if (bytes[length - 1] != (byte) 0)
        {
          count = length;
          break;
        }
      }
      return Encoding.UTF8.GetString(bytes, 0, count);
    }

    private void CheckBounds(uint count)
    {
      if ((ulong) this._reader.BaseStream.Position + (ulong) count <= (ulong) this._maxOffset)
        return;
      Throw.ImageTooSmall();
    }

    private void CheckBounds(long startPosition, int count)
    {
      if ((ulong) startPosition + (ulong) (uint) count <= (ulong) this._maxOffset)
        return;
      Throw.ImageTooSmallOrContainsInvalidOffsetOrCount();
    }
  }
}
