// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.HandleKind
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

namespace Datadog.System.Reflection.Metadata
{
  public enum HandleKind : byte
  {
    ModuleDefinition = 0,
    TypeReference = 1,
    TypeDefinition = 2,
    FieldDefinition = 4,
    MethodDefinition = 6,
    Parameter = 8,
    InterfaceImplementation = 9,
    MemberReference = 10, // 0x0A
    Constant = 11, // 0x0B
    CustomAttribute = 12, // 0x0C
    DeclarativeSecurityAttribute = 14, // 0x0E
    StandaloneSignature = 17, // 0x11
    EventDefinition = 20, // 0x14
    PropertyDefinition = 23, // 0x17
    MethodImplementation = 25, // 0x19
    ModuleReference = 26, // 0x1A
    TypeSpecification = 27, // 0x1B
    AssemblyDefinition = 32, // 0x20
    AssemblyReference = 35, // 0x23
    AssemblyFile = 38, // 0x26
    ExportedType = 39, // 0x27
    ManifestResource = 40, // 0x28
    GenericParameter = 42, // 0x2A
    MethodSpecification = 43, // 0x2B
    GenericParameterConstraint = 44, // 0x2C
    Document = 48, // 0x30
    MethodDebugInformation = 49, // 0x31
    LocalScope = 50, // 0x32
    LocalVariable = 51, // 0x33
    LocalConstant = 52, // 0x34
    ImportScope = 53, // 0x35
    CustomDebugInformation = 55, // 0x37
    UserString = 112, // 0x70
    Blob = 113, // 0x71
    Guid = 114, // 0x72
    String = 120, // 0x78
    NamespaceDefinition = 124, // 0x7C
  }
}
