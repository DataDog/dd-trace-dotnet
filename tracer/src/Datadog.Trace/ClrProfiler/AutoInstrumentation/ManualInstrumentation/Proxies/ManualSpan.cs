// <copyright file="ManualSpan.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Proxies;

/// <summary>
/// Used to reverse duck-type an ISpan in custom instrumentation
/// </summary>
internal class ManualSpan
{
    private readonly ManualSpanContext _spanContext;

    public ManualSpan(ISpan span, ManualSpanContext spanContext, Type spanType)
    {
        _spanContext = spanContext;
        AutomaticSpan = span;
        Proxy = this.DuckImplement(spanType);
    }

    /// <summary>
    /// Gets the reverse-duck-type of this object for manual instrumentation
    /// </summary>
    internal object Proxy { get; }

    public ISpan AutomaticSpan { get; }

    [DuckReverseMethod]
    public string OperationName
    {
        get => AutomaticSpan.OperationName;
        set => AutomaticSpan.OperationName = value;
    }

    [DuckReverseMethod]
    public string ResourceName
    {
        get => AutomaticSpan.ResourceName;
        set => AutomaticSpan.ResourceName = value;
    }

    [DuckReverseMethod]
    public string Type
    {
        get => AutomaticSpan.Type;
        set => AutomaticSpan.Type = value;
    }

    [DuckReverseMethod]
    public bool Error
    {
        get => AutomaticSpan.Error;
        set => AutomaticSpan.Error = value;
    }

    [DuckReverseMethod]
    public string ServiceName
    {
        get => AutomaticSpan.ServiceName;
        set => AutomaticSpan.ServiceName = value;
    }

    [DuckReverseMethod]
    public ulong TraceId => AutomaticSpan.TraceId;

    [DuckReverseMethod]
    public ulong SpanId => AutomaticSpan.SpanId;

    [DuckReverseMethod(ParameterTypeNames = new[] { "Datadog.Trace.ISpanContext, Datadog.Trace.Manual" })]
    public object Context => _spanContext.Proxy;

    [DuckReverseMethod]
    public void Dispose() => AutomaticSpan.Dispose();

    [DuckReverseMethod]
    public object SetTag(string key, string? value)
    {
        AutomaticSpan.SetTag(key, value);
        return Proxy;
    }

    [DuckReverseMethod]
    public void Finish() => AutomaticSpan.Finish();

    [DuckReverseMethod]
    public void Finish(DateTimeOffset finishTimestamp) => AutomaticSpan.Finish(finishTimestamp);

    [DuckReverseMethod]
    public void SetException(Exception exception) => AutomaticSpan.SetException(exception);

    [DuckReverseMethod]
    public string? GetTag(string key) => AutomaticSpan.GetTag(key);
}
