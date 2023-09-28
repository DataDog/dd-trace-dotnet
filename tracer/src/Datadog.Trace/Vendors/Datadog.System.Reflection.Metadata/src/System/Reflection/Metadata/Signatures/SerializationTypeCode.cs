// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.SerializationTypeCode
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

namespace Datadog.System.Reflection.Metadata
{
  /// <summary>
  /// Type codes used to encode types of values in Custom Attribute value blob.
  /// </summary>
  public enum SerializationTypeCode : byte
  {
    /// <summary>
    /// Equivalent to <see cref="F:System.Reflection.Metadata.SignatureTypeCode.Invalid" />.
    /// </summary>
    Invalid = 0,
    /// <summary>
    /// Equivalent to <see cref="F:System.Reflection.Metadata.SignatureTypeCode.Boolean" />.
    /// </summary>
    Boolean = 2,
    /// <summary>
    /// Equivalent to <see cref="F:System.Reflection.Metadata.SignatureTypeCode.Char" />.
    /// </summary>
    Char = 3,
    /// <summary>
    /// Equivalent to <see cref="F:System.Reflection.Metadata.SignatureTypeCode.SByte" />.
    /// </summary>
    SByte = 4,
    /// <summary>
    /// Equivalent to <see cref="F:System.Reflection.Metadata.SignatureTypeCode.Byte" />.
    /// </summary>
    Byte = 5,
    /// <summary>
    /// Equivalent to <see cref="F:System.Reflection.Metadata.SignatureTypeCode.Int16" />.
    /// </summary>
    Int16 = 6,
    /// <summary>
    /// Equivalent to <see cref="F:System.Reflection.Metadata.SignatureTypeCode.UInt16" />.
    /// </summary>
    UInt16 = 7,
    /// <summary>
    /// Equivalent to <see cref="F:System.Reflection.Metadata.SignatureTypeCode.Int32" />.
    /// </summary>
    Int32 = 8,
    /// <summary>
    /// Equivalent to <see cref="F:System.Reflection.Metadata.SignatureTypeCode.UInt32" />.
    /// </summary>
    UInt32 = 9,
    /// <summary>
    /// Equivalent to <see cref="F:System.Reflection.Metadata.SignatureTypeCode.Int64" />.
    /// </summary>
    Int64 = 10, // 0x0A
    /// <summary>
    /// Equivalent to <see cref="F:System.Reflection.Metadata.SignatureTypeCode.UInt64" />.
    /// </summary>
    UInt64 = 11, // 0x0B
    /// <summary>
    /// Equivalent to <see cref="F:System.Reflection.Metadata.SignatureTypeCode.Single" />.
    /// </summary>
    Single = 12, // 0x0C
    /// <summary>
    /// Equivalent to <see cref="F:System.Reflection.Metadata.SignatureTypeCode.Double" />.
    /// </summary>
    Double = 13, // 0x0D
    /// <summary>
    /// Equivalent to <see cref="F:System.Reflection.Metadata.SignatureTypeCode.String" />.
    /// </summary>
    String = 14, // 0x0E
    /// <summary>
    /// Equivalent to <see cref="F:System.Reflection.Metadata.SignatureTypeCode.SZArray" />.
    /// </summary>
    SZArray = 29, // 0x1D
    /// <summary>The attribute argument is a System.Type instance.</summary>
    Type = 80, // 0x50
    /// <summary>
    /// The attribute argument is "boxed" (passed to a parameter, field, or property of type object) and carries type information in the attribute blob.
    /// </summary>
    TaggedObject = 81, // 0x51
    /// <summary>The attribute argument is an Enum instance.</summary>
    Enum = 85, // 0x55
  }
}
