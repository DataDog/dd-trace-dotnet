﻿// Decompiled with JetBrains decompiler
// Type: System.Buffers.Text.Utf8Parser
// Assembly: System.Memory, Version=4.0.1.2, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51
// MVID: 805945F3-27B0-47AD-B8F6-389D9D8F82C3
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Memory.4.5.5\lib\net461\System.Memory.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Memory.4.5.5\lib\net461\System.Memory.xml

using System;
using Datadog.System.Runtime.CompilerServices.Unsafe;

namespace Datadog.System.Buffers.Text
{
    public static class Utf8Parser
  {
    private const uint FlipCase = 32;
    private const uint NoFlipCase = 0;
    private static readonly int[] s_daysToMonth365 = new int[13]
    {
      0,
      31,
      59,
      90,
      120,
      151,
      181,
      212,
      243,
      273,
      304,
      334,
      365
    };
    private static readonly int[] s_daysToMonth366 = new int[13]
    {
      0,
      31,
      60,
      91,
      121,
      152,
      182,
      213,
      244,
      274,
      305,
      335,
      366
    };

    public static bool TryParse(
      ReadOnlySpan<byte> source,
      out bool value,
      out int bytesConsumed,
      char standardFormat = '\0')
    {
      if (standardFormat != char.MinValue && standardFormat != 'G' && standardFormat != 'l')
        return ThrowHelper.TryParseThrowFormatException<bool>(out value, out bytesConsumed);
      if (source.Length >= 4)
      {
        if ((source[0] == (byte) 84 || source[0] == (byte) 116) && (source[1] == (byte) 82 || source[1] == (byte) 114) && (source[2] == (byte) 85 || source[2] == (byte) 117) && (source[3] == (byte) 69 || source[3] == (byte) 101))
        {
          bytesConsumed = 4;
          value = true;
          return true;
        }
        if (source.Length >= 5 && (source[0] == (byte) 70 || source[0] == (byte) 102) && (source[1] == (byte) 65 || source[1] == (byte) 97) && (source[2] == (byte) 76 || source[2] == (byte) 108) && (source[3] == (byte) 83 || source[3] == (byte) 115) && (source[4] == (byte) 69 || source[4] == (byte) 101))
        {
          bytesConsumed = 5;
          value = false;
          return true;
        }
      }
      bytesConsumed = 0;
      value = false;
      return false;
    }

    public static bool TryParse(
      ReadOnlySpan<byte> source,
      out DateTime value,
      out int bytesConsumed,
      char standardFormat = '\0')
    {
      switch (standardFormat)
      {
        case char.MinValue:
        case 'G':
          return Utf8Parser.TryParseDateTimeG(source, out value, out DateTimeOffset _, out bytesConsumed);
        case 'O':
          DateTimeOffset dateTimeOffset1;
          DateTimeKind kind;
          if (!Utf8Parser.TryParseDateTimeOffsetO(source, out dateTimeOffset1, out bytesConsumed, out kind))
          {
            value = new DateTime();
            bytesConsumed = 0;
            return false;
          }
          switch (kind)
          {
            case DateTimeKind.Utc:
              value = dateTimeOffset1.UtcDateTime;
              break;
            case DateTimeKind.Local:
              value = dateTimeOffset1.LocalDateTime;
              break;
            default:
              value = dateTimeOffset1.DateTime;
              break;
          }
          return true;
        case 'R':
          DateTimeOffset dateTimeOffset2;
          if (!Utf8Parser.TryParseDateTimeOffsetR(source, 0U, out dateTimeOffset2, out bytesConsumed))
          {
            value = new DateTime();
            return false;
          }
          value = dateTimeOffset2.DateTime;
          return true;
        case 'l':
          DateTimeOffset dateTimeOffset3;
          if (!Utf8Parser.TryParseDateTimeOffsetR(source, 32U, out dateTimeOffset3, out bytesConsumed))
          {
            value = new DateTime();
            return false;
          }
          value = dateTimeOffset3.DateTime;
          return true;
        default:
          return ThrowHelper.TryParseThrowFormatException<DateTime>(out value, out bytesConsumed);
      }
    }

    public static bool TryParse(
      ReadOnlySpan<byte> source,
      out DateTimeOffset value,
      out int bytesConsumed,
      char standardFormat = '\0')
    {
      switch (standardFormat)
      {
        case char.MinValue:
          return Utf8Parser.TryParseDateTimeOffsetDefault(source, out value, out bytesConsumed);
        case 'G':
          return Utf8Parser.TryParseDateTimeG(source, out DateTime _, out value, out bytesConsumed);
        case 'O':
          return Utf8Parser.TryParseDateTimeOffsetO(source, out value, out bytesConsumed, out DateTimeKind _);
        case 'R':
          return Utf8Parser.TryParseDateTimeOffsetR(source, 0U, out value, out bytesConsumed);
        case 'l':
          return Utf8Parser.TryParseDateTimeOffsetR(source, 32U, out value, out bytesConsumed);
        default:
          return ThrowHelper.TryParseThrowFormatException<DateTimeOffset>(out value, out bytesConsumed);
      }
    }

    private static bool TryParseDateTimeOffsetDefault(
      ReadOnlySpan<byte> source,
      out DateTimeOffset value,
      out int bytesConsumed)
    {
      if (source.Length < 26)
      {
        bytesConsumed = 0;
        value = new DateTimeOffset();
        return false;
      }
      DateTime dateTime;
      if (!Utf8Parser.TryParseDateTimeG(source, out dateTime, out DateTimeOffset _, out int _))
      {
        bytesConsumed = 0;
        value = new DateTimeOffset();
        return false;
      }
      if (source[19] != (byte) 32)
      {
        bytesConsumed = 0;
        value = new DateTimeOffset();
        return false;
      }
      byte num1 = source[20];
      switch (num1)
      {
        case 43:
        case 45:
          uint num2 = (uint) source[21] - 48U;
          uint num3 = (uint) source[22] - 48U;
          if (num2 > 9U || num3 > 9U)
          {
            bytesConsumed = 0;
            value = new DateTimeOffset();
            return false;
          }
          int num4 = (int) num2 * 10 + (int) num3;
          if (source[23] != (byte) 58)
          {
            bytesConsumed = 0;
            value = new DateTimeOffset();
            return false;
          }
          uint num5 = (uint) source[24] - 48U;
          uint num6 = (uint) source[25] - 48U;
          if (num5 > 9U || num6 > 9U)
          {
            bytesConsumed = 0;
            value = new DateTimeOffset();
            return false;
          }
          int num7 = (int) num5 * 10 + (int) num6;
          TimeSpan timeSpan = new TimeSpan(num4, num7, 0);
          if (num1 == (byte) 45)
            timeSpan = -timeSpan;
          if (!Utf8Parser.TryCreateDateTimeOffset(dateTime, num1 == (byte) 45, num4, num7, out value))
          {
            bytesConsumed = 0;
            value = new DateTimeOffset();
            return false;
          }
          bytesConsumed = 26;
          return true;
        default:
          bytesConsumed = 0;
          value = new DateTimeOffset();
          return false;
      }
    }

    private static bool TryParseDateTimeG(
      ReadOnlySpan<byte> source,
      out DateTime value,
      out DateTimeOffset valueAsOffset,
      out int bytesConsumed)
    {
      if (source.Length < 19)
      {
        bytesConsumed = 0;
        value = new DateTime();
        valueAsOffset = new DateTimeOffset();
        return false;
      }
      uint num1 = (uint) source[0] - 48U;
      uint num2 = (uint) source[1] - 48U;
      if (num1 > 9U || num2 > 9U)
      {
        bytesConsumed = 0;
        value = new DateTime();
        valueAsOffset = new DateTimeOffset();
        return false;
      }
      int month = (int) num1 * 10 + (int) num2;
      if (source[2] != (byte) 47)
      {
        bytesConsumed = 0;
        value = new DateTime();
        valueAsOffset = new DateTimeOffset();
        return false;
      }
      uint num3 = (uint) source[3] - 48U;
      uint num4 = (uint) source[4] - 48U;
      if (num3 > 9U || num4 > 9U)
      {
        bytesConsumed = 0;
        value = new DateTime();
        valueAsOffset = new DateTimeOffset();
        return false;
      }
      int day = (int) num3 * 10 + (int) num4;
      if (source[5] != (byte) 47)
      {
        bytesConsumed = 0;
        value = new DateTime();
        valueAsOffset = new DateTimeOffset();
        return false;
      }
      uint num5 = (uint) source[6] - 48U;
      uint num6 = (uint) source[7] - 48U;
      uint num7 = (uint) source[8] - 48U;
      uint num8 = (uint) source[9] - 48U;
      if (num5 > 9U || num6 > 9U || num7 > 9U || num8 > 9U)
      {
        bytesConsumed = 0;
        value = new DateTime();
        valueAsOffset = new DateTimeOffset();
        return false;
      }
      int year = (int) num5 * 1000 + (int) num6 * 100 + (int) num7 * 10 + (int) num8;
      if (source[10] != (byte) 32)
      {
        bytesConsumed = 0;
        value = new DateTime();
        valueAsOffset = new DateTimeOffset();
        return false;
      }
      uint num9 = (uint) source[11] - 48U;
      uint num10 = (uint) source[12] - 48U;
      if (num9 > 9U || num10 > 9U)
      {
        bytesConsumed = 0;
        value = new DateTime();
        valueAsOffset = new DateTimeOffset();
        return false;
      }
      int hour = (int) num9 * 10 + (int) num10;
      if (source[13] != (byte) 58)
      {
        bytesConsumed = 0;
        value = new DateTime();
        valueAsOffset = new DateTimeOffset();
        return false;
      }
      uint num11 = (uint) source[14] - 48U;
      uint num12 = (uint) source[15] - 48U;
      if (num11 > 9U || num12 > 9U)
      {
        bytesConsumed = 0;
        value = new DateTime();
        valueAsOffset = new DateTimeOffset();
        return false;
      }
      int minute = (int) num11 * 10 + (int) num12;
      if (source[16] != (byte) 58)
      {
        bytesConsumed = 0;
        value = new DateTime();
        valueAsOffset = new DateTimeOffset();
        return false;
      }
      uint num13 = (uint) source[17] - 48U;
      uint num14 = (uint) source[18] - 48U;
      if (num13 > 9U || num14 > 9U)
      {
        bytesConsumed = 0;
        value = new DateTime();
        valueAsOffset = new DateTimeOffset();
        return false;
      }
      int second = (int) num13 * 10 + (int) num14;
      if (!Utf8Parser.TryCreateDateTimeOffsetInterpretingDataAsLocalTime(year, month, day, hour, minute, second, 0, out valueAsOffset))
      {
        bytesConsumed = 0;
        value = new DateTime();
        valueAsOffset = new DateTimeOffset();
        return false;
      }
      bytesConsumed = 19;
      value = valueAsOffset.DateTime;
      return true;
    }

    private static bool TryCreateDateTimeOffset(
      DateTime dateTime,
      bool offsetNegative,
      int offsetHours,
      int offsetMinutes,
      out DateTimeOffset value)
    {
      if ((uint) offsetHours > 14U)
      {
        value = new DateTimeOffset();
        return false;
      }
      if ((uint) offsetMinutes > 59U)
      {
        value = new DateTimeOffset();
        return false;
      }
      if (offsetHours == 14 && offsetMinutes != 0)
      {
        value = new DateTimeOffset();
        return false;
      }
      long ticks = ((long) offsetHours * 3600L + (long) offsetMinutes * 60L) * 10000000L;
      if (offsetNegative)
        ticks = -ticks;
      try
      {
        value = new DateTimeOffset(dateTime.Ticks, new TimeSpan(ticks));
      }
      catch (ArgumentOutOfRangeException ex)
      {
        value = new DateTimeOffset();
        return false;
      }
      return true;
    }

    private static bool TryCreateDateTimeOffset(
      int year,
      int month,
      int day,
      int hour,
      int minute,
      int second,
      int fraction,
      bool offsetNegative,
      int offsetHours,
      int offsetMinutes,
      out DateTimeOffset value)
    {
      DateTime dateTime;
      if (!Utf8Parser.TryCreateDateTime(year, month, day, hour, minute, second, fraction, DateTimeKind.Unspecified, out dateTime))
      {
        value = new DateTimeOffset();
        return false;
      }
      if (Utf8Parser.TryCreateDateTimeOffset(dateTime, offsetNegative, offsetHours, offsetMinutes, out value))
        return true;
      value = new DateTimeOffset();
      return false;
    }

    private static bool TryCreateDateTimeOffsetInterpretingDataAsLocalTime(
      int year,
      int month,
      int day,
      int hour,
      int minute,
      int second,
      int fraction,
      out DateTimeOffset value)
    {
      DateTime dateTime;
      if (!Utf8Parser.TryCreateDateTime(year, month, day, hour, minute, second, fraction, DateTimeKind.Local, out dateTime))
      {
        value = new DateTimeOffset();
        return false;
      }
      try
      {
        value = new DateTimeOffset(dateTime);
      }
      catch (ArgumentOutOfRangeException ex)
      {
        value = new DateTimeOffset();
        return false;
      }
      return true;
    }

