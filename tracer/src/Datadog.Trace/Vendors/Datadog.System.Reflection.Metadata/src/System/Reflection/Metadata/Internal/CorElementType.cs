﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.CorElementType
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  internal enum CorElementType : byte
  {
    Invalid = 0,
    ELEMENT_TYPE_VOID = 1,
    ELEMENT_TYPE_BOOLEAN = 2,
    ELEMENT_TYPE_CHAR = 3,
    ELEMENT_TYPE_I1 = 4,
    ELEMENT_TYPE_U1 = 5,
    ELEMENT_TYPE_I2 = 6,
    ELEMENT_TYPE_U2 = 7,
    ELEMENT_TYPE_I4 = 8,
    ELEMENT_TYPE_U4 = 9,
    ELEMENT_TYPE_I8 = 10, // 0x0A
    ELEMENT_TYPE_U8 = 11, // 0x0B
    ELEMENT_TYPE_R4 = 12, // 0x0C
    ELEMENT_TYPE_R8 = 13, // 0x0D
    ELEMENT_TYPE_STRING = 14, // 0x0E
    ELEMENT_TYPE_PTR = 15, // 0x0F
    ELEMENT_TYPE_BYREF = 16, // 0x10
    ELEMENT_TYPE_VALUETYPE = 17, // 0x11
    ELEMENT_TYPE_CLASS = 18, // 0x12
    ELEMENT_TYPE_VAR = 19, // 0x13
    ELEMENT_TYPE_ARRAY = 20, // 0x14
    ELEMENT_TYPE_GENERICINST = 21, // 0x15
    ELEMENT_TYPE_TYPEDBYREF = 22, // 0x16
    ELEMENT_TYPE_I = 24, // 0x18
    ELEMENT_TYPE_U = 25, // 0x19
    ELEMENT_TYPE_FNPTR = 27, // 0x1B
    ELEMENT_TYPE_OBJECT = 28, // 0x1C
    ELEMENT_TYPE_SZARRAY = 29, // 0x1D
    ELEMENT_TYPE_MVAR = 30, // 0x1E
    ELEMENT_TYPE_CMOD_REQD = 31, // 0x1F
    ELEMENT_TYPE_CMOD_OPT = 32, // 0x20
    ELEMENT_TYPE_HANDLE = 64, // 0x40
    ELEMENT_TYPE_SENTINEL = 65, // 0x41
    ELEMENT_TYPE_PINNED = 69, // 0x45
  }
}
