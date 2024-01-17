﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.HandleType
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  /// <summary>
  /// These constants are all in the byte range and apply to the interpretation of <see cref="P:System.Reflection.Metadata.Handle.VType" />,
  /// </summary>
  internal static class HandleType
  {
    internal const uint Module = 0;
    internal const uint TypeRef = 1;
    internal const uint TypeDef = 2;
    internal const uint FieldDef = 4;
    internal const uint MethodDef = 6;
    internal const uint ParamDef = 8;
    internal const uint InterfaceImpl = 9;
    internal const uint MemberRef = 10;
    internal const uint Constant = 11;
    internal const uint CustomAttribute = 12;
    internal const uint DeclSecurity = 14;
    internal const uint Signature = 17;
    internal const uint EventMap = 18;
    internal const uint Event = 20;
    internal const uint PropertyMap = 21;
    internal const uint Property = 23;
    internal const uint MethodSemantics = 24;
    internal const uint MethodImpl = 25;
    internal const uint ModuleRef = 26;
    internal const uint TypeSpec = 27;
    internal const uint Assembly = 32;
    internal const uint AssemblyRef = 35;
    internal const uint File = 38;
    internal const uint ExportedType = 39;
    internal const uint ManifestResource = 40;
    internal const uint NestedClass = 41;
    internal const uint GenericParam = 42;
    internal const uint MethodSpec = 43;
    internal const uint GenericParamConstraint = 44;
    internal const uint Document = 48;
    internal const uint MethodDebugInformation = 49;
    internal const uint LocalScope = 50;
    internal const uint LocalVariable = 51;
    internal const uint LocalConstant = 52;
    internal const uint ImportScope = 53;
    internal const uint AsyncMethod = 54;
    internal const uint CustomDebugInformation = 55;
    internal const uint UserString = 112;
    internal const uint Blob = 113;
    internal const uint Guid = 114;
    internal const uint String = 120;
    internal const uint String1 = 121;
    internal const uint String2 = 122;
    internal const uint String3 = 123;
    internal const uint Namespace = 124;
    internal const uint HeapMask = 112;
    internal const uint TypeMask = 127;
    /// <summary>
    /// Use the highest bit to mark tokens that are virtual (synthesized).
    /// We create virtual tokens to represent projected WinMD entities.
    /// </summary>
    internal const uint VirtualBit = 128;
    /// <summary>
    /// In the case of string handles, the two lower bits that (in addition to the
    /// virtual bit not included in this mask) encode how to obtain the string value.
    /// </summary>
    internal const uint NonVirtualStringTypeMask = 3;
  }
}
