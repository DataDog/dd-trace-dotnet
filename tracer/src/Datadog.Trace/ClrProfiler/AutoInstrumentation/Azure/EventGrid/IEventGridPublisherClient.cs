// <copyright file="IEventGridPublisherClient.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.EventGrid;

/// <summary>
/// Duck type for Azure.Messaging.EventGrid.EventGridPublisherClient
/// </summary>
internal interface IEventGridPublisherClient : IDuckType
{
    [DuckField(Name = "_uriBuilder")]
    IRequestUriBuilder? UriBuilder { get; }
}

/// <summary>
/// Duck type for Azure.Core.RequestUriBuilder
/// </summary>
internal interface IRequestUriBuilder : IDuckType
{
    string? Host { get; }
}

/// <summary>
/// Duck type for EventGridEvent.Id and CloudEvent.Id
/// </summary>
internal interface IEventGridEventId
{
    string? Id { get; }
}
