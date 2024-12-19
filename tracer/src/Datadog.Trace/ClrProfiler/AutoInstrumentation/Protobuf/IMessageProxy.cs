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
    [Duck(ExplicitInterfaceTypeName = "Google.Protobuf.IMessage")]
    IMessageDescriptorProxy Descriptor { get; }
}

internal interface IDescriptorProxy : IDuckType
{
    string Name { get; }
}

/// <summary>
/// DuckTyping interface for Google.Protobuf.Reflection.MessageDescriptor
/// </summary>
internal interface IMessageDescriptorProxy : IDescriptorProxy
{
    IFieldCollectionProxy Fields { get; }

    IDescriptorProxy File { get; }
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
internal interface IFieldDescriptorProxy : IDescriptorProxy
{
    bool IsRepeated { get; }

    int FieldType { get; } // actually an enum

    int FieldNumber { get; }

    IEnumDescriptorProxy EnumType { get; }

    IMessageDescriptorProxy MessageType { get; }
}

/// <summary>
/// DuckTyping interface for Google.Protobuf.Reflection.EnumDescriptor
/// </summary>
internal interface IEnumDescriptorProxy : IDescriptorProxy
{
    IList Values { get; } // <EnumValueDescriptor>
}
