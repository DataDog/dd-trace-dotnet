﻿// Decompiled with JetBrains decompiler
// Type: System.SpanHelpers
// Assembly: System.Memory, Version=4.0.1.2, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51
// MVID: 805945F3-27B0-47AD-B8F6-389D9D8F82C3
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Memory.4.5.5\lib\net461\System.Memory.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Memory.4.5.5\lib\net461\System.Memory.xml

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Datadog.System.Runtime.CompilerServices.Unsafe;
using Datadog.System.Runtime.InteropServices;

namespace Datadog.System
{
    internal static class SpanHelpers
    {
        private const ulong XorPowerOfTwoToHighByte = 283686952306184;
        private const ulong XorPowerOfTwoToHighChar = 4295098372;

        [MethodImpl((MethodImplOptions)256)]
        public static int BinarySearch<T, TComparable>(
          this ReadOnlySpan<T> span,
          TComparable comparable)
          where TComparable : IComparable<T>
        {
            if ((object)comparable == null)
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.comparable);
            return SpanHelpers.BinarySearch<T, TComparable>(ref MemoryMarshal.GetReference<T>(span), span.Length, comparable);
        }

        public static int BinarySearch<T, TComparable>(
          ref T spanStart,
          int length,
          TComparable comparable)
          where TComparable : IComparable<T>
        {
            int num1 = 0;
            int num2 = length - 1;
            while (num1 <= num2)
            {
                int elementOffset = num2 + num1 >>> 1;
                int num3 = comparable.CompareTo(Unsafe.Add<T>(ref spanStart, elementOffset));
                if (num3 == 0)
                    return elementOffset;
                if (num3 > 0)
                    num1 = elementOffset + 1;
                else
                    num2 = elementOffset - 1;
            }
            return ~num1;
        }

        public static int IndexOf(
          ref byte searchSpace,
          int searchSpaceLength,
          ref byte value,
          int valueLength)
        {
            if (valueLength == 0)
                return 0;
            byte num1 = value;
            ref byte local = ref Unsafe.Add<byte>(ref value, 1);
            int length1 = valueLength - 1;
            int elementOffset = 0;
            int num2;
            while (true)
            {
                int length2 = searchSpaceLength - elementOffset - length1;
                if (length2 > 0)
                {
                    int num3 = SpanHelpers.IndexOf(ref Unsafe.Add<byte>(ref searchSpace, elementOffset), num1, length2);
                    if (num3 != -1)
                    {
                        num2 = elementOffset + num3;
                        if (!SpanHelpers.SequenceEqual<byte>(ref Unsafe.Add<byte>(ref searchSpace, num2 + 1), ref local, length1))
                            elementOffset = num2 + 1;
                        else
                            break;
                    }
                    else
                        goto label_8;
                }
                else
                    goto label_8;
            }
            return num2;
        label_8:
            return -1;
        }

        public static int IndexOfAny(
          ref byte searchSpace,
          int searchSpaceLength,
          ref byte value,
          int valueLength)
        {
            if (valueLength == 0)
                return 0;
            int num1 = -1;
            for (int elementOffset = 0; elementOffset < valueLength; ++elementOffset)
            {
                int num2 = SpanHelpers.IndexOf(ref searchSpace, Unsafe.Add<byte>(ref value, elementOffset), searchSpaceLength);
                if ((uint)num2 < (uint)num1)
                {
                    num1 = num2;
                    searchSpaceLength = num2;
                    if (num1 == 0)
                        break;
                }
            }
            return num1;
        }

        public static int LastIndexOfAny(
          ref byte searchSpace,
          int searchSpaceLength,
          ref byte value,
          int valueLength)
        {
            if (valueLength == 0)
                return 0;
            int num1 = -1;
            for (int elementOffset = 0; elementOffset < valueLength; ++elementOffset)
            {
                int num2 = SpanHelpers.LastIndexOf(ref searchSpace, Unsafe.Add<byte>(ref value, elementOffset), searchSpaceLength);
                if (num2 > num1)
                    num1 = num2;
            }
            return num1;
        }

        //public static unsafe int IndexOf(ref byte searchSpace, byte value, int length)
        //{
        //    uint num1 = (uint)value;
        //    IntPtr byteOffset = (IntPtr)0;
        //    IntPtr num2 = (IntPtr)length;
        //    if (Vector.IsHardwareAccelerated && length >= Vector<byte>.Count * 2)
        //        num2 = (IntPtr)(Vector<byte>.Count - ((int)Unsafe.AsPointer<byte>(ref searchSpace) & Vector<byte>.Count - 1) & Vector<byte>.Count - 1);
        //    while (true)
        //    {
        //        while ((UIntPtr)(void*)num2 >= new UIntPtr(8))
        //        {
        //            num2 -= 8;
        //            if ((int)num1 != (int)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset))
        //            {
        //                if ((int)num1 != (int)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 1))
        //                {
        //                    if ((int)num1 != (int)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 2))
        //                    {
        //                        if ((int)num1 != (int)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 3))
        //                        {
        //                            if ((int)num1 == (int)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 4))
        //                                return (int)(void*)(byteOffset + 4);
        //                            if ((int)num1 == (int)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 5))
        //                                return (int)(void*)(byteOffset + 5);
        //                            if ((int)num1 == (int)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 6))
        //                                return (int)(void*)(byteOffset + 6);
        //                            if ((int)num1 == (int)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 7))
        //                                return (int)(void*)(byteOffset + 7);
        //                            byteOffset += 8;
        //                        }
        //                        else
        //                            goto label_33;
        //                    }
        //                    else
        //                        goto label_32;
        //                }
        //                else
        //                    goto label_31;
        //            }
        //            else
        //                goto label_30;
        //        }
        //        if ((UIntPtr)(void*)num2 >= new UIntPtr(4))
        //        {
        //            num2 -= 4;
        //            if ((int)num1 != (int)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset))
        //            {
        //                if ((int)num1 != (int)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 1))
        //                {
        //                    if ((int)num1 != (int)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 2))
        //                    {
        //                        if ((int)num1 != (int)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 3))
        //                            byteOffset += 4;
        //                        else
        //                            goto label_33;
        //                    }
        //                    else
        //                        goto label_32;
        //                }
        //                else
        //                    goto label_31;
        //            }
        //            else
        //                goto label_30;
        //        }
        //        while ((UIntPtr)(void*)num2 > UIntPtr.Zero)
        //        {
        //            num2 -= 1;
        //            if ((int)num1 != (int)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset))
        //                byteOffset += 1;
        //            else
        //                goto label_30;
        //        }
        //        if (Vector.IsHardwareAccelerated && (int)(void*)byteOffset < length)
        //        {
        //            IntPtr num3 = (IntPtr)(length - (int)(void*)byteOffset & ~(Vector<byte>.Count - 1));
        //            Vector<byte> vector = SpanHelpers.GetVector(value);
        //            while ((void*)num3 > (void*)byteOffset)
        //            {
        //                Vector<byte> match = Vector.Equals<byte>(vector, Unsafe.ReadUnaligned<Vector<byte>>(ref Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset)));
        //                if (!Vector<byte>.Zero.Equals(match))
        //                    return (int)(void*)byteOffset + SpanHelpers.LocateFirstFoundByte(match);
        //                byteOffset += Vector<byte>.Count;
        //            }
        //            if ((int)(void*)byteOffset < length)
        //                num2 = (IntPtr)(length - (int)(void*)byteOffset);
        //            else
        //                break;
        //        }
        //        else
        //            break;
        //    }
        //    return -1;
        //label_30:
        //    return (int)(void*)byteOffset;
        //label_31:
        //    return (int)(void*)(byteOffset + 1);
        //label_32:
        //    return (int)(void*)(byteOffset + 2);
        //label_33:
        //    return (int)(void*)(byteOffset + 3);
        //}

        //public static int LastIndexOf(
        //  ref byte searchSpace,
        //  int searchSpaceLength,
        //  ref byte value,
        //  int valueLength)
        //{
        //    if (valueLength == 0)
        //        return 0;
        //    byte num1 = value;
        //    ref byte local = ref Unsafe.Add<byte>(ref value, 1);
        //    int length1 = valueLength - 1;
        //    int num2 = 0;
        //    int num3;
        //    while (true)
        //    {
        //        int length2 = searchSpaceLength - num2 - length1;
        //        if (length2 > 0)
        //        {
        //            num3 = SpanHelpers.LastIndexOf(ref searchSpace, num1, length2);
        //            if (num3 != -1)
        //            {
        //                if (!SpanHelpers.SequenceEqual<byte>(ref Unsafe.Add<byte>(ref searchSpace, num3 + 1), ref local, length1))
        //                    num2 += length2 - num3;
        //                else
        //                    break;
        //            }
        //            else
        //                goto label_8;
        //        }
        //        else
        //            goto label_8;
        //    }
        //    return num3;
        //label_8:
        //    return -1;
        //}

        //public static unsafe int LastIndexOf(ref byte searchSpace, byte value, int length)
        //{
        //    uint num1 = (uint)value;
        //    IntPtr byteOffset = (IntPtr)length;
        //    IntPtr num2 = (IntPtr)length;
        //    if (Vector.IsHardwareAccelerated && length >= Vector<byte>.Count * 2)
        //    {
        //        int num3 = (int)Unsafe.AsPointer<byte>(ref searchSpace) & Vector<byte>.Count - 1;
        //        num2 = (IntPtr)((length & Vector<byte>.Count - 1) + num3 & Vector<byte>.Count - 1);
        //    }
        //    while (true)
        //    {
        //        while ((UIntPtr)(void*)num2 >= new UIntPtr(8))
        //        {
        //            num2 -= 8;
        //            byteOffset -= 8;
        //            if ((int)num1 == (int)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 7))
        //                return (int)(void*)(byteOffset + 7);
        //            if ((int)num1 == (int)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 6))
        //                return (int)(void*)(byteOffset + 6);
        //            if ((int)num1 == (int)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 5))
        //                return (int)(void*)(byteOffset + 5);
        //            if ((int)num1 == (int)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 4))
        //                return (int)(void*)(byteOffset + 4);
        //            if ((int)num1 != (int)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 3))
        //            {
        //                if ((int)num1 != (int)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 2))
        //                {
        //                    if ((int)num1 != (int)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 1))
        //                    {
        //                        if ((int)num1 == (int)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset))
        //                            goto label_27;
        //                    }
        //                    else
        //                        goto label_28;
        //                }
        //                else
        //                    goto label_29;
        //            }
        //            else
        //                goto label_30;
        //        }
        //        if ((UIntPtr)(void*)num2 >= new UIntPtr(4))
        //        {
        //            num2 -= 4;
        //            byteOffset -= 4;
        //            if ((int)num1 != (int)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 3))
        //            {
        //                if ((int)num1 != (int)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 2))
        //                {
        //                    if ((int)num1 != (int)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 1))
        //                    {
        //                        if ((int)num1 == (int)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset))
        //                            goto label_27;
        //                    }
        //                    else
        //                        goto label_28;
        //                }
        //                else
        //                    goto label_29;
        //            }
        //            else
        //                goto label_30;
        //        }
        //        while ((UIntPtr)(void*)num2 > UIntPtr.Zero)
        //        {
        //            num2 -= 1;
        //            byteOffset -= 1;
        //            if ((int)num1 == (int)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset))
        //                goto label_27;
        //        }
        //        if (Vector.IsHardwareAccelerated && (UIntPtr)(void*)byteOffset > UIntPtr.Zero)
        //        {
        //            IntPtr num4 = (IntPtr)((int)(void*)byteOffset & ~(Vector<byte>.Count - 1));
        //            Vector<byte> vector = SpanHelpers.GetVector(value);
        //            while ((UIntPtr)(void*)num4 > (UIntPtr)(Vector<byte>.Count - 1))
        //            {
        //                Vector<byte> match = Vector.Equals<byte>(vector, Unsafe.ReadUnaligned<Vector<byte>>(ref Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset - Vector<byte>.Count)));
        //                if (!Vector<byte>.Zero.Equals(match))
        //                    return (int)byteOffset - Vector<byte>.Count + SpanHelpers.LocateLastFoundByte(match);
        //                byteOffset -= Vector<byte>.Count;
        //                num4 -= Vector<byte>.Count;
        //            }
        //            if ((UIntPtr)(void*)byteOffset > UIntPtr.Zero)
        //                num2 = byteOffset;
        //            else
        //                break;
        //        }
        //        else
        //            break;
        //    }
        //    return -1;
        //label_27:
        //    return (int)(void*)byteOffset;
        //label_28:
        //    return (int)(void*)(byteOffset + 1);
        //label_29:
        //    return (int)(void*)(byteOffset + 2);
        //label_30:
        //    return (int)(void*)(byteOffset + 3);
        //}

