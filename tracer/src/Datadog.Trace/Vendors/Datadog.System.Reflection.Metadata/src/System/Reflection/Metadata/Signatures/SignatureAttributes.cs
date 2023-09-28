// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.SignatureAttributes
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;

namespace Datadog.System.Reflection.Metadata
{
  /// <summary>
  /// Specified additional flags that can be applied to method signatures.
  /// Underlying values correspond to the representation in the leading signature
  /// byte represented by <see cref="T:System.Reflection.Metadata.SignatureHeader" />.
  /// </summary>
  [Flags]
  public enum SignatureAttributes : byte
  {
    /// <summary>No flags.</summary>
    None = 0,
    /// <summary>Generic method.</summary>
    Generic = 16, // 0x10
    /// <summary>Instance method.</summary>
    /// <remarks>Ecma 335 CLI Specification refers to this flag as HAS_THIS.</remarks>
    Instance = 32, // 0x20
    /// <summary>
    /// The first explicitly declared parameter represents the instance pointer.
    /// </summary>
    ExplicitThis = 64, // 0x40
  }
}
