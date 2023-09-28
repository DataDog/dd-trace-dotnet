// Decompiled with JetBrains decompiler
// Type: System.Reflection.Internal.BitArithmetic
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

namespace Datadog.System.Reflection.Internal
{
  internal static class BitArithmetic
  {
    internal static int CountBits(int v) => BitArithmetic.CountBits((uint) v);

    internal static int CountBits(uint v)
    {
      v -= v >> 1 & 1431655765U;
      v = (uint) (((int) v & 858993459) + ((int) (v >> 2) & 858993459));
      return ((int) v + (int) (v >> 4) & 252645135) * 16843009 >> 24;
    }

    internal static int CountBits(ulong v)
    {
      v -= v >> 1 & 6148914691236517205UL;
      v = (ulong) (((long) v & 3689348814741910323L) + ((long) (v >> 2) & 3689348814741910323L));
      return (int) (((long) v + (long) (v >> 4) & 1085102592571150095L) * 72340172838076673L >>> 56);
    }

    internal static uint Align(uint position, uint alignment)
    {
      uint num = position & (uint) ~((int) alignment - 1);
      return (int) num == (int) position ? num : num + alignment;
    }

    internal static int Align(int position, int alignment)
    {
      int num = position & ~(alignment - 1);
      return num == position ? num : num + alignment;
    }
  }
}
