// <copyright file="IEventData.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.EventHubs;

/// <summary>
/// Duck type for Azure.Messaging.EventHubs.EventData
/// </summary>
internal interface IEventData : IDuckType
{
    IDictionary<string, object> Properties { get; }

    string MessageId { get; }

    IEventDataBody? EventBody { get; }

    DateTimeOffset EnqueuedTime { get; }
}

/// <summary>
/// Duck type for Azure.BinaryData
/// </summary>
internal interface IEventDataBody
{
    IEventDataBodyMemory ToMemory();
}

/// <summary>
/// Duck type for System.ReadOnlyMemory&lt;byte&gt;
/// </summary>
internal interface IEventDataBodyMemory
{
    int Length { get; }
}
