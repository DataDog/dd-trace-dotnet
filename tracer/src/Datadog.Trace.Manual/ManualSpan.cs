// <copyright file="ManualSpan.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Stubs;

namespace Datadog.Trace;

/// <summary>
/// Used to reverse duck-type an ISpan in custom instrumentation
/// </summary>
internal class ManualSpan : ISpan
{
    private readonly ManualSpanContext _spanContext = new();
    private ISpan _proxy = NullSpan.Instance;

    /// <summary>
    /// Gets the associated <see cref="ISpan"/> from Datadog.Trace.
    /// Null when running under manual-only instrumentation.
    /// Non-null when running under automatic instrumentation
    /// </summary>
    internal object? AutomaticSpan { get; private set; }

    public string OperationName
    {
        get => _proxy.OperationName;
        set => _proxy.OperationName = value;
    }

    public string ResourceName
    {
        get => _proxy.ResourceName;
        set => _proxy.ResourceName = value;
    }

    public string Type
    {
        get => _proxy.Type;
        set => _proxy.Type = value;
    }

    public bool Error
    {
        get => _proxy.Error;
        set => _proxy.Error = value;
    }

    public string ServiceName
    {
        get => _proxy.ServiceName;
        set => _proxy.ServiceName = value;
    }

    public ulong TraceId => _proxy.TraceId;

    public ulong SpanId => _proxy.SpanId;

    public ISpanContext Context => _spanContext;

    public void Dispose() => _proxy.Dispose();

    public ISpan SetTag(string key, string? value)
    {
        _proxy.SetTag(key, value);
        return this;
    }

    public void Finish() => _proxy.Finish();

    public void Finish(DateTimeOffset finishTimestamp) => _proxy.Finish(finishTimestamp);

    public void SetException(Exception exception) => _proxy.SetException(exception);

    public string? GetTag(string key) => _proxy.GetTag(key);

    [DuckTypeTarget]
    internal void SetAutomatic(object span, object spanContext)
    {
        AutomaticSpan = span;
        _proxy = span.DuckCast<ISpan>() ?? NullSpan.Instance;
        _spanContext.SetAutomatic(spanContext);
    }
}
