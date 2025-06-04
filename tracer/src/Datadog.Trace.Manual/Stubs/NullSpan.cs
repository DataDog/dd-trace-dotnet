// <copyright file="NullSpan.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Stubs;

internal class NullSpan : ISpan
{
    public static readonly NullSpan Instance = new();

    private NullSpan()
    {
    }

    public string? OperationName
    {
        get => string.Empty;
        set { }
    }

    public string? ResourceName
    {
        get => string.Empty;
        set { }
    }

    public string? Type
    {
        get => string.Empty;
        set { }
    }

    public bool Error
    {
        get => false;
        set { }
    }

    public string? ServiceName
    {
        get => string.Empty;
        set { }
    }

    public ulong TraceId => Context.TraceId;

    public ulong SpanId => Context.SpanId;

    public ISpanContext Context => NullSpanContext.Instance;

    public void Dispose()
    {
    }

    public ISpan SetTag(string key, string? value)
    {
        return this;
    }

    public void Finish()
    {
    }

    public void Finish(DateTimeOffset finishTimestamp)
    {
    }

    public void SetException(Exception exception)
    {
    }

    public string? GetTag(string key) => null;
}
