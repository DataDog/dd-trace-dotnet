﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.BlobUtilities
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Datadog.System.Collections.Immutable;
using Datadog.System.Reflection.Internal;


#nullable enable
namespace Datadog.System.Reflection
{
    internal static class BlobUtilities
  {
    public const int SizeOfSerializedDecimal = 13;
    public const int SizeOfGuid = 16;

    public static unsafe byte[] ReadBytes(byte* buffer, int byteCount)
    {
      if (byteCount == 0)
        return Array.Empty<byte>();
      byte[] destination = new byte[byteCount];
      Marshal.Copy((IntPtr) (void*) buffer, destination, 0, byteCount);
      return destination;
    }

    public static unsafe ImmutableArray<byte> ReadImmutableBytes(byte* buffer, int byteCount)
    {
      byte[] array = BlobUtilities.ReadBytes(buffer, byteCount);
      return ImmutableByteArrayInterop.DangerousCreateFromUnderlyingArray(ref array);
    }

    public static unsafe void WriteBytes(this byte[] buffer, int start, byte value, int byteCount)
    {
      fixed (byte* numPtr1 = &buffer[0])
      {
        byte* numPtr2 = numPtr1 + start;
        for (int index = 0; index < byteCount; ++index)
          numPtr2[index] = value;
      }
    }

    public static unsafe void WriteDouble(this byte[] buffer, int start, double value) => buffer.WriteUInt64(start, (ulong) *(long*) &value);

    public static unsafe void WriteSingle(this byte[] buffer, int start, float value) => buffer.WriteUInt32(start, *(uint*) &value);

    public static void WriteByte(this byte[] buffer, int start, byte value) => buffer[start] = value;

    public static unsafe void WriteUInt16(this byte[] buffer, int start, ushort value)
    {
      fixed (byte* numPtr = &buffer[start])
      {
        *numPtr = (byte) value;
        numPtr[1] = (byte) ((uint) value >> 8);
      }
    }

    public static unsafe void WriteUInt16BE(this byte[] buffer, int start, ushort value)
    {
      fixed (byte* numPtr = &buffer[start])
      {
        *numPtr = (byte) ((uint) value >> 8);
        numPtr[1] = (byte) value;
      }
    }

    public static unsafe void WriteUInt32BE(this byte[] buffer, int start, uint value)
    {
      fixed (byte* numPtr = &buffer[start])
      {
        *numPtr = (byte) (value >> 24);
        numPtr[1] = (byte) (value >> 16);
        numPtr[2] = (byte) (value >> 8);
        numPtr[3] = (byte) value;
      }
    }

    public static unsafe void WriteUInt32(this byte[] buffer, int start, uint value)
    {
      fixed (byte* numPtr = &buffer[start])
      {
        *numPtr = (byte) value;
        numPtr[1] = (byte) (value >> 8);
        numPtr[2] = (byte) (value >> 16);
        numPtr[3] = (byte) (value >> 24);
      }
    }

    public static void WriteUInt64(this byte[] buffer, int start, ulong value)
    {
      buffer.WriteUInt32(start, (uint) value);
      buffer.WriteUInt32(start + 4, (uint) (value >> 32));
    }

    public static void WriteDecimal(this byte[] buffer, int start, Decimal value)
    {
      bool isNegative;
      byte scale;
      uint low;
      uint mid;
      uint high;
      value.GetBits(out isNegative, out scale, out low, out mid, out high);
      buffer.WriteByte(start, (byte) ((int) scale | (isNegative ? 128 : 0)));
      buffer.WriteUInt32(start + 1, low);
      buffer.WriteUInt32(start + 5, mid);
      buffer.WriteUInt32(start + 9, high);
    }

    public static unsafe void WriteGuid(this byte[] buffer, int start, Guid value)
    {
      fixed (byte* numPtr1 = &buffer[start])
      {
        byte* numPtr2 = (byte*) &value;
        uint num1 = *(uint*) numPtr2;
        *numPtr1 = (byte) num1;
        numPtr1[1] = (byte) (num1 >> 8);
        numPtr1[2] = (byte) (num1 >> 16);
        numPtr1[3] = (byte) (num1 >> 24);
        ushort num2 = *(ushort*) (numPtr2 + 4);
        numPtr1[4] = (byte) num2;
        numPtr1[5] = (byte) ((uint) num2 >> 8);
        ushort num3 = *(ushort*) (numPtr2 + 6);
        numPtr1[6] = (byte) num3;
        numPtr1[7] = (byte) ((uint) num3 >> 8);
        numPtr1[8] = numPtr2[8];
        numPtr1[9] = numPtr2[9];
        numPtr1[10] = numPtr2[10];
        numPtr1[11] = numPtr2[11];
        numPtr1[12] = numPtr2[12];
        numPtr1[13] = numPtr2[13];
        numPtr1[14] = numPtr2[14];
        numPtr1[15] = numPtr2[15];
      }
    }

