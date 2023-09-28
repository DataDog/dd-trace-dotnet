// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.StringUtils
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

namespace Datadog.System.Reflection.Metadata
{
  internal static class StringUtils
  {
    internal static int IgnoreCaseMask(bool ignoreCase) => !ignoreCase ? (int) byte.MaxValue : 32;

    internal static bool IsEqualAscii(int a, int b, int ignoreCaseMask)
    {
      if (a == b)
        return true;
      return (a | 32) == (b | 32) && (uint) ((a | ignoreCaseMask) - 97) <= 25U;
    }
  }
}
