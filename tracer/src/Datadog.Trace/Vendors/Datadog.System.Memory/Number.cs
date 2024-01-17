﻿// Decompiled with JetBrains decompiler
// Type: System.Number
// Assembly: System.Memory, Version=4.0.1.2, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51
// MVID: 805945F3-27B0-47AD-B8F6-389D9D8F82C3
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Memory.4.5.5\lib\net461\System.Memory.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Memory.4.5.5\lib\net461\System.Memory.xml

using System;
using Datadog.System.Buffers.Text;
using Datadog.System.Runtime.CompilerServices.Unsafe;

namespace Datadog.System
{
    internal static class Number
  {
    internal const int DECIMAL_PRECISION = 29;
    private static readonly ulong[] s_rgval64Power10 = new ulong[30]
    {
      11529215046068469760UL,
      14411518807585587200UL,
      18014398509481984000UL,
      11258999068426240000UL,
      14073748835532800000UL,
      17592186044416000000UL,
      10995116277760000000UL,
      13743895347200000000UL,
      17179869184000000000UL,
      10737418240000000000UL,
      13421772800000000000UL,
      16777216000000000000UL,
      10485760000000000000UL,
      13107200000000000000UL,
      16384000000000000000UL,
      14757395258967641293UL,
      11805916207174113035UL,
      9444732965739290428UL,
      15111572745182864686UL,
      12089258196146291749UL,
      9671406556917033399UL,
      15474250491067253438UL,
      12379400392853802751UL,
      9903520314283042201UL,
      15845632502852867522UL,
      12676506002282294018UL,
      10141204801825835215UL,
      16225927682921336344UL,
      12980742146337069075UL,
      10384593717069655260UL
    };
    private static readonly sbyte[] s_rgexp64Power10 = new sbyte[15]
    {
      (sbyte) 4,
      (sbyte) 7,
      (sbyte) 10,
      (sbyte) 14,
      (sbyte) 17,
      (sbyte) 20,
      (sbyte) 24,
      (sbyte) 27,
      (sbyte) 30,
      (sbyte) 34,
      (sbyte) 37,
      (sbyte) 40,
      (sbyte) 44,
      (sbyte) 47,
      (sbyte) 50
    };
    private static readonly ulong[] s_rgval64Power10By16 = new ulong[42]
    {
      10240000000000000000UL,
      11368683772161602974UL,
      12621774483536188886UL,
      14012984643248170708UL,
      15557538194652854266UL,
      17272337110188889248UL,
      9588073174409622172UL,
      10644899600020376798UL,
      11818212630765741798UL,
      13120851772591970216UL,
      14567071740625403792UL,
      16172698447808779622UL,
      17955302187076837696UL,
      9967194951097567532UL,
      11065809325636130658UL,
      12285516299433008778UL,
      13639663065038175358UL,
      15143067982934716296UL,
      16812182738118149112UL,
      9332636185032188787UL,
      10361307573072618722UL,
      16615349947311448416UL,
      14965776766268445891UL,
      13479973333575319909UL,
      12141680576410806707UL,
      10936253623915059637UL,
      9850501549098619819UL,
      17745086042373215136UL,
      15983352577617880260UL,
      14396524142538228461UL,
      12967236152753103031UL,
      11679847981112819795UL,
      10520271803096747049UL,
      9475818434452569218UL,
      17070116948172427008UL,
      15375394465392026135UL,
      13848924157002783096UL,
      12474001934591998882UL,
      11235582092889474480UL,
      10120112665365530972UL,
      18230774251475056952UL,
      16420821625123739930UL
    };
    private static readonly short[] s_rgexp64Power10By16 = new short[21]
    {
      (short) 54,
      (short) 107,
      (short) 160,
      (short) 213,
      (short) 266,
      (short) 319,
      (short) 373,
      (short) 426,
      (short) 479,
      (short) 532,
      (short) 585,
      (short) 638,
      (short) 691,
      (short) 745,
      (short) 798,
      (short) 851,
      (short) 904,
      (short) 957,
      (short) 1010,
      (short) 1064,
      (short) 1117
    };

    public static void RoundNumber(ref NumberBuffer number, int pos)
    {
      Span<byte> digits = number.Digits;
      int index = 0;
      while (index < pos && digits[index] != (byte) 0)
        ++index;
      if (index == pos && digits[index] >= (byte) 53)
      {
        while (index > 0 && digits[index - 1] == (byte) 57)
          --index;
        if (index > 0)
        {
          ++digits[index - 1];
        }
        else
        {
          ++number.Scale;
          digits[0] = (byte) 49;
          index = 1;
        }
      }
      else
      {
        while (index > 0 && digits[index - 1] == (byte) 48)
          --index;
      }
      if (index == 0)
      {
        number.Scale = 0;
        number.IsNegative = false;
      }
      digits[index] = (byte) 0;
    }

