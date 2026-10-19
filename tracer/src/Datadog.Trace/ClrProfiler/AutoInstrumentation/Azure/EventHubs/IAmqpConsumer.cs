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
[DuckType("Azure.Messaging.EventHubs.Amqp.AmqpConsumer", "Azure.Messaging.EventHubs")]
internal interface IAmqpConsumer : IDuckType
{
    string EventHubName { get; }

    IAmqpConnectionScope? ConnectionScope { get; }
}

[DuckType("Azure.Messaging.EventHubs.Amqp.AmqpConnectionScope", "Azure.Messaging.EventHubs")]
internal interface IAmqpConnectionScope
{
    System.Uri? ServiceEndpoint { get; }
}
