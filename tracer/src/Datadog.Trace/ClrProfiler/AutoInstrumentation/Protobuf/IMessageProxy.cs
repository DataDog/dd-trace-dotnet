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
internal interface IMessageProxy : IDuckType
{
    /// <summary>
    /// Gets the descriptor.
    /// Accessing this property can generate a nullref in some cases,
    /// use <see cref="Helper.TryGetDescriptor"/> to avoid that.
    /// </summary>
    [Duck(ExplicitInterfaceTypeName = "Google.Protobuf.IMessage")]
    MessageDescriptorProxy Descriptor { get; }
}

/// <summary>
/// DuckTyping interface for Google.Protobuf.Reflection.MessageDescriptor/FieldCollection
/// </summary>
internal interface IFieldCollectionProxy : IDuckType
{
    IList InDeclarationOrder(); // <IFieldDescriptorProxy>

    IList InFieldNumberOrder(); // <IFieldDescriptorProxy>
}

/// <summary>
/// DuckTyping interface for Google.Protobuf.Reflection.FieldDescriptor
/// </summary>
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
internal struct MessageDescriptorProxy
{
    public string Name;
    public string FullName;
    public IFieldCollectionProxy Fields;

    public IDescriptorProxy File;
}

[DuckCopy]
internal struct IDescriptorProxy
{
    public string Name;
}

/// <summary>
/// DuckTyping interface for Google.Protobuf.Reflection.EnumDescriptor
/// </summary>
[DuckCopy]
internal struct EnumDescriptorProxy
{
    public string Name;
    public IList Values; // <EnumValueDescriptor>
}
