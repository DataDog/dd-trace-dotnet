// Decompiled with JetBrains decompiler
// Type: System.Reflection.Internal.DecimalUtilities
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;

namespace Datadog.System.Reflection.Internal
{
  internal static class DecimalUtilities
  {
    public static int GetScale(this Decimal value) => (int) (byte) (Decimal.GetBits(value)[3] >> 16);

    public static void GetBits(
      this Decimal value,
      out bool isNegative,
      out byte scale,
      out uint low,
      out uint mid,
      out uint high)
    {
      int[] bits = Decimal.GetBits(value);
      low = (uint) bits[0];
      mid = (uint) bits[1];
      high = (uint) bits[2];
      scale = (byte) (bits[3] >> 16);
      isNegative = ((ulong) bits[3] & 2147483648UL) > 0UL;
    }
  }
}
