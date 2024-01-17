﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Internal.ReadOnlyUnmanagedMemoryStream
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using System.IO;
using System.Runtime.InteropServices;


#nullable enable
namespace Datadog.System.Reflection.Internal
{
    internal sealed class ReadOnlyUnmanagedMemoryStream : Stream
  {

    #nullable disable
    private readonly unsafe byte* _data;
    private readonly int _length;
    private int _position;


    #nullable enable
    public unsafe ReadOnlyUnmanagedMemoryStream(byte* data, int length)
    {
      this._data = data;
      this._length = length;
    }

    public override unsafe int ReadByte() => this._position >= this._length ? -1 : (int) this._data[this._position++];

    public override unsafe int Read(byte[] buffer, int offset, int count)
    {
      int length = Math.Min(count, this._length - this._position);
      Marshal.Copy((IntPtr) (void*) (this._data + this._position), buffer, offset, length);
      this._position += length;
      return length;
    }

    public override void Flush()
    {
    }

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    public override long Length => (long) this._length;

    public override long Position
    {
      get => (long) this._position;
      set => this.Seek(value, SeekOrigin.Begin);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
      long num1;
      try
      {
        long num2;
        switch (origin)
        {
          case SeekOrigin.Begin:
            num2 = offset;
            break;
          case SeekOrigin.Current:
            num2 = checked (offset + (long) this._position);
            break;
          case SeekOrigin.End:
            num2 = checked (offset + (long) this._length);
            break;
          default:
            throw new ArgumentOutOfRangeException(nameof (origin));
        }
        num1 = num2;
      }
      catch (OverflowException ex)
      {
        throw new ArgumentOutOfRangeException(nameof (offset));
      }
      this._position = num1 >= 0L && num1 <= (long) int.MaxValue ? (int) num1 : throw new ArgumentOutOfRangeException(nameof (offset));
      return num1;
    }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
  }
}
