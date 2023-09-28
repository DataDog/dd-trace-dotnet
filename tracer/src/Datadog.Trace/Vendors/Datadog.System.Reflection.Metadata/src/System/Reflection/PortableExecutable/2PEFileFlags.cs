// Decompiled with JetBrains decompiler
// Type: System.Reflection.PortableExecutable.Subsystem
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

namespace Datadog.System.Reflection.PortableExecutable
{
  public enum Subsystem : ushort
  {
    Unknown = 0,
    Native = 1,
    WindowsGui = 2,
    WindowsCui = 3,
    OS2Cui = 5,
    PosixCui = 7,
    NativeWindows = 8,
    WindowsCEGui = 9,
    EfiApplication = 10, // 0x000A
    EfiBootServiceDriver = 11, // 0x000B
    EfiRuntimeDriver = 12, // 0x000C
    EfiRom = 13, // 0x000D
    Xbox = 14, // 0x000E
    WindowsBootApplication = 16, // 0x0010
  }
}
