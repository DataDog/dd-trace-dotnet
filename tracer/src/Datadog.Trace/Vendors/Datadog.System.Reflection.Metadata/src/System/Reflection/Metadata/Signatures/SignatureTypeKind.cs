// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.SignatureTypeKind
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

namespace Datadog.System.Reflection.Metadata
{
  public enum SignatureTypeKind : byte
  {
    /// <summary>
    /// It is not known in the current context if the type reference or definition is a class or value type.
    /// </summary>
    Unknown = 0,
    /// <summary>
    /// The type definition or reference refers to a value type.
    /// </summary>
    ValueType = 17, // 0x11
    /// <summary>The type definition or reference refers to a class.</summary>
    Class = 18, // 0x12
  }
}
