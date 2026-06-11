// <copyright file="IContextContainer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit.DuckTypes;

/// <summary>
/// Duck-typing interface for MassTransit diagnostic payload wrappers that expose the real context via a public Context property.
/// </summary>
internal interface IContextContainer
{
    object? Context { get; }
}
