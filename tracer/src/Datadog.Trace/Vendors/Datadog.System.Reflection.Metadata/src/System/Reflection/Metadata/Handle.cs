// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Handle
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
  /// Represents any metadata entity (type reference/definition/specification, method definition, custom attribute, etc.) or value (string, blob, guid, user string).
  /// </summary>
  /// <remarks>
  /// Use <see cref="T:System.Reflection.Metadata.Handle" /> to store multiple kinds of handles.
  /// </remarks>
  public readonly struct Handle : IEquatable<Handle>
  {
    private readonly int _value;
    private readonly byte _vType;
    public static readonly ModuleDefinitionHandle ModuleDefinition = new ModuleDefinitionHandle(1);
    public static readonly AssemblyDefinitionHandle AssemblyDefinition = new AssemblyDefinitionHandle(1);

    /// <summary>
    /// Creates <see cref="T:System.Reflection.Metadata.Handle" /> from a token or a token combined with a virtual flag.
    /// </summary>
    internal static Handle FromVToken(uint vToken) => new Handle((byte) (vToken >> 24), (int) vToken & 16777215);

    internal Handle(byte vType, int value)
    {
      this._vType = vType;
      this._value = value;
    }

    internal int RowId => this._value;

    internal int Offset => this._value;

    /// <summary>
    /// Token type (0x##000000), does not include virtual flag.
    /// </summary>
    internal uint EntityHandleType => this.Type << 24;

    /// <summary>
    /// Small token type (0x##), does not include virtual flag.
    /// </summary>
    internal uint Type => (uint) this._vType & (uint) sbyte.MaxValue;

    /// <summary>
    /// Value stored in an <see cref="T:System.Reflection.Metadata.EntityHandle" />.
    /// </summary>
    internal uint EntityHandleValue => (uint) ((int) this._vType << 24 | this._value);

    /// <summary>
    /// Value stored in a concrete entity handle (see <see cref="T:System.Reflection.Metadata.TypeDefinitionHandle" />, <see cref="T:System.Reflection.Metadata.MethodDefinitionHandle" />, etc.).
    /// </summary>
    internal uint SpecificEntityHandleValue => (uint) (((int) this._vType & 128) << 24 | this._value);

    internal byte VType => this._vType;

    internal bool IsVirtual => ((uint) this._vType & 128U) > 0U;

    internal bool IsHeapHandle => ((int) this._vType & 112) == 112;

    public HandleKind Kind
    {
      get
      {
        uint type = this.Type;
        return ((int) type & -4) == 120 ? HandleKind.String : (HandleKind) type;
      }
    }

    public bool IsNil => (this._value | (int) this._vType & 128) == 0;

    internal bool IsEntityOrUserStringHandle => this.Type <= 112U;

    internal int Token => (int) this._vType << 24 | this._value;

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is Handle other && this.Equals(other);

    public bool Equals(Handle other) => this._value == other._value && (int) this._vType == (int) other._vType;

    public override int GetHashCode() => this._value ^ (int) this._vType << 24;

    public static bool operator ==(Handle left, Handle right) => left.Equals(right);

    public static bool operator !=(Handle left, Handle right) => !left.Equals(right);

    internal static int Compare(Handle left, Handle right) => ((long) (uint) left._value | (long) left._vType << 32).CompareTo((long) (uint) right._value | (long) right._vType << 32);
  }
}