    private static bool TryCreateDateTime(
      int year,
      int month,
      int day,
      int hour,
      int minute,
      int second,
      int fraction,
      DateTimeKind kind,
      out DateTime value)
    {
      if (year == 0)
      {
        value = new DateTime();
        return false;
      }
      if ((uint) (month - 1) >= 12U)
      {
        value = new DateTime();
        return false;
      }
      uint num1 = (uint) (day - 1);
      if (num1 >= 28U && (long) num1 >= (long) DateTime.DaysInMonth(year, month))
      {
        value = new DateTime();
        return false;
      }
      if ((uint) hour > 23U)
      {
        value = new DateTime();
        return false;
      }
      if ((uint) minute > 59U)
      {
        value = new DateTime();
        return false;
      }
      if ((uint) second > 59U)
      {
        value = new DateTime();
        return false;
      }
      int[] numArray = DateTime.IsLeapYear(year) ? Utf8Parser.s_daysToMonth366 : Utf8Parser.s_daysToMonth365;
      int num2 = year - 1;
      long ticks = (long) (num2 * 365 + num2 / 4 - num2 / 100 + num2 / 400 + numArray[month - 1] + day - 1) * 864000000000L + (long) (hour * 3600 + minute * 60 + second) * 10000000L + (long) fraction;
      value = new DateTime(ticks, kind);
      return true;
    }

    private static bool TryParseDateTimeOffsetO(
      ReadOnlySpan<byte> source,
      out DateTimeOffset value,
      out int bytesConsumed,
      out DateTimeKind kind)
    {
      if (source.Length < 27)
      {
        value = new DateTimeOffset();
        bytesConsumed = 0;
        kind = DateTimeKind.Unspecified;
        return false;
      }
      uint num1 = (uint) source[0] - 48U;
      uint num2 = (uint) source[1] - 48U;
      uint num3 = (uint) source[2] - 48U;
      uint num4 = (uint) source[3] - 48U;
      if (num1 > 9U || num2 > 9U || num3 > 9U || num4 > 9U)
      {
        value = new DateTimeOffset();
        bytesConsumed = 0;
        kind = DateTimeKind.Unspecified;
        return false;
      }
      int year = (int) num1 * 1000 + (int) num2 * 100 + (int) num3 * 10 + (int) num4;
      if (source[4] != (byte) 45)
      {
        value = new DateTimeOffset();
        bytesConsumed = 0;
        kind = DateTimeKind.Unspecified;
        return false;
      }
      uint num5 = (uint) source[5] - 48U;
      uint num6 = (uint) source[6] - 48U;
      if (num5 > 9U || num6 > 9U)
      {
        value = new DateTimeOffset();
        bytesConsumed = 0;
        kind = DateTimeKind.Unspecified;
        return false;
      }
      int month = (int) num5 * 10 + (int) num6;
      if (source[7] != (byte) 45)
      {
        value = new DateTimeOffset();
        bytesConsumed = 0;
        kind = DateTimeKind.Unspecified;
        return false;
      }
      uint num7 = (uint) source[8] - 48U;
      uint num8 = (uint) source[9] - 48U;
      if (num7 > 9U || num8 > 9U)
      {
        value = new DateTimeOffset();
        bytesConsumed = 0;
        kind = DateTimeKind.Unspecified;
        return false;
      }
      int day = (int) num7 * 10 + (int) num8;
      if (source[10] != (byte) 84)
      {
        value = new DateTimeOffset();
        bytesConsumed = 0;
        kind = DateTimeKind.Unspecified;
        return false;
      }
      uint num9 = (uint) source[11] - 48U;
      uint num10 = (uint) source[12] - 48U;
      if (num9 > 9U || num10 > 9U)
      {
        value = new DateTimeOffset();
        bytesConsumed = 0;
        kind = DateTimeKind.Unspecified;
        return false;
      }
      int hour = (int) num9 * 10 + (int) num10;
      if (source[13] != (byte) 58)
      {
        value = new DateTimeOffset();
        bytesConsumed = 0;
        kind = DateTimeKind.Unspecified;
        return false;
      }
      uint num11 = (uint) source[14] - 48U;
      uint num12 = (uint) source[15] - 48U;
      if (num11 > 9U || num12 > 9U)
      {
        value = new DateTimeOffset();
        bytesConsumed = 0;
        kind = DateTimeKind.Unspecified;
        return false;
      }
      int minute = (int) num11 * 10 + (int) num12;
      if (source[16] != (byte) 58)
      {
        value = new DateTimeOffset();
        bytesConsumed = 0;
        kind = DateTimeKind.Unspecified;
        return false;
      }
      uint num13 = (uint) source[17] - 48U;
      uint num14 = (uint) source[18] - 48U;
      if (num13 > 9U || num14 > 9U)
      {
        value = new DateTimeOffset();
        bytesConsumed = 0;
        kind = DateTimeKind.Unspecified;
        return false;
      }
      int second = (int) num13 * 10 + (int) num14;
      if (source[19] != (byte) 46)
      {
        value = new DateTimeOffset();
        bytesConsumed = 0;
        kind = DateTimeKind.Unspecified;
        return false;
      }
      uint num15 = (uint) source[20] - 48U;
      uint num16 = (uint) source[21] - 48U;
      uint num17 = (uint) source[22] - 48U;
      uint num18 = (uint) source[23] - 48U;
      uint num19 = (uint) source[24] - 48U;
      uint num20 = (uint) source[25] - 48U;
      uint num21 = (uint) source[26] - 48U;
      if (num15 > 9U || num16 > 9U || num17 > 9U || num18 > 9U || num19 > 9U || num20 > 9U || num21 > 9U)
      {
        value = new DateTimeOffset();
        bytesConsumed = 0;
        kind = DateTimeKind.Unspecified;
        return false;
      }
      int fraction = (int) num15 * 1000000 + (int) num16 * 100000 + (int) num17 * 10000 + (int) num18 * 1000 + (int) num19 * 100 + (int) num20 * 10 + (int) num21;
      byte num22 = source.Length <= 27 ? (byte) 0 : source[27];
      switch (num22)
      {
        case 43:
        case 45:
        case 90:
          if (num22 == (byte) 90)
          {
            if (!Utf8Parser.TryCreateDateTimeOffset(year, month, day, hour, minute, second, fraction, false, 0, 0, out value))
            {
              value = new DateTimeOffset();
              bytesConsumed = 0;
              kind = DateTimeKind.Unspecified;
              return false;
            }
            bytesConsumed = 28;
            kind = DateTimeKind.Utc;
            return true;
          }
          if (source.Length < 33)
          {
            value = new DateTimeOffset();
            bytesConsumed = 0;
            kind = DateTimeKind.Unspecified;
            return false;
          }
          uint num23 = (uint) source[28] - 48U;
          uint num24 = (uint) source[29] - 48U;
          if (num23 > 9U || num24 > 9U)
          {
            value = new DateTimeOffset();
            bytesConsumed = 0;
            kind = DateTimeKind.Unspecified;
            return false;
          }
          int offsetHours = (int) num23 * 10 + (int) num24;
          if (source[30] != (byte) 58)
          {
            value = new DateTimeOffset();
            bytesConsumed = 0;
            kind = DateTimeKind.Unspecified;
            return false;
          }
          uint num25 = (uint) source[31] - 48U;
          uint num26 = (uint) source[32] - 48U;
          if (num25 > 9U || num26 > 9U)
          {
            value = new DateTimeOffset();
            bytesConsumed = 0;
            kind = DateTimeKind.Unspecified;
            return false;
          }
          int offsetMinutes = (int) num25 * 10 + (int) num26;
          if (!Utf8Parser.TryCreateDateTimeOffset(year, month, day, hour, minute, second, fraction, num22 == (byte) 45, offsetHours, offsetMinutes, out value))
          {
            value = new DateTimeOffset();
            bytesConsumed = 0;
            kind = DateTimeKind.Unspecified;
            return false;
          }
          bytesConsumed = 33;
          kind = DateTimeKind.Local;
          return true;
        default:
          if (!Utf8Parser.TryCreateDateTimeOffsetInterpretingDataAsLocalTime(year, month, day, hour, minute, second, fraction, out value))
          {
            value = new DateTimeOffset();
            bytesConsumed = 0;
            kind = DateTimeKind.Unspecified;
            return false;
          }
          bytesConsumed = 27;
          kind = DateTimeKind.Unspecified;
          return true;
      }
    }

    private static bool TryParseDateTimeOffsetR(
      ReadOnlySpan<byte> source,
      uint caseFlipXorMask,
      out DateTimeOffset dateTimeOffset,
      out int bytesConsumed)
    {
      if (source.Length < 29)
      {
        bytesConsumed = 0;
        dateTimeOffset = new DateTimeOffset();
        return false;
      }
      DayOfWeek dayOfWeek;
      switch ((uint) ((int) ((uint) source[0] ^ caseFlipXorMask) << 24 | (int) source[1] << 16 | (int) source[2] << 8) | (uint) source[3])
      {
        case 1181903148:
          dayOfWeek = DayOfWeek.Friday;
          break;
        case 1299148332:
          dayOfWeek = DayOfWeek.Monday;
          break;
        case 1398895660:
          dayOfWeek = DayOfWeek.Saturday;
          break;
        case 1400204844:
          dayOfWeek = DayOfWeek.Sunday;
          break;
        case 1416131884:
          dayOfWeek = DayOfWeek.Thursday;
          break;
        case 1416979756:
          dayOfWeek = DayOfWeek.Tuesday;
          break;
        case 1466262572:
          dayOfWeek = DayOfWeek.Wednesday;
          break;
        default:
          bytesConsumed = 0;
          dateTimeOffset = new DateTimeOffset();
          return false;
      }
      if (source[4] != (byte) 32)
      {
        bytesConsumed = 0;
        dateTimeOffset = new DateTimeOffset();
        return false;
      }
      uint num1 = (uint) source[5] - 48U;
      uint num2 = (uint) source[6] - 48U;
      if (num1 > 9U || num2 > 9U)
      {
        bytesConsumed = 0;
        dateTimeOffset = new DateTimeOffset();
        return false;
      }
      int day = (int) num1 * 10 + (int) num2;
      if (source[7] != (byte) 32)
      {
        bytesConsumed = 0;
        dateTimeOffset = new DateTimeOffset();
        return false;
      }
      int month;
      switch ((uint) ((int) ((uint) source[8] ^ caseFlipXorMask) << 24 | (int) source[9] << 16 | (int) source[10] << 8) | (uint) source[11])
      {
        case 1097888288:
          month = 4;
          break;
        case 1098213152:
          month = 8;
          break;
        case 1147495200:
          month = 12;
          break;
        case 1181049376:
          month = 2;
          break;
        case 1247899168:
          month = 1;
          break;
        case 1249209376:
          month = 7;
          break;
        case 1249209888:
          month = 6;
          break;
        case 1298231840:
          month = 3;
          break;
        case 1298233632:
          month = 5;
          break;
        case 1315927584:
          month = 11;
          break;
        case 1331917856:
          month = 10;
          break;
        case 1399156768:
          month = 9;
          break;
        default:
          bytesConsumed = 0;
          dateTimeOffset = new DateTimeOffset();
          return false;
      }
      uint num3 = (uint) source[12] - 48U;
      uint num4 = (uint) source[13] - 48U;
      uint num5 = (uint) source[14] - 48U;
      uint num6 = (uint) source[15] - 48U;
      if (num3 > 9U || num4 > 9U || num5 > 9U || num6 > 9U)
      {
        bytesConsumed = 0;
        dateTimeOffset = new DateTimeOffset();
        return false;
      }
      int year = (int) num3 * 1000 + (int) num4 * 100 + (int) num5 * 10 + (int) num6;
      if (source[16] != (byte) 32)
      {
        bytesConsumed = 0;
        dateTimeOffset = new DateTimeOffset();
        return false;
      }
      uint num7 = (uint) source[17] - 48U;
      uint num8 = (uint) source[18] - 48U;
      if (num7 > 9U || num8 > 9U)
      {
        bytesConsumed = 0;
        dateTimeOffset = new DateTimeOffset();
        return false;
      }
      int hour = (int) num7 * 10 + (int) num8;
      if (source[19] != (byte) 58)
      {
        bytesConsumed = 0;
        dateTimeOffset = new DateTimeOffset();
        return false;
      }
      uint num9 = (uint) source[20] - 48U;
      uint num10 = (uint) source[21] - 48U;
      if (num9 > 9U || num10 > 9U)
      {
        bytesConsumed = 0;
        dateTimeOffset = new DateTimeOffset();
        return false;
      }
      int minute = (int) num9 * 10 + (int) num10;
      if (source[22] != (byte) 58)
      {
        bytesConsumed = 0;
        dateTimeOffset = new DateTimeOffset();
        return false;
      }
      uint num11 = (uint) source[23] - 48U;
      uint num12 = (uint) source[24] - 48U;
      if (num11 > 9U || num12 > 9U)
      {
        bytesConsumed = 0;
        dateTimeOffset = new DateTimeOffset();
        return false;
      }
      int second = (int) num11 * 10 + (int) num12;
      if (((uint) ((int) source[25] << 24 | (int) ((uint) source[26] ^ caseFlipXorMask) << 16 | (int) ((uint) source[27] ^ caseFlipXorMask) << 8) | (uint) source[28] ^ caseFlipXorMask) != 541543764U)
      {
        bytesConsumed = 0;
        dateTimeOffset = new DateTimeOffset();
        return false;
      }
      if (!Utf8Parser.TryCreateDateTimeOffset(year, month, day, hour, minute, second, 0, false, 0, 0, out dateTimeOffset))
      {
        bytesConsumed = 0;
        dateTimeOffset = new DateTimeOffset();
        return false;
      }
      if (dayOfWeek != dateTimeOffset.DayOfWeek)
      {
        bytesConsumed = 0;
        dateTimeOffset = new DateTimeOffset();
        return false;
      }
      bytesConsumed = 29;
      return true;
    }

