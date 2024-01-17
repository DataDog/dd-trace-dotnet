﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.SignatureTypeCode
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

namespace Datadog.System.Reflection.Metadata
{
  /// <summary>
  /// Represents the type codes that are used in signature encoding.
  /// </summary>
  public enum SignatureTypeCode : byte
  {
    /// <summary>
    /// Represents an invalid or uninitialized type code. It will not appear in valid signatures.
    /// </summary>
    Invalid = 0,
    /// <summary>
    /// Represents <see cref="T:System.Void" /> in signatures.
    /// </summary>
    Void = 1,
    /// <summary>
    /// Represents <see cref="T:System.Boolean" /> in signatures.
    /// </summary>
    Boolean = 2,
    /// <summary>
    /// Represents <see cref="T:System.Char" /> in signatures.
    /// </summary>
    Char = 3,
    /// <summary>
    /// Represents <see cref="T:System.SByte" /> in signatures.
    /// </summary>
    SByte = 4,
    /// <summary>
    /// Represents <see cref="T:System.Byte" /> in signatures.
    /// </summary>
    Byte = 5,
    /// <summary>
    /// Represents <see cref="T:System.Int16" /> in signatures.
    /// </summary>
    Int16 = 6,
    /// <summary>
    /// Represents <see cref="T:System.UInt16" /> in signatures.
    /// </summary>
    UInt16 = 7,
    /// <summary>
    /// Represents <see cref="T:System.Int32" /> in signatures.
    /// </summary>
    Int32 = 8,
    /// <summary>
    /// Represents <see cref="T:System.UInt32" /> in signatures.
    /// </summary>
    UInt32 = 9,
    /// <summary>
    /// Represents <see cref="T:System.Int64" /> in signatures.
    /// </summary>
    Int64 = 10, // 0x0A
    /// <summary>
    /// Represents <see cref="T:System.UInt64" /> in signatures.
    /// </summary>
    UInt64 = 11, // 0x0B
    /// <summary>
    /// Represents <see cref="T:System.Single" /> in signatures.
    /// </summary>
    Single = 12, // 0x0C
    /// <summary>
    /// Represents <see cref="T:System.Double" /> in signatures.
    /// </summary>
    Double = 13, // 0x0D
    /// <summary>
    /// Represents <see cref="T:System.String" /> in signatures.
    /// </summary>
    String = 14, // 0x0E
    /// <summary>
    /// Represents a unmanaged pointers in signatures.
    /// It is followed in the blob by the signature encoding of the underlying type.
    /// </summary>
    Pointer = 15, // 0x0F
    /// <summary>
    /// Represents managed pointers (byref return values and parameters) in signatures.
    /// It is followed in the blob by the signature encoding of the underlying type.
    /// </summary>
    ByReference = 16, // 0x10
    /// <summary>
    /// Represents a generic type parameter used within a signature.
    /// </summary>
    GenericTypeParameter = 19, // 0x13
    /// <summary>
    /// Represents a generalized <see cref="T:System.Array" /> in signatures.
    /// </summary>
    Array = 20, // 0x14
    /// <summary>
    /// Represents the instantiation of a generic type in signatures.
    /// </summary>
    GenericTypeInstance = 21, // 0x15
    /// <summary>Represents a System.TypedReference in signatures.</summary>
    TypedReference = 22, // 0x16
    /// <summary>
    /// Represents a <see cref="T:System.IntPtr" /> in signatures.
    /// </summary>
    IntPtr = 24, // 0x18
    /// <summary>
    /// Represents a <see cref="T:System.UIntPtr" /> in signatures.
    /// </summary>
    UIntPtr = 25, // 0x19
    /// <summary>Represents function pointer types in signatures.</summary>
    FunctionPointer = 27, // 0x1B
    /// <summary>
    /// Represents <see cref="T:System.Object" />
    /// </summary>
    Object = 28, // 0x1C
    /// <summary>
    /// Represents a single dimensional <see cref="T:System.Array" /> with 0 lower bound.
    /// </summary>
    SZArray = 29, // 0x1D
    /// <summary>
    /// Represents a generic method parameter used within a signature.
    /// </summary>
    GenericMethodParameter = 30, // 0x1E
    /// <summary>
    /// Represents a custom modifier applied to a type within a signature that the caller must understand.
    /// </summary>
    RequiredModifier = 31, // 0x1F
    /// <summary>
    /// Represents a custom modifier applied to a type within a signature that the caller can ignore.
    /// </summary>
    OptionalModifier = 32, // 0x20
    /// <summary>
    /// Precedes a type <see cref="T:System.Reflection.Metadata.EntityHandle" /> in signatures.
    /// </summary>
    /// <remarks>
    /// In raw metadata, this will be encoded as either ELEMENT_TYPE_CLASS (0x12) for reference
    /// types and ELEMENT_TYPE_VALUETYPE (0x11) for value types. This is collapsed to a single
    /// code because Windows Runtime projections can project from class to value type or vice-versa
    /// and the raw code is misleading in those cases.
    /// </remarks>
    TypeHandle = 64, // 0x40
    /// <summary>
    /// Represents a marker to indicate the end of fixed arguments and the beginning of variable arguments.
    /// </summary>
    Sentinel = 65, // 0x41
    /// <summary>
    /// Represents a local variable that is pinned by garbage collector
    /// </summary>
    Pinned = 69, // 0x45
  }
}
