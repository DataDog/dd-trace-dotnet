// Decompiled with JetBrains decompiler
// Type: System.Reflection.PortableExecutable.DllCharacteristics
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;

namespace Datadog.System.Reflection.PortableExecutable
{
  [Flags]
  public enum DllCharacteristics : ushort
  {
    /// <summary>Reserved.</summary>
    ProcessInit = 1,
    /// <summary>Reserved.</summary>
    ProcessTerm = 2,
    /// <summary>Reserved.</summary>
    ThreadInit = 4,
    /// <summary>Reserved.</summary>
    ThreadTerm = 8,
    /// <summary>
    /// Image can handle a high entropy 64-bit virtual address space.
    /// </summary>
    HighEntropyVirtualAddressSpace = 32, // 0x0020
    /// <summary>DLL can move.</summary>
    DynamicBase = 64, // 0x0040
    /// <summary>Image is NX compatible.</summary>
    NxCompatible = 256, // 0x0100
    /// <summary>Image understands isolation and doesn't want it.</summary>
    NoIsolation = 512, // 0x0200
    /// <summary>
    /// Image does not use SEH.  No SE handler may reside in this image.
    /// </summary>
    NoSeh = 1024, // 0x0400
    /// <summary>Do not bind this image.</summary>
    NoBind = 2048, // 0x0800
    /// <summary>The image must run inside an AppContainer.</summary>
    AppContainer = 4096, // 0x1000
    /// <summary>Driver uses WDM model.</summary>
    WdmDriver = 8192, // 0x2000
    TerminalServerAware = 32768, // 0x8000
  }
}
