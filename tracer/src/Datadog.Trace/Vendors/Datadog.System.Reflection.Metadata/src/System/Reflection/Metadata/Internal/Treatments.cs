// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.TypeDefTreatment
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  [Flags]
  internal enum TypeDefTreatment : byte
  {
    None = 0,
    KindMask = 15, // 0x0F
    NormalNonAttribute = 1,
    NormalAttribute = 2,
    UnmangleWinRTName = NormalAttribute | NormalNonAttribute, // 0x03
    PrefixWinRTName = 4,
    RedirectedToClrType = PrefixWinRTName | NormalNonAttribute, // 0x05
    RedirectedToClrAttribute = PrefixWinRTName | NormalAttribute, // 0x06
    MarkAbstractFlag = 16, // 0x10
    MarkInternalFlag = 32, // 0x20
  }
}
