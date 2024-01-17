﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.TableMask
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  [Flags]
  internal enum TableMask : ulong
  {
    Module = 1,
    TypeRef = 2,
    TypeDef = 4,
    FieldPtr = 8,
    Field = 16, // 0x0000000000000010
    MethodPtr = 32, // 0x0000000000000020
    MethodDef = 64, // 0x0000000000000040
    ParamPtr = 128, // 0x0000000000000080
    Param = 256, // 0x0000000000000100
    InterfaceImpl = 512, // 0x0000000000000200
    MemberRef = 1024, // 0x0000000000000400
    Constant = 2048, // 0x0000000000000800
    CustomAttribute = 4096, // 0x0000000000001000
    FieldMarshal = 8192, // 0x0000000000002000
    DeclSecurity = 16384, // 0x0000000000004000
    ClassLayout = 32768, // 0x0000000000008000
    FieldLayout = 65536, // 0x0000000000010000
    StandAloneSig = 131072, // 0x0000000000020000
    EventMap = 262144, // 0x0000000000040000
    EventPtr = 524288, // 0x0000000000080000
    Event = 1048576, // 0x0000000000100000
    PropertyMap = 2097152, // 0x0000000000200000
    PropertyPtr = 4194304, // 0x0000000000400000
    Property = 8388608, // 0x0000000000800000
    MethodSemantics = 16777216, // 0x0000000001000000
    MethodImpl = 33554432, // 0x0000000002000000
    ModuleRef = 67108864, // 0x0000000004000000
    TypeSpec = 134217728, // 0x0000000008000000
    ImplMap = 268435456, // 0x0000000010000000
    FieldRva = 536870912, // 0x0000000020000000
    EnCLog = 1073741824, // 0x0000000040000000
    EnCMap = 2147483648, // 0x0000000080000000
    Assembly = 4294967296, // 0x0000000100000000
    AssemblyRef = 34359738368, // 0x0000000800000000
    File = 274877906944, // 0x0000004000000000
    ExportedType = 549755813888, // 0x0000008000000000
    ManifestResource = 1099511627776, // 0x0000010000000000
    NestedClass = 2199023255552, // 0x0000020000000000
    GenericParam = 4398046511104, // 0x0000040000000000
    MethodSpec = 8796093022208, // 0x0000080000000000
    GenericParamConstraint = 17592186044416, // 0x0000100000000000
    Document = 281474976710656, // 0x0001000000000000
    MethodDebugInformation = 562949953421312, // 0x0002000000000000
    LocalScope = 1125899906842624, // 0x0004000000000000
    LocalVariable = 2251799813685248, // 0x0008000000000000
    LocalConstant = 4503599627370496, // 0x0010000000000000
    ImportScope = 9007199254740992, // 0x0020000000000000
    StateMachineMethod = 18014398509481984, // 0x0040000000000000
    CustomDebugInformation = 36028797018963968, // 0x0080000000000000
    PtrTables = PropertyPtr | EventPtr | ParamPtr | MethodPtr | FieldPtr, // 0x00000000004800A8
    EncTables = EnCMap | EnCLog, // 0x00000000C0000000
    TypeSystemTables = EncTables | PtrTables | GenericParamConstraint | MethodSpec | GenericParam | NestedClass | ManifestResource | ExportedType | File | AssemblyRef | Assembly | FieldRva | ImplMap | TypeSpec | ModuleRef | MethodImpl | MethodSemantics | Property | PropertyMap | Event | EventMap | StandAloneSig | FieldLayout | ClassLayout | DeclSecurity | FieldMarshal | CustomAttribute | Constant | MemberRef | InterfaceImpl | Param | MethodDef | Field | TypeDef | TypeRef | Module, // 0x00001FC9FFFFFFFF
    DebugTables = CustomDebugInformation | StateMachineMethod | ImportScope | LocalConstant | LocalVariable | LocalScope | MethodDebugInformation | Document, // 0x00FF000000000000
    AllTables = DebugTables | TypeSystemTables, // 0x00FF1FC9FFFFFFFF
    ValidPortablePdbExternalTables = GenericParamConstraint | MethodSpec | GenericParam | NestedClass | ManifestResource | ExportedType | File | AssemblyRef | Assembly | FieldRva | ImplMap | TypeSpec | ModuleRef | MethodImpl | MethodSemantics | Property | PropertyMap | Event | EventMap | StandAloneSig | FieldLayout | ClassLayout | DeclSecurity | FieldMarshal | CustomAttribute | Constant | MemberRef | InterfaceImpl | Param | MethodDef | Field | TypeDef | TypeRef | Module, // 0x00001FC93FB7FF57
  }
}
