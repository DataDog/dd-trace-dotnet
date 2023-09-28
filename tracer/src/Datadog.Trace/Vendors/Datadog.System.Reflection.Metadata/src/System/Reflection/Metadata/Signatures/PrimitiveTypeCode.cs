// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.PrimitiveTypeCode
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

namespace Datadog.System.Reflection.Metadata
{
  /// <summary>
  /// Represents a primitive type found in metadata signatures.
  /// </summary>
  public enum PrimitiveTypeCode : byte
  {
    Void = 1,
    Boolean = 2,
    Char = 3,
    SByte = 4,
    Byte = 5,
    Int16 = 6,
    UInt16 = 7,
    Int32 = 8,
    UInt32 = 9,
    Int64 = 10, // 0x0A
    UInt64 = 11, // 0x0B
    Single = 12, // 0x0C
    Double = 13, // 0x0D
    String = 14, // 0x0E
    TypedReference = 22, // 0x16
    IntPtr = 24, // 0x18
    UIntPtr = 25, // 0x19
    Object = 28, // 0x1C
  }
}
