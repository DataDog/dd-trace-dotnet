// <copyright file="SpanContextHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Diagnostics.CodeAnalysis;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Proxies;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation;

internal static class SpanContextHelper
{
    [return: NotNullIfNotNull(nameof(context))]
    public static ISpanContext? GetContext<T>(T context)
        => context switch
        {
            null => null,
            SpanContext c => c,
            ISpanContext c => c,
            IDuckType { Instance: SpanContext c } => c,
            _ when context.TryDuckCast<SpanContextProxy>(out var spanContextProxy) => new SpanContext(
                new TraceId(Upper: spanContextProxy.TraceIdUpper, Lower: spanContextProxy.TraceId),
                spanContextProxy.SpanId,
                spanContextProxy.SamplingPriority,
                spanContextProxy.ServiceName,
                origin: null),
            _ => GetISpanContext(context),
        };

    private static SpanContext GetISpanContext<T>(T parent)
    {
        var context = parent.DuckCast<ISpanContextProxy>();
        return new SpanContext(
            new TraceId(Upper: 0, Lower: context.TraceId),
            context.SpanId,
            samplingPriority: null,
            context.ServiceName,
            origin: null);
    }
}