        //public static unsafe int IndexOfAny(
        //  ref byte searchSpace,
        //  byte value0,
        //  byte value1,
        //  int length)
        //{
        //    uint num1 = (uint)value0;
        //    uint num2 = (uint)value1;
        //    IntPtr byteOffset = (IntPtr)0;
        //    IntPtr num3 = (IntPtr)length;
        //    if (Vector.IsHardwareAccelerated && length >= Vector<byte>.Count * 2)
        //        num3 = (IntPtr)(Vector<byte>.Count - ((int)Unsafe.AsPointer<byte>(ref searchSpace) & Vector<byte>.Count - 1) & Vector<byte>.Count - 1);
        //    while (true)
        //    {
        //        while ((UIntPtr)(void*)num3 >= new UIntPtr(8))
        //        {
        //            num3 -= 8;
        //            uint num4 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset);
        //            if ((int)num1 != (int)num4 && (int)num2 != (int)num4)
        //            {
        //                uint num5 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 1);
        //                if ((int)num1 != (int)num5 && (int)num2 != (int)num5)
        //                {
        //                    uint num6 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 2);
        //                    if ((int)num1 != (int)num6 && (int)num2 != (int)num6)
        //                    {
        //                        uint num7 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 3);
        //                        if ((int)num1 != (int)num7 && (int)num2 != (int)num7)
        //                        {
        //                            uint num8 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 4);
        //                            if ((int)num1 == (int)num8 || (int)num2 == (int)num8)
        //                                return (int)(void*)(byteOffset + 4);
        //                            uint num9 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 5);
        //                            if ((int)num1 == (int)num9 || (int)num2 == (int)num9)
        //                                return (int)(void*)(byteOffset + 5);
        //                            uint num10 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 6);
        //                            if ((int)num1 == (int)num10 || (int)num2 == (int)num10)
        //                                return (int)(void*)(byteOffset + 6);
        //                            uint num11 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 7);
        //                            if ((int)num1 == (int)num11 || (int)num2 == (int)num11)
        //                                return (int)(void*)(byteOffset + 7);
        //                            byteOffset += 8;
        //                        }
        //                        else
        //                            goto label_33;
        //                    }
        //                    else
        //                        goto label_32;
        //                }
        //                else
        //                    goto label_31;
        //            }
        //            else
        //                goto label_30;
        //        }
        //        if ((UIntPtr)(void*)num3 >= new UIntPtr(4))
        //        {
        //            num3 -= 4;
        //            uint num12 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset);
        //            if ((int)num1 != (int)num12 && (int)num2 != (int)num12)
        //            {
        //                uint num13 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 1);
        //                if ((int)num1 != (int)num13 && (int)num2 != (int)num13)
        //                {
        //                    uint num14 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 2);
        //                    if ((int)num1 != (int)num14 && (int)num2 != (int)num14)
        //                    {
        //                        uint num15 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 3);
        //                        if ((int)num1 != (int)num15 && (int)num2 != (int)num15)
        //                            byteOffset += 4;
        //                        else
        //                            goto label_33;
        //                    }
        //                    else
        //                        goto label_32;
        //                }
        //                else
        //                    goto label_31;
        //            }
        //            else
        //                goto label_30;
        //        }
        //        while ((UIntPtr)(void*)num3 > UIntPtr.Zero)
        //        {
        //            num3 -= 1;
        //            uint num16 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset);
        //            if ((int)num1 != (int)num16 && (int)num2 != (int)num16)
        //                byteOffset += 1;
        //            else
        //                goto label_30;
        //        }
        //        if (Vector.IsHardwareAccelerated && (int)(void*)byteOffset < length)
        //        {
        //            IntPtr num17 = (IntPtr)(length - (int)(void*)byteOffset & ~(Vector<byte>.Count - 1));
        //            Vector<byte> vector1 = SpanHelpers.GetVector(value0);
        //            Vector<byte> vector2 = SpanHelpers.GetVector(value1);
        //            while ((void*)num17 > (void*)byteOffset)
        //            {
        //                Vector<byte> vector3 = Unsafe.ReadUnaligned<Vector<byte>>(ref Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset));
        //                Vector<byte> match = Vector.BitwiseOr<byte>(Vector.Equals<byte>(vector3, vector1), Vector.Equals<byte>(vector3, vector2));
        //                if (!Vector<byte>.Zero.Equals(match))
        //                    return (int)(void*)byteOffset + SpanHelpers.LocateFirstFoundByte(match);
        //                byteOffset += Vector<byte>.Count;
        //            }
        //            if ((int)(void*)byteOffset < length)
        //                num3 = (IntPtr)(length - (int)(void*)byteOffset);
        //            else
        //                break;
        //        }
        //        else
        //            break;
        //    }
        //    return -1;
        //label_30:
        //    return (int)(void*)byteOffset;
        //label_31:
        //    return (int)(void*)(byteOffset + 1);
        //label_32:
        //    return (int)(void*)(byteOffset + 2);
        //label_33:
        //    return (int)(void*)(byteOffset + 3);
        //}

        //public static unsafe int IndexOfAny(
        //  ref byte searchSpace,
        //  byte value0,
        //  byte value1,
        //  byte value2,
        //  int length)
        //{
        //    uint num1 = (uint)value0;
        //    uint num2 = (uint)value1;
        //    uint num3 = (uint)value2;
        //    IntPtr byteOffset = (IntPtr)0;
        //    IntPtr num4 = (IntPtr)length;
        //    if (Vector.IsHardwareAccelerated && length >= Vector<byte>.Count * 2)
        //        num4 = (IntPtr)(Vector<byte>.Count - ((int)Unsafe.AsPointer<byte>(ref searchSpace) & Vector<byte>.Count - 1) & Vector<byte>.Count - 1);
        //    while (true)
        //    {
        //        while ((UIntPtr)(void*)num4 >= new UIntPtr(8))
        //        {
        //            num4 -= 8;
        //            uint num5 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset);
        //            if ((int)num1 != (int)num5 && (int)num2 != (int)num5 && (int)num3 != (int)num5)
        //            {
        //                uint num6 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 1);
        //                if ((int)num1 != (int)num6 && (int)num2 != (int)num6 && (int)num3 != (int)num6)
        //                {
        //                    uint num7 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 2);
        //                    if ((int)num1 != (int)num7 && (int)num2 != (int)num7 && (int)num3 != (int)num7)
        //                    {
        //                        uint num8 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 3);
        //                        if ((int)num1 != (int)num8 && (int)num2 != (int)num8 && (int)num3 != (int)num8)
        //                        {
        //                            uint num9 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 4);
        //                            if ((int)num1 == (int)num9 || (int)num2 == (int)num9 || (int)num3 == (int)num9)
        //                                return (int)(void*)(byteOffset + 4);
        //                            uint num10 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 5);
        //                            if ((int)num1 == (int)num10 || (int)num2 == (int)num10 || (int)num3 == (int)num10)
        //                                return (int)(void*)(byteOffset + 5);
        //                            uint num11 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 6);
        //                            if ((int)num1 == (int)num11 || (int)num2 == (int)num11 || (int)num3 == (int)num11)
        //                                return (int)(void*)(byteOffset + 6);
        //                            uint num12 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 7);
        //                            if ((int)num1 == (int)num12 || (int)num2 == (int)num12 || (int)num3 == (int)num12)
        //                                return (int)(void*)(byteOffset + 7);
        //                            byteOffset += 8;
        //                        }
        //                        else
        //                            goto label_33;
        //                    }
        //                    else
        //                        goto label_32;
        //                }
        //                else
        //                    goto label_31;
        //            }
        //            else
        //                goto label_30;
        //        }
        //        if ((UIntPtr)(void*)num4 >= new UIntPtr(4))
        //        {
        //            num4 -= 4;
        //            uint num13 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset);
        //            if ((int)num1 != (int)num13 && (int)num2 != (int)num13 && (int)num3 != (int)num13)
        //            {
        //                uint num14 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 1);
        //                if ((int)num1 != (int)num14 && (int)num2 != (int)num14 && (int)num3 != (int)num14)
        //                {
        //                    uint num15 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 2);
        //                    if ((int)num1 != (int)num15 && (int)num2 != (int)num15 && (int)num3 != (int)num15)
        //                    {
        //                        uint num16 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 3);
        //                        if ((int)num1 != (int)num16 && (int)num2 != (int)num16 && (int)num3 != (int)num16)
        //                            byteOffset += 4;
        //                        else
        //                            goto label_33;
        //                    }
        //                    else
        //                        goto label_32;
        //                }
        //                else
        //                    goto label_31;
        //            }
        //            else
        //                goto label_30;
        //        }
        //        while ((UIntPtr)(void*)num4 > UIntPtr.Zero)
        //        {
        //            num4 -= 1;
        //            uint num17 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset);
        //            if ((int)num1 != (int)num17 && (int)num2 != (int)num17 && (int)num3 != (int)num17)
        //                byteOffset += 1;
        //            else
        //                goto label_30;
        //        }
        //        if (Vector.IsHardwareAccelerated && (int)(void*)byteOffset < length)
        //        {
        //            IntPtr num18 = (IntPtr)(length - (int)(void*)byteOffset & ~(Vector<byte>.Count - 1));
        //            Vector<byte> vector1 = SpanHelpers.GetVector(value0);
        //            Vector<byte> vector2 = SpanHelpers.GetVector(value1);
        //            Vector<byte> vector3 = SpanHelpers.GetVector(value2);
        //            while ((void*)num18 > (void*)byteOffset)
        //            {
        //                Vector<byte> vector4 = Unsafe.ReadUnaligned<Vector<byte>>(ref Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset));
        //                Vector<byte> match = Vector.BitwiseOr<byte>(Vector.BitwiseOr<byte>(Vector.Equals<byte>(vector4, vector1), Vector.Equals<byte>(vector4, vector2)), Vector.Equals<byte>(vector4, vector3));
        //                if (!Vector<byte>.Zero.Equals(match))
        //                    return (int)(void*)byteOffset + SpanHelpers.LocateFirstFoundByte(match);
        //                byteOffset += Vector<byte>.Count;
        //            }
        //            if ((int)(void*)byteOffset < length)
        //                num4 = (IntPtr)(length - (int)(void*)byteOffset);
        //            else
        //                break;
        //        }
        //        else
        //            break;
        //    }
        //    return -1;
        //label_30:
        //    return (int)(void*)byteOffset;
        //label_31:
        //    return (int)(void*)(byteOffset + 1);
        //label_32:
        //    return (int)(void*)(byteOffset + 2);
        //label_33:
        //    return (int)(void*)(byteOffset + 3);
        //}

