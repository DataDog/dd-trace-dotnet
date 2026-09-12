// <copyright file="IWorkerTraceContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Functions;

/// <summary>
/// Duck type for Microsoft.Azure.Functions.Worker.TraceContext.
/// </summary>
internal interface IWorkerTraceContext : IDuckType
{
    string? TraceParent { get; }

    string? TraceState { get; }
}

#endif
