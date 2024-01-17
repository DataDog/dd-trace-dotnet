﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.PortableExecutable.SectionCharacteristics
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;

namespace Datadog.System.Reflection.PortableExecutable
{
  [Flags]
  public enum SectionCharacteristics : uint
  {
    TypeReg = 0,
    TypeDSect = 1,
    TypeNoLoad = 2,
    TypeGroup = 4,
    TypeNoPad = 8,
    TypeCopy = 16, // 0x00000010
    ContainsCode = 32, // 0x00000020
    ContainsInitializedData = 64, // 0x00000040
    ContainsUninitializedData = 128, // 0x00000080
    LinkerOther = 256, // 0x00000100
    LinkerInfo = 512, // 0x00000200
    TypeOver = 1024, // 0x00000400
    LinkerRemove = 2048, // 0x00000800
    LinkerComdat = 4096, // 0x00001000
    MemProtected = 16384, // 0x00004000
    NoDeferSpecExc = MemProtected, // 0x00004000
    GPRel = 32768, // 0x00008000
    MemFardata = GPRel, // 0x00008000
    MemSysheap = 65536, // 0x00010000
    MemPurgeable = 131072, // 0x00020000
    Mem16Bit = MemPurgeable, // 0x00020000
    MemLocked = 262144, // 0x00040000
    MemPreload = 524288, // 0x00080000
    Align1Bytes = 1048576, // 0x00100000
    Align2Bytes = 2097152, // 0x00200000
    Align4Bytes = Align2Bytes | Align1Bytes, // 0x00300000
    Align8Bytes = 4194304, // 0x00400000
    Align16Bytes = Align8Bytes | Align1Bytes, // 0x00500000
    Align32Bytes = Align8Bytes | Align2Bytes, // 0x00600000
    Align64Bytes = Align32Bytes | Align1Bytes, // 0x00700000
    Align128Bytes = 8388608, // 0x00800000
    Align256Bytes = Align128Bytes | Align1Bytes, // 0x00900000
    Align512Bytes = Align128Bytes | Align2Bytes, // 0x00A00000
    Align1024Bytes = Align512Bytes | Align1Bytes, // 0x00B00000
    Align2048Bytes = Align128Bytes | Align8Bytes, // 0x00C00000
    Align4096Bytes = Align2048Bytes | Align1Bytes, // 0x00D00000
    Align8192Bytes = Align2048Bytes | Align2Bytes, // 0x00E00000
    AlignMask = Align8192Bytes | Align1Bytes, // 0x00F00000
    LinkerNRelocOvfl = 16777216, // 0x01000000
    MemDiscardable = 33554432, // 0x02000000
    MemNotCached = 67108864, // 0x04000000
    MemNotPaged = 134217728, // 0x08000000
    MemShared = 268435456, // 0x10000000
    MemExecute = 536870912, // 0x20000000
    MemRead = 1073741824, // 0x40000000
    MemWrite = 2147483648, // 0x80000000
  }
}