        //public static unsafe int LastIndexOfAny(
        //  ref byte searchSpace,
        //  byte value0,
        //  byte value1,
        //  int length)
        //{
        //    uint num1 = (uint)value0;
        //    uint num2 = (uint)value1;
        //    IntPtr byteOffset = (IntPtr)length;
        //    IntPtr num3 = (IntPtr)length;
        //    if (Vector.IsHardwareAccelerated && length >= Vector<byte>.Count * 2)
        //    {
        //        int num4 = (int)Unsafe.AsPointer<byte>(ref searchSpace) & Vector<byte>.Count - 1;
        //        num3 = (IntPtr)((length & Vector<byte>.Count - 1) + num4 & Vector<byte>.Count - 1);
        //    }
        //    while (true)
        //    {
        //        while ((UIntPtr)(void*)num3 >= new UIntPtr(8))
        //        {
        //            num3 -= 8;
        //            byteOffset -= 8;
        //            uint num5 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 7);
        //            if ((int)num1 == (int)num5 || (int)num2 == (int)num5)
        //                return (int)(void*)(byteOffset + 7);
        //            uint num6 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 6);
        //            if ((int)num1 == (int)num6 || (int)num2 == (int)num6)
        //                return (int)(void*)(byteOffset + 6);
        //            uint num7 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 5);
        //            if ((int)num1 == (int)num7 || (int)num2 == (int)num7)
        //                return (int)(void*)(byteOffset + 5);
        //            uint num8 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 4);
        //            if ((int)num1 == (int)num8 || (int)num2 == (int)num8)
        //                return (int)(void*)(byteOffset + 4);
        //            uint num9 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 3);
        //            if ((int)num1 != (int)num9 && (int)num2 != (int)num9)
        //            {
        //                uint num10 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 2);
        //                if ((int)num1 != (int)num10 && (int)num2 != (int)num10)
        //                {
        //                    uint num11 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 1);
        //                    if ((int)num1 != (int)num11 && (int)num2 != (int)num11)
        //                    {
        //                        uint num12 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset);
        //                        if ((int)num1 == (int)num12 || (int)num2 == (int)num12)
        //                            goto label_27;
        //                    }
        //                    else
        //                        goto label_28;
        //                }
        //                else
        //                    goto label_29;
        //            }
        //            else
        //                goto label_30;
        //        }
        //        if ((UIntPtr)(void*)num3 >= new UIntPtr(4))
        //        {
        //            num3 -= 4;
        //            byteOffset -= 4;
        //            uint num13 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 3);
        //            if ((int)num1 != (int)num13 && (int)num2 != (int)num13)
        //            {
        //                uint num14 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 2);
        //                if ((int)num1 != (int)num14 && (int)num2 != (int)num14)
        //                {
        //                    uint num15 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 1);
        //                    if ((int)num1 != (int)num15 && (int)num2 != (int)num15)
        //                    {
        //                        uint num16 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset);
        //                        if ((int)num1 == (int)num16 || (int)num2 == (int)num16)
        //                            goto label_27;
        //                    }
        //                    else
        //                        goto label_28;
        //                }
        //                else
        //                    goto label_29;
        //            }
        //            else
        //                goto label_30;
        //        }
        //        while ((UIntPtr)(void*)num3 > UIntPtr.Zero)
        //        {
        //            num3 -= 1;
        //            byteOffset -= 1;
        //            uint num17 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset);
        //            if ((int)num1 == (int)num17 || (int)num2 == (int)num17)
        //                goto label_27;
        //        }
        //        if (Vector.IsHardwareAccelerated && (UIntPtr)(void*)byteOffset > UIntPtr.Zero)
        //        {
        //            IntPtr num18 = (IntPtr)((int)(void*)byteOffset & ~(Vector<byte>.Count - 1));
        //            Vector<byte> vector1 = SpanHelpers.GetVector(value0);
        //            Vector<byte> vector2 = SpanHelpers.GetVector(value1);
        //            while ((UIntPtr)(void*)num18 > (UIntPtr)(Vector<byte>.Count - 1))
        //            {
        //                Vector<byte> vector3 = Unsafe.ReadUnaligned<Vector<byte>>(ref Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset - Vector<byte>.Count));
        //                Vector<byte> match = Vector.BitwiseOr<byte>(Vector.Equals<byte>(vector3, vector1), Vector.Equals<byte>(vector3, vector2));
        //                if (!Vector<byte>.Zero.Equals(match))
        //                    return (int)byteOffset - Vector<byte>.Count + SpanHelpers.LocateLastFoundByte(match);
        //                byteOffset -= Vector<byte>.Count;
        //                num18 -= Vector<byte>.Count;
        //            }
        //            if ((UIntPtr)(void*)byteOffset > UIntPtr.Zero)
        //                num3 = byteOffset;
        //            else
        //                break;
        //        }
        //        else
        //            break;
        //    }
        //    return -1;
        //label_27:
        //    return (int)(void*)byteOffset;
        //label_28:
        //    return (int)(void*)(byteOffset + 1);
        //label_29:
        //    return (int)(void*)(byteOffset + 2);
        //label_30:
        //    return (int)(void*)(byteOffset + 3);
        //}

        //public static unsafe int LastIndexOfAny(
        //  ref byte searchSpace,
        //  byte value0,
        //  byte value1,
        //  byte value2,
        //  int length)
        //{
        //    uint num1 = (uint)value0;
        //    uint num2 = (uint)value1;
        //    uint num3 = (uint)value2;
        //    IntPtr byteOffset = (IntPtr)length;
        //    IntPtr num4 = (IntPtr)length;
        //    if (Vector.IsHardwareAccelerated && length >= Vector<byte>.Count * 2)
        //    {
        //        int num5 = (int)Unsafe.AsPointer<byte>(ref searchSpace) & Vector<byte>.Count - 1;
        //        num4 = (IntPtr)((length & Vector<byte>.Count - 1) + num5 & Vector<byte>.Count - 1);
        //    }
        //    while (true)
        //    {
        //        while ((UIntPtr)(void*)num4 >= new UIntPtr(8))
        //        {
        //            num4 -= 8;
        //            byteOffset -= 8;
        //            uint num6 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 7);
        //            if ((int)num1 == (int)num6 || (int)num2 == (int)num6 || (int)num3 == (int)num6)
        //                return (int)(void*)(byteOffset + 7);
        //            uint num7 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 6);
        //            if ((int)num1 == (int)num7 || (int)num2 == (int)num7 || (int)num3 == (int)num7)
        //                return (int)(void*)(byteOffset + 6);
        //            uint num8 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 5);
        //            if ((int)num1 == (int)num8 || (int)num2 == (int)num8 || (int)num3 == (int)num8)
        //                return (int)(void*)(byteOffset + 5);
        //            uint num9 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 4);
        //            if ((int)num1 == (int)num9 || (int)num2 == (int)num9 || (int)num3 == (int)num9)
        //                return (int)(void*)(byteOffset + 4);
        //            uint num10 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 3);
        //            if ((int)num1 != (int)num10 && (int)num2 != (int)num10 && (int)num3 != (int)num10)
        //            {
        //                uint num11 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 2);
        //                if ((int)num1 != (int)num11 && (int)num2 != (int)num11 && (int)num3 != (int)num11)
        //                {
        //                    uint num12 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 1);
        //                    if ((int)num1 != (int)num12 && (int)num2 != (int)num12 && (int)num3 != (int)num12)
        //                    {
        //                        uint num13 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset);
        //                        if ((int)num1 == (int)num13 || (int)num2 == (int)num13 || (int)num3 == (int)num13)
        //                            goto label_27;
        //                    }
        //                    else
        //                        goto label_28;
        //                }
        //                else
        //                    goto label_29;
        //            }
        //            else
        //                goto label_30;
        //        }
        //        if ((UIntPtr)(void*)num4 >= new UIntPtr(4))
        //        {
        //            num4 -= 4;
        //            byteOffset -= 4;
        //            uint num14 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 3);
        //            if ((int)num1 != (int)num14 && (int)num2 != (int)num14 && (int)num3 != (int)num14)
        //            {
        //                uint num15 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 2);
        //                if ((int)num1 != (int)num15 && (int)num2 != (int)num15 && (int)num3 != (int)num15)
        //                {
        //                    uint num16 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset + 1);
        //                    if ((int)num1 != (int)num16 && (int)num2 != (int)num16 && (int)num3 != (int)num16)
        //                    {
        //                        uint num17 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset);
        //                        if ((int)num1 == (int)num17 || (int)num2 == (int)num17 || (int)num3 == (int)num17)
        //                            goto label_27;
        //                    }
        //                    else
        //                        goto label_28;
        //                }
        //                else
        //                    goto label_29;
        //            }
        //            else
        //                goto label_30;
        //        }
        //        while ((UIntPtr)(void*)num4 > UIntPtr.Zero)
        //        {
        //            num4 -= 1;
        //            byteOffset -= 1;
        //            uint num18 = (uint)Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset);
        //            if ((int)num1 == (int)num18 || (int)num2 == (int)num18 || (int)num3 == (int)num18)
        //                goto label_27;
        //        }
        //        if (Vector.IsHardwareAccelerated && (UIntPtr)(void*)byteOffset > UIntPtr.Zero)
        //        {
        //            IntPtr num19 = (IntPtr)((int)(void*)byteOffset & ~(Vector<byte>.Count - 1));
        //            Vector<byte> vector1 = SpanHelpers.GetVector(value0);
        //            Vector<byte> vector2 = SpanHelpers.GetVector(value1);
        //            Vector<byte> vector3 = SpanHelpers.GetVector(value2);
        //            while ((UIntPtr)(void*)num19 > (UIntPtr)(Vector<byte>.Count - 1))
        //            {
        //                Vector<byte> vector4 = Unsafe.ReadUnaligned<Vector<byte>>(ref Unsafe.AddByteOffset<byte>(ref searchSpace, byteOffset - Vector<byte>.Count));
        //                Vector<byte> match = Vector.BitwiseOr<byte>(Vector.BitwiseOr<byte>(Vector.Equals<byte>(vector4, vector1), Vector.Equals<byte>(vector4, vector2)), Vector.Equals<byte>(vector4, vector3));
        //                if (!Vector<byte>.Zero.Equals(match))
        //                    return (int)byteOffset - Vector<byte>.Count + SpanHelpers.LocateLastFoundByte(match);
        //                byteOffset -= Vector<byte>.Count;
        //                num19 -= Vector<byte>.Count;
        //            }
        //            if ((UIntPtr)(void*)byteOffset > UIntPtr.Zero)
        //                num4 = byteOffset;
        //            else
        //                break;
        //        }
        //        else
        //            break;
        //    }
        //    return -1;
        //label_27:
        //    return (int)(void*)byteOffset;
        //label_28:
        //    return (int)(void*)(byteOffset + 1);
        //label_29:
        //    return (int)(void*)(byteOffset + 2);
        //label_30:
        //    return (int)(void*)(byteOffset + 3);
        //}

