﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.MethodImportAttributes
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;

namespace Datadog.System.Reflection
{
  [Flags]
  public enum MethodImportAttributes : short
  {
    None = 0,
    ExactSpelling = 1,
    BestFitMappingDisable = 32, // 0x0020
    BestFitMappingEnable = 16, // 0x0010
    BestFitMappingMask = BestFitMappingEnable | BestFitMappingDisable, // 0x0030
    CharSetAnsi = 2,
    CharSetUnicode = 4,
    CharSetAuto = CharSetUnicode | CharSetAnsi, // 0x0006
    CharSetMask = CharSetAuto, // 0x0006
    ThrowOnUnmappableCharEnable = 4096, // 0x1000
    ThrowOnUnmappableCharDisable = 8192, // 0x2000
    ThrowOnUnmappableCharMask = ThrowOnUnmappableCharDisable | ThrowOnUnmappableCharEnable, // 0x3000
    SetLastError = 64, // 0x0040
    CallingConventionWinApi = 256, // 0x0100
    CallingConventionCDecl = 512, // 0x0200
    CallingConventionStdCall = CallingConventionCDecl | CallingConventionWinApi, // 0x0300
    CallingConventionThisCall = 1024, // 0x0400
    CallingConventionFastCall = CallingConventionThisCall | CallingConventionWinApi, // 0x0500
    CallingConventionMask = CallingConventionFastCall | CallingConventionCDecl, // 0x0700
  }
}
