// Decompiled with JetBrains decompiler
// Type: System.Reflection.MethodSemanticsAttributes
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;

namespace Datadog.System.Reflection
{
  [Flags]
  public enum MethodSemanticsAttributes
  {
    /// <summary>
    /// Used to modify the value of the property.
    /// CLS-compliant setters are named with set_ prefix.
    /// </summary>
    Setter = 1,
    /// <summary>
    /// Used to read the value of the property.
    /// CLS-compliant getters are named with get_ prefix.
    /// </summary>
    Getter = 2,
    /// <summary>
    /// Other method for property (not getter or setter) or event (not adder, remover, or raiser).
    /// </summary>
    Other = 4,
    /// <summary>
    /// Used to add a handler for an event.
    /// Corresponds to the AddOn flag in the Ecma 335 CLI specification.
    /// CLS-compliant adders are named with add_ prefix.
    /// </summary>
    Adder = 8,
    /// <summary>
    /// Used to remove a handler for an event.
    /// Corresponds to the RemoveOn flag in the Ecma 335 CLI specification.
    /// CLS-compliant removers are named with remove_ prefix.
    /// </summary>
    Remover = 16, // 0x00000010
    /// <summary>
    /// Used to indicate that an event has occurred.
    /// Corresponds to the Fire flag in the Ecma 335 CLI specification.
    /// CLS-compliant raisers are named with raise_ prefix.
    /// </summary>
    Raiser = 32, // 0x00000020
  }
}