        //public static unsafe bool SequenceEqual(ref byte first, ref byte second, NUInt length)
        //{
        //    if (!Unsafe.AreSame<byte>(ref first, ref second))
        //    {
        //        IntPtr byteOffset1 = (IntPtr)0;
        //        IntPtr num = (IntPtr)(void*)length;
        //        if (Vector.IsHardwareAccelerated && (UIntPtr)(void*)num >= (UIntPtr)Vector<byte>.Count)
        //        {
        //            IntPtr byteOffset2 = num - Vector<byte>.Count;
        //            while ((void*)byteOffset2 > (void*)byteOffset1)
        //            {
        //                if (!Vector<byte>.op_Inequality(Unsafe.ReadUnaligned<Vector<byte>>(ref Unsafe.AddByteOffset<byte>(ref first, byteOffset1)), Unsafe.ReadUnaligned<Vector<byte>>(ref Unsafe.AddByteOffset<byte>(ref second, byteOffset1))))
        //                    byteOffset1 += Vector<byte>.Count;
        //                else
        //                    goto label_17;
        //            }
        //            return Vector<byte>.op_Equality(Unsafe.ReadUnaligned<Vector<byte>>(ref Unsafe.AddByteOffset<byte>(ref first, byteOffset2)), Unsafe.ReadUnaligned<Vector<byte>>(ref Unsafe.AddByteOffset<byte>(ref second, byteOffset2)));
        //        }
        //        if ((UIntPtr)(void*)num >= (UIntPtr)sizeof(UIntPtr))
        //        {
        //            IntPtr byteOffset3 = num - sizeof(UIntPtr);
        //            while ((void*)byteOffset3 > (void*)byteOffset1)
        //            {
        //                if (!(Unsafe.ReadUnaligned<UIntPtr>(ref Unsafe.AddByteOffset<byte>(ref first, byteOffset1)) != Unsafe.ReadUnaligned<UIntPtr>(ref Unsafe.AddByteOffset<byte>(ref second, byteOffset1))))
        //                    byteOffset1 += sizeof(UIntPtr);
        //                else
        //                    goto label_17;
        //            }
        //            return Unsafe.ReadUnaligned<UIntPtr>(ref Unsafe.AddByteOffset<byte>(ref first, byteOffset3)) == Unsafe.ReadUnaligned<UIntPtr>(ref Unsafe.AddByteOffset<byte>(ref second, byteOffset3));
        //        }
        //        while ((void*)num > (void*)byteOffset1)
        //        {
        //            if ((int)Unsafe.AddByteOffset<byte>(ref first, byteOffset1) == (int)Unsafe.AddByteOffset<byte>(ref second, byteOffset1))
        //                byteOffset1 += 1;
        //            else
        //                goto label_17;
        //        }
        //        goto label_16;
        //    label_17:
        //        return false;
        //    }
        //label_16:
        //    return true;
        //}

        //[MethodImpl((MethodImplOptions)256)]
        //private static int LocateFirstFoundByte(Vector<byte> match)
        //{
        //    Vector<ulong> vector = Vector.AsVectorUInt64<byte>(match);
        //    ulong match1 = 0;
        //    int num;
        //    for (num = 0; num < Vector<ulong>.Count; ++num)
        //    {
        //        match1 = vector[num];
        //        if (match1 != 0UL)
        //            break;
        //    }
        //    return num * 8 + SpanHelpers.LocateFirstFoundByte(match1);
        //}

        //public static unsafe int SequenceCompareTo(
        //  ref byte first,
        //  int firstLength,
        //  ref byte second,
        //  int secondLength)
        //{
        //    if (!Unsafe.AreSame<byte>(ref first, ref second))
        //    {
        //        IntPtr num1 = (IntPtr)(firstLength < secondLength ? firstLength : secondLength);
        //        IntPtr byteOffset = (IntPtr)0;
        //        IntPtr num2 = (IntPtr)(void*)num1;
        //        if (Vector.IsHardwareAccelerated && (UIntPtr)(void*)num2 > (UIntPtr)Vector<byte>.Count)
        //        {
        //            IntPtr num3 = num2 - Vector<byte>.Count;
        //            while ((void*)num3 > (void*)byteOffset && !Vector<byte>.op_Inequality(Unsafe.ReadUnaligned<Vector<byte>>(ref Unsafe.AddByteOffset<byte>(ref first, byteOffset)), Unsafe.ReadUnaligned<Vector<byte>>(ref Unsafe.AddByteOffset<byte>(ref second, byteOffset))))
        //                byteOffset += Vector<byte>.Count;
        //        }
        //        else if ((UIntPtr)(void*)num2 > (UIntPtr)sizeof(UIntPtr))
        //        {
        //            IntPtr num4 = num2 - sizeof(UIntPtr);
        //            while ((void*)num4 > (void*)byteOffset && !(Unsafe.ReadUnaligned<UIntPtr>(ref Unsafe.AddByteOffset<byte>(ref first, byteOffset)) != Unsafe.ReadUnaligned<UIntPtr>(ref Unsafe.AddByteOffset<byte>(ref second, byteOffset))))
        //                byteOffset += sizeof(UIntPtr);
        //        }
        //        while ((void*)num1 > (void*)byteOffset)
        //        {
        //            int num5 = Unsafe.AddByteOffset<byte>(ref first, byteOffset).CompareTo(Unsafe.AddByteOffset<byte>(ref second, byteOffset));
        //            if (num5 != 0)
        //                return num5;
        //            byteOffset += 1;
        //        }
        //    }
        //    return firstLength - secondLength;
        //}

        //[MethodImpl((MethodImplOptions)256)]
        //private static int LocateLastFoundByte(Vector<byte> match)
        //{
        //    Vector<ulong> vector = Vector.AsVectorUInt64<byte>(match);
        //    ulong match1 = 0;
        //    int num;
        //    for (num = Vector<ulong>.Count - 1; num >= 0; --num)
        //    {
        //        match1 = vector[num];
        //        if (match1 != 0UL)
        //            break;
        //    }
        //    return num * 8 + SpanHelpers.LocateLastFoundByte(match1);
        //}

        [MethodImpl((MethodImplOptions)256)]
        private static int LocateFirstFoundByte(ulong match) => (int)((match ^ match - 1UL) * 283686952306184UL >> 57);

        [MethodImpl((MethodImplOptions)256)]
        private static int LocateLastFoundByte(ulong match)
        {
            int num = 7;
            while ((long)match > 0L)
            {
                match <<= 8;
                --num;
            }
            return num;
        }

        //[MethodImpl((MethodImplOptions)256)]
        //private static Vector<byte> GetVector(byte vectorByte) => Vector.AsVectorByte<uint>(new Vector<uint>((uint)vectorByte * 16843009U));

        //public static unsafe int SequenceCompareTo(
        //  ref char first,
        //  int firstLength,
        //  ref char second,
        //  int secondLength)
        //{
        //    int num1 = firstLength - secondLength;
        //    if (!Unsafe.AreSame<char>(ref first, ref second))
        //    {
        //        IntPtr num2 = (IntPtr)(firstLength < secondLength ? firstLength : secondLength);
        //        IntPtr elementOffset = (IntPtr)0;
        //        if ((UIntPtr)(void*)num2 >= (UIntPtr)(sizeof(UIntPtr) / 2))
        //        {
        //            if (Vector.IsHardwareAccelerated && (UIntPtr)(void*)num2 >= (UIntPtr)Vector<ushort>.Count)
        //            {
        //                IntPtr num3 = num2 - Vector<ushort>.Count;
        //                while (!Vector<ushort>.op_Inequality(Unsafe.ReadUnaligned<Vector<ushort>>(ref Unsafe.As<char, byte>(ref Unsafe.Add<char>(ref first, elementOffset))), Unsafe.ReadUnaligned<Vector<ushort>>(ref Unsafe.As<char, byte>(ref Unsafe.Add<char>(ref second, elementOffset)))))
        //                {
        //                    elementOffset += Vector<ushort>.Count;
        //                    if ((void*)num3 < (void*)elementOffset)
        //                        break;
        //                }
        //            }
        //            while ((void*)num2 >= (void*)(elementOffset + sizeof(UIntPtr) / 2) && !(Unsafe.ReadUnaligned<UIntPtr>(ref Unsafe.As<char, byte>(ref Unsafe.Add<char>(ref first, elementOffset))) != Unsafe.ReadUnaligned<UIntPtr>(ref Unsafe.As<char, byte>(ref Unsafe.Add<char>(ref second, elementOffset)))))
        //                elementOffset += sizeof(UIntPtr) / 2;
        //        }
        //        if (sizeof(UIntPtr) > 4 && (void*)num2 >= (void*)(elementOffset + 2) && Unsafe.ReadUnaligned<int>(ref Unsafe.As<char, byte>(ref Unsafe.Add<char>(ref first, elementOffset))) == Unsafe.ReadUnaligned<int>(ref Unsafe.As<char, byte>(ref Unsafe.Add<char>(ref second, elementOffset))))
        //            elementOffset += 2;
        //        while ((void*)elementOffset < (void*)num2)
        //        {
        //            int num4 = Unsafe.Add<char>(ref first, elementOffset).CompareTo(Unsafe.Add<char>(ref second, elementOffset));
        //            if (num4 != 0)
        //                return num4;
        //            elementOffset += 1;
        //        }
        //    }
        //    return num1;
        //}

