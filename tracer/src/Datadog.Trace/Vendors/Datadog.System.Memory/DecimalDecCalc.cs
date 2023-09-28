// Decompiled with JetBrains decompiler
// Type: System.DecimalDecCalc
// Assembly: System.Memory, Version=4.0.1.2, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51
// MVID: 805945F3-27B0-47AD-B8F6-389D9D8F82C3
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Memory.4.5.5\lib\net461\System.Memory.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Memory.4.5.5\lib\net461\System.Memory.xml

namespace Datadog.System
{
  internal static class DecimalDecCalc
  {
    private static uint D32DivMod1E9(uint hi32, ref uint lo32)
    {
      ulong num = (ulong) hi32 << 32 | (ulong) lo32;
      lo32 = (uint) (num / 1000000000UL);
      return (uint) (num % 1000000000UL);
    }

    internal static uint DecDivMod1E9(ref MutableDecimal value) => DecimalDecCalc.D32DivMod1E9(DecimalDecCalc.D32DivMod1E9(DecimalDecCalc.D32DivMod1E9(0U, ref value.High), ref value.Mid), ref value.Low);

    internal static void DecAddInt32(ref MutableDecimal value, uint i)
    {
      if (!DecimalDecCalc.D32AddCarry(ref value.Low, i) || !DecimalDecCalc.D32AddCarry(ref value.Mid, 1U))
        return;
      DecimalDecCalc.D32AddCarry(ref value.High, 1U);
    }

    private static bool D32AddCarry(ref uint value, uint i)
    {
      uint num1 = value;
      uint num2 = num1 + i;
      value = num2;
      return num2 < num1 || num2 < i;
    }

    internal static void DecMul10(ref MutableDecimal value)
    {
      MutableDecimal d = value;
      DecimalDecCalc.DecShiftLeft(ref value);
      DecimalDecCalc.DecShiftLeft(ref value);
      DecimalDecCalc.DecAdd(ref value, d);
      DecimalDecCalc.DecShiftLeft(ref value);
    }

    private static void DecShiftLeft(ref MutableDecimal value)
    {
      uint num1 = ((int) value.Low & int.MinValue) != 0 ? 1U : 0U;
      uint num2 = ((int) value.Mid & int.MinValue) != 0 ? 1U : 0U;
      value.Low <<= 1;
      value.Mid = value.Mid << 1 | num1;
      value.High = value.High << 1 | num2;
    }

    private static void DecAdd(ref MutableDecimal value, MutableDecimal d)
    {
      if (DecimalDecCalc.D32AddCarry(ref value.Low, d.Low) && DecimalDecCalc.D32AddCarry(ref value.Mid, 1U))
        DecimalDecCalc.D32AddCarry(ref value.High, 1U);
      if (DecimalDecCalc.D32AddCarry(ref value.Mid, d.Mid))
        DecimalDecCalc.D32AddCarry(ref value.High, 1U);
      DecimalDecCalc.D32AddCarry(ref value.High, d.High);
    }
  }
}
