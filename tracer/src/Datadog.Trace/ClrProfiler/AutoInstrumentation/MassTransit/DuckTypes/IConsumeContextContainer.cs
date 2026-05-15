// <copyright file="IConsumeContextContainer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit.DuckTypes;

/// <summary>
/// Duck-typing interface for diagnostic payload wrappers that expose the consume context via a public ConsumeContext property.
/// </summary>
internal interface IConsumeContextContainer
{
    object? ConsumeContext { get; }
}
