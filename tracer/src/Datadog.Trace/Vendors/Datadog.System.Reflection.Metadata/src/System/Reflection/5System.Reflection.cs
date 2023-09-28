// Decompiled with JetBrains decompiler
// Type: System.Reflection.AssemblyFlags
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;

namespace Datadog.System.Reflection
{
  [Flags]
  public enum AssemblyFlags
  {
    /// <summary>
    /// The assembly reference holds the full (unhashed) public key.
    /// Not applicable on assembly definition.
    /// </summary>
    PublicKey = 1,
    /// <summary>
    /// The implementation of the referenced assembly used at runtime is not expected to match the version seen at compile time.
    /// </summary>
    Retargetable = 256, // 0x00000100
    /// <summary>The assembly contains Windows Runtime code.</summary>
    WindowsRuntime = 512, // 0x00000200
    /// <summary>
    /// Content type mask. Masked bits correspond to values of <see cref="T:System.Reflection.AssemblyContentType" />.
    /// </summary>
    ContentTypeMask = 3584, // 0x00000E00
    /// <summary>
    /// Specifies that just-in-time (JIT) compiler optimization is disabled for the assembly.
    /// </summary>
    DisableJitCompileOptimizer = 16384, // 0x00004000
    /// <summary>
    /// Specifies that just-in-time (JIT) compiler tracking is enabled for the assembly.
    /// </summary>
    EnableJitCompileTracking = 32768, // 0x00008000
  }
}
