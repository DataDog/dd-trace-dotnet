﻿// Decompiled with JetBrains decompiler
// Type: System.Buffers.Text.Base64
// Assembly: System.Memory, Version=4.0.1.2, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51
// MVID: 805945F3-27B0-47AD-B8F6-389D9D8F82C3
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Memory.4.5.5\lib\net461\System.Memory.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Memory.4.5.5\lib\net461\System.Memory.xml

using System.Runtime.CompilerServices;
using Datadog.System.Runtime.CompilerServices.Unsafe;
using Datadog.System.Runtime.InteropServices;

namespace Datadog.System.Buffers.Text
{
    public static class Base64
  {
    private static readonly sbyte[] s_decodingMap = new sbyte[256]
    {
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) 62,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) 63,
      (sbyte) 52,
      (sbyte) 53,
      (sbyte) 54,
      (sbyte) 55,
      (sbyte) 56,
      (sbyte) 57,
      (sbyte) 58,
      (sbyte) 59,
      (sbyte) 60,
      (sbyte) 61,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) 0,
      (sbyte) 1,
      (sbyte) 2,
      (sbyte) 3,
      (sbyte) 4,
      (sbyte) 5,
      (sbyte) 6,
      (sbyte) 7,
      (sbyte) 8,
      (sbyte) 9,
      (sbyte) 10,
      (sbyte) 11,
      (sbyte) 12,
      (sbyte) 13,
      (sbyte) 14,
      (sbyte) 15,
      (sbyte) 16,
      (sbyte) 17,
      (sbyte) 18,
      (sbyte) 19,
      (sbyte) 20,
      (sbyte) 21,
      (sbyte) 22,
      (sbyte) 23,
      (sbyte) 24,
      (sbyte) 25,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) 26,
      (sbyte) 27,
      (sbyte) 28,
      (sbyte) 29,
      (sbyte) 30,
      (sbyte) 31,
      (sbyte) 32,
      (sbyte) 33,
      (sbyte) 34,
      (sbyte) 35,
      (sbyte) 36,
      (sbyte) 37,
      (sbyte) 38,
      (sbyte) 39,
      (sbyte) 40,
      (sbyte) 41,
      (sbyte) 42,
      (sbyte) 43,
      (sbyte) 44,
      (sbyte) 45,
      (sbyte) 46,
      (sbyte) 47,
      (sbyte) 48,
      (sbyte) 49,
      (sbyte) 50,
      (sbyte) 51,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1,
      (sbyte) -1
    };
    private static readonly byte[] s_encodingMap = new byte[64]
    {
      (byte) 65,
      (byte) 66,
      (byte) 67,
      (byte) 68,
      (byte) 69,
      (byte) 70,
      (byte) 71,
      (byte) 72,
      (byte) 73,
      (byte) 74,
      (byte) 75,
      (byte) 76,
      (byte) 77,
      (byte) 78,
      (byte) 79,
      (byte) 80,
      (byte) 81,
      (byte) 82,
      (byte) 83,
      (byte) 84,
      (byte) 85,
      (byte) 86,
      (byte) 87,
      (byte) 88,
      (byte) 89,
      (byte) 90,
      (byte) 97,
      (byte) 98,
      (byte) 99,
      (byte) 100,
      (byte) 101,
      (byte) 102,
      (byte) 103,
      (byte) 104,
      (byte) 105,
      (byte) 106,
      (byte) 107,
      (byte) 108,
      (byte) 109,
      (byte) 110,
      (byte) 111,
      (byte) 112,
      (byte) 113,
      (byte) 114,
      (byte) 115,
      (byte) 116,
      (byte) 117,
      (byte) 118,
      (byte) 119,
      (byte) 120,
      (byte) 121,
      (byte) 122,
      (byte) 48,
      (byte) 49,
      (byte) 50,
      (byte) 51,
      (byte) 52,
      (byte) 53,
      (byte) 54,
      (byte) 55,
      (byte) 56,
      (byte) 57,
      (byte) 43,
      (byte) 47
    };
    private const byte EncodingPad = 61;
    private const int MaximumEncodeLength = 1610612733;

    public static OperationStatus DecodeFromUtf8(
      ReadOnlySpan<byte> utf8,
      Span<byte> bytes,
      out int bytesConsumed,
      out int bytesWritten,
      bool isFinalBlock = true)
    {
      ref byte local1 = ref MemoryMarshal.GetReference<byte>(utf8);
      ref byte local2 = ref MemoryMarshal.GetReference<byte>(bytes);
      int length1 = utf8.Length & -4;
      int length2 = bytes.Length;
      int elementOffset1 = 0;
      int elementOffset2 = 0;
      if (utf8.Length != 0)
      {
        ref sbyte local3 = ref Base64.s_decodingMap[0];
        int num1 = isFinalBlock ? 4 : 0;
        int num2;
        for (num2 = length2 < Base64.GetMaxDecodedFromUtf8Length(length1) ? length2 / 3 * 4 : length1 - num1; elementOffset1 < num2; elementOffset1 += 4)
        {
          int num3 = Base64.Decode(ref Unsafe.Add<byte>(ref local1, elementOffset1), ref local3);
          if (num3 >= 0)
          {
            Base64.WriteThreeLowOrderBytes(ref Unsafe.Add<byte>(ref local2, elementOffset2), num3);
            elementOffset2 += 3;
          }
          else
            goto label_24;
        }
        if (num2 == length1 - num1)
        {
          if (elementOffset1 == length1)
          {
            if (!isFinalBlock)
            {
              bytesConsumed = elementOffset1;
              bytesWritten = elementOffset2;
              return OperationStatus.NeedMoreData;
            }
            goto label_24;
          }
          else
          {
            int elementOffset3 = (int) Unsafe.Add<byte>(ref local1, length1 - 4);
            int elementOffset4 = (int) Unsafe.Add<byte>(ref local1, length1 - 3);
            int elementOffset5 = (int) Unsafe.Add<byte>(ref local1, length1 - 2);
            int elementOffset6 = (int) Unsafe.Add<byte>(ref local1, length1 - 1);
            int num4 = (int) Unsafe.Add<sbyte>(ref local3, elementOffset3) << 18 | (int) Unsafe.Add<sbyte>(ref local3, elementOffset4) << 12;
            if (elementOffset6 != 61)
            {
              int num5 = (int) Unsafe.Add<sbyte>(ref local3, elementOffset5);
              int num6 = (int) Unsafe.Add<sbyte>(ref local3, elementOffset6);
              int num7 = num5 << 6;
              int num8 = num4 | num6 | num7;
              if (num8 >= 0)
              {
                if (elementOffset2 <= length2 - 3)
                {
                  Base64.WriteThreeLowOrderBytes(ref Unsafe.Add<byte>(ref local2, elementOffset2), num8);
                  elementOffset2 += 3;
                }
                else
                  goto label_21;
              }
              else
                goto label_24;
            }
            else if (elementOffset5 != 61)
            {
              int num9 = (int) Unsafe.Add<sbyte>(ref local3, elementOffset5) << 6;
              int num10 = num4 | num9;
              if (num10 >= 0)
              {
                if (elementOffset2 <= length2 - 2)
                {
                  Unsafe.Add<byte>(ref local2, elementOffset2) = (byte) (num10 >> 16);
                  Unsafe.Add<byte>(ref local2, elementOffset2 + 1) = (byte) (num10 >> 8);
                  elementOffset2 += 2;
                }
                else
                  goto label_21;
              }
              else
                goto label_24;
            }
            else if (num4 >= 0)
            {
              if (elementOffset2 <= length2 - 1)
              {
                Unsafe.Add<byte>(ref local2, elementOffset2) = (byte) (num4 >> 16);
                ++elementOffset2;
              }
              else
                goto label_21;
            }
            else
              goto label_24;
            elementOffset1 += 4;
            if (length1 != utf8.Length)
              goto label_24;
            else
              goto label_20;
          }
        }
label_21:
        if (!(length1 != utf8.Length & isFinalBlock))
        {
          bytesConsumed = elementOffset1;
          bytesWritten = elementOffset2;
          return OperationStatus.DestinationTooSmall;
        }
label_24:
        bytesConsumed = elementOffset1;
        bytesWritten = elementOffset2;
        return OperationStatus.InvalidData;
      }
label_20:
      bytesConsumed = elementOffset1;
      bytesWritten = elementOffset2;
      return OperationStatus.Done;
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static int GetMaxDecodedFromUtf8Length(int length)
    {
      if (length < 0)
        ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.length);
      return (length >> 2) * 3;
    }

    public static OperationStatus DecodeFromUtf8InPlace(Span<byte> buffer, out int bytesWritten)
    {
      int length = buffer.Length;
      int elementOffset1 = 0;
      int elementOffset2 = 0;
      if (length == (length >> 2) * 4)
      {
        if (length != 0)
        {
          ref byte local1 = ref MemoryMarshal.GetReference<byte>(buffer);
          ref sbyte local2 = ref Base64.s_decodingMap[0];
          for (; elementOffset1 < length - 4; elementOffset1 += 4)
          {
            int num = Base64.Decode(ref Unsafe.Add<byte>(ref local1, elementOffset1), ref local2);
            if (num >= 0)
            {
              Base64.WriteThreeLowOrderBytes(ref Unsafe.Add<byte>(ref local1, elementOffset2), num);
              elementOffset2 += 3;
            }
            else
              goto label_15;
          }
          int elementOffset3 = (int) Unsafe.Add<byte>(ref local1, length - 4);
          int elementOffset4 = (int) Unsafe.Add<byte>(ref local1, length - 3);
          int elementOffset5 = (int) Unsafe.Add<byte>(ref local1, length - 2);
          int elementOffset6 = (int) Unsafe.Add<byte>(ref local1, length - 1);
          int num1 = (int) Unsafe.Add<sbyte>(ref local2, elementOffset3) << 18 | (int) Unsafe.Add<sbyte>(ref local2, elementOffset4) << 12;
          if (elementOffset6 != 61)
          {
            int num2 = (int) Unsafe.Add<sbyte>(ref local2, elementOffset5);
            int num3 = (int) Unsafe.Add<sbyte>(ref local2, elementOffset6);
            int num4 = num2 << 6;
            int num5 = num1 | num3 | num4;
            if (num5 >= 0)
            {
              Base64.WriteThreeLowOrderBytes(ref Unsafe.Add<byte>(ref local1, elementOffset2), num5);
              elementOffset2 += 3;
            }
            else
              goto label_15;
          }
          else if (elementOffset5 != 61)
          {
            int num6 = (int) Unsafe.Add<sbyte>(ref local2, elementOffset5) << 6;
            int num7 = num1 | num6;
            if (num7 >= 0)
            {
              Unsafe.Add<byte>(ref local1, elementOffset2) = (byte) (num7 >> 16);
              Unsafe.Add<byte>(ref local1, elementOffset2 + 1) = (byte) (num7 >> 8);
              elementOffset2 += 2;
            }
            else
              goto label_15;
          }
          else if (num1 >= 0)
          {
            Unsafe.Add<byte>(ref local1, elementOffset2) = (byte) (num1 >> 16);
            ++elementOffset2;
          }
          else
            goto label_15;
        }
        bytesWritten = elementOffset2;
        return OperationStatus.Done;
      }
label_15:
      bytesWritten = elementOffset2;
      return OperationStatus.InvalidData;
    }

    [MethodImpl((MethodImplOptions) 256)]
    private static int Decode(ref byte encodedBytes, ref sbyte decodingMap)
    {
      int elementOffset1 = (int) encodedBytes;
      int elementOffset2 = (int) Unsafe.Add<byte>(ref encodedBytes, 1);
      int elementOffset3 = (int) Unsafe.Add<byte>(ref encodedBytes, 2);
      int elementOffset4 = (int) Unsafe.Add<byte>(ref encodedBytes, 3);
      int num1 = (int) Unsafe.Add<sbyte>(ref decodingMap, elementOffset1);
      int num2 = (int) Unsafe.Add<sbyte>(ref decodingMap, elementOffset2);
      int num3 = (int) Unsafe.Add<sbyte>(ref decodingMap, elementOffset3);
      int num4 = (int) Unsafe.Add<sbyte>(ref decodingMap, elementOffset4);
      int num5 = num1 << 18;
      int num6 = num2 << 12;
      int num7 = num3 << 6;
      return num5 | num4 | num6 | num7;
    }

    [MethodImpl((MethodImplOptions) 256)]
    private static void WriteThreeLowOrderBytes(ref byte destination, int value)
    {
      destination = (byte) (value >> 16);
      Unsafe.Add<byte>(ref destination, 1) = (byte) (value >> 8);
      Unsafe.Add<byte>(ref destination, 2) = (byte) value;
    }

    public static OperationStatus EncodeToUtf8(
      ReadOnlySpan<byte> bytes,
      Span<byte> utf8,
      out int bytesConsumed,
      out int bytesWritten,
      bool isFinalBlock = true)
    {
      ref byte local1 = ref MemoryMarshal.GetReference<byte>(bytes);
      ref byte local2 = ref MemoryMarshal.GetReference<byte>(utf8);
      int length1 = bytes.Length;
      int length2 = utf8.Length;
      int num1 = length1 > 1610612733 || length2 < Base64.GetMaxEncodedToUtf8Length(length1) ? (length2 >> 2) * 3 - 2 : length1 - 2;
      int elementOffset1 = 0;
      int elementOffset2 = 0;
      ref byte local3 = ref Base64.s_encodingMap[0];
      for (; elementOffset1 < num1; elementOffset1 += 3)
      {
        int num2 = Base64.Encode(ref Unsafe.Add<byte>(ref local1, elementOffset1), ref local3);
        Unsafe.WriteUnaligned<int>(ref Unsafe.Add<byte>(ref local2, elementOffset2), num2);
        elementOffset2 += 4;
      }
      if (num1 == length1 - 2)
      {
        if (isFinalBlock)
        {
          if (elementOffset1 == length1 - 1)
          {
            int num3 = Base64.EncodeAndPadTwo(ref Unsafe.Add<byte>(ref local1, elementOffset1), ref local3);
            Unsafe.WriteUnaligned<int>(ref Unsafe.Add<byte>(ref local2, elementOffset2), num3);
            elementOffset2 += 4;
            ++elementOffset1;
          }
          else if (elementOffset1 == length1 - 2)
          {
            int num4 = Base64.EncodeAndPadOne(ref Unsafe.Add<byte>(ref local1, elementOffset1), ref local3);
            Unsafe.WriteUnaligned<int>(ref Unsafe.Add<byte>(ref local2, elementOffset2), num4);
            elementOffset2 += 4;
            elementOffset1 += 2;
          }
          bytesConsumed = elementOffset1;
          bytesWritten = elementOffset2;
          return OperationStatus.Done;
        }
        bytesConsumed = elementOffset1;
        bytesWritten = elementOffset2;
        return OperationStatus.NeedMoreData;
      }
      bytesConsumed = elementOffset1;
      bytesWritten = elementOffset2;
      return OperationStatus.DestinationTooSmall;
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static int GetMaxEncodedToUtf8Length(int length)
    {
      if ((uint) length > 1610612733U)
        ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.length);
      return (length + 2) / 3 * 4;
    }

    public static OperationStatus EncodeToUtf8InPlace(
      Span<byte> buffer,
      int dataLength,
      out int bytesWritten)
    {
      int encodedToUtf8Length = Base64.GetMaxEncodedToUtf8Length(dataLength);
      if (buffer.Length >= encodedToUtf8Length)
      {
        int num1 = dataLength - dataLength / 3 * 3;
        int elementOffset1 = encodedToUtf8Length - 4;
        int elementOffset2 = dataLength - num1;
        ref byte local1 = ref Base64.s_encodingMap[0];
        ref byte local2 = ref MemoryMarshal.GetReference<byte>(buffer);
        switch (num1)
        {
          case 0:
            for (int elementOffset3 = elementOffset2 - 3; elementOffset3 >= 0; elementOffset3 -= 3)
            {
              int num2 = Base64.Encode(ref Unsafe.Add<byte>(ref local2, elementOffset3), ref local1);
              Unsafe.WriteUnaligned<int>(ref Unsafe.Add<byte>(ref local2, elementOffset1), num2);
              elementOffset1 -= 4;
            }
            bytesWritten = encodedToUtf8Length;
            return OperationStatus.Done;
          case 1:
            int num3 = Base64.EncodeAndPadTwo(ref Unsafe.Add<byte>(ref local2, elementOffset2), ref local1);
            Unsafe.WriteUnaligned<int>(ref Unsafe.Add<byte>(ref local2, elementOffset1), num3);
            elementOffset1 -= 4;
            goto case 0;
          default:
            int num4 = Base64.EncodeAndPadOne(ref Unsafe.Add<byte>(ref local2, elementOffset2), ref local1);
            Unsafe.WriteUnaligned<int>(ref Unsafe.Add<byte>(ref local2, elementOffset1), num4);
            elementOffset1 -= 4;
            goto case 0;
        }
      }
      else
      {
        bytesWritten = 0;
        return OperationStatus.DestinationTooSmall;
      }
    }

    [MethodImpl((MethodImplOptions) 256)]
    private static int Encode(ref byte threeBytes, ref byte encodingMap)
    {
      int num = (int) threeBytes << 16 | (int) Unsafe.Add<byte>(ref threeBytes, 1) << 8 | (int) Unsafe.Add<byte>(ref threeBytes, 2);
      return (int) Unsafe.Add<byte>(ref encodingMap, num >> 18) | (int) Unsafe.Add<byte>(ref encodingMap, num >> 12 & 63) << 8 | (int) Unsafe.Add<byte>(ref encodingMap, num >> 6 & 63) << 16 | (int) Unsafe.Add<byte>(ref encodingMap, num & 63) << 24;
    }

    [MethodImpl((MethodImplOptions) 256)]
    private static int EncodeAndPadOne(ref byte twoBytes, ref byte encodingMap)
    {
      int num = (int) twoBytes << 16 | (int) Unsafe.Add<byte>(ref twoBytes, 1) << 8;
      return (int) Unsafe.Add<byte>(ref encodingMap, num >> 18) | (int) Unsafe.Add<byte>(ref encodingMap, num >> 12 & 63) << 8 | (int) Unsafe.Add<byte>(ref encodingMap, num >> 6 & 63) << 16 | 1023410176;
    }

    [MethodImpl((MethodImplOptions) 256)]
    private static int EncodeAndPadTwo(ref byte oneByte, ref byte encodingMap)
    {
      int num = (int) oneByte << 8;
      return (int) Unsafe.Add<byte>(ref encodingMap, num >> 10) | (int) Unsafe.Add<byte>(ref encodingMap, num >> 4 & 63) << 8 | 3997696 | 1023410176;
    }
  }
}
