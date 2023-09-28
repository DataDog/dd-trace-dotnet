// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.LabelHandle
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using Datadog.System.Diagnostics.CodeAnalysis;


#nullable enable
namespace Datadog.System.Reflection.Metadata.Ecma335
{
  public readonly struct LabelHandle : IEquatable<LabelHandle>
  {
    /// <summary>
    /// 1-based id identifying the label within the context of a <see cref="T:System.Reflection.Metadata.Ecma335.ControlFlowBuilder" />.
    /// </summary>
    public int Id { get; }

    internal LabelHandle(int id) => this.Id = id;

    public bool IsNil => this.Id == 0;

    public bool Equals(LabelHandle other) => this.Id == other.Id;

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is LabelHandle other && this.Equals(other);

    public override int GetHashCode() => this.Id.GetHashCode();

    public static bool operator ==(LabelHandle left, LabelHandle right) => left.Equals(right);

    public static bool operator !=(LabelHandle left, LabelHandle right) => !left.Equals(right);
  }
}
