// <copyright file="IEventGridEventId.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.EventGrid;

/// <summary>
/// Duck type for EventGridEvent.Id and CloudEvent.Id
/// </summary>
internal interface IEventGridEventId
{
    string? Id { get; }
}
