// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.MethodDefTreatment
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  [Flags]
  internal enum MethodDefTreatment : byte
  {
    None = 0,
    KindMask = 15, // 0x0F
    Other = 1,
    DelegateMethod = 2,
    AttributeMethod = DelegateMethod | Other, // 0x03
    InterfaceMethod = 4,
    Implementation = InterfaceMethod | Other, // 0x05
    HiddenInterfaceImplementation = InterfaceMethod | DelegateMethod, // 0x06
    DisposeMethod = HiddenInterfaceImplementation | Other, // 0x07
    MarkAbstractFlag = 16, // 0x10
    MarkPublicFlag = 32, // 0x20
  }
}
