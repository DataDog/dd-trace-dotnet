// Decompiled with JetBrains decompiler
// Type: System.Diagnostics.CodeAnalysis.MemberNotNullWhenAttribute
// Assembly: System.Collections.Immutable, Version=7.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 5F9FF90F-0D16-4469-A104-76829D3705E2
// Assembly location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.dll
// XML documentation location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.xml


#nullable enable
using System;

namespace Datadog.System.Diagnostics.CodeAnalysis
{
  [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
  internal sealed class MemberNotNullWhenAttribute : Attribute
  {
    public MemberNotNullWhenAttribute(bool returnValue, string member)
    {
      this.ReturnValue = returnValue;
      this.Members = new string[1]{ member };
    }

    public MemberNotNullWhenAttribute(bool returnValue, params string[] members)
    {
      this.ReturnValue = returnValue;
      this.Members = members;
    }

    public bool ReturnValue { get; }

    public string[] Members { get; }
  }
}