        //public static unsafe int IndexOf(ref char searchSpace, char value, int length)
        //{
        //    fixed (char* chPtr1 = &searchSpace)
        //    {
        //        char* source = chPtr1;
        //        char* chPtr2 = source + length;
        //        if (Vector.IsHardwareAccelerated && length >= Vector<ushort>.Count * 2)
        //            length = Vector<ushort>.Count - ((int)source & Unsafe.SizeOf<Vector<ushort>>() - 1) / 2 & Vector<ushort>.Count - 1;
        //        while (true)
        //        {
        //            while (length >= 4)
        //            {
        //                length -= 4;
        //                if ((int)*source != (int)value)
        //                {
        //                    if ((int)source[1] != (int)value)
        //                    {
        //                        if ((int)source[2] != (int)value)
        //                        {
        //                            if ((int)source[3] != (int)value)
        //                            {
        //                                source += 4;
        //                                continue;
        //                            }
        //                            ++source;
        //                        }
        //                        ++source;
        //                    }
        //                    ++source;
        //                    goto label_23;
        //                }
        //                else
        //                    goto label_23;
        //            }
        //            while (length > 0)
        //            {
        //                --length;
        //                if ((int)*source != (int)value)
        //                    ++source;
        //                else
        //                    goto label_23;
        //            }
        //            if (Vector.IsHardwareAccelerated && source < chPtr2)
        //            {
        //                length = (int)(chPtr2 - source & (long)~(Vector<ushort>.Count - 1));
        //                Vector<ushort> vector = new Vector<ushort>((ushort)value);
        //                for (; length > 0; length -= Vector<ushort>.Count)
        //                {
        //                    Vector<ushort> match = Vector.Equals<ushort>(vector, Unsafe.Read<Vector<ushort>>((void*)source));
        //                    if (!Vector<ushort>.Zero.Equals(match))
        //                        return (int)(source - chPtr1) + SpanHelpers.LocateFirstFoundChar(match);
        //                    source += Vector<ushort>.Count;
        //                }
        //                if (source < chPtr2)
        //                    length = (int)(chPtr2 - source);
        //                else
        //                    break;
        //            }
        //            else
        //                break;
        //        }
        //        return -1;
        //    label_23:
        //        return (int)(source - chPtr1);
        //    }
        //}

        //public static unsafe int LastIndexOf(ref char searchSpace, char value, int length)
        //{
        //    fixed (char* chPtr1 = &searchSpace)
        //    {
        //        char* chPtr2 = chPtr1 + length;
        //        char* chPtr3 = chPtr1;
        //        if (Vector.IsHardwareAccelerated && length >= Vector<ushort>.Count * 2)
        //            length = ((int)chPtr2 & Unsafe.SizeOf<Vector<ushort>>() - 1) / 2;
        //        while (true)
        //        {
        //            while (length >= 4)
        //            {
        //                length -= 4;
        //                chPtr2 -= 4;
        //                if ((int)chPtr2[3] == (int)value)
        //                    return (int)(chPtr2 - chPtr3) + 3;
        //                if ((int)chPtr2[2] == (int)value)
        //                    return (int)(chPtr2 - chPtr3) + 2;
        //                if ((int)chPtr2[1] == (int)value)
        //                    return (int)(chPtr2 - chPtr3) + 1;
        //                if ((int)*chPtr2 == (int)value)
        //                    goto label_18;
        //            }
        //            while (length > 0)
        //            {
        //                --length;
        //                --chPtr2;
        //                if ((int)*chPtr2 == (int)value)
        //                    goto label_18;
        //            }
        //            if (Vector.IsHardwareAccelerated && chPtr2 > chPtr3)
        //            {
        //                length = (int)(chPtr2 - chPtr3 & (long)~(Vector<ushort>.Count - 1));
        //                Vector<ushort> vector = new Vector<ushort>((ushort)value);
        //                for (; length > 0; length -= Vector<ushort>.Count)
        //                {
        //                    char* source = chPtr2 - Vector<ushort>.Count;
        //                    Vector<ushort> match = Vector.Equals<ushort>(vector, Unsafe.Read<Vector<ushort>>((void*)source));
        //                    if (!Vector<ushort>.Zero.Equals(match))
        //                        return (int)(source - chPtr3) + SpanHelpers.LocateLastFoundChar(match);
        //                    chPtr2 -= Vector<ushort>.Count;
        //                }
        //                if (chPtr2 > chPtr3)
        //                    length = (int)(chPtr2 - chPtr3);
        //                else
        //                    break;
        //            }
        //            else
        //                break;
        //        }
        //        return -1;
        //    label_18:
        //        return (int)(chPtr2 - chPtr3);
        //    }
        //}

        //[MethodImpl((MethodImplOptions)256)]
        //private static int LocateFirstFoundChar(Vector<ushort> match)
        //{
        //    Vector<ulong> vector = Vector.AsVectorUInt64<ushort>(match);
        //    ulong match1 = 0;
        //    int num;
        //    for (num = 0; num < Vector<ulong>.Count; ++num)
        //    {
        //        match1 = vector[num];
        //        if (match1 != 0UL)
        //            break;
        //    }
        //    return num * 4 + SpanHelpers.LocateFirstFoundChar(match1);
        //}

        //[MethodImpl((MethodImplOptions)256)]
        //private static int LocateFirstFoundChar(ulong match) => (int)((match ^ match - 1UL) * 4295098372UL >> 49);

        //[MethodImpl((MethodImplOptions)256)]
        //private static int LocateLastFoundChar(Vector<ushort> match)
        //{
        //    Vector<ulong> vector = Vector.AsVectorUInt64<ushort>(match);
        //    ulong match1 = 0;
        //    int num;
        //    for (num = Vector<ulong>.Count - 1; num >= 0; --num)
        //    {
        //        match1 = vector[num];
        //        if (match1 != 0UL)
        //            break;
        //    }
        //    return num * 4 + SpanHelpers.LocateLastFoundChar(match1);
        //}

        [MethodImpl((MethodImplOptions)256)]
        private static int LocateLastFoundChar(ulong match)
        {
            int num = 3;
            while ((long)match > 0L)
            {
                match <<= 16;
                --num;
            }
            return num;
        }

        public static int IndexOf<T>(
          ref T searchSpace,
          int searchSpaceLength,
          ref T value,
          int valueLength)
          where T : IEquatable<T>
        {
            if (valueLength == 0)
                return 0;
            T obj = value;
            ref T local = ref Unsafe.Add<T>(ref value, 1);
            int length1 = valueLength - 1;
            int elementOffset = 0;
            int num1;
            while (true)
            {
                int length2 = searchSpaceLength - elementOffset - length1;
                if (length2 > 0)
                {
                    int num2 = SpanHelpers.IndexOf<T>(ref Unsafe.Add<T>(ref searchSpace, elementOffset), obj, length2);
                    if (num2 != -1)
                    {
                        num1 = elementOffset + num2;
                        if (!SpanHelpers.SequenceEqual<T>(ref Unsafe.Add<T>(ref searchSpace, num1 + 1), ref local, length1))
                            elementOffset = num1 + 1;
                        else
                            break;
                    }
                    else
                        goto label_8;
                }
                else
                    goto label_8;
            }
            return num1;
        label_8:
            return -1;
        }

        public static unsafe int IndexOf<T>(ref T searchSpace, T value, int length) where T : IEquatable<T>
        {
            IntPtr elementOffset = (IntPtr)0;
            while (length >= 8)
            {
                length -= 8;
                if (!value.Equals(Unsafe.Add<T>(ref searchSpace, elementOffset)))
                {
                    if (!value.Equals(Unsafe.Add<T>(ref searchSpace, elementOffset + 1)))
                    {
                        if (!value.Equals(Unsafe.Add<T>(ref searchSpace, elementOffset + 2)))
                        {
                            if (!value.Equals(Unsafe.Add<T>(ref searchSpace, elementOffset + 3)))
                            {
                                if (value.Equals(Unsafe.Add<T>(ref searchSpace, elementOffset + 4)))
                                    return (int)(void*)(elementOffset + 4);
                                if (value.Equals(Unsafe.Add<T>(ref searchSpace, elementOffset + 5)))
                                    return (int)(void*)(elementOffset + 5);
                                if (value.Equals(Unsafe.Add<T>(ref searchSpace, elementOffset + 6)))
                                    return (int)(void*)(elementOffset + 6);
                                if (value.Equals(Unsafe.Add<T>(ref searchSpace, elementOffset + 7)))
                                    return (int)(void*)(elementOffset + 7);
                                elementOffset += 8;
                            }
                            else
                                goto label_24;
                        }
                        else
                            goto label_23;
                    }
                    else
                        goto label_22;
                }
                else
                    goto label_21;
            }
            if (length >= 4)
            {
                length -= 4;
                if (!value.Equals(Unsafe.Add<T>(ref searchSpace, elementOffset)))
                {
                    if (!value.Equals(Unsafe.Add<T>(ref searchSpace, elementOffset + 1)))
                    {
                        if (!value.Equals(Unsafe.Add<T>(ref searchSpace, elementOffset + 2)))
                        {
                            if (!value.Equals(Unsafe.Add<T>(ref searchSpace, elementOffset + 3)))
                                elementOffset += 4;
                            else
                                goto label_24;
                        }
                        else
                            goto label_23;
                    }
                    else
                        goto label_22;
                }
                else
                    goto label_21;
            }
            for (; length > 0; --length)
            {
                if (!value.Equals(Unsafe.Add<T>(ref searchSpace, elementOffset)))
                    elementOffset += 1;
                else
                    goto label_21;
            }
            return -1;
        label_21:
            return (int)(void*)elementOffset;
        label_22:
            return (int)(void*)(elementOffset + 1);
        label_23:
            return (int)(void*)(elementOffset + 2);
        label_24:
            return (int)(void*)(elementOffset + 3);
        }

