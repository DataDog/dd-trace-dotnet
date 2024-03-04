// <copyright file="NullSpanContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Stubs;

internal class NullSpanContext : ISpanContext
{
    public static readonly NullSpanContext Instance = new();

    private NullSpanContext()
    {
    }

    public ulong TraceId => 0;

    public ulong SpanId => 0;

    public string ServiceName => string.Empty;
}