    public static bool TryParse(
      ReadOnlySpan<byte> source,
      out Decimal value,
      out int bytesConsumed,
      char standardFormat = '\0')
    {
      Utf8Parser.ParseNumberOptions options;
      switch (standardFormat)
      {
        case char.MinValue:
        case 'E':
        case 'G':
        case 'e':
        case 'g':
          options = Utf8Parser.ParseNumberOptions.AllowExponent;
          break;
        case 'F':
        case 'f':
          options = (Utf8Parser.ParseNumberOptions) 0;
          break;
        default:
          return ThrowHelper.TryParseThrowFormatException<Decimal>(out value, out bytesConsumed);
      }
      NumberBuffer number = new NumberBuffer();
      bool textUsedExponentNotation;
      if (!Utf8Parser.TryParseNumber(source, ref number, out bytesConsumed, options, out textUsedExponentNotation))
      {
        value = 0M;
        return false;
      }
      if (!textUsedExponentNotation && (standardFormat == 'E' || standardFormat == 'e'))
      {
        value = 0M;
        bytesConsumed = 0;
        return false;
      }
      if (number.Digits[0] == (byte) 0 && number.Scale == 0)
        number.IsNegative = false;
      value = 0M;
      if (Number.NumberBufferToDecimal(ref number, ref value))
        return true;
      value = 0M;
      bytesConsumed = 0;
      return false;
    }

    public static bool TryParse(
      ReadOnlySpan<byte> source,
      out float value,
      out int bytesConsumed,
      char standardFormat = '\0')
    {
      double num;
      if (!Utf8Parser.TryParseNormalAsFloatingPoint(source, out num, out bytesConsumed, standardFormat))
        return Utf8Parser.TryParseAsSpecialFloatingPoint<float>(source, float.PositiveInfinity, float.NegativeInfinity, float.NaN, out value, out bytesConsumed);
      value = (float) num;
      if (!float.IsInfinity(value))
        return true;
      value = 0.0f;
      bytesConsumed = 0;
      return false;
    }

    public static bool TryParse(
      ReadOnlySpan<byte> source,
      out double value,
      out int bytesConsumed,
      char standardFormat = '\0')
    {
      return Utf8Parser.TryParseNormalAsFloatingPoint(source, out value, out bytesConsumed, standardFormat) || Utf8Parser.TryParseAsSpecialFloatingPoint<double>(source, double.PositiveInfinity, double.NegativeInfinity, double.NaN, out value, out bytesConsumed);
    }

    private static bool TryParseNormalAsFloatingPoint(
      ReadOnlySpan<byte> source,
      out double value,
      out int bytesConsumed,
      char standardFormat)
    {
      Utf8Parser.ParseNumberOptions options;
      switch (standardFormat)
      {
        case char.MinValue:
        case 'E':
        case 'G':
        case 'e':
        case 'g':
          options = Utf8Parser.ParseNumberOptions.AllowExponent;
          break;
        case 'F':
        case 'f':
          options = (Utf8Parser.ParseNumberOptions) 0;
          break;
        default:
          return ThrowHelper.TryParseThrowFormatException<double>(out value, out bytesConsumed);
      }
      NumberBuffer number = new NumberBuffer();
      bool textUsedExponentNotation;
      if (!Utf8Parser.TryParseNumber(source, ref number, out bytesConsumed, options, out textUsedExponentNotation))
      {
        value = 0.0;
        return false;
      }
      if (!textUsedExponentNotation && (standardFormat == 'E' || standardFormat == 'e'))
      {
        value = 0.0;
        bytesConsumed = 0;
        return false;
      }
      if (number.Digits[0] == (byte) 0)
        number.IsNegative = false;
      if (Number.NumberBufferToDouble(ref number, out value))
        return true;
      value = 0.0;
      bytesConsumed = 0;
      return false;
    }

    private static bool TryParseAsSpecialFloatingPoint<T>(
      ReadOnlySpan<byte> source,
      T positiveInfinity,
      T negativeInfinity,
      T nan,
      out T value,
      out int bytesConsumed)
    {
      if (source.Length >= 8 && source[0] == (byte) 73 && source[1] == (byte) 110 && source[2] == (byte) 102 && source[3] == (byte) 105 && source[4] == (byte) 110 && source[5] == (byte) 105 && source[6] == (byte) 116 && source[7] == (byte) 121)
      {
        value = positiveInfinity;
        bytesConsumed = 8;
        return true;
      }
      if (source.Length >= 9 && source[0] == (byte) 45 && source[1] == (byte) 73 && source[2] == (byte) 110 && source[3] == (byte) 102 && source[4] == (byte) 105 && source[5] == (byte) 110 && source[6] == (byte) 105 && source[7] == (byte) 116 && source[8] == (byte) 121)
      {
        value = negativeInfinity;
        bytesConsumed = 9;
        return true;
      }
      if (source.Length >= 3 && source[0] == (byte) 78 && source[1] == (byte) 97 && source[2] == (byte) 78)
      {
        value = nan;
        bytesConsumed = 3;
        return true;
      }
      value = default (T);
      bytesConsumed = 0;
      return false;
    }

    public static bool TryParse(
      ReadOnlySpan<byte> source,
      out Guid value,
      out int bytesConsumed,
      char standardFormat = '\0')
    {
      switch (standardFormat)
      {
        case char.MinValue:
        case 'D':
          return Utf8Parser.TryParseGuidCore(source, false, ' ', ' ', out value, out bytesConsumed);
        case 'B':
          return Utf8Parser.TryParseGuidCore(source, true, '{', '}', out value, out bytesConsumed);
        case 'N':
          return Utf8Parser.TryParseGuidN(source, out value, out bytesConsumed);
        case 'P':
          return Utf8Parser.TryParseGuidCore(source, true, '(', ')', out value, out bytesConsumed);
        default:
          return ThrowHelper.TryParseThrowFormatException<Guid>(out value, out bytesConsumed);
      }
    }

    private static bool TryParseGuidN(
      ReadOnlySpan<byte> text,
      out Guid value,
      out int bytesConsumed)
    {
      if (text.Length < 32)
      {
        value = new Guid();
        bytesConsumed = 0;
        return false;
      }
      uint a;
      int bytesConsumed1;
      if (!Utf8Parser.TryParseUInt32X(text.Slice(0, 8), out a, out bytesConsumed1) || bytesConsumed1 != 8)
      {
        value = new Guid();
        bytesConsumed = 0;
        return false;
      }
      ushort b;
      if (!Utf8Parser.TryParseUInt16X(text.Slice(8, 4), out b, out bytesConsumed1) || bytesConsumed1 != 4)
      {
        value = new Guid();
        bytesConsumed = 0;
        return false;
      }
      ushort c;
      if (!Utf8Parser.TryParseUInt16X(text.Slice(12, 4), out c, out bytesConsumed1) || bytesConsumed1 != 4)
      {
        value = new Guid();
        bytesConsumed = 0;
        return false;
      }
      ushort e;
      if (!Utf8Parser.TryParseUInt16X(text.Slice(16, 4), out e, out bytesConsumed1) || bytesConsumed1 != 4)
      {
        value = new Guid();
        bytesConsumed = 0;
        return false;
      }
      ulong k;
      if (!Utf8Parser.TryParseUInt64X(text.Slice(20), out k, out bytesConsumed1) || bytesConsumed1 != 12)
      {
        value = new Guid();
        bytesConsumed = 0;
        return false;
      }
      bytesConsumed = 32;
      value = new Guid((int) a, (short) b, (short) c, (byte) ((uint) e >> 8), (byte) e, (byte) (k >> 40), (byte) (k >> 32), (byte) (k >> 24), (byte) (k >> 16), (byte) (k >> 8), (byte) k);
      return true;
    }

    private static bool TryParseGuidCore(
      ReadOnlySpan<byte> source,
      bool ends,
      char begin,
      char end,
      out Guid value,
      out int bytesConsumed)
    {
      int num = 36 + (ends ? 2 : 0);
      if (source.Length < num)
      {
        value = new Guid();
        bytesConsumed = 0;
        return false;
      }
      if (ends)
      {
        if ((int) source[0] != (int) begin)
        {
          value = new Guid();
          bytesConsumed = 0;
          return false;
        }
        source = source.Slice(1);
      }
      uint a;
      int bytesConsumed1;
      if (!Utf8Parser.TryParseUInt32X(source, out a, out bytesConsumed1))
      {
        value = new Guid();
        bytesConsumed = 0;
        return false;
      }
      if (bytesConsumed1 != 8)
      {
        value = new Guid();
        bytesConsumed = 0;
        return false;
      }
      if (source[bytesConsumed1] != (byte) 45)
      {
        value = new Guid();
        bytesConsumed = 0;
        return false;
      }
      source = source.Slice(9);
      ushort b;
      if (!Utf8Parser.TryParseUInt16X(source, out b, out bytesConsumed1))
      {
        value = new Guid();
        bytesConsumed = 0;
        return false;
      }
      if (bytesConsumed1 != 4)
      {
        value = new Guid();
        bytesConsumed = 0;
        return false;
      }
      if (source[bytesConsumed1] != (byte) 45)
      {
        value = new Guid();
        bytesConsumed = 0;
        return false;
      }
      source = source.Slice(5);
      ushort c;
      if (!Utf8Parser.TryParseUInt16X(source, out c, out bytesConsumed1))
      {
        value = new Guid();
        bytesConsumed = 0;
        return false;
      }
      if (bytesConsumed1 != 4)
      {
        value = new Guid();
        bytesConsumed = 0;
        return false;
      }
      if (source[bytesConsumed1] != (byte) 45)
      {
        value = new Guid();
        bytesConsumed = 0;
        return false;
      }
      source = source.Slice(5);
      ushort e;
      if (!Utf8Parser.TryParseUInt16X(source, out e, out bytesConsumed1))
      {
        value = new Guid();
        bytesConsumed = 0;
        return false;
      }
      if (bytesConsumed1 != 4)
      {
        value = new Guid();
        bytesConsumed = 0;
        return false;
      }
      if (source[bytesConsumed1] != (byte) 45)
      {
        value = new Guid();
        bytesConsumed = 0;
        return false;
      }
      source = source.Slice(5);
      ulong k;
      if (!Utf8Parser.TryParseUInt64X(source, out k, out bytesConsumed1))
      {
        value = new Guid();
        bytesConsumed = 0;
        return false;
      }
      if (bytesConsumed1 != 12)
      {
        value = new Guid();
        bytesConsumed = 0;
        return false;
      }
      if (ends && (int) source[bytesConsumed1] != (int) end)
      {
        value = new Guid();
        bytesConsumed = 0;
        return false;
      }
      bytesConsumed = num;
      value = new Guid((int) a, (short) b, (short) c, (byte) ((uint) e >> 8), (byte) e, (byte) (k >> 40), (byte) (k >> 32), (byte) (k >> 24), (byte) (k >> 16), (byte) (k >> 8), (byte) k);
      return true;
    }

    [CLSCompliant(false)]
    public static bool TryParse(
      ReadOnlySpan<byte> source,
      out sbyte value,
      out int bytesConsumed,
      char standardFormat = '\0')
    {
      switch (standardFormat)
      {
        case char.MinValue:
        case 'D':
        case 'G':
        case 'd':
        case 'g':
          return Utf8Parser.TryParseSByteD(source, out value, out bytesConsumed);
        case 'N':
        case 'n':
          return Utf8Parser.TryParseSByteN(source, out value, out bytesConsumed);
        case 'X':
        case 'x':
          value = (sbyte) 0;
          return Utf8Parser.TryParseByteX(source, out Unsafe.As<sbyte, byte>(ref value), out bytesConsumed);
        default:
          return ThrowHelper.TryParseThrowFormatException<sbyte>(out value, out bytesConsumed);
      }
    }

    public static bool TryParse(
      ReadOnlySpan<byte> source,
      out short value,
      out int bytesConsumed,
      char standardFormat = '\0')
    {
      switch (standardFormat)
      {
        case char.MinValue:
        case 'D':
        case 'G':
        case 'd':
        case 'g':
          return Utf8Parser.TryParseInt16D(source, out value, out bytesConsumed);
        case 'N':
        case 'n':
          return Utf8Parser.TryParseInt16N(source, out value, out bytesConsumed);
        case 'X':
        case 'x':
          value = (short) 0;
          return Utf8Parser.TryParseUInt16X(source, out Unsafe.As<short, ushort>(ref value), out bytesConsumed);
        default:
          return ThrowHelper.TryParseThrowFormatException<short>(out value, out bytesConsumed);
      }
    }

