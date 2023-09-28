// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.MethodBodyAttributes
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  /// <summary>Method body attributes.</summary>
  [Flags]
  public enum MethodBodyAttributes
  {
    /// <summary>No local memory initialization is performed.</summary>
    None = 0,
    /// <summary>
    /// Zero-initialize any locals the method defines and dynamically allocated local memory.
    /// </summary>
    InitLocals = 1,
  }
}
