// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.EditAndContinueLogEntry
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using Datadog.System.Diagnostics.CodeAnalysis;


#nullable enable
namespace Datadog.System.Reflection.Metadata.Ecma335
{
  public readonly struct EditAndContinueLogEntry : IEquatable<EditAndContinueLogEntry>
  {
    public EntityHandle Handle { get; }

    public EditAndContinueOperation Operation { get; }

    public EditAndContinueLogEntry(EntityHandle handle, EditAndContinueOperation operation)
    {
      this.Handle = handle;
      this.Operation = operation;
    }

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is EditAndContinueLogEntry other && this.Equals(other);

    public bool Equals(EditAndContinueLogEntry other) => this.Operation == other.Operation && this.Handle == other.Handle;

    public override int GetHashCode() => (int) (this.Operation ^ (EditAndContinueOperation) this.Handle.GetHashCode());
  }
}
