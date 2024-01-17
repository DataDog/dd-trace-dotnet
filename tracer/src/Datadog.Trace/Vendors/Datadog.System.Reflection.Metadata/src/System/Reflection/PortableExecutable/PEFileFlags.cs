﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.PortableExecutable.Characteristics
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;

namespace Datadog.System.Reflection.PortableExecutable
{
  [Flags]
  public enum Characteristics : ushort
  {
    RelocsStripped = 1,
    ExecutableImage = 2,
    LineNumsStripped = 4,
    LocalSymsStripped = 8,
    AggressiveWSTrim = 16, // 0x0010
    LargeAddressAware = 32, // 0x0020
    BytesReversedLo = 128, // 0x0080
    Bit32Machine = 256, // 0x0100
    DebugStripped = 512, // 0x0200
    RemovableRunFromSwap = 1024, // 0x0400
    NetRunFromSwap = 2048, // 0x0800
    System = 4096, // 0x1000
    Dll = 8192, // 0x2000
    UpSystemOnly = 16384, // 0x4000
    BytesReversedHi = 32768, // 0x8000
  }
}