    public static unsafe void WriteUTF8(
      this byte[] buffer,
      int start,
      char* charPtr,
      int charCount,
      int byteCount,
      bool allowUnpairedSurrogates)
    {
      char* chPtr = charPtr + charCount;
      fixed (byte* numPtr1 = &buffer[0])
      {
        byte* numPtr2 = numPtr1 + start;
        if (byteCount == charCount)
        {
          while (charPtr < chPtr)
            *numPtr2++ = (byte) *charPtr++;
        }
        else
        {
          while (charPtr < chPtr)
          {
            char c = *charPtr++;
            if (c < '\u0080')
              *numPtr2++ = (byte) c;
            else if (c < 'ࠀ')
            {
              *numPtr2 = (byte) ((int) c >> 6 & 31 | 192);
              numPtr2[1] = (byte) ((int) c & 63 | 128);
              numPtr2 += 2;
            }
            else
            {
              if (BlobUtilities.IsSurrogateChar((int) c))
              {
                if (BlobUtilities.IsHighSurrogateChar((int) c) && charPtr < chPtr && BlobUtilities.IsLowSurrogateChar((int) *charPtr))
                {
                  int num = ((int) c - 55296 << 10) + (int) *charPtr++ - 56320 + 65536;
                  *numPtr2 = (byte) (num >> 18 & 7 | 240);
                  numPtr2[1] = (byte) (num >> 12 & 63 | 128);
                  numPtr2[2] = (byte) (num >> 6 & 63 | 128);
                  numPtr2[3] = (byte) (num & 63 | 128);
                  numPtr2 += 4;
                  continue;
                }
                if (!allowUnpairedSurrogates)
                  c = '�';
              }
              *numPtr2 = (byte) ((int) c >> 12 & 15 | 224);
              numPtr2[1] = (byte) ((int) c >> 6 & 63 | 128);
              numPtr2[2] = (byte) ((int) c & 63 | 128);
              numPtr2 += 3;
            }
          }
        }
      }
    }

    internal static unsafe int GetUTF8ByteCount(string str)
    {
      fixed (char* str1 = str)
        return BlobUtilities.GetUTF8ByteCount(str1, str.Length);
    }

    internal static unsafe int GetUTF8ByteCount(char* str, int charCount) => BlobUtilities.GetUTF8ByteCount(str, charCount, int.MaxValue, out char* _);

    internal static unsafe int GetUTF8ByteCount(
      char* str,
      int charCount,
      int byteLimit,
      out char* remainder)
    {
      char* chPtr1 = str + charCount;
      char* chPtr2 = str;
      int utF8ByteCount = 0;
      while (chPtr2 < chPtr1)
      {
        char c = *chPtr2++;
        int num;
        if (c < '\u0080')
          num = 1;
        else if (c < 'ࠀ')
          num = 2;
        else if (BlobUtilities.IsHighSurrogateChar((int) c) && chPtr2 < chPtr1 && BlobUtilities.IsLowSurrogateChar((int) *chPtr2))
        {
          num = 4;
          ++chPtr2;
        }
        else
          num = 3;
        if (utF8ByteCount + num > byteLimit)
        {
          chPtr2 -= (num < 4 ? new IntPtr(1) : new IntPtr(2)).ToInt64();
          break;
        }
        utF8ByteCount += num;
      }
      remainder = chPtr2;
      return utF8ByteCount;
    }

    internal static bool IsSurrogateChar(int c) => (uint) (c - 55296) <= 2047U;

    internal static bool IsHighSurrogateChar(int c) => (uint) (c - 55296) <= 1023U;

    internal static bool IsLowSurrogateChar(int c) => (uint) (c - 56320) <= 1023U;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ValidateRange(
      int bufferLength,
      int start,
      int byteCount,
      string byteCountParameterName)
    {
      if (start < 0 || start > bufferLength)
        Throw.ArgumentOutOfRange(nameof (start));
      if (byteCount >= 0 && byteCount <= bufferLength - start)
        return;
      Throw.ArgumentOutOfRange(byteCountParameterName);
    }

    internal static int GetUserStringByteLength(int characterCount) => characterCount * 2 + 1;

    internal static byte GetUserStringTrailingByte(string str)
    {
      foreach (char ch in str)
      {
        if (ch >= '\u007F')
          return 1;
        switch ((int) ch - 1)
        {
          case 0:
          case 1:
          case 2:
          case 3:
          case 4:
          case 5:
          case 6:
          case 7:
          case 13:
          case 14:
          case 15:
          case 16:
          case 17:
          case 18:
          case 19:
          case 20:
          case 21:
          case 22:
          case 23:
          case 24:
          case 25:
          case 26:
          case 27:
          case 28:
          case 29:
          case 30:
          case 38:
          case 44:
            return 1;
          default:
            continue;
        }
      }
      return 0;
    }
  }
}