        public static int IndexOfAny<T>(ref T searchSpace, T value0, T value1, int length) where T : IEquatable<T>
        {
            int elementOffset;
            for (elementOffset = 0; length - elementOffset >= 8; elementOffset += 8)
            {
                T other1 = Unsafe.Add<T>(ref searchSpace, elementOffset);
                if (!value0.Equals(other1) && !value1.Equals(other1))
                {
                    T other2 = Unsafe.Add<T>(ref searchSpace, elementOffset + 1);
                    if (!value0.Equals(other2) && !value1.Equals(other2))
                    {
                        T other3 = Unsafe.Add<T>(ref searchSpace, elementOffset + 2);
                        if (!value0.Equals(other3) && !value1.Equals(other3))
                        {
                            T other4 = Unsafe.Add<T>(ref searchSpace, elementOffset + 3);
                            if (!value0.Equals(other4) && !value1.Equals(other4))
                            {
                                T other5 = Unsafe.Add<T>(ref searchSpace, elementOffset + 4);
                                if (value0.Equals(other5) || value1.Equals(other5))
                                    return elementOffset + 4;
                                T other6 = Unsafe.Add<T>(ref searchSpace, elementOffset + 5);
                                if (value0.Equals(other6) || value1.Equals(other6))
                                    return elementOffset + 5;
                                T other7 = Unsafe.Add<T>(ref searchSpace, elementOffset + 6);
                                if (value0.Equals(other7) || value1.Equals(other7))
                                    return elementOffset + 6;
                                T other8 = Unsafe.Add<T>(ref searchSpace, elementOffset + 7);
                                if (value0.Equals(other8) || value1.Equals(other8))
                                    return elementOffset + 7;
                            }
                            else
                                goto label_24;
                        }
                        else
                            goto label_23;
                    }
                    else
                        goto label_22;
                }
                else
                    goto label_21;
            }
            if (length - elementOffset >= 4)
            {
                T other9 = Unsafe.Add<T>(ref searchSpace, elementOffset);
                if (!value0.Equals(other9) && !value1.Equals(other9))
                {
                    T other10 = Unsafe.Add<T>(ref searchSpace, elementOffset + 1);
                    if (!value0.Equals(other10) && !value1.Equals(other10))
                    {
                        T other11 = Unsafe.Add<T>(ref searchSpace, elementOffset + 2);
                        if (!value0.Equals(other11) && !value1.Equals(other11))
                        {
                            T other12 = Unsafe.Add<T>(ref searchSpace, elementOffset + 3);
                            if (!value0.Equals(other12) && !value1.Equals(other12))
                                elementOffset += 4;
                            else
                                goto label_24;
                        }
                        else
                            goto label_23;
                    }
                    else
                        goto label_22;
                }
                else
                    goto label_21;
            }
            for (; elementOffset < length; ++elementOffset)
            {
                T other = Unsafe.Add<T>(ref searchSpace, elementOffset);
                if (value0.Equals(other) || value1.Equals(other))
                    goto label_21;
            }
            return -1;
        label_21:
            return elementOffset;
        label_22:
            return elementOffset + 1;
        label_23:
            return elementOffset + 2;
        label_24:
            return elementOffset + 3;
        }

        public static int IndexOfAny<T>(ref T searchSpace, T value0, T value1, T value2, int length) where T : IEquatable<T>
        {
            int elementOffset;
            for (elementOffset = 0; length - elementOffset >= 8; elementOffset += 8)
            {
                T other1 = Unsafe.Add<T>(ref searchSpace, elementOffset);
                if (!value0.Equals(other1) && !value1.Equals(other1) && !value2.Equals(other1))
                {
                    T other2 = Unsafe.Add<T>(ref searchSpace, elementOffset + 1);
                    if (!value0.Equals(other2) && !value1.Equals(other2) && !value2.Equals(other2))
                    {
                        T other3 = Unsafe.Add<T>(ref searchSpace, elementOffset + 2);
                        if (!value0.Equals(other3) && !value1.Equals(other3) && !value2.Equals(other3))
                        {
                            T other4 = Unsafe.Add<T>(ref searchSpace, elementOffset + 3);
                            if (!value0.Equals(other4) && !value1.Equals(other4) && !value2.Equals(other4))
                            {
                                T other5 = Unsafe.Add<T>(ref searchSpace, elementOffset + 4);
                                if (value0.Equals(other5) || value1.Equals(other5) || value2.Equals(other5))
                                    return elementOffset + 4;
                                T other6 = Unsafe.Add<T>(ref searchSpace, elementOffset + 5);
                                if (value0.Equals(other6) || value1.Equals(other6) || value2.Equals(other6))
                                    return elementOffset + 5;
                                T other7 = Unsafe.Add<T>(ref searchSpace, elementOffset + 6);
                                if (value0.Equals(other7) || value1.Equals(other7) || value2.Equals(other7))
                                    return elementOffset + 6;
                                T other8 = Unsafe.Add<T>(ref searchSpace, elementOffset + 7);
                                if (value0.Equals(other8) || value1.Equals(other8) || value2.Equals(other8))
                                    return elementOffset + 7;
                            }
                            else
                                goto label_24;
                        }
                        else
                            goto label_23;
                    }
                    else
                        goto label_22;
                }
                else
                    goto label_21;
            }
            if (length - elementOffset >= 4)
            {
                T other9 = Unsafe.Add<T>(ref searchSpace, elementOffset);
                if (!value0.Equals(other9) && !value1.Equals(other9) && !value2.Equals(other9))
                {
                    T other10 = Unsafe.Add<T>(ref searchSpace, elementOffset + 1);
                    if (!value0.Equals(other10) && !value1.Equals(other10) && !value2.Equals(other10))
                    {
                        T other11 = Unsafe.Add<T>(ref searchSpace, elementOffset + 2);
                        if (!value0.Equals(other11) && !value1.Equals(other11) && !value2.Equals(other11))
                        {
                            T other12 = Unsafe.Add<T>(ref searchSpace, elementOffset + 3);
                            if (!value0.Equals(other12) && !value1.Equals(other12) && !value2.Equals(other12))
                                elementOffset += 4;
                            else
                                goto label_24;
                        }
                        else
                            goto label_23;
                    }
                    else
                        goto label_22;
                }
                else
                    goto label_21;
            }
            for (; elementOffset < length; ++elementOffset)
            {
                T other = Unsafe.Add<T>(ref searchSpace, elementOffset);
                if (value0.Equals(other) || value1.Equals(other) || value2.Equals(other))
                    goto label_21;
            }
            return -1;
        label_21:
            return elementOffset;
        label_22:
            return elementOffset + 1;
        label_23:
            return elementOffset + 2;
        label_24:
            return elementOffset + 3;
        }

        public static int IndexOfAny<T>(
          ref T searchSpace,
          int searchSpaceLength,
          ref T value,
          int valueLength)
          where T : IEquatable<T>
        {
            if (valueLength == 0)
                return 0;
            int num1 = -1;
            for (int elementOffset = 0; elementOffset < valueLength; ++elementOffset)
            {
                int num2 = SpanHelpers.IndexOf<T>(ref searchSpace, Unsafe.Add<T>(ref value, elementOffset), searchSpaceLength);
                if ((uint)num2 < (uint)num1)
                {
                    num1 = num2;
                    searchSpaceLength = num2;
                    if (num1 == 0)
                        break;
                }
            }
            return num1;
        }

        public static int LastIndexOf<T>(
          ref T searchSpace,
          int searchSpaceLength,
          ref T value,
          int valueLength)
          where T : IEquatable<T>
        {
            if (valueLength == 0)
                return 0;
            T obj = value;
            ref T local = ref Unsafe.Add<T>(ref value, 1);
            int length1 = valueLength - 1;
            int num1 = 0;
            int num2;
            while (true)
            {
                int length2 = searchSpaceLength - num1 - length1;
                if (length2 > 0)
                {
                    num2 = SpanHelpers.LastIndexOf<T>(ref searchSpace, obj, length2);
                    if (num2 != -1)
                    {
                        if (!SpanHelpers.SequenceEqual<T>(ref Unsafe.Add<T>(ref searchSpace, num2 + 1), ref local, length1))
                            num1 += length2 - num2;
                        else
                            break;
                    }
                    else
                        goto label_8;
                }
                else
                    goto label_8;
            }
            return num2;
        label_8:
            return -1;
        }

        public static int LastIndexOf<T>(ref T searchSpace, T value, int length) where T : IEquatable<T>
        {
            while (length >= 8)
            {
                length -= 8;
                if (value.Equals(Unsafe.Add<T>(ref searchSpace, length + 7)))
                    return length + 7;
                if (value.Equals(Unsafe.Add<T>(ref searchSpace, length + 6)))
                    return length + 6;
                if (value.Equals(Unsafe.Add<T>(ref searchSpace, length + 5)))
                    return length + 5;
                if (value.Equals(Unsafe.Add<T>(ref searchSpace, length + 4)))
                    return length + 4;
                if (!value.Equals(Unsafe.Add<T>(ref searchSpace, length + 3)))
                {
                    if (!value.Equals(Unsafe.Add<T>(ref searchSpace, length + 2)))
                    {
                        if (!value.Equals(Unsafe.Add<T>(ref searchSpace, length + 1)))
                        {
                            if (value.Equals(Unsafe.Add<T>(ref searchSpace, length)))
                                goto label_18;
                        }
                        else
                            goto label_19;
                    }
                    else
                        goto label_20;
                }
                else
                    goto label_21;
            }
            if (length >= 4)
            {
                length -= 4;
                if (!value.Equals(Unsafe.Add<T>(ref searchSpace, length + 3)))
                {
                    if (!value.Equals(Unsafe.Add<T>(ref searchSpace, length + 2)))
                    {
                        if (!value.Equals(Unsafe.Add<T>(ref searchSpace, length + 1)))
                        {
                            if (value.Equals(Unsafe.Add<T>(ref searchSpace, length)))
                                goto label_18;
                        }
                        else
                            goto label_19;
                    }
                    else
                        goto label_20;
                }
                else
                    goto label_21;
            }
            while (length > 0)
            {
                --length;
                if (value.Equals(Unsafe.Add<T>(ref searchSpace, length)))
                    goto label_18;
            }
            return -1;
        label_18:
            return length;
        label_19:
            return length + 1;
        label_20:
            return length + 2;
        label_21:
            return length + 3;
        }

        public static int LastIndexOfAny<T>(ref T searchSpace, T value0, T value1, int length) where T : IEquatable<T>
        {
            while (length >= 8)
            {
                length -= 8;
                T other1 = Unsafe.Add<T>(ref searchSpace, length + 7);
                if (value0.Equals(other1) || value1.Equals(other1))
                    return length + 7;
                T other2 = Unsafe.Add<T>(ref searchSpace, length + 6);
                if (value0.Equals(other2) || value1.Equals(other2))
                    return length + 6;
                T other3 = Unsafe.Add<T>(ref searchSpace, length + 5);
                if (value0.Equals(other3) || value1.Equals(other3))
                    return length + 5;
                T other4 = Unsafe.Add<T>(ref searchSpace, length + 4);
                if (value0.Equals(other4) || value1.Equals(other4))
                    return length + 4;
                T other5 = Unsafe.Add<T>(ref searchSpace, length + 3);
                if (!value0.Equals(other5) && !value1.Equals(other5))
                {
                    T other6 = Unsafe.Add<T>(ref searchSpace, length + 2);
                    if (!value0.Equals(other6) && !value1.Equals(other6))
                    {
                        T other7 = Unsafe.Add<T>(ref searchSpace, length + 1);
                        if (!value0.Equals(other7) && !value1.Equals(other7))
                        {
                            T other8 = Unsafe.Add<T>(ref searchSpace, length);
                            if (value0.Equals(other8) || value1.Equals(other8))
                                goto label_18;
                        }
                        else
                            goto label_19;
                    }
                    else
                        goto label_20;
                }
                else
                    goto label_21;
            }
            if (length >= 4)
            {
                length -= 4;
                T other9 = Unsafe.Add<T>(ref searchSpace, length + 3);
                if (!value0.Equals(other9) && !value1.Equals(other9))
                {
                    T other10 = Unsafe.Add<T>(ref searchSpace, length + 2);
                    if (!value0.Equals(other10) && !value1.Equals(other10))
                    {
                        T other11 = Unsafe.Add<T>(ref searchSpace, length + 1);
                        if (!value0.Equals(other11) && !value1.Equals(other11))
                        {
                            T other12 = Unsafe.Add<T>(ref searchSpace, length);
                            if (value0.Equals(other12) || value1.Equals(other12))
                                goto label_18;
                        }
                        else
                            goto label_19;
                    }
                    else
                        goto label_20;
                }
                else
                    goto label_21;
            }
            while (length > 0)
            {
                --length;
                T other = Unsafe.Add<T>(ref searchSpace, length);
                if (value0.Equals(other) || value1.Equals(other))
                    goto label_18;
            }
            return -1;
        label_18:
            return length;
        label_19:
            return length + 1;
        label_20:
            return length + 2;
        label_21:
            return length + 3;
        }

