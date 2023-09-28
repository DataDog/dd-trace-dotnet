// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.EntityHandle
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using Datadog.System.Diagnostics.CodeAnalysis;


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
  /// <summary>
  /// Represents a metadata entity (type reference/definition/specification, method definition, custom attribute, etc.).
  /// </summary>
  /// <remarks>
  /// Use <see cref="T:System.Reflection.Metadata.EntityHandle" /> to store multiple kinds of entity handles.
  /// It has smaller memory footprint than <see cref="T:System.Reflection.Metadata.Handle" />.
  /// </remarks>
  public readonly struct EntityHandle : IEquatable<EntityHandle>
  {
    private readonly uint _vToken;
    public static readonly ModuleDefinitionHandle ModuleDefinition = new ModuleDefinitionHandle(1);
    public static readonly AssemblyDefinitionHandle AssemblyDefinition = new AssemblyDefinitionHandle(1);

    internal EntityHandle(uint vToken) => this._vToken = vToken;

    public static implicit operator Handle(EntityHandle handle) => Handle.FromVToken(handle._vToken);

    public static explicit operator EntityHandle(Handle handle)
    {
      if (handle.IsHeapHandle)
        Throw.InvalidCast();
      return new EntityHandle(handle.EntityHandleValue);
    }

    internal uint Type => this._vToken & 2130706432U;

    internal uint VType => this._vToken & 4278190080U;

    internal bool IsVirtual => (this._vToken & 2147483648U) > 0U;

    public bool IsNil => ((int) this._vToken & -2130706433) == 0;

    internal int RowId => (int) this._vToken & 16777215;

    /// <summary>
    /// Value stored in a specific entity handle (see <see cref="T:System.Reflection.Metadata.TypeDefinitionHandle" />, <see cref="T:System.Reflection.Metadata.MethodDefinitionHandle" />, etc.).
    /// </summary>
    internal uint SpecificHandleValue => this._vToken & 2164260863U;

    public HandleKind Kind => (HandleKind) (this.Type >> 24);

    internal int Token => (int) this._vToken;

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is EntityHandle other && this.Equals(other);

    public bool Equals(EntityHandle other) => (int) this._vToken == (int) other._vToken;

    public override int GetHashCode() => (int) this._vToken;

    public static bool operator ==(EntityHandle left, EntityHandle right) => left.Equals(right);

    public static bool operator !=(EntityHandle left, EntityHandle right) => !left.Equals(right);

    internal static int Compare(EntityHandle left, EntityHandle right) => left._vToken.CompareTo(right._vToken);
  }
}