    public static bool TryParse(
      ReadOnlySpan<byte> source,
      out int value,
      out int bytesConsumed,
      char standardFormat = '\0')
    {
      switch (standardFormat)
      {
        case char.MinValue:
        case 'D':
        case 'G':
        case 'd':
        case 'g':
          return Utf8Parser.TryParseInt32D(source, out value, out bytesConsumed);
        case 'N':
        case 'n':
          return Utf8Parser.TryParseInt32N(source, out value, out bytesConsumed);
        case 'X':
        case 'x':
          value = 0;
          return Utf8Parser.TryParseUInt32X(source, out Unsafe.As<int, uint>(ref value), out bytesConsumed);
        default:
          return ThrowHelper.TryParseThrowFormatException<int>(out value, out bytesConsumed);
      }
    }

    public static bool TryParse(
      ReadOnlySpan<byte> source,
      out long value,
      out int bytesConsumed,
      char standardFormat = '\0')
    {
      switch (standardFormat)
      {
        case char.MinValue:
        case 'D':
        case 'G':
        case 'd':
        case 'g':
          return Utf8Parser.TryParseInt64D(source, out value, out bytesConsumed);
        case 'N':
        case 'n':
          return Utf8Parser.TryParseInt64N(source, out value, out bytesConsumed);
        case 'X':
        case 'x':
          value = 0L;
          return Utf8Parser.TryParseUInt64X(source, out Unsafe.As<long, ulong>(ref value), out bytesConsumed);
        default:
          return ThrowHelper.TryParseThrowFormatException<long>(out value, out bytesConsumed);
      }
    }

    private static bool TryParseSByteD(
      ReadOnlySpan<byte> source,
      out sbyte value,
      out int bytesConsumed)
    {
      if (source.Length >= 1)
      {
        int num1 = 1;
        int index = 0;
        int i1 = (int) source[index];
        switch (i1)
        {
          case 43:
            ++index;
            if ((uint) index < (uint) source.Length)
            {
              i1 = (int) source[index];
              break;
            }
            goto label_16;
          case 45:
            num1 = -1;
            ++index;
            if ((uint) index < (uint) source.Length)
            {
              i1 = (int) source[index];
              break;
            }
            goto label_16;
        }
        int num2 = 0;
        if (ParserHelpers.IsDigit(i1))
        {
          if (i1 == 48)
          {
            do
            {
              ++index;
              if ((uint) index < (uint) source.Length)
                i1 = (int) source[index];
              else
                goto label_17;
            }
            while (i1 == 48);
            if (!ParserHelpers.IsDigit(i1))
              goto label_17;
          }
          num2 = i1 - 48;
          ++index;
          if ((uint) index < (uint) source.Length)
          {
            int i2 = (int) source[index];
            if (ParserHelpers.IsDigit(i2))
            {
              ++index;
              num2 = 10 * num2 + i2 - 48;
              if ((uint) index < (uint) source.Length)
              {
                int i3 = (int) source[index];
                if (ParserHelpers.IsDigit(i3))
                {
                  ++index;
                  num2 = num2 * 10 + i3 - 48;
                  if ((long) (uint) num2 > (long) sbyte.MaxValue + (long) ((-1 * num1 + 1) / 2) || (uint) index < (uint) source.Length && ParserHelpers.IsDigit((int) source[index]))
                    goto label_16;
                }
              }
            }
          }
label_17:
          bytesConsumed = index;
          value = (sbyte) (num2 * num1);
          return true;
        }
      }
label_16:
      bytesConsumed = 0;
      value = (sbyte) 0;
      return false;
    }

    private static bool TryParseInt16D(
      ReadOnlySpan<byte> source,
      out short value,
      out int bytesConsumed)
    {
      if (source.Length >= 1)
      {
        int num1 = 1;
        int index = 0;
        int i1 = (int) source[index];
        switch (i1)
        {
          case 43:
            ++index;
            if ((uint) index < (uint) source.Length)
            {
              i1 = (int) source[index];
              break;
            }
            goto label_20;
          case 45:
            num1 = -1;
            ++index;
            if ((uint) index < (uint) source.Length)
            {
              i1 = (int) source[index];
              break;
            }
            goto label_20;
        }
        int num2 = 0;
        if (ParserHelpers.IsDigit(i1))
        {
          if (i1 == 48)
          {
            do
            {
              ++index;
              if ((uint) index < (uint) source.Length)
                i1 = (int) source[index];
              else
                goto label_21;
            }
            while (i1 == 48);
            if (!ParserHelpers.IsDigit(i1))
              goto label_21;
          }
          num2 = i1 - 48;
          ++index;
          if ((uint) index < (uint) source.Length)
          {
            int i2 = (int) source[index];
            if (ParserHelpers.IsDigit(i2))
            {
              ++index;
              num2 = 10 * num2 + i2 - 48;
              if ((uint) index < (uint) source.Length)
              {
                int i3 = (int) source[index];
                if (ParserHelpers.IsDigit(i3))
                {
                  ++index;
                  num2 = 10 * num2 + i3 - 48;
                  if ((uint) index < (uint) source.Length)
                  {
                    int i4 = (int) source[index];
                    if (ParserHelpers.IsDigit(i4))
                    {
                      ++index;
                      num2 = 10 * num2 + i4 - 48;
                      if ((uint) index < (uint) source.Length)
                      {
                        int i5 = (int) source[index];
                        if (ParserHelpers.IsDigit(i5))
                        {
                          ++index;
                          num2 = num2 * 10 + i5 - 48;
                          if ((long) (uint) num2 > (long) short.MaxValue + (long) ((-1 * num1 + 1) / 2) || (uint) index < (uint) source.Length && ParserHelpers.IsDigit((int) source[index]))
                            goto label_20;
                        }
                      }
                    }
                  }
                }
              }
            }
          }
label_21:
          bytesConsumed = index;
          value = (short) (num2 * num1);
          return true;
        }
      }
label_20:
      bytesConsumed = 0;
      value = (short) 0;
      return false;
    }

