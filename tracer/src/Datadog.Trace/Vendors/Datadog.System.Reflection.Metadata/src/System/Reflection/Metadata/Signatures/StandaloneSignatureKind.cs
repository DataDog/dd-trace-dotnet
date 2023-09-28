// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.StandaloneSignatureKind
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

namespace Datadog.System.Reflection.Metadata
{
  /// <summary>
  /// Indicates whether a <see cref="T:System.Reflection.Metadata.StandaloneSignature" /> represents a standalone method or local variable signature.
  /// </summary>
  public enum StandaloneSignatureKind
  {
    /// <summary>
    /// The <see cref="T:System.Reflection.Metadata.StandaloneSignature" /> represents a standalone method signature.
    /// </summary>
    Method,
    /// <summary>
    /// The <see cref="T:System.Reflection.Metadata.MemberReference" /> references a local variable signature.
    /// </summary>
    LocalVariables,
  }
}
