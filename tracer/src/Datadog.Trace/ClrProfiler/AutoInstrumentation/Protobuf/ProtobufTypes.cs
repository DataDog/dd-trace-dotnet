// <copyright file="ProtobufTypes.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Protobuf;

/// <summary>
/// This represents protobuf types when they are handled by the C# code.
/// copy-pasted from https://github.com/protocolbuffers/protobuf/blob/main/csharp/src/Google.Protobuf/Reflection/FieldType.cs
/// </summary>
internal enum ProtobufDotnetFieldType
{
    Double,
    Float,
    Int64,
    UInt64,
    Int32,
    Fixed64,
    Fixed32,
    Bool,
    String,
    Group,
    Message,
    Bytes,
    UInt32,
    SFixed32,
    SFixed64,
    SInt32,
    SInt64,
    Enum
}

/// <summary>
/// This is the definition of types common to all languages.
/// copy pasted from https://github.com/protocolbuffers/protobuf/blob/78db3094a46e7a8cc34d207808b8008997b5fc82/csharp/src/Google.Protobuf/Reflection/Descriptor.pb.cs#L3962
/// </summary>
internal enum ProtobufDotnetProtoType
{
    Double = 1,
    Float = 2,
    Int64 = 3,
    Uint64 = 4,
    Int32 = 5,
    Fixed64 = 6,
    Fixed32 = 7,
    Bool = 8,
    String = 9,
    Group = 10,
    Message = 11,
    Bytes = 12,
    Uint32 = 13,
    Enum = 14,
    Sfixed32 = 15,
    Sfixed64 = 16,
    Sint32 = 17,
    Sint64 = 18,
    Unknown = 999 // this one I added myself, to avoid using exceptions
}

/// <summary> Just a class to host the extension method. Must match the name of the file. </summary>
internal static class ProtobufTypes
{
    /// <summary>
    /// Converts from the dotnet-specific enum to the common enum.
    /// this is the opposite of this function https://github.com/protocolbuffers/protobuf/blob/78db3094a46e7a8cc34d207808b8008997b5fc82/csharp/src/Google.Protobuf/Reflection/FieldDescriptor.cs#L206
    /// </summary>
    public static ProtobufDotnetProtoType ToProtoType(this ProtobufDotnetFieldType type)
    {
        return type switch
        {
            ProtobufDotnetFieldType.Double => ProtobufDotnetProtoType.Double,
            ProtobufDotnetFieldType.Float => ProtobufDotnetProtoType.Float,
            ProtobufDotnetFieldType.Int64 => ProtobufDotnetProtoType.Int64,
            ProtobufDotnetFieldType.UInt64 => ProtobufDotnetProtoType.Uint64,
            ProtobufDotnetFieldType.Int32 => ProtobufDotnetProtoType.Int32,
            ProtobufDotnetFieldType.Fixed64 => ProtobufDotnetProtoType.Fixed64,
            ProtobufDotnetFieldType.Fixed32 => ProtobufDotnetProtoType.Fixed32,
            ProtobufDotnetFieldType.Bool => ProtobufDotnetProtoType.Bool,
            ProtobufDotnetFieldType.String => ProtobufDotnetProtoType.String,
            ProtobufDotnetFieldType.Group => ProtobufDotnetProtoType.Group,
            ProtobufDotnetFieldType.Message => ProtobufDotnetProtoType.Message,
            ProtobufDotnetFieldType.Bytes => ProtobufDotnetProtoType.Bytes,
            ProtobufDotnetFieldType.UInt32 => ProtobufDotnetProtoType.Uint32,
            ProtobufDotnetFieldType.Enum => ProtobufDotnetProtoType.Enum,
            ProtobufDotnetFieldType.SFixed32 => ProtobufDotnetProtoType.Sfixed32,
            ProtobufDotnetFieldType.SFixed64 => ProtobufDotnetProtoType.Sfixed64,
            ProtobufDotnetFieldType.SInt32 => ProtobufDotnetProtoType.Sint32,
            ProtobufDotnetFieldType.SInt64 => ProtobufDotnetProtoType.Sint64,
            _ => ProtobufDotnetProtoType.Unknown
        };
    }
}