    private static bool TryParseInt32D(
      ReadOnlySpan<byte> source,
      out int value,
      out int bytesConsumed)
    {
      if (source.Length >= 1)
      {
        int num1 = 1;
        int index = 0;
        int i1 = (int) source[index];
        switch (i1)
        {
          case 43:
            ++index;
            if ((uint) index < (uint) source.Length)
            {
              i1 = (int) source[index];
              break;
            }
            goto label_31;
          case 45:
            num1 = -1;
            ++index;
            if ((uint) index < (uint) source.Length)
            {
              i1 = (int) source[index];
              break;
            }
            goto label_31;
        }
        int num2 = 0;
        if (ParserHelpers.IsDigit(i1))
        {
          if (i1 == 48)
          {
            do
            {
              ++index;
              if ((uint) index < (uint) source.Length)
                i1 = (int) source[index];
              else
                goto label_32;
            }
            while (i1 == 48);
            if (!ParserHelpers.IsDigit(i1))
              goto label_32;
          }
          num2 = i1 - 48;
          ++index;
          if ((uint) index < (uint) source.Length)
          {
            int i2 = (int) source[index];
            if (ParserHelpers.IsDigit(i2))
            {
              ++index;
              num2 = 10 * num2 + i2 - 48;
              if ((uint) index < (uint) source.Length)
              {
                int i3 = (int) source[index];
                if (ParserHelpers.IsDigit(i3))
                {
                  ++index;
                  num2 = 10 * num2 + i3 - 48;
                  if ((uint) index < (uint) source.Length)
                  {
                    int i4 = (int) source[index];
                    if (ParserHelpers.IsDigit(i4))
                    {
                      ++index;
                      num2 = 10 * num2 + i4 - 48;
                      if ((uint) index < (uint) source.Length)
                      {
                        int i5 = (int) source[index];
                        if (ParserHelpers.IsDigit(i5))
                        {
                          ++index;
                          num2 = 10 * num2 + i5 - 48;
                          if ((uint) index < (uint) source.Length)
                          {
                            int i6 = (int) source[index];
                            if (ParserHelpers.IsDigit(i6))
                            {
                              ++index;
                              num2 = 10 * num2 + i6 - 48;
                              if ((uint) index < (uint) source.Length)
                              {
                                int i7 = (int) source[index];
                                if (ParserHelpers.IsDigit(i7))
                                {
                                  ++index;
                                  num2 = 10 * num2 + i7 - 48;
                                  if ((uint) index < (uint) source.Length)
                                  {
                                    int i8 = (int) source[index];
                                    if (ParserHelpers.IsDigit(i8))
                                    {
                                      ++index;
                                      num2 = 10 * num2 + i8 - 48;
                                      if ((uint) index < (uint) source.Length)
                                      {
                                        int i9 = (int) source[index];
                                        if (ParserHelpers.IsDigit(i9))
                                        {
                                          ++index;
                                          num2 = 10 * num2 + i9 - 48;
                                          if ((uint) index < (uint) source.Length)
                                          {
                                            int i10 = (int) source[index];
                                            if (ParserHelpers.IsDigit(i10))
                                            {
                                              ++index;
                                              if (num2 <= 214748364)
                                              {
                                                num2 = num2 * 10 + i10 - 48;
                                                if ((long) (uint) num2 > (long) int.MaxValue + (long) ((-1 * num1 + 1) / 2) || (uint) index < (uint) source.Length && ParserHelpers.IsDigit((int) source[index]))
                                                  goto label_31;
                                              }
                                              else
                                                goto label_31;
                                            }
                                          }
                                        }
                                      }
                                    }
                                  }
                                }
                              }
                            }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
          }
label_32:
          bytesConsumed = index;
          value = num2 * num1;
          return true;
        }
      }
label_31:
      bytesConsumed = 0;
      value = 0;
      return false;
    }

    private static bool TryParseInt64D(
      ReadOnlySpan<byte> source,
      out long value,
      out int bytesConsumed)
    {
      if (source.Length < 1)
      {
        bytesConsumed = 0;
        value = 0L;
        return false;
      }
      int index1 = 0;
      int num1 = 1;
      if (source[0] == (byte) 45)
      {
        index1 = 1;
        num1 = -1;
        if (source.Length <= index1)
        {
          bytesConsumed = 0;
          value = 0L;
          return false;
        }
      }
      else if (source[0] == (byte) 43)
      {
        index1 = 1;
        if (source.Length <= index1)
        {
          bytesConsumed = 0;
          value = 0L;
          return false;
        }
      }
      int num2 = 19 + index1;
      long num3 = (long) ((int) source[index1] - 48);
      if (num3 < 0L || num3 > 9L)
      {
        bytesConsumed = 0;
        value = 0L;
        return false;
      }
      ulong num4 = (ulong) num3;
      if (source.Length < num2)
      {
        for (int index2 = index1 + 1; index2 < source.Length; ++index2)
        {
          long num5 = (long) ((int) source[index2] - 48);
          if (num5 < 0L || num5 > 9L)
          {
            bytesConsumed = index2;
            value = (long) num4 * (long) num1;
            return true;
          }
          num4 = (ulong) ((long) num4 * 10L + num5);
        }
      }
      else
      {
        for (int index3 = index1 + 1; index3 < num2 - 1; ++index3)
        {
          long num6 = (long) ((int) source[index3] - 48);
          if (num6 < 0L || num6 > 9L)
          {
            bytesConsumed = index3;
            value = (long) num4 * (long) num1;
            return true;
          }
          num4 = (ulong) ((long) num4 * 10L + num6);
        }
        for (int index4 = num2 - 1; index4 < source.Length; ++index4)
        {
          long num7 = (long) ((int) source[index4] - 48);
          if (num7 < 0L || num7 > 9L)
          {
            bytesConsumed = index4;
            value = (long) num4 * (long) num1;
            return true;
          }
          bool flag1 = num1 > 0;
          bool flag2 = num7 > 8L || flag1 && num7 > 7L;
          if (num4 > 922337203685477580UL || num4 == 922337203685477580UL & flag2)
          {
            bytesConsumed = 0;
            value = 0L;
            return false;
          }
          num4 = (ulong) ((long) num4 * 10L + num7);
        }
      }
      bytesConsumed = source.Length;
      value = (long) num4 * (long) num1;
      return true;
    }

    private static bool TryParseSByteN(
      ReadOnlySpan<byte> source,
      out sbyte value,
      out int bytesConsumed)
    {
      if (source.Length >= 1)
      {
        int num1 = 1;
        int index = 0;
        int i1 = (int) source[index];
        if (i1 == 45)
        {
          num1 = -1;
          ++index;
          if ((uint) index < (uint) source.Length)
            i1 = (int) source[index];
          else
            goto label_18;
        }
        else if (i1 == 43)
        {
          ++index;
          if ((uint) index < (uint) source.Length)
            i1 = (int) source[index];
          else
            goto label_18;
        }
        int num2;
        if (i1 != 46)
        {
          if (ParserHelpers.IsDigit(i1))
          {
            num2 = i1 - 48;
            do
            {
              ++index;
              if ((uint) index < (uint) source.Length)
              {
                int i2 = (int) source[index];
                switch (i2)
                {
                  case 44:
                    continue;
                  case 46:
                    goto label_15;
                  default:
                    if (ParserHelpers.IsDigit(i2))
                    {
                      num2 = num2 * 10 + i2 - 48;
                      continue;
                    }
                    goto label_19;
                }
              }
              else
                goto label_19;
            }
            while (num2 <= (int) sbyte.MaxValue + (-1 * num1 + 1) / 2);
            goto label_18;
          }
          else
            goto label_18;
        }
        else
        {
          num2 = 0;
          ++index;
          if ((uint) index >= (uint) source.Length || source[index] != (byte) 48)
            goto label_18;
        }
label_15:
        int i3;
        do
        {
          ++index;
          if ((uint) index < (uint) source.Length)
            i3 = (int) source[index];
          else
            goto label_19;
        }
        while (i3 == 48);
        if (ParserHelpers.IsDigit(i3))
          goto label_18;
label_19:
        bytesConsumed = index;
        value = (sbyte) (num2 * num1);
        return true;
      }
label_18:
      bytesConsumed = 0;
      value = (sbyte) 0;
      return false;
    }

    private static bool TryParseInt16N(
      ReadOnlySpan<byte> source,
      out short value,
      out int bytesConsumed)
    {
      if (source.Length >= 1)
      {
        int num1 = 1;
        int index = 0;
        int i1 = (int) source[index];
        if (i1 == 45)
        {
          num1 = -1;
          ++index;
          if ((uint) index < (uint) source.Length)
            i1 = (int) source[index];
          else
            goto label_18;
        }
        else if (i1 == 43)
        {
          ++index;
          if ((uint) index < (uint) source.Length)
            i1 = (int) source[index];
          else
            goto label_18;
        }
        int num2;
        if (i1 != 46)
        {
          if (ParserHelpers.IsDigit(i1))
          {
            num2 = i1 - 48;
            do
            {
              ++index;
              if ((uint) index < (uint) source.Length)
              {
                int i2 = (int) source[index];
                switch (i2)
                {
                  case 44:
                    continue;
                  case 46:
                    goto label_15;
                  default:
                    if (ParserHelpers.IsDigit(i2))
                    {
                      num2 = num2 * 10 + i2 - 48;
                      continue;
                    }
                    goto label_19;
                }
              }
              else
                goto label_19;
            }
            while (num2 <= (int) short.MaxValue + (-1 * num1 + 1) / 2);
            goto label_18;
          }
          else
            goto label_18;
        }
        else
        {
          num2 = 0;
          ++index;
          if ((uint) index >= (uint) source.Length || source[index] != (byte) 48)
            goto label_18;
        }
label_15:
        int i3;
        do
        {
          ++index;
          if ((uint) index < (uint) source.Length)
            i3 = (int) source[index];
          else
            goto label_19;
        }
        while (i3 == 48);
        if (ParserHelpers.IsDigit(i3))
          goto label_18;
label_19:
        bytesConsumed = index;
        value = (short) (num2 * num1);
        return true;
      }
label_18:
      bytesConsumed = 0;
      value = (short) 0;
      return false;
    }

    private static bool TryParseInt32N(
      ReadOnlySpan<byte> source,
      out int value,
      out int bytesConsumed)
    {
      if (source.Length >= 1)
      {
        int num1 = 1;
        int index = 0;
        int i1 = (int) source[index];
        if (i1 == 45)
        {
          num1 = -1;
          ++index;
          if ((uint) index < (uint) source.Length)
            i1 = (int) source[index];
          else
            goto label_19;
        }
        else if (i1 == 43)
        {
          ++index;
          if ((uint) index < (uint) source.Length)
            i1 = (int) source[index];
          else
            goto label_19;
        }
        int num2;
        if (i1 != 46)
        {
          if (ParserHelpers.IsDigit(i1))
          {
            num2 = i1 - 48;
            do
            {
              ++index;
              if ((uint) index < (uint) source.Length)
              {
                int i2 = (int) source[index];
                switch (i2)
                {
                  case 44:
                    continue;
                  case 46:
                    goto label_16;
                  default:
                    if (ParserHelpers.IsDigit(i2))
                    {
                      if ((uint) num2 <= 214748364U)
                      {
                        num2 = num2 * 10 + i2 - 48;
                        continue;
                      }
                      goto label_19;
                    }
                    else
                      goto label_20;
                }
              }
              else
                goto label_20;
            }
            while ((long) (uint) num2 <= (long) int.MaxValue + (long) ((-1 * num1 + 1) / 2));
            goto label_19;
          }
          else
            goto label_19;
        }
        else
        {
          num2 = 0;
          ++index;
          if ((uint) index >= (uint) source.Length || source[index] != (byte) 48)
            goto label_19;
        }
label_16:
        int i3;
        do
        {
          ++index;
          if ((uint) index < (uint) source.Length)
            i3 = (int) source[index];
          else
            goto label_20;
        }
        while (i3 == 48);
        if (ParserHelpers.IsDigit(i3))
          goto label_19;
label_20:
        bytesConsumed = index;
        value = num2 * num1;
        return true;
      }
label_19:
      bytesConsumed = 0;
      value = 0;
      return false;
    }

    private static bool TryParseInt64N(
      ReadOnlySpan<byte> source,
      out long value,
      out int bytesConsumed)
    {
      if (source.Length >= 1)
      {
        int num1 = 1;
        int index = 0;
        int i1 = (int) source[index];
        if (i1 == 45)
        {
          num1 = -1;
          ++index;
          if ((uint) index < (uint) source.Length)
            i1 = (int) source[index];
          else
            goto label_19;
        }
        else if (i1 == 43)
        {
          ++index;
          if ((uint) index < (uint) source.Length)
            i1 = (int) source[index];
          else
            goto label_19;
        }
        long num2;
        if (i1 != 46)
        {
          if (ParserHelpers.IsDigit(i1))
          {
            num2 = (long) (i1 - 48);
            do
            {
              ++index;
              if ((uint) index < (uint) source.Length)
              {
                int i2 = (int) source[index];
                switch (i2)
                {
                  case 44:
                    continue;
                  case 46:
                    goto label_16;
                  default:
                    if (ParserHelpers.IsDigit(i2))
                    {
                      if ((ulong) num2 <= 922337203685477580UL)
                      {
                        num2 = num2 * 10L + (long) i2 - 48L;
                        continue;
                      }
                      goto label_19;
                    }
                    else
                      goto label_20;
                }
              }
              else
                goto label_20;
            }
            while ((ulong) num2 <= (ulong) long.MaxValue + (ulong) ((-1 * num1 + 1) / 2));
            goto label_19;
          }
          else
            goto label_19;
        }
        else
        {
          num2 = 0L;
          ++index;
          if ((uint) index >= (uint) source.Length || source[index] != (byte) 48)
            goto label_19;
        }
label_16:
        int i3;
        do
        {
          ++index;
          if ((uint) index < (uint) source.Length)
            i3 = (int) source[index];
          else
            goto label_20;
        }
        while (i3 == 48);
        if (ParserHelpers.IsDigit(i3))
          goto label_19;
label_20:
        bytesConsumed = index;
        value = num2 * (long) num1;
        return true;
      }
label_19:
      bytesConsumed = 0;
      value = 0L;
      return false;
    }

    public static bool TryParse(
      ReadOnlySpan<byte> source,
      out byte value,
      out int bytesConsumed,
      char standardFormat = '\0')
    {
      switch (standardFormat)
      {
        case char.MinValue:
        case 'D':
        case 'G':
        case 'd':
        case 'g':
          return Utf8Parser.TryParseByteD(source, out value, out bytesConsumed);
        case 'N':
        case 'n':
          return Utf8Parser.TryParseByteN(source, out value, out bytesConsumed);
        case 'X':
        case 'x':
          return Utf8Parser.TryParseByteX(source, out value, out bytesConsumed);
        default:
          return ThrowHelper.TryParseThrowFormatException<byte>(out value, out bytesConsumed);
      }
    }

    [CLSCompliant(false)]
    public static bool TryParse(
      ReadOnlySpan<byte> source,
      out ushort value,
      out int bytesConsumed,
      char standardFormat = '\0')
    {
      switch (standardFormat)
      {
        case char.MinValue:
        case 'D':
        case 'G':
        case 'd':
        case 'g':
          return Utf8Parser.TryParseUInt16D(source, out value, out bytesConsumed);
        case 'N':
        case 'n':
          return Utf8Parser.TryParseUInt16N(source, out value, out bytesConsumed);
        case 'X':
        case 'x':
          return Utf8Parser.TryParseUInt16X(source, out value, out bytesConsumed);
        default:
          return ThrowHelper.TryParseThrowFormatException<ushort>(out value, out bytesConsumed);
      }
    }

    [CLSCompliant(false)]
    public static bool TryParse(
      ReadOnlySpan<byte> source,
      out uint value,
      out int bytesConsumed,
      char standardFormat = '\0')
    {
      switch (standardFormat)
      {
        case char.MinValue:
        case 'D':
        case 'G':
        case 'd':
        case 'g':
          return Utf8Parser.TryParseUInt32D(source, out value, out bytesConsumed);
        case 'N':
        case 'n':
          return Utf8Parser.TryParseUInt32N(source, out value, out bytesConsumed);
        case 'X':
        case 'x':
          return Utf8Parser.TryParseUInt32X(source, out value, out bytesConsumed);
        default:
          return ThrowHelper.TryParseThrowFormatException<uint>(out value, out bytesConsumed);
      }
    }

    [CLSCompliant(false)]
    public static bool TryParse(
      ReadOnlySpan<byte> source,
      out ulong value,
      out int bytesConsumed,
      char standardFormat = '\0')
    {
      switch (standardFormat)
      {
        case char.MinValue:
        case 'D':
        case 'G':
        case 'd':
        case 'g':
          return Utf8Parser.TryParseUInt64D(source, out value, out bytesConsumed);
        case 'N':
        case 'n':
          return Utf8Parser.TryParseUInt64N(source, out value, out bytesConsumed);
        case 'X':
        case 'x':
          return Utf8Parser.TryParseUInt64X(source, out value, out bytesConsumed);
        default:
          return ThrowHelper.TryParseThrowFormatException<ulong>(out value, out bytesConsumed);
      }
    }

    private static bool TryParseByteD(
      ReadOnlySpan<byte> source,
      out byte value,
      out int bytesConsumed)
    {
      if (source.Length >= 1)
      {
        int index = 0;
        int i1 = (int) source[index];
        int num = 0;
        if (ParserHelpers.IsDigit(i1))
        {
          if (i1 == 48)
          {
            do
            {
              ++index;
              if ((uint) index < (uint) source.Length)
                i1 = (int) source[index];
              else
                goto label_12;
            }
            while (i1 == 48);
            if (!ParserHelpers.IsDigit(i1))
              goto label_12;
          }
          num = i1 - 48;
          ++index;
          if ((uint) index < (uint) source.Length)
          {
            int i2 = (int) source[index];
            if (ParserHelpers.IsDigit(i2))
            {
              ++index;
              num = 10 * num + i2 - 48;
              if ((uint) index < (uint) source.Length)
              {
                int i3 = (int) source[index];
                if (ParserHelpers.IsDigit(i3))
                {
                  ++index;
                  num = num * 10 + i3 - 48;
                  if ((uint) num > (uint) byte.MaxValue || (uint) index < (uint) source.Length && ParserHelpers.IsDigit((int) source[index]))
                    goto label_11;
                }
              }
            }
          }
label_12:
          bytesConsumed = index;
          value = (byte) num;
          return true;
        }
      }
label_11:
      bytesConsumed = 0;
      value = (byte) 0;
      return false;
    }

    private static bool TryParseUInt16D(
      ReadOnlySpan<byte> source,
      out ushort value,
      out int bytesConsumed)
    {
      if (source.Length >= 1)
      {
        int index = 0;
        int i1 = (int) source[index];
        int num = 0;
        if (ParserHelpers.IsDigit(i1))
        {
          if (i1 == 48)
          {
            do
            {
              ++index;
              if ((uint) index < (uint) source.Length)
                i1 = (int) source[index];
              else
                goto label_16;
            }
            while (i1 == 48);
            if (!ParserHelpers.IsDigit(i1))
              goto label_16;
          }
          num = i1 - 48;
          ++index;
          if ((uint) index < (uint) source.Length)
          {
            int i2 = (int) source[index];
            if (ParserHelpers.IsDigit(i2))
            {
              ++index;
              num = 10 * num + i2 - 48;
              if ((uint) index < (uint) source.Length)
              {
                int i3 = (int) source[index];
                if (ParserHelpers.IsDigit(i3))
                {
                  ++index;
                  num = 10 * num + i3 - 48;
                  if ((uint) index < (uint) source.Length)
                  {
                    int i4 = (int) source[index];
                    if (ParserHelpers.IsDigit(i4))
                    {
                      ++index;
                      num = 10 * num + i4 - 48;
                      if ((uint) index < (uint) source.Length)
                      {
                        int i5 = (int) source[index];
                        if (ParserHelpers.IsDigit(i5))
                        {
                          ++index;
                          num = num * 10 + i5 - 48;
                          if ((uint) num > (uint) ushort.MaxValue || (uint) index < (uint) source.Length && ParserHelpers.IsDigit((int) source[index]))
                            goto label_15;
                        }
                      }
                    }
                  }
                }
              }
            }
          }
label_16:
          bytesConsumed = index;
          value = (ushort) num;
          return true;
        }
      }
label_15:
      bytesConsumed = 0;
      value = (ushort) 0;
      return false;
    }

    private static bool TryParseUInt32D(
      ReadOnlySpan<byte> source,
      out uint value,
      out int bytesConsumed)
    {
      if (source.Length >= 1)
      {
        int index = 0;
        int i1 = (int) source[index];
        int num = 0;
        if (ParserHelpers.IsDigit(i1))
        {
          if (i1 == 48)
          {
            do
            {
              ++index;
              if ((uint) index < (uint) source.Length)
                i1 = (int) source[index];
              else
                goto label_27;
            }
            while (i1 == 48);
            if (!ParserHelpers.IsDigit(i1))
              goto label_27;
          }
          num = i1 - 48;
          ++index;
          if ((uint) index < (uint) source.Length)
          {
            int i2 = (int) source[index];
            if (ParserHelpers.IsDigit(i2))
            {
              ++index;
              num = 10 * num + i2 - 48;
              if ((uint) index < (uint) source.Length)
              {
                int i3 = (int) source[index];
                if (ParserHelpers.IsDigit(i3))
                {
                  ++index;
                  num = 10 * num + i3 - 48;
                  if ((uint) index < (uint) source.Length)
                  {
                    int i4 = (int) source[index];
                    if (ParserHelpers.IsDigit(i4))
                    {
                      ++index;
                      num = 10 * num + i4 - 48;
                      if ((uint) index < (uint) source.Length)
                      {
                        int i5 = (int) source[index];
                        if (ParserHelpers.IsDigit(i5))
                        {
                          ++index;
                          num = 10 * num + i5 - 48;
                          if ((uint) index < (uint) source.Length)
                          {
                            int i6 = (int) source[index];
                            if (ParserHelpers.IsDigit(i6))
                            {
                              ++index;
                              num = 10 * num + i6 - 48;
                              if ((uint) index < (uint) source.Length)
                              {
                                int i7 = (int) source[index];
                                if (ParserHelpers.IsDigit(i7))
                                {
                                  ++index;
                                  num = 10 * num + i7 - 48;
                                  if ((uint) index < (uint) source.Length)
                                  {
                                    int i8 = (int) source[index];
                                    if (ParserHelpers.IsDigit(i8))
                                    {
                                      ++index;
                                      num = 10 * num + i8 - 48;
                                      if ((uint) index < (uint) source.Length)
                                      {
                                        int i9 = (int) source[index];
                                        if (ParserHelpers.IsDigit(i9))
                                        {
                                          ++index;
                                          num = 10 * num + i9 - 48;
                                          if ((uint) index < (uint) source.Length)
                                          {
                                            int i10 = (int) source[index];
                                            if (ParserHelpers.IsDigit(i10))
                                            {
                                              ++index;
                                              if ((uint) num <= 429496729U && (num != 429496729 || i10 <= 53))
                                              {
                                                num = num * 10 + i10 - 48;
                                                if ((uint) index < (uint) source.Length && ParserHelpers.IsDigit((int) source[index]))
                                                  goto label_26;
                                              }
                                              else
                                                goto label_26;
                                            }
                                          }
                                        }
                                      }
                                    }
                                  }
                                }
                              }
                            }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
          }
label_27:
          bytesConsumed = index;
          value = (uint) num;
          return true;
        }
      }
label_26:
      bytesConsumed = 0;
      value = 0U;
      return false;
    }

    private static bool TryParseUInt64D(
      ReadOnlySpan<byte> source,
      out ulong value,
      out int bytesConsumed)
    {
      if (source.Length < 1)
      {
        bytesConsumed = 0;
        value = 0UL;
        return false;
      }
      ulong num1 = (ulong) ((uint) source[0] - 48U);
      if (num1 > 9UL)
      {
        bytesConsumed = 0;
        value = 0UL;
        return false;
      }
      ulong num2 = num1;
      if (source.Length < 19)
      {
        for (int index = 1; index < source.Length; ++index)
        {
          ulong num3 = (ulong) ((uint) source[index] - 48U);
          if (num3 > 9UL)
          {
            bytesConsumed = index;
            value = num2;
            return true;
          }
          num2 = num2 * 10UL + num3;
        }
      }
      else
      {
        for (int index = 1; index < 18; ++index)
        {
          ulong num4 = (ulong) ((uint) source[index] - 48U);
          if (num4 > 9UL)
          {
            bytesConsumed = index;
            value = num2;
            return true;
          }
          num2 = num2 * 10UL + num4;
        }
        for (int index = 18; index < source.Length; ++index)
        {
          ulong num5 = (ulong) ((uint) source[index] - 48U);
          if (num5 > 9UL)
          {
            bytesConsumed = index;
            value = num2;
            return true;
          }
          if (num2 > 1844674407370955161UL || num2 == 1844674407370955161UL && num5 > 5UL)
          {
            bytesConsumed = 0;
            value = 0UL;
            return false;
          }
          num2 = num2 * 10UL + num5;
        }
      }
      bytesConsumed = source.Length;
      value = num2;
      return true;
    }

    private static bool TryParseByteN(
      ReadOnlySpan<byte> source,
      out byte value,
      out int bytesConsumed)
    {
      if (source.Length >= 1)
      {
        int index = 0;
        int i1 = (int) source[index];
        if (i1 == 43)
        {
          ++index;
          if ((uint) index < (uint) source.Length)
            i1 = (int) source[index];
          else
            goto label_15;
        }
        int num;
        if (i1 != 46)
        {
          if (ParserHelpers.IsDigit(i1))
          {
            num = i1 - 48;
            do
            {
              ++index;
              if ((uint) index < (uint) source.Length)
              {
                int i2 = (int) source[index];
                switch (i2)
                {
                  case 44:
                    continue;
                  case 46:
                    goto label_12;
                  default:
                    if (ParserHelpers.IsDigit(i2))
                    {
                      num = num * 10 + i2 - 48;
                      continue;
                    }
                    goto label_16;
                }
              }
              else
                goto label_16;
            }
            while (num <= (int) byte.MaxValue);
            goto label_15;
          }
          else
            goto label_15;
        }
        else
        {
          num = 0;
          ++index;
          if ((uint) index >= (uint) source.Length || source[index] != (byte) 48)
            goto label_15;
        }
label_12:
        int i3;
        do
        {
          ++index;
          if ((uint) index < (uint) source.Length)
            i3 = (int) source[index];
          else
            goto label_16;
        }
        while (i3 == 48);
        if (ParserHelpers.IsDigit(i3))
          goto label_15;
label_16:
        bytesConsumed = index;
        value = (byte) num;
        return true;
      }
label_15:
      bytesConsumed = 0;
      value = (byte) 0;
      return false;
    }

    private static bool TryParseUInt16N(
      ReadOnlySpan<byte> source,
      out ushort value,
      out int bytesConsumed)
    {
      if (source.Length >= 1)
      {
        int index = 0;
        int i1 = (int) source[index];
        if (i1 == 43)
        {
          ++index;
          if ((uint) index < (uint) source.Length)
            i1 = (int) source[index];
          else
            goto label_15;
        }
        int num;
        if (i1 != 46)
        {
          if (ParserHelpers.IsDigit(i1))
          {
            num = i1 - 48;
            do
            {
              ++index;
              if ((uint) index < (uint) source.Length)
              {
                int i2 = (int) source[index];
                switch (i2)
                {
                  case 44:
                    continue;
                  case 46:
                    goto label_12;
                  default:
                    if (ParserHelpers.IsDigit(i2))
                    {
                      num = num * 10 + i2 - 48;
                      continue;
                    }
                    goto label_16;
                }
              }
              else
                goto label_16;
            }
            while (num <= (int) ushort.MaxValue);
            goto label_15;
          }
          else
            goto label_15;
        }
        else
        {
          num = 0;
          ++index;
          if ((uint) index >= (uint) source.Length || source[index] != (byte) 48)
            goto label_15;
        }
label_12:
        int i3;
        do
        {
          ++index;
          if ((uint) index < (uint) source.Length)
            i3 = (int) source[index];
          else
            goto label_16;
        }
        while (i3 == 48);
        if (ParserHelpers.IsDigit(i3))
          goto label_15;
label_16:
        bytesConsumed = index;
        value = (ushort) num;
        return true;
      }
label_15:
      bytesConsumed = 0;
      value = (ushort) 0;
      return false;
    }

    private static bool TryParseUInt32N(
      ReadOnlySpan<byte> source,
      out uint value,
      out int bytesConsumed)
    {
      if (source.Length >= 1)
      {
        int index = 0;
        int i1 = (int) source[index];
        if (i1 == 43)
        {
          ++index;
          if ((uint) index < (uint) source.Length)
            i1 = (int) source[index];
          else
            goto label_16;
        }
        int num;
        if (i1 != 46)
        {
          if (ParserHelpers.IsDigit(i1))
          {
            num = i1 - 48;
            while (true)
            {
              ++index;
              if ((uint) index < (uint) source.Length)
              {
                int i2 = (int) source[index];
                switch (i2)
                {
                  case 44:
                    continue;
                  case 46:
                    goto label_13;
                  default:
                    if (ParserHelpers.IsDigit(i2))
                    {
                      if ((uint) num <= 429496729U && (num != 429496729 || i2 <= 53))
                      {
                        num = num * 10 + i2 - 48;
                        continue;
                      }
                      goto label_16;
                    }
                    else
                      goto label_17;
                }
              }
              else
                goto label_17;
            }
          }
          else
            goto label_16;
        }
        else
        {
          num = 0;
          ++index;
          if ((uint) index >= (uint) source.Length || source[index] != (byte) 48)
            goto label_16;
        }
label_13:
        int i3;
        do
        {
          ++index;
          if ((uint) index < (uint) source.Length)
            i3 = (int) source[index];
          else
            goto label_17;
        }
        while (i3 == 48);
        if (ParserHelpers.IsDigit(i3))
          goto label_16;
label_17:
        bytesConsumed = index;
        value = (uint) num;
        return true;
      }
label_16:
      bytesConsumed = 0;
      value = 0U;
      return false;
    }

    private static bool TryParseUInt64N(
      ReadOnlySpan<byte> source,
      out ulong value,
      out int bytesConsumed)
    {
      if (source.Length >= 1)
      {
        int index = 0;
        int i1 = (int) source[index];
        if (i1 == 43)
        {
          ++index;
          if ((uint) index < (uint) source.Length)
            i1 = (int) source[index];
          else
            goto label_16;
        }
        long num;
        if (i1 != 46)
        {
          if (ParserHelpers.IsDigit(i1))
          {
            num = (long) (i1 - 48);
            while (true)
            {
              ++index;
              if ((uint) index < (uint) source.Length)
              {
                int i2 = (int) source[index];
                switch (i2)
                {
                  case 44:
                    continue;
                  case 46:
                    goto label_13;
                  default:
                    if (ParserHelpers.IsDigit(i2))
                    {
                      if ((ulong) num <= 1844674407370955161UL && (num != 1844674407370955161L || i2 <= 53))
                      {
                        num = num * 10L + (long) i2 - 48L;
                        continue;
                      }
                      goto label_16;
                    }
                    else
                      goto label_17;
                }
              }
              else
                goto label_17;
            }
          }
          else
            goto label_16;
        }
        else
        {
          num = 0L;
          ++index;
          if ((uint) index >= (uint) source.Length || source[index] != (byte) 48)
            goto label_16;
        }
label_13:
        int i3;
        do
        {
          ++index;
          if ((uint) index < (uint) source.Length)
            i3 = (int) source[index];
          else
            goto label_17;
        }
        while (i3 == 48);
        if (ParserHelpers.IsDigit(i3))
          goto label_16;
label_17:
        bytesConsumed = index;
        value = (ulong) num;
        return true;
      }
label_16:
      bytesConsumed = 0;
      value = 0UL;
      return false;
    }

    private static bool TryParseByteX(
      ReadOnlySpan<byte> source,
      out byte value,
      out int bytesConsumed)
    {
      if (source.Length < 1)
      {
        bytesConsumed = 0;
        value = (byte) 0;
        return false;
      }
      byte[] hexLookup = ParserHelpers.s_hexLookup;
      byte index1 = source[0];
      byte num1 = hexLookup[(int) index1];
      if (num1 == byte.MaxValue)
      {
        bytesConsumed = 0;
        value = (byte) 0;
        return false;
      }
      uint num2 = (uint) num1;
      if (source.Length <= 2)
      {
        for (int index2 = 1; index2 < source.Length; ++index2)
        {
          byte index3 = source[index2];
          byte num3 = hexLookup[(int) index3];
          if (num3 == byte.MaxValue)
          {
            bytesConsumed = index2;
            value = (byte) num2;
            return true;
          }
          num2 = (num2 << 4) + (uint) num3;
        }
      }
      else
      {
        for (int index4 = 1; index4 < 2; ++index4)
        {
          byte index5 = source[index4];
          byte num4 = hexLookup[(int) index5];
          if (num4 == byte.MaxValue)
          {
            bytesConsumed = index4;
            value = (byte) num2;
            return true;
          }
          num2 = (num2 << 4) + (uint) num4;
        }
        for (int index6 = 2; index6 < source.Length; ++index6)
        {
          byte index7 = source[index6];
          byte num5 = hexLookup[(int) index7];
          if (num5 == byte.MaxValue)
          {
            bytesConsumed = index6;
            value = (byte) num2;
            return true;
          }
          if (num2 > 15U)
          {
            bytesConsumed = 0;
            value = (byte) 0;
            return false;
          }
          num2 = (num2 << 4) + (uint) num5;
        }
      }
      bytesConsumed = source.Length;
      value = (byte) num2;
      return true;
    }

    private static bool TryParseUInt16X(
      ReadOnlySpan<byte> source,
      out ushort value,
      out int bytesConsumed)
    {
      if (source.Length < 1)
      {
        bytesConsumed = 0;
        value = (ushort) 0;
        return false;
      }
      byte[] hexLookup = ParserHelpers.s_hexLookup;
      byte index1 = source[0];
      byte num1 = hexLookup[(int) index1];
      if (num1 == byte.MaxValue)
      {
        bytesConsumed = 0;
        value = (ushort) 0;
        return false;
      }
      uint num2 = (uint) num1;
      if (source.Length <= 4)
      {
        for (int index2 = 1; index2 < source.Length; ++index2)
        {
          byte index3 = source[index2];
          byte num3 = hexLookup[(int) index3];
          if (num3 == byte.MaxValue)
          {
            bytesConsumed = index2;
            value = (ushort) num2;
            return true;
          }
          num2 = (num2 << 4) + (uint) num3;
        }
      }
      else
      {
        for (int index4 = 1; index4 < 4; ++index4)
        {
          byte index5 = source[index4];
          byte num4 = hexLookup[(int) index5];
          if (num4 == byte.MaxValue)
          {
            bytesConsumed = index4;
            value = (ushort) num2;
            return true;
          }
          num2 = (num2 << 4) + (uint) num4;
        }
        for (int index6 = 4; index6 < source.Length; ++index6)
        {
          byte index7 = source[index6];
          byte num5 = hexLookup[(int) index7];
          if (num5 == byte.MaxValue)
          {
            bytesConsumed = index6;
            value = (ushort) num2;
            return true;
          }
          if (num2 > 4095U)
          {
            bytesConsumed = 0;
            value = (ushort) 0;
            return false;
          }
          num2 = (num2 << 4) + (uint) num5;
        }
      }
      bytesConsumed = source.Length;
      value = (ushort) num2;
      return true;
    }

    private static bool TryParseUInt32X(
      ReadOnlySpan<byte> source,
      out uint value,
      out int bytesConsumed)
    {
      if (source.Length < 1)
      {
        bytesConsumed = 0;
        value = 0U;
        return false;
      }
      byte[] hexLookup = ParserHelpers.s_hexLookup;
      byte index1 = source[0];
      byte num1 = hexLookup[(int) index1];
      if (num1 == byte.MaxValue)
      {
        bytesConsumed = 0;
        value = 0U;
        return false;
      }
      uint num2 = (uint) num1;
      if (source.Length <= 8)
      {
        for (int index2 = 1; index2 < source.Length; ++index2)
        {
          byte index3 = source[index2];
          byte num3 = hexLookup[(int) index3];
          if (num3 == byte.MaxValue)
          {
            bytesConsumed = index2;
            value = num2;
            return true;
          }
          num2 = (num2 << 4) + (uint) num3;
        }
      }
      else
      {
        for (int index4 = 1; index4 < 8; ++index4)
        {
          byte index5 = source[index4];
          byte num4 = hexLookup[(int) index5];
          if (num4 == byte.MaxValue)
          {
            bytesConsumed = index4;
            value = num2;
            return true;
          }
          num2 = (num2 << 4) + (uint) num4;
        }
        for (int index6 = 8; index6 < source.Length; ++index6)
        {
          byte index7 = source[index6];
          byte num5 = hexLookup[(int) index7];
          if (num5 == byte.MaxValue)
          {
            bytesConsumed = index6;
            value = num2;
            return true;
          }
          if (num2 > 268435455U)
          {
            bytesConsumed = 0;
            value = 0U;
            return false;
          }
          num2 = (num2 << 4) + (uint) num5;
        }
      }
      bytesConsumed = source.Length;
      value = num2;
      return true;
    }

    private static bool TryParseUInt64X(
      ReadOnlySpan<byte> source,
      out ulong value,
      out int bytesConsumed)
    {
      if (source.Length < 1)
      {
        bytesConsumed = 0;
        value = 0UL;
        return false;
      }
      byte[] hexLookup = ParserHelpers.s_hexLookup;
      byte index1 = source[0];
      byte num1 = hexLookup[(int) index1];
      if (num1 == byte.MaxValue)
      {
        bytesConsumed = 0;
        value = 0UL;
        return false;
      }
      ulong num2 = (ulong) num1;
      if (source.Length <= 16)
      {
        for (int index2 = 1; index2 < source.Length; ++index2)
        {
          byte index3 = source[index2];
          byte num3 = hexLookup[(int) index3];
          if (num3 == byte.MaxValue)
          {
            bytesConsumed = index2;
            value = num2;
            return true;
          }
          num2 = (num2 << 4) + (ulong) num3;
        }
      }
      else
      {
        for (int index4 = 1; index4 < 16; ++index4)
        {
          byte index5 = source[index4];
          byte num4 = hexLookup[(int) index5];
          if (num4 == byte.MaxValue)
          {
            bytesConsumed = index4;
            value = num2;
            return true;
          }
          num2 = (num2 << 4) + (ulong) num4;
        }
        for (int index6 = 16; index6 < source.Length; ++index6)
        {
          byte index7 = source[index6];
          byte num5 = hexLookup[(int) index7];
          if (num5 == byte.MaxValue)
          {
            bytesConsumed = index6;
            value = num2;
            return true;
          }
          if (num2 > 1152921504606846975UL)
          {
            bytesConsumed = 0;
            value = 0UL;
            return false;
          }
          num2 = (num2 << 4) + (ulong) num5;
        }
      }
      bytesConsumed = source.Length;
      value = num2;
      return true;
    }

    private static bool TryParseNumber(
      ReadOnlySpan<byte> source,
      ref NumberBuffer number,
      out int bytesConsumed,
      Utf8Parser.ParseNumberOptions options,
      out bool textUsedExponentNotation)
    {
      textUsedExponentNotation = false;
      if (source.Length == 0)
      {
        bytesConsumed = 0;
        return false;
      }
      Span<byte> digits = number.Digits;
      int index = 0;
      int num1 = 0;
      byte num2 = source[index];
      switch (num2)
      {
        case 43:
          ++index;
          if (index == source.Length)
          {
            bytesConsumed = 0;
            return false;
          }
          num2 = source[index];
          break;
        case 45:
          number.IsNegative = true;
          goto case 43;
      }
      int num3 = index;
      for (; index != source.Length; ++index)
      {
        num2 = source[index];
        if (num2 != (byte) 48)
          break;
      }
      if (index == source.Length)
      {
        digits[0] = (byte) 0;
        number.Scale = 0;
        bytesConsumed = index;
        return true;
      }
      int start1 = index;
      for (; index != source.Length; ++index)
      {
        num2 = source[index];
        switch (num2)
        {
          case 48:
          case 49:
          case 50:
          case 51:
          case 52:
          case 53:
          case 54:
          case 55:
          case 56:
          case 57:
            continue;
          default:
            goto label_17;
        }
      }
label_17:
      int num4 = index - num3;
      int val1 = index - start1;
      int length1 = Math.Min(val1, 50);
      ReadOnlySpan<byte> readOnlySpan = source.Slice(start1, length1);
      readOnlySpan.CopyTo(digits);
      int start2 = length1;
      number.Scale = val1;
      if (index == source.Length)
      {
        bytesConsumed = index;
        return true;
      }
      int num5 = 0;
      if (num2 == (byte) 46)
      {
        ++index;
        int num6 = index;
        for (; index != source.Length; ++index)
        {
          num2 = source[index];
          switch (num2)
          {
            case 48:
            case 49:
            case 50:
            case 51:
            case 52:
            case 53:
            case 54:
            case 55:
            case 56:
            case 57:
              continue;
            default:
              goto label_24;
          }
        }
label_24:
        num5 = index - num6;
        int num7 = num6;
        if (start2 == 0)
        {
          for (; num7 < index && source[num7] == (byte) 48; ++num7)
            --number.Scale;
        }
        int length2 = Math.Min(index - num7, 51 - start2 - 1);
        readOnlySpan = source.Slice(num7, length2);
        readOnlySpan.CopyTo(digits.Slice(start2));
        num1 = start2 + length2;
        if (index == source.Length)
        {
          if (num4 == 0 && num5 == 0)
          {
            bytesConsumed = 0;
            return false;
          }
          bytesConsumed = index;
          return true;
        }
      }
      if (num4 == 0 && num5 == 0)
      {
        bytesConsumed = 0;
        return false;
      }
      if (((int) num2 & -33) != 69)
      {
        bytesConsumed = index;
        return true;
      }
      textUsedExponentNotation = true;
      int num8 = index + 1;
      if ((options & Utf8Parser.ParseNumberOptions.AllowExponent) == (Utf8Parser.ParseNumberOptions) 0)
      {
        bytesConsumed = 0;
        return false;
      }
      if (num8 == source.Length)
      {
        bytesConsumed = 0;
        return false;
      }
      bool flag = false;
      switch (source[num8])
      {
        case 43:
          ++num8;
          if (num8 == source.Length)
          {
            bytesConsumed = 0;
            return false;
          }
          byte num9 = source[num8];
          break;
        case 45:
          flag = true;
          goto case 43;
      }
      uint num10;
      int bytesConsumed1;
      if (!Utf8Parser.TryParseUInt32D(source.Slice(num8), out num10, out bytesConsumed1))
      {
        bytesConsumed = 0;
        return false;
      }
      int num11 = num8 + bytesConsumed1;
      if (flag)
      {
        if ((long) number.Scale < (long) int.MinValue + (long) num10)
          number.Scale = int.MinValue;
        else
          number.Scale -= (int) num10;
      }
      else
      {
        if ((long) number.Scale > (long) int.MaxValue - (long) num10)
        {
          bytesConsumed = 0;
          return false;
        }
        number.Scale += (int) num10;
      }
      bytesConsumed = num11;
      return true;
    }

    private static bool TryParseTimeSpanBigG(
      ReadOnlySpan<byte> source,
      out TimeSpan value,
      out int bytesConsumed)
    {
      int num1 = 0;
      byte num2 = 0;
      for (; num1 != source.Length; ++num1)
      {
        num2 = source[num1];
        switch (num2)
        {
          case 9:
          case 32:
            continue;
          default:
            goto label_4;
        }
      }
label_4:
      if (num1 == source.Length)
      {
        value = new TimeSpan();
        bytesConsumed = 0;
        return false;
      }
      bool isNegative = false;
      if (num2 == (byte) 45)
      {
        isNegative = true;
        ++num1;
        if (num1 == source.Length)
        {
          value = new TimeSpan();
          bytesConsumed = 0;
          return false;
        }
      }
      uint days;
      int bytesConsumed1;
      if (!Utf8Parser.TryParseUInt32D(source.Slice(num1), out days, out bytesConsumed1))
      {
        value = new TimeSpan();
        bytesConsumed = 0;
        return false;
      }
      int num3 = num1 + bytesConsumed1;
      if (num3 != source.Length)
      {
        ref ReadOnlySpan<byte> local1 = ref source;
        int index1 = num3;
        int start1 = index1 + 1;
        if (local1[index1] == (byte) 58)
        {
          uint hours;
          if (!Utf8Parser.TryParseUInt32D(source.Slice(start1), out hours, out bytesConsumed1))
          {
            value = new TimeSpan();
            bytesConsumed = 0;
            return false;
          }
          int num4 = start1 + bytesConsumed1;
          if (num4 != source.Length)
          {
            ref ReadOnlySpan<byte> local2 = ref source;
            int index2 = num4;
            int start2 = index2 + 1;
            if (local2[index2] == (byte) 58)
            {
              uint minutes;
              if (!Utf8Parser.TryParseUInt32D(source.Slice(start2), out minutes, out bytesConsumed1))
              {
                value = new TimeSpan();
                bytesConsumed = 0;
                return false;
              }
              int num5 = start2 + bytesConsumed1;
              if (num5 != source.Length)
              {
                ref ReadOnlySpan<byte> local3 = ref source;
                int index3 = num5;
                int start3 = index3 + 1;
                if (local3[index3] == (byte) 58)
                {
                  uint seconds;
                  if (!Utf8Parser.TryParseUInt32D(source.Slice(start3), out seconds, out bytesConsumed1))
                  {
                    value = new TimeSpan();
                    bytesConsumed = 0;
                    return false;
                  }
                  int num6 = start3 + bytesConsumed1;
                  if (num6 != source.Length)
                  {
                    ref ReadOnlySpan<byte> local4 = ref source;
                    int index4 = num6;
                    int start4 = index4 + 1;
                    if (local4[index4] == (byte) 46)
                    {
                      uint fraction;
                      if (!Utf8Parser.TryParseTimeSpanFraction(source.Slice(start4), out fraction, out bytesConsumed1))
                      {
                        value = new TimeSpan();
                        bytesConsumed = 0;
                        return false;
                      }
                      int index5 = start4 + bytesConsumed1;
                      if (!Utf8Parser.TryCreateTimeSpan(isNegative, days, hours, minutes, seconds, fraction, out value))
                      {
                        value = new TimeSpan();
                        bytesConsumed = 0;
                        return false;
                      }
                      if (index5 != source.Length && (source[index5] == (byte) 46 || source[index5] == (byte) 58))
                      {
                        value = new TimeSpan();
                        bytesConsumed = 0;
                        return false;
                      }
                      bytesConsumed = index5;
                      return true;
                    }
                  }
                  value = new TimeSpan();
                  bytesConsumed = 0;
                  return false;
                }
              }
              value = new TimeSpan();
              bytesConsumed = 0;
              return false;
            }
          }
          value = new TimeSpan();
          bytesConsumed = 0;
          return false;
        }
      }
      value = new TimeSpan();
      bytesConsumed = 0;
      return false;
    }

    private static bool TryParseTimeSpanC(
      ReadOnlySpan<byte> source,
      out TimeSpan value,
      out int bytesConsumed)
    {
      Utf8Parser.TimeSpanSplitter timeSpanSplitter = new Utf8Parser.TimeSpanSplitter();
      if (!timeSpanSplitter.TrySplitTimeSpan(source, true, out bytesConsumed))
      {
        value = new TimeSpan();
        return false;
      }
      bool isNegative = timeSpanSplitter.IsNegative;
      bool flag;
      switch (timeSpanSplitter.Separators)
      {
        case 0:
          flag = Utf8Parser.TryCreateTimeSpan(isNegative, timeSpanSplitter.V1, 0U, 0U, 0U, 0U, out value);
          break;
        case 16777216:
          flag = Utf8Parser.TryCreateTimeSpan(isNegative, 0U, timeSpanSplitter.V1, timeSpanSplitter.V2, 0U, 0U, out value);
          break;
        case 16842752:
          flag = Utf8Parser.TryCreateTimeSpan(isNegative, 0U, timeSpanSplitter.V1, timeSpanSplitter.V2, timeSpanSplitter.V3, 0U, out value);
          break;
        case 16843264:
          flag = Utf8Parser.TryCreateTimeSpan(isNegative, 0U, timeSpanSplitter.V1, timeSpanSplitter.V2, timeSpanSplitter.V3, timeSpanSplitter.V4, out value);
          break;
        case 33619968:
          flag = Utf8Parser.TryCreateTimeSpan(isNegative, timeSpanSplitter.V1, timeSpanSplitter.V2, timeSpanSplitter.V3, 0U, 0U, out value);
          break;
        case 33620224:
          flag = Utf8Parser.TryCreateTimeSpan(isNegative, timeSpanSplitter.V1, timeSpanSplitter.V2, timeSpanSplitter.V3, timeSpanSplitter.V4, 0U, out value);
          break;
        case 33620226:
          flag = Utf8Parser.TryCreateTimeSpan(isNegative, timeSpanSplitter.V1, timeSpanSplitter.V2, timeSpanSplitter.V3, timeSpanSplitter.V4, timeSpanSplitter.V5, out value);
          break;
        default:
          value = new TimeSpan();
          flag = false;
          break;
      }
      if (flag)
        return true;
      bytesConsumed = 0;
      return false;
    }

    public static bool TryParse(
      ReadOnlySpan<byte> source,
      out TimeSpan value,
      out int bytesConsumed,
      char standardFormat = '\0')
    {
      switch (standardFormat)
      {
        case char.MinValue:
        case 'T':
        case 'c':
        case 't':
          return Utf8Parser.TryParseTimeSpanC(source, out value, out bytesConsumed);
        case 'G':
          return Utf8Parser.TryParseTimeSpanBigG(source, out value, out bytesConsumed);
        case 'g':
          return Utf8Parser.TryParseTimeSpanLittleG(source, out value, out bytesConsumed);
        default:
          return ThrowHelper.TryParseThrowFormatException<TimeSpan>(out value, out bytesConsumed);
      }
    }

    private static bool TryParseTimeSpanFraction(
      ReadOnlySpan<byte> source,
      out uint value,
      out int bytesConsumed)
    {
      int index1 = 0;
      if (index1 == source.Length)
      {
        value = 0U;
        bytesConsumed = 0;
        return false;
      }
      uint num1 = (uint) source[index1] - 48U;
      if (num1 > 9U)
      {
        value = 0U;
        bytesConsumed = 0;
        return false;
      }
      int index2 = index1 + 1;
      uint num2 = num1;
      int num3 = 1;
      while (index2 != source.Length)
      {
        uint num4 = (uint) source[index2] - 48U;
        if (num4 <= 9U)
        {
          ++index2;
          ++num3;
          if (num3 > 7)
          {
            value = 0U;
            bytesConsumed = 0;
            return false;
          }
          num2 = 10U * num2 + num4;
        }
        else
          break;
      }
      switch (num3)
      {
        case 2:
          num2 *= 100000U;
          goto case 7;
        case 3:
          num2 *= 10000U;
          goto case 7;
        case 4:
          num2 *= 1000U;
          goto case 7;
        case 5:
          num2 *= 100U;
          goto case 7;
        case 6:
          num2 *= 10U;
          goto case 7;
        case 7:
          value = num2;
          bytesConsumed = index2;
          return true;
        default:
          num2 *= 1000000U;
          goto case 7;
      }
    }

    private static bool TryCreateTimeSpan(
      bool isNegative,
      uint days,
      uint hours,
      uint minutes,
      uint seconds,
      uint fraction,
      out TimeSpan timeSpan)
    {
      if (hours > 23U || minutes > 59U || seconds > 59U)
      {
        timeSpan = new TimeSpan();
        return false;
      }
      long num1 = ((long) days * 3600L * 24L + (long) hours * 3600L + (long) minutes * 60L + (long) seconds) * 1000L;
      long ticks;
      if (isNegative)
      {
        long num2 = -num1;
        if (num2 < -922337203685477L)
        {
          timeSpan = new TimeSpan();
          return false;
        }
        long num3 = num2 * 10000L;
        if (num3 < long.MinValue + (long) fraction)
        {
          timeSpan = new TimeSpan();
          return false;
        }
        ticks = num3 - (long) fraction;
      }
      else
      {
        if (num1 > 922337203685477L)
        {
          timeSpan = new TimeSpan();
          return false;
        }
        long num4 = num1 * 10000L;
        if (num4 > long.MaxValue - (long) fraction)
        {
          timeSpan = new TimeSpan();
          return false;
        }
        ticks = num4 + (long) fraction;
      }
      timeSpan = new TimeSpan(ticks);
      return true;
    }

    private static bool TryParseTimeSpanLittleG(
      ReadOnlySpan<byte> source,
      out TimeSpan value,
      out int bytesConsumed)
    {
      Utf8Parser.TimeSpanSplitter timeSpanSplitter = new Utf8Parser.TimeSpanSplitter();
      if (!timeSpanSplitter.TrySplitTimeSpan(source, false, out bytesConsumed))
      {
        value = new TimeSpan();
        return false;
      }
      bool isNegative = timeSpanSplitter.IsNegative;
      bool flag;
      switch (timeSpanSplitter.Separators)
      {
        case 0:
          flag = Utf8Parser.TryCreateTimeSpan(isNegative, timeSpanSplitter.V1, 0U, 0U, 0U, 0U, out value);
          break;
        case 16777216:
          flag = Utf8Parser.TryCreateTimeSpan(isNegative, 0U, timeSpanSplitter.V1, timeSpanSplitter.V2, 0U, 0U, out value);
          break;
        case 16842752:
          flag = Utf8Parser.TryCreateTimeSpan(isNegative, 0U, timeSpanSplitter.V1, timeSpanSplitter.V2, timeSpanSplitter.V3, 0U, out value);
          break;
        case 16843008:
          flag = Utf8Parser.TryCreateTimeSpan(isNegative, timeSpanSplitter.V1, timeSpanSplitter.V2, timeSpanSplitter.V3, timeSpanSplitter.V4, 0U, out value);
          break;
        case 16843010:
          flag = Utf8Parser.TryCreateTimeSpan(isNegative, timeSpanSplitter.V1, timeSpanSplitter.V2, timeSpanSplitter.V3, timeSpanSplitter.V4, timeSpanSplitter.V5, out value);
          break;
        case 16843264:
          flag = Utf8Parser.TryCreateTimeSpan(isNegative, 0U, timeSpanSplitter.V1, timeSpanSplitter.V2, timeSpanSplitter.V3, timeSpanSplitter.V4, out value);
          break;
        default:
          value = new TimeSpan();
          flag = false;
          break;
      }
      if (flag)
        return true;
      bytesConsumed = 0;
      return false;
    }

    [Flags]
    private enum ParseNumberOptions
    {
      AllowExponent = 1,
    }

    private enum ComponentParseResult : byte
    {
      NoMoreData,
      Colon,
      Period,
      ParseFailure,
    }

    private struct TimeSpanSplitter
    {
      public uint V1;
      public uint V2;
      public uint V3;
      public uint V4;
      public uint V5;
      public bool IsNegative;
      public uint Separators;

      public bool TrySplitTimeSpan(
        ReadOnlySpan<byte> source,
        bool periodUsedToSeparateDay,
        out int bytesConsumed)
      {
        int num1 = 0;
        byte num2 = 0;
        for (; num1 != source.Length; ++num1)
        {
          num2 = source[num1];
          switch (num2)
          {
            case 9:
            case 32:
              continue;
            default:
              goto label_4;
          }
        }
label_4:
        if (num1 == source.Length)
        {
          bytesConsumed = 0;
          return false;
        }
        if (num2 == (byte) 45)
        {
          this.IsNegative = true;
          ++num1;
          if (num1 == source.Length)
          {
            bytesConsumed = 0;
            return false;
          }
        }
        int bytesConsumed1;
        if (!Utf8Parser.TryParseUInt32D(source.Slice(num1), out this.V1, out bytesConsumed1))
        {
          bytesConsumed = 0;
          return false;
        }
        int srcIndex = num1 + bytesConsumed1;
        Utf8Parser.ComponentParseResult component1 = Utf8Parser.TimeSpanSplitter.ParseComponent(source, periodUsedToSeparateDay, ref srcIndex, out this.V2);
        switch (component1)
        {
          case Utf8Parser.ComponentParseResult.NoMoreData:
            bytesConsumed = srcIndex;
            return true;
          case Utf8Parser.ComponentParseResult.ParseFailure:
            bytesConsumed = 0;
            return false;
          default:
            this.Separators |= (uint) component1 << 24;
            Utf8Parser.ComponentParseResult component2 = Utf8Parser.TimeSpanSplitter.ParseComponent(source, false, ref srcIndex, out this.V3);
            switch (component2)
            {
              case Utf8Parser.ComponentParseResult.NoMoreData:
                bytesConsumed = srcIndex;
                return true;
              case Utf8Parser.ComponentParseResult.ParseFailure:
                bytesConsumed = 0;
                return false;
              default:
                this.Separators |= (uint) component2 << 16;
                Utf8Parser.ComponentParseResult component3 = Utf8Parser.TimeSpanSplitter.ParseComponent(source, false, ref srcIndex, out this.V4);
                switch (component3)
                {
                  case Utf8Parser.ComponentParseResult.NoMoreData:
                    bytesConsumed = srcIndex;
                    return true;
                  case Utf8Parser.ComponentParseResult.ParseFailure:
                    bytesConsumed = 0;
                    return false;
                  default:
                    this.Separators |= (uint) component3 << 8;
                    Utf8Parser.ComponentParseResult component4 = Utf8Parser.TimeSpanSplitter.ParseComponent(source, false, ref srcIndex, out this.V5);
                    switch (component4)
                    {
                      case Utf8Parser.ComponentParseResult.NoMoreData:
                        bytesConsumed = srcIndex;
                        return true;
                      case Utf8Parser.ComponentParseResult.ParseFailure:
                        bytesConsumed = 0;
                        return false;
                      default:
                        this.Separators = (uint) ((Utf8Parser.ComponentParseResult) this.Separators | component4);
                        if (srcIndex != source.Length && (source[srcIndex] == (byte) 46 || source[srcIndex] == (byte) 58))
                        {
                          bytesConsumed = 0;
                          return false;
                        }
                        bytesConsumed = srcIndex;
                        return true;
                    }
                }
            }
        }
      }

      private static Utf8Parser.ComponentParseResult ParseComponent(
        ReadOnlySpan<byte> source,
        bool neverParseAsFraction,
        ref int srcIndex,
        out uint value)
      {
        if (srcIndex == source.Length)
        {
          value = 0U;
          return Utf8Parser.ComponentParseResult.NoMoreData;
        }
        byte num = source[srcIndex];
        if (num == (byte) 58 || num == (byte) 46 & neverParseAsFraction)
        {
          ++srcIndex;
          int bytesConsumed;
          if (!Utf8Parser.TryParseUInt32D(source.Slice(srcIndex), out value, out bytesConsumed))
          {
            value = 0U;
            return Utf8Parser.ComponentParseResult.ParseFailure;
          }
          srcIndex += bytesConsumed;
          return num != (byte) 58 ? Utf8Parser.ComponentParseResult.Period : Utf8Parser.ComponentParseResult.Colon;
        }
        if (num == (byte) 46)
        {
          ++srcIndex;
          int bytesConsumed;
          if (!Utf8Parser.TryParseTimeSpanFraction(source.Slice(srcIndex), out value, out bytesConsumed))
          {
            value = 0U;
            return Utf8Parser.ComponentParseResult.ParseFailure;
          }
          srcIndex += bytesConsumed;
          return Utf8Parser.ComponentParseResult.Period;
        }
        value = 0U;
        return Utf8Parser.ComponentParseResult.NoMoreData;
      }
    }
  }
}
