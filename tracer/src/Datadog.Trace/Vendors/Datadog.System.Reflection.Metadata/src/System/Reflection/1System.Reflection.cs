// Decompiled with JetBrains decompiler
// Type: System.Reflection.DeclarativeSecurityAction
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

namespace Datadog.System.Reflection
{
  /// <summary>
  /// Specifies the security actions that can be performed using declarative security.
  /// </summary>
  public enum DeclarativeSecurityAction : short
  {
    /// <summary>No declarative security action.</summary>
    None = 0,
    /// <summary>
    /// Check that all callers in the call chain have been granted specified permission,
    /// </summary>
    Demand = 2,
    /// <summary>
    /// The calling code can access the resource identified by the current permission object, even if callers higher in the stack have not been granted permission to access the resource.
    /// </summary>
    Assert = 3,
    /// <summary>
    /// Without further checks refuse Demand for the specified permission.
    /// </summary>
    Deny = 4,
    /// <summary>
    /// Without further checks, refuse Demand for all permissions other than those specified.
    /// </summary>
    PermitOnly = 5,
    /// <summary>
    /// Check that the immediate caller has been granted the specified permission;
    /// </summary>
    LinkDemand = 6,
    /// <summary>
    /// The derived class inheriting the class or overriding a method is required to have been granted the specified permission.
    /// </summary>
    InheritanceDemand = 7,
    /// <summary>
    /// The request for the minimum permissions required for code to run. This action can only be used within the scope of the assembly.
    /// </summary>
    RequestMinimum = 8,
    /// <summary>
    /// The request for additional permissions that are optional (not required to run). This request implicitly refuses all other permissions not specifically requested. This action can only be used within the scope of the assembly.
    /// </summary>
    RequestOptional = 9,
    /// <summary>
    /// The request that permissions that might be misused will not be granted to the calling code. This action can only be used within the scope of the assembly.
    /// </summary>
    RequestRefuse = 10, // 0x000A
  }
}
