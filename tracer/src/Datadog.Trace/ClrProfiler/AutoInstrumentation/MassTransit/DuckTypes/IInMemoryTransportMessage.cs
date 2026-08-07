// <copyright file="IInMemoryTransportMessage.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit.DuckTypes;

/// <summary>
/// Duck-typing interface for MassTransit.Transports.InMemory.Fabric.InMemoryTransportMessage.
/// Headers is a public IDictionary&lt;string, object&gt; property on this type.
/// </summary>
internal interface IInMemoryTransportMessage
{
    IDictionary<string, object>? Headers { get; }
}