    internal static bool NumberBufferToDouble(ref NumberBuffer number, out double value)
    {
      double d = Number.NumberToDouble(ref number);
      uint num1 = Number.DoubleHelper.Exponent(d);
      ulong num2 = Number.DoubleHelper.Mantissa(d);
      switch (num1)
      {
        case 0:
          if (num2 == 0UL)
          {
            d = 0.0;
            break;
          }
          break;
        case 2047:
          value = 0.0;
          return false;
      }
      value = d;
      return true;
    }

    public static unsafe bool NumberBufferToDecimal(ref NumberBuffer number, ref Decimal value)
    {
      MutableDecimal source = new MutableDecimal();
      byte* unsafeDigits = number.UnsafeDigits;
      int num1 = number.Scale;
      if (*unsafeDigits == (byte) 0)
      {
        if (num1 > 0)
          num1 = 0;
      }
      else
      {
        if (num1 > 29)
          return false;
        for (; (num1 > 0 || *unsafeDigits != (byte) 0 && num1 > -28) && (source.High < 429496729U || source.High == 429496729U && (source.Mid < 2576980377U || source.Mid == 2576980377U && (source.Low < 2576980377U || source.Low == 2576980377U && *unsafeDigits <= (byte) 53))); --num1)
        {
          DecimalDecCalc.DecMul10(ref source);
          if (*unsafeDigits != (byte) 0)
            DecimalDecCalc.DecAddInt32(ref source, (uint) *unsafeDigits++ - 48U);
        }
        byte* numPtr1 = unsafeDigits;
        byte* numPtr2 = numPtr1 + 1;
        if (*numPtr1 >= (byte) 53)
        {
          bool flag = true;
          if (*(numPtr2 - 1) == (byte) 53 && (int) *(numPtr2 - 2) % 2 == 0)
          {
            int num2;
            for (num2 = 20; *numPtr2 == (byte) 48 && num2 != 0; --num2)
              ++numPtr2;
            if (*numPtr2 == (byte) 0 || num2 == 0)
              flag = false;
          }
          if (flag)
          {
            DecimalDecCalc.DecAddInt32(ref source, 1U);
            if (((int) source.High | (int) source.Mid | (int) source.Low) == 0)
            {
              source.High = 429496729U;
              source.Mid = 2576980377U;
              source.Low = 2576980378U;
              ++num1;
            }
          }
        }
      }
      if (num1 > 0)
        return false;
      if (num1 <= -29)
      {
        source.High = 0U;
        source.Low = 0U;
        source.Mid = 0U;
        source.Scale = 28;
      }
      else
        source.Scale = -num1;
      source.IsNegative = number.IsNegative;
      value = Unsafe.As<MutableDecimal, Decimal>(ref source);
      return true;
    }

    public static void DecimalToNumber(Decimal value, ref NumberBuffer number)
    {
      ref MutableDecimal local = ref Unsafe.As<Decimal, MutableDecimal>(ref value);
      Span<byte> digits1 = number.Digits;
      number.IsNegative = local.IsNegative;
      int num1 = 29;
      while (local.Mid > 0U | local.High > 0U)
      {
        uint num2 = DecimalDecCalc.DecDivMod1E9(ref local);
        for (int index = 0; index < 9; ++index)
        {
          digits1[--num1] = (byte) (num2 % 10U + 48U);
          num2 /= 10U;
        }
      }
      for (uint low = local.Low; low != 0U; low /= 10U)
        digits1[--num1] = (byte) (low % 10U + 48U);
      int num3 = 29 - num1;
      number.Scale = num3 - local.Scale;
      Span<byte> digits2 = number.Digits;
      int index1 = 0;
      while (--num3 >= 0)
        digits2[index1++] = digits1[num1++];
      digits2[index1] = (byte) 0;
    }

    private static uint DigitsToInt(ReadOnlySpan<byte> digits, int count)
    {
      uint num;
      Utf8Parser.TryParse(digits.Slice(0, count), out num, out int _, 'D');
      return num;
    }

    private static ulong Mul32x32To64(uint a, uint b) => (ulong) a * (ulong) b;

