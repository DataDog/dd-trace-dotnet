﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.PortableExecutable.Machine
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

namespace Datadog.System.Reflection.PortableExecutable
{
  public enum Machine : ushort
  {
    /// <summary>The target CPU is unknown or not specified.</summary>
    Unknown = 0,
    /// <summary>Intel 386.</summary>
    I386 = 332, // 0x014C
    /// <summary>MIPS little-endian WCE v2</summary>
    WceMipsV2 = 361, // 0x0169
    /// <summary>Alpha</summary>
    Alpha = 388, // 0x0184
    /// <summary>Hitachi SH3 little endian</summary>
    SH3 = 418, // 0x01A2
    /// <summary>Hitachi SH3 DSP.</summary>
    SH3Dsp = 419, // 0x01A3
    /// <summary>Hitachi SH3 little endian.</summary>
    SH3E = 420, // 0x01A4
    /// <summary>Hitachi SH4 little endian.</summary>
    SH4 = 422, // 0x01A6
    /// <summary>Hitachi SH5.</summary>
    SH5 = 424, // 0x01A8
    /// <summary>ARM little endian</summary>
    Arm = 448, // 0x01C0
    /// <summary>Thumb.</summary>
    Thumb = 450, // 0x01C2
    /// <summary>ARM Thumb-2 little endian.</summary>
    ArmThumb2 = 452, // 0x01C4
    /// <summary>Matsushita AM33.</summary>
    AM33 = 467, // 0x01D3
    /// <summary>IBM PowerPC little endian.</summary>
    PowerPC = 496, // 0x01F0
    /// <summary>PowerPCFP</summary>
    PowerPCFP = 497, // 0x01F1
    /// <summary>Intel 64</summary>
    IA64 = 512, // 0x0200
    /// <summary>MIPS</summary>
    MIPS16 = 614, // 0x0266
    /// <summary>ALPHA64</summary>
    Alpha64 = 644, // 0x0284
    /// <summary>MIPS with FPU.</summary>
    MipsFpu = 870, // 0x0366
    /// <summary>MIPS16 with FPU.</summary>
    MipsFpu16 = 1126, // 0x0466
    /// <summary>Infineon</summary>
    Tricore = 1312, // 0x0520
    /// <summary>EFI Byte Code</summary>
    Ebc = 3772, // 0x0EBC
    /// <summary>LOONGARCH32</summary>
    LoongArch32 = 25138, // 0x6232
    /// <summary>LOONGARCH64</summary>
    LoongArch64 = 25188, // 0x6264
    /// <summary>AMD64 (K8)</summary>
    Amd64 = 34404, // 0x8664
    /// <summary>M32R little-endian</summary>
    M32R = 36929, // 0x9041
    /// <summary>ARM64</summary>
    Arm64 = 43620, // 0xAA64
  }
}