        public static int LastIndexOfAny<T>(
          ref T searchSpace,
          T value0,
          T value1,
          T value2,
          int length)
          where T : IEquatable<T>
        {
            while (length >= 8)
            {
                length -= 8;
                T other1 = Unsafe.Add<T>(ref searchSpace, length + 7);
                if (value0.Equals(other1) || value1.Equals(other1) || value2.Equals(other1))
                    return length + 7;
                T other2 = Unsafe.Add<T>(ref searchSpace, length + 6);
                if (value0.Equals(other2) || value1.Equals(other2) || value2.Equals(other2))
                    return length + 6;
                T other3 = Unsafe.Add<T>(ref searchSpace, length + 5);
                if (value0.Equals(other3) || value1.Equals(other3) || value2.Equals(other3))
                    return length + 5;
                T other4 = Unsafe.Add<T>(ref searchSpace, length + 4);
                if (value0.Equals(other4) || value1.Equals(other4) || value2.Equals(other4))
                    return length + 4;
                T other5 = Unsafe.Add<T>(ref searchSpace, length + 3);
                if (!value0.Equals(other5) && !value1.Equals(other5) && !value2.Equals(other5))
                {
                    T other6 = Unsafe.Add<T>(ref searchSpace, length + 2);
                    if (!value0.Equals(other6) && !value1.Equals(other6) && !value2.Equals(other6))
                    {
                        T other7 = Unsafe.Add<T>(ref searchSpace, length + 1);
                        if (!value0.Equals(other7) && !value1.Equals(other7) && !value2.Equals(other7))
                        {
                            T other8 = Unsafe.Add<T>(ref searchSpace, length);
                            if (value0.Equals(other8) || value1.Equals(other8) || value2.Equals(other8))
                                goto label_18;
                        }
                        else
                            goto label_19;
                    }
                    else
                        goto label_20;
                }
                else
                    goto label_21;
            }
            if (length >= 4)
            {
                length -= 4;
                T other9 = Unsafe.Add<T>(ref searchSpace, length + 3);
                if (!value0.Equals(other9) && !value1.Equals(other9) && !value2.Equals(other9))
                {
                    T other10 = Unsafe.Add<T>(ref searchSpace, length + 2);
                    if (!value0.Equals(other10) && !value1.Equals(other10) && !value2.Equals(other10))
                    {
                        T other11 = Unsafe.Add<T>(ref searchSpace, length + 1);
                        if (!value0.Equals(other11) && !value1.Equals(other11) && !value2.Equals(other11))
                        {
                            T other12 = Unsafe.Add<T>(ref searchSpace, length);
                            if (value0.Equals(other12) || value1.Equals(other12) || value2.Equals(other12))
                                goto label_18;
                        }
                        else
                            goto label_19;
                    }
                    else
                        goto label_20;
                }
                else
                    goto label_21;
            }
            while (length > 0)
            {
                --length;
                T other = Unsafe.Add<T>(ref searchSpace, length);
                if (value0.Equals(other) || value1.Equals(other) || value2.Equals(other))
                    goto label_18;
            }
            return -1;
        label_18:
            return length;
        label_19:
            return length + 1;
        label_20:
            return length + 2;
        label_21:
            return length + 3;
        }

        public static int LastIndexOfAny<T>(
          ref T searchSpace,
          int searchSpaceLength,
          ref T value,
          int valueLength)
          where T : IEquatable<T>
        {
            if (valueLength == 0)
                return 0;
            int num1 = -1;
            for (int elementOffset = 0; elementOffset < valueLength; ++elementOffset)
            {
                int num2 = SpanHelpers.LastIndexOf<T>(ref searchSpace, Unsafe.Add<T>(ref value, elementOffset), searchSpaceLength);
                if (num2 > num1)
                    num1 = num2;
            }
            return num1;
        }

        public static bool SequenceEqual<T>(ref T first, ref T second, int length) where T : IEquatable<T>
        {
            if (!Unsafe.AreSame<T>(ref first, ref second))
            {
                IntPtr elementOffset = (IntPtr)0;
                while (length >= 8)
                {
                    length -= 8;
                    if (Unsafe.Add<T>(ref first, elementOffset).Equals(Unsafe.Add<T>(ref second, elementOffset)) && Unsafe.Add<T>(ref first, elementOffset + 1).Equals(Unsafe.Add<T>(ref second, elementOffset + 1)) && Unsafe.Add<T>(ref first, elementOffset + 2).Equals(Unsafe.Add<T>(ref second, elementOffset + 2)) && Unsafe.Add<T>(ref first, elementOffset + 3).Equals(Unsafe.Add<T>(ref second, elementOffset + 3)) && Unsafe.Add<T>(ref first, elementOffset + 4).Equals(Unsafe.Add<T>(ref second, elementOffset + 4)) && Unsafe.Add<T>(ref first, elementOffset + 5).Equals(Unsafe.Add<T>(ref second, elementOffset + 5)) && Unsafe.Add<T>(ref first, elementOffset + 6).Equals(Unsafe.Add<T>(ref second, elementOffset + 6)) && Unsafe.Add<T>(ref first, elementOffset + 7).Equals(Unsafe.Add<T>(ref second, elementOffset + 7)))
                        elementOffset += 8;
                    else
                        goto label_12;
                }
                if (length >= 4)
                {
                    length -= 4;
                    if (Unsafe.Add<T>(ref first, elementOffset).Equals(Unsafe.Add<T>(ref second, elementOffset)) && Unsafe.Add<T>(ref first, elementOffset + 1).Equals(Unsafe.Add<T>(ref second, elementOffset + 1)) && Unsafe.Add<T>(ref first, elementOffset + 2).Equals(Unsafe.Add<T>(ref second, elementOffset + 2)) && Unsafe.Add<T>(ref first, elementOffset + 3).Equals(Unsafe.Add<T>(ref second, elementOffset + 3)))
                        elementOffset += 4;
                    else
                        goto label_12;
                }
                for (; length > 0; --length)
                {
                    if (Unsafe.Add<T>(ref first, elementOffset).Equals(Unsafe.Add<T>(ref second, elementOffset)))
                        elementOffset += 1;
                    else
                        goto label_12;
                }
                goto label_11;
            label_12:
                return false;
            }
        label_11:
            return true;
        }

        public static int SequenceCompareTo<T>(
          ref T first,
          int firstLength,
          ref T second,
          int secondLength)
          where T : IComparable<T>
        {
            int num1 = firstLength;
            if (num1 > secondLength)
                num1 = secondLength;
            for (int elementOffset = 0; elementOffset < num1; ++elementOffset)
            {
                int num2 = Unsafe.Add<T>(ref first, elementOffset).CompareTo(Unsafe.Add<T>(ref second, elementOffset));
                if (num2 != 0)
                    return num2;
            }
            return firstLength.CompareTo(secondLength);
        }

        public static unsafe void CopyTo<T>(ref T dst, int dstLength, ref T src, int srcLength)
        {
            IntPtr num1 = Unsafe.ByteOffset<T>(ref src, ref Unsafe.Add<T>(ref src, srcLength));
            IntPtr num2 = Unsafe.ByteOffset<T>(ref dst, ref Unsafe.Add<T>(ref dst, dstLength));
            IntPtr num3 = Unsafe.ByteOffset<T>(ref src, ref dst);
            if (!(sizeof(IntPtr) == 4 ? (uint)(int)num3 < (uint)(int)num1 || (uint)(int)num3 > (uint)-(int)num2 : (ulong)(long)num3 < (ulong)(long)num1 || (ulong)(long)num3 > (ulong)-(long)num2) && !SpanHelpers.IsReferenceOrContainsReferences<T>())
            {
                ref byte local1 = ref Unsafe.As<T, byte>(ref dst);
                ref byte local2 = ref Unsafe.As<T, byte>(ref src);
                ulong num4 = (ulong)(long)num1;
                uint byteCount;
                for (ulong elementOffset = 0; elementOffset < num4; elementOffset += (ulong)byteCount)
                {
                    byteCount = num4 - elementOffset > (ulong)uint.MaxValue ? uint.MaxValue : (uint)(num4 - elementOffset);
                    Unsafe.CopyBlock(ref Unsafe.Add<byte>(ref local1, (IntPtr)(long)elementOffset), ref Unsafe.Add<byte>(ref local2, (IntPtr)(long)elementOffset), byteCount);
                }
            }
            else
            {
                bool flag = sizeof(IntPtr) == 4 ? (uint)(int)num3 > (uint)-(int)num2 : (ulong)(long)num3 > (ulong)-(long)num2;
                int num5 = flag ? 1 : -1;
                int elementOffset = flag ? 0 : srcLength - 1;
                int num6;
                for (num6 = 0; num6 < (srcLength & -8); num6 += 8)
                {
                    Unsafe.Add<T>(ref dst, elementOffset) = Unsafe.Add<T>(ref src, elementOffset);
                    Unsafe.Add<T>(ref dst, elementOffset + num5) = Unsafe.Add<T>(ref src, elementOffset + num5);
                    Unsafe.Add<T>(ref dst, elementOffset + num5 * 2) = Unsafe.Add<T>(ref src, elementOffset + num5 * 2);
                    Unsafe.Add<T>(ref dst, elementOffset + num5 * 3) = Unsafe.Add<T>(ref src, elementOffset + num5 * 3);
                    Unsafe.Add<T>(ref dst, elementOffset + num5 * 4) = Unsafe.Add<T>(ref src, elementOffset + num5 * 4);
                    Unsafe.Add<T>(ref dst, elementOffset + num5 * 5) = Unsafe.Add<T>(ref src, elementOffset + num5 * 5);
                    Unsafe.Add<T>(ref dst, elementOffset + num5 * 6) = Unsafe.Add<T>(ref src, elementOffset + num5 * 6);
                    Unsafe.Add<T>(ref dst, elementOffset + num5 * 7) = Unsafe.Add<T>(ref src, elementOffset + num5 * 7);
                    elementOffset += num5 * 8;
                }
                if (num6 < (srcLength & -4))
                {
                    Unsafe.Add<T>(ref dst, elementOffset) = Unsafe.Add<T>(ref src, elementOffset);
                    Unsafe.Add<T>(ref dst, elementOffset + num5) = Unsafe.Add<T>(ref src, elementOffset + num5);
                    Unsafe.Add<T>(ref dst, elementOffset + num5 * 2) = Unsafe.Add<T>(ref src, elementOffset + num5 * 2);
                    Unsafe.Add<T>(ref dst, elementOffset + num5 * 3) = Unsafe.Add<T>(ref src, elementOffset + num5 * 3);
                    elementOffset += num5 * 4;
                    num6 += 4;
                }
                for (; num6 < srcLength; ++num6)
                {
                    Unsafe.Add<T>(ref dst, elementOffset) = Unsafe.Add<T>(ref src, elementOffset);
                    elementOffset += num5;
                }
            }
        }

