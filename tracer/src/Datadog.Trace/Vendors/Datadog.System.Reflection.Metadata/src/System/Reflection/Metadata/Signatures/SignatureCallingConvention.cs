// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.SignatureCallingConvention
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

namespace Datadog.System.Reflection.Metadata
{
  /// <summary>
  /// Specifies how arguments in a given signature are passed from the caller to the callee.
  /// Underlying values correspond to the representation in the leading signature byte
  /// represented by <see cref="T:System.Reflection.Metadata.SignatureHeader" />.
  /// </summary>
  public enum SignatureCallingConvention : byte
  {
    /// <summary>
    /// Managed calling convention with fixed-length argument list.
    /// </summary>
    Default = 0,
    /// <summary>
    /// Unmanaged C/C++-style calling convention where the call stack is cleaned by the caller.
    /// </summary>
    CDecl = 1,
    /// <summary>
    /// Unmanaged calling convention where call stack is cleaned up by the callee.
    /// </summary>
    StdCall = 2,
    /// <summary>
    /// Unmanaged C++-style calling convention for calling instance member functions with a fixed argument list.
    /// </summary>
    ThisCall = 3,
    /// <summary>
    /// Unmanaged calling convention where arguments are passed in registers when possible.
    /// </summary>
    FastCall = 4,
    /// <summary>
    /// Managed calling convention for passing extra arguments.
    /// </summary>
    VarArgs = 5,
    /// <summary>
    /// Indicating the specifics of the unmanaged calling convention are encoded as modopts.
    /// </summary>
    Unmanaged = 9,
  }
}
