﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Internal.StreamExtensions
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using System.IO;
using Datadog.System.Refelaction.Metadata.src.System.Reflection.Internal.Utilities;
using System.Runtime.InteropServices;


#nullable enable
namespace Datadog.System.Reflection.Internal
{
  internal static class StreamExtensions
  {
    internal const int StreamCopyBufferSize = 81920;

    private static bool IsWindows => Path.DirectorySeparatorChar == '\\';


    #nullable disable
    private static SafeHandle GetSafeFileHandle(FileStream stream)
    {
      SafeHandle safeFileHandle;
      try
      {
        safeFileHandle = (SafeHandle) stream.SafeFileHandle;
      }
      catch
      {
        return (SafeHandle) null;
      }
      return safeFileHandle != null && safeFileHandle.IsInvalid ? (SafeHandle) null : safeFileHandle;
    }


    #nullable enable
    internal static unsafe int Read(this Stream stream, byte* buffer, int size)
    {
      if (!StreamExtensions.IsWindows || !(stream is FileStream stream1))
        return 0;
      SafeHandle safeFileHandle = StreamExtensions.GetSafeFileHandle(stream1);
      int numBytesRead;
      return safeFileHandle == null || Interop.Kernel32.ReadFile(safeFileHandle, buffer, size, out numBytesRead, IntPtr.Zero) == 0 ? 0 : numBytesRead;
    }

    /// <summary>
    /// Copies specified amount of data from given stream to a target memory pointer.
    /// </summary>
    /// <exception cref="T:System.IO.IOException">unexpected stream end.</exception>
    internal static unsafe void CopyTo(this Stream source, byte* destination, int size)
    {
      byte[] numArray = new byte[Math.Min(81920, size)];
      int length;
      for (; size > 0; size -= length)
      {
        int count = Math.Min(size, numArray.Length);
        length = source.Read(numArray, 0, count);
        if (length <= 0 || length > count)
          throw new IOException(SR.UnexpectedStreamEnd);
        Marshal.Copy(numArray, 0, (IntPtr) (void*) destination, length);
        destination += length;
      }
    }

    /// <summary>
    /// Attempts to read all of the requested bytes from the stream into the buffer
    /// </summary>
    /// <returns>
    /// The number of bytes read. Less than <paramref name="count" /> will
    /// only be returned if the end of stream is reached before all bytes can be read.
    /// </returns>
    /// <remarks>
    /// Unlike <see cref="M:System.IO.Stream.Read(System.Byte[],System.Int32,System.Int32)" /> it is not guaranteed that
    /// the stream position or the output buffer will be unchanged if an exception is
    /// returned.
    /// </remarks>
    internal static int TryReadAll(this Stream stream, byte[] buffer, int offset, int count)
    {
      int num1;
      int num2;
      for (num1 = 0; num1 < count; num1 += num2)
      {
        num2 = stream.Read(buffer, offset + num1, count - num1);
        if (num2 == 0)
          break;
      }
      return num1;
    }

    /// <summary>
    /// Resolve image size as either the given user-specified size or distance from current position to end-of-stream.
    /// Also performs the relevant argument validation and publicly visible caller has same argument names.
    /// </summary>
    /// <exception cref="T:System.ArgumentException">size is 0 and distance from current position to end-of-stream can't fit in Int32.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">Size is negative or extends past the end-of-stream from current position.</exception>
    internal static int GetAndValidateSize(Stream stream, int size, string streamParameterName)
    {
      long num = stream.Length - stream.Position;
      if (size < 0 || (long) size > num)
        throw new ArgumentOutOfRangeException(nameof (size));
      if (size != 0)
        return size;
      return num <= (long) int.MaxValue ? (int) num : throw new ArgumentException(SR.StreamTooLarge, streamParameterName);
    }
  }
}