        [MethodImpl((MethodImplOptions)256)]
        public static unsafe IntPtr Add<T>(this IntPtr start, int index)
        {
            if (sizeof(IntPtr) == 4)
            {
                uint num = (uint)(index * Unsafe.SizeOf<T>());
                return (IntPtr)(void*)((IntPtr)(void*)start + (nint)num);
            }
            ulong num1 = (ulong)index * (ulong)Unsafe.SizeOf<T>();
            return (IntPtr)(void*)((IntPtr)(void*)start + (nint)num1);
        }

        public static bool IsReferenceOrContainsReferences<T>() => SpanHelpers.PerTypeValues<T>.IsReferenceOrContainsReferences;

        private static bool IsReferenceOrContainsReferencesCore(Type type)
        {
            if (((Type)IntrospectionExtensions.GetTypeInfo(type)).IsPrimitive)
                return false;
            if (!((Type)IntrospectionExtensions.GetTypeInfo(type)).IsValueType)
                return true;
            Type underlyingType = Nullable.GetUnderlyingType(type);
            if (underlyingType != (Type)null)
                type = underlyingType;
            if (((Type)IntrospectionExtensions.GetTypeInfo(type)).IsEnum)
                return false;
            foreach (FieldInfo declaredField in IntrospectionExtensions.GetTypeInfo(type).DeclaredFields)
            {
                if (!declaredField.IsStatic && SpanHelpers.IsReferenceOrContainsReferencesCore(declaredField.FieldType))
                    return true;
            }
            return false;
        }

        public static unsafe void ClearLessThanPointerSized(byte* ptr, UIntPtr byteLength)
        {
            if (sizeof(UIntPtr) == 4)
            {
                Unsafe.InitBlockUnaligned((void*)ptr, (byte)0, (uint)byteLength);
            }
            else
            {
                ulong num1 = (ulong)byteLength;
                uint byteCount1 = (uint)(num1 & (ulong)uint.MaxValue);
                Unsafe.InitBlockUnaligned((void*)ptr, (byte)0, byteCount1);
                ulong num2 = num1 - (ulong)byteCount1;
                ptr += byteCount1;
                uint byteCount2;
                for (; num2 > 0UL; num2 -= (ulong)byteCount2)
                {
                    byteCount2 = num2 >= (ulong)uint.MaxValue ? uint.MaxValue : (uint)num2;
                    Unsafe.InitBlockUnaligned((void*)ptr, (byte)0, byteCount2);
                    ptr += byteCount2;
                }
            }
        }

        public static unsafe void ClearLessThanPointerSized(ref byte b, UIntPtr byteLength)
        {
            if (sizeof(UIntPtr) == 4)
            {
                Unsafe.InitBlockUnaligned(ref b, (byte)0, (uint)byteLength);
            }
            else
            {
                ulong num1 = (ulong)byteLength;
                uint byteCount1 = (uint)(num1 & (ulong)uint.MaxValue);
                Unsafe.InitBlockUnaligned(ref b, (byte)0, byteCount1);
                ulong num2 = num1 - (ulong)byteCount1;
                long elementOffset = (long)byteCount1;
                uint byteCount2;
                for (; num2 > 0UL; num2 -= (ulong)byteCount2)
                {
                    byteCount2 = num2 >= (ulong)uint.MaxValue ? uint.MaxValue : (uint)num2;
                    Unsafe.InitBlockUnaligned(ref Unsafe.Add<byte>(ref b, (IntPtr)elementOffset), (byte)0, byteCount2);
                    elementOffset += (long)byteCount2;
                }
            }
        }

        public static unsafe void ClearPointerSizedWithoutReferences(ref byte b, UIntPtr byteLength)
        {
            IntPtr zero = IntPtr.Zero;
            while (zero.LessThanEqual(byteLength - sizeof(SpanHelpers.Reg64)))
            {
                Unsafe.As<byte, SpanHelpers.Reg64>(ref Unsafe.Add<byte>(ref b, zero)) = new SpanHelpers.Reg64();
                //todo: fix
                // *(SpanHelpers.Reg64*)Unsafe.As<byte, SpanHelpers.Reg64>(ref Unsafe.Add<byte>(ref b, zero)) = new SpanHelpers.Reg64();
                zero += sizeof(SpanHelpers.Reg64);
            }
            if (zero.LessThanEqual(byteLength - sizeof(SpanHelpers.Reg32)))
            {
                Unsafe.As<byte, SpanHelpers.Reg32>(ref Unsafe.Add<byte>(ref b, zero)) = new SpanHelpers.Reg32();
                //*(SpanHelpers.Reg32*)ref Unsafe.As<byte, SpanHelpers.Reg32>(ref Unsafe.Add<byte>(ref b, zero)) = new SpanHelpers.Reg32();
                zero += sizeof(SpanHelpers.Reg32);
            }
            if (zero.LessThanEqual(byteLength - sizeof(SpanHelpers.Reg16)))
            {
                Unsafe.As<byte, SpanHelpers.Reg16>(ref Unsafe.Add<byte>(ref b, zero)) = new SpanHelpers.Reg16();
                //*(SpanHelpers.Reg16*)ref Unsafe.As<byte, SpanHelpers.Reg16>(ref Unsafe.Add<byte>(ref b, zero)) = new SpanHelpers.Reg16();
                zero += sizeof(SpanHelpers.Reg16);
            }
            if (zero.LessThanEqual(byteLength - 8))
            {
                Unsafe.As<byte, long>(ref Unsafe.Add<byte>(ref b, zero)) = 0L;
                zero += 8;
            }
            if (sizeof(IntPtr) != 4 || !zero.LessThanEqual(byteLength - 4))
                return;
            Unsafe.As<byte, int>(ref Unsafe.Add<byte>(ref b, zero)) = 0;
            IntPtr num = zero + 4;
        }

        public static unsafe void ClearPointerSizedWithReferences(
          ref IntPtr ip,
          UIntPtr pointerSizeLength)
        {
            IntPtr elementOffset = IntPtr.Zero;
            IntPtr zero = IntPtr.Zero;
            IntPtr num1;
            for (; (num1 = elementOffset + 8).LessThanEqual(pointerSizeLength); elementOffset = num1)
            {
                *(IntPtr*)Unsafe.Add<IntPtr>(ref ip, elementOffset + 0) = new IntPtr();
                *(IntPtr*)Unsafe.Add<IntPtr>(ref ip, elementOffset + 1) = new IntPtr();
                *(IntPtr*)Unsafe.Add<IntPtr>(ref ip, elementOffset + 2) = new IntPtr();
                *(IntPtr*)Unsafe.Add<IntPtr>(ref ip, elementOffset + 3) = new IntPtr();
                *(IntPtr*)Unsafe.Add<IntPtr>(ref ip, elementOffset + 4) = new IntPtr();
                *(IntPtr*)Unsafe.Add<IntPtr>(ref ip, elementOffset + 5) = new IntPtr();
                *(IntPtr*)Unsafe.Add<IntPtr>(ref ip, elementOffset + 6) = new IntPtr();
                *(IntPtr*)Unsafe.Add<IntPtr>(ref ip, elementOffset + 7) = new IntPtr();
            }
            IntPtr num2;
            if ((num2 = elementOffset + 4).LessThanEqual(pointerSizeLength))
            {
                *(IntPtr*)Unsafe.Add<IntPtr>(ref ip, elementOffset + 0) = new IntPtr();
                *(IntPtr*)Unsafe.Add<IntPtr>(ref ip, elementOffset + 1) = new IntPtr();
                *(IntPtr*)Unsafe.Add<IntPtr>(ref ip, elementOffset + 2) = new IntPtr();
                *(IntPtr*)Unsafe.Add<IntPtr>(ref ip, elementOffset + 3) = new IntPtr();

                elementOffset = num2;
            }
            IntPtr num3;
            if ((num3 = elementOffset + 2).LessThanEqual(pointerSizeLength))
            {
                *(IntPtr*)Unsafe.Add<IntPtr>(ref ip, elementOffset + 0) = new IntPtr();
                *(IntPtr*)Unsafe.Add<IntPtr>(ref ip, elementOffset + 1) = new IntPtr();
                elementOffset = num3;
            }
            if (!(elementOffset + 1).LessThanEqual(pointerSizeLength))
                return;
            *(IntPtr*)Unsafe.Add<IntPtr>(ref ip, elementOffset) = new IntPtr();
        }

        [MethodImpl((MethodImplOptions)256)]
        private static unsafe bool LessThanEqual(this IntPtr index, UIntPtr length) => sizeof(UIntPtr) != 4 ? (long)index <= (long)(ulong)length : (int)index <= (int)(uint)length;

        internal struct ComparerComparable<T, TComparer> : IComparable<T> where TComparer : IComparer<T>
        {
            private readonly T _value;
            private readonly TComparer _comparer;

            public ComparerComparable(T value, TComparer comparer)
            {
                this._value = value;
                this._comparer = comparer;
            }

            [MethodImpl((MethodImplOptions)256)]
            public int CompareTo(T other) => this._comparer.Compare(this._value, other);
        }

        [StructLayout(LayoutKind.Sequential, Size = 64)]
        private struct Reg64
        {
        }

        [StructLayout(LayoutKind.Sequential, Size = 32)]
        private struct Reg32
        {
        }

        [StructLayout(LayoutKind.Sequential, Size = 16)]
        private struct Reg16
        {
        }

        public static class PerTypeValues<T>
        {
            public static readonly bool IsReferenceOrContainsReferences = SpanHelpers.IsReferenceOrContainsReferencesCore(typeof(T));
            public static readonly T[] EmptyArray = new T[0];
            public static readonly IntPtr ArrayAdjustment = SpanHelpers.PerTypeValues<T>.MeasureArrayAdjustment();

            private static IntPtr MeasureArrayAdjustment()
            {
                T[] o = new T[1];
                return Unsafe.ByteOffset<T>(ref Unsafe.As<Pinnable<T>>((object)o).Data, ref o[0]);
            }
        }
    }
}