    private static ulong Mul64Lossy(ulong a, ulong b, ref int pexp)
    {
      ulong num = Number.Mul32x32To64((uint) (a >> 32), (uint) (b >> 32)) + (Number.Mul32x32To64((uint) (a >> 32), (uint) b) >> 32) + (Number.Mul32x32To64((uint) a, (uint) (b >> 32)) >> 32);
      if (((long) num & long.MinValue) == 0L)
      {
        num <<= 1;
        --pexp;
      }
      return num;
    }

    private static int abs(int value) => value < 0 ? -value : value;

    private static unsafe double NumberToDouble(ref NumberBuffer number)
    {
      ReadOnlySpan<byte> digits = (ReadOnlySpan<byte>) number.Digits;
      int index = 0;
      int numDigits = number.NumDigits;
      int val1_1 = numDigits;
      for (; digits[index] == (byte) 48; ++index)
        --val1_1;
      if (val1_1 == 0)
        return 0.0;
      int count1 = Math.Min(val1_1, 9);
      int val1_2 = val1_1 - count1;
      ulong a = (ulong) Number.DigitsToInt(digits, count1);
      if (val1_2 > 0)
      {
        int count2 = Math.Min(val1_2, 9);
        val1_2 -= count2;
        uint b = (uint) (Number.s_rgval64Power10[count2 - 1] >> 64 - (int) Number.s_rgexp64Power10[count2 - 1]);
        a = Number.Mul32x32To64((uint) a, b) + (ulong) Number.DigitsToInt(digits.Slice(9), count2);
      }
      int num1 = number.Scale - (numDigits - val1_2);
      int num2 = Number.abs(num1);
      if (num2 >= 352)
      {
        ulong num3 = num1 > 0 ? 9218868437227405312UL : 0UL;
        if (number.IsNegative)
          num3 |= 9223372036854775808UL;
        return *(double*) &num3;
      }
      int pexp = 64;
      if (((long) a & -4294967296L) == 0L)
      {
        a <<= 32;
        pexp -= 32;
      }
      if (((long) a & -281474976710656L) == 0L)
      {
        a <<= 16;
        pexp -= 16;
      }
      if (((long) a & -72057594037927936L) == 0L)
      {
        a <<= 8;
        pexp -= 8;
      }
      if (((long) a & -1152921504606846976L) == 0L)
      {
        a <<= 4;
        pexp -= 4;
      }
      if (((long) a & -4611686018427387904L) == 0L)
      {
        a <<= 2;
        pexp -= 2;
      }
      if (((long) a & long.MinValue) == 0L)
      {
        a <<= 1;
        --pexp;
      }
      int num4 = num2 & 15;
      if (num4 != 0)
      {
        int num5 = (int) Number.s_rgexp64Power10[num4 - 1];
        pexp += num1 < 0 ? -num5 + 1 : num5;
        ulong b = Number.s_rgval64Power10[num4 + (num1 < 0 ? 15 : 0) - 1];
        a = Number.Mul64Lossy(a, b, ref pexp);
      }
      int num6 = num2 >> 4;
      if (num6 != 0)
      {
        int num7 = (int) Number.s_rgexp64Power10By16[num6 - 1];
        pexp += num1 < 0 ? -num7 + 1 : num7;
        ulong b = Number.s_rgval64Power10By16[num6 + (num1 < 0 ? 21 : 0) - 1];
        a = Number.Mul64Lossy(a, b, ref pexp);
      }
      if (((int) a & 1024) != 0)
      {
        ulong num8 = a + 1023UL + (ulong) ((int) a >> 11 & 1);
        if (num8 < a)
        {
          num8 = num8 >> 1 | 9223372036854775808UL;
          ++pexp;
        }
        a = num8;
      }
      int num9 = pexp + 1022;
      ulong num10 = num9 > 0 ? (num9 < 2047 ? (ulong) (((long) num9 << 52) + ((long) (a >> 11) & 4503599627370495L)) : 9218868437227405312UL) : (num9 != -52 || a < 9223372036854775896UL ? (num9 > -52 ? a >> -num9 + 11 + 1 : 0UL) : 1UL);
      if (number.IsNegative)
        num10 |= 9223372036854775808UL;
      return *(double*) &num10;
    }

    private static class DoubleHelper
    {
      public static unsafe uint Exponent(double d) => *(uint*) ((byte*) &d + 4) >> 20 & 2047U;

      public static unsafe ulong Mantissa(double d) => (ulong) *(uint*) &d | (ulong) (*(uint*) ((byte*) &d + 4) & 1048575U) << 32;

      public static unsafe bool Sign(double d) => *(uint*) ((byte*) &d + 4) >> 31 > 0U;
    }
  }
}
