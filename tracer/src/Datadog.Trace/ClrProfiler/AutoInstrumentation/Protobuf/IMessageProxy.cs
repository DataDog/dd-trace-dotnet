// <copyright file="IMessageProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Protobuf;

/// <summary>
/// DuckTyping interface for Google.Protobuf.IMessage
/// </summary>
[DuckType("Google.Protobuf.IMessage", "Google.Protobuf", IncludeDerivedTypes = true)]
internal interface IMessageProxy : IDuckType
{
    /// <summary>
    /// Gets the descriptor.
    /// </summary>
    [Duck(ExplicitInterfaceTypeName = "Google.Protobuf.IMessage")]
    MessageDescriptorProxy Descriptor { get; }
}

/// <summary>
/// DuckTyping interface for Google.Protobuf.Reflection.MessageDescriptor/FieldCollection
/// </summary>
[DuckType("Google.Protobuf.Reflection.MessageDescriptor+FieldCollection", "Google.Protobuf")]
internal interface IFieldCollectionProxy : IDuckType
{
    IList InDeclarationOrder(); // <IFieldDescriptorProxy>

    IList InFieldNumberOrder(); // <IFieldDescriptorProxy>
}

/// <summary>
/// DuckTyping interface for Google.Protobuf.Reflection.FieldDescriptor
/// </summary>
[DuckType("Google.Protobuf.Reflection.FieldDescriptor", "Google.Protobuf")]
internal interface IFieldDescriptorProxy
{
    string Name { get; }

    bool IsRepeated { get; }

    ProtobufDotnetFieldType FieldType { get; }

    int FieldNumber { get; }

    EnumDescriptorProxy EnumType { get; } // will throw if called on a field that is not an enum

    MessageDescriptorProxy MessageType { get; }
}

/// <summary>
/// DuckTyping interface for Google.Protobuf.Reflection.MessageDescriptor
/// </summary>
[DuckCopy]
[DuckCopy("Google.Protobuf.Reflection.MessageDescriptor", "Google.Protobuf")]
internal struct MessageDescriptorProxy
{
    public string Name;
    public string FullName;
    public IFieldCollectionProxy Fields;

    public IDescriptorProxy File;
}

[DuckCopy]
[DuckCopy("Google.Protobuf.Reflection.EnumValueDescriptor", "Google.Protobuf")]
[DuckCopy("Google.Protobuf.Reflection.FileDescriptor", "Google.Protobuf")]
internal struct IDescriptorProxy
{
    public string Name;
}

/// <summary>
/// DuckTyping interface for Google.Protobuf.Reflection.EnumDescriptor
/// </summary>
[DuckCopy]
[DuckCopy("Google.Protobuf.Reflection.EnumDescriptor", "Google.Protobuf")]
internal struct EnumDescriptorProxy
{
    public string Name;
    public IList Values; // <EnumValueDescriptor>
}
