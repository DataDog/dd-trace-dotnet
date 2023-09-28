// Decompiled with JetBrains decompiler
// Type: System.Buffers.Text.Utf8Constants
// Assembly: System.Memory, Version=4.0.1.2, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51
// MVID: 805945F3-27B0-47AD-B8F6-389D9D8F82C3
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Memory.4.5.5\lib\net461\System.Memory.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Memory.4.5.5\lib\net461\System.Memory.xml

using System;

namespace Datadog.System.Buffers.Text
{
  internal static class Utf8Constants
  {
    public const byte Colon = 58;
    public const byte Comma = 44;
    public const byte Minus = 45;
    public const byte Period = 46;
    public const byte Plus = 43;
    public const byte Slash = 47;
    public const byte Space = 32;
    public const byte Hyphen = 45;
    public const byte Separator = 44;
    public const int GroupSize = 3;
    public static readonly TimeSpan s_nullUtcOffset = TimeSpan.MinValue;
    public const int DateTimeMaxUtcOffsetHours = 14;
    public const int DateTimeNumFractionDigits = 7;
    public const int MaxDateTimeFraction = 9999999;
    public const ulong BillionMaxUIntValue = 4294967295000000000;
    public const uint Billion = 1000000000;
  }
}
