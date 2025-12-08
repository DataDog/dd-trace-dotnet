// <copyright file="IAmqpConsumer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.EventHubs;

/// <summary>
/// Duck type for Azure.Messaging.EventHubs.Amqp.AmqpConsumer
/// </summary>
internal interface IAmqpConsumer : IDuckType
{
    string EventHubName { get; }

    IAmqpConnectionScope? ConnectionScope { get; }
}

internal interface IAmqpConnectionScope
{
    System.Uri? ServiceEndpoint { get; }
}
