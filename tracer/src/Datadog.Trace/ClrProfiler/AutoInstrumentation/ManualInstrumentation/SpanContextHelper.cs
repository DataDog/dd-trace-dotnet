// <copyright file="SpanContextHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Diagnostics.CodeAnalysis;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Proxies;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Internal;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation;

internal static class SpanContextHelper
{
    [return: NotNullIfNotNull(nameof(context))]
    public static IInternalSpanContext? GetContext<T>(T context)
        => context switch
        {
            null => null,
            InternalSpanContext c => c,
            IInternalSpanContext c => c,
            IDuckType { Instance: InternalSpanContext c } => c,
            _ when context.TryDuckCast<SpanContextProxy>(out var spanContextProxy) => new InternalSpanContext(
                new TraceId(Upper: spanContextProxy.TraceIdUpper, Lower: spanContextProxy.TraceId),
                spanContextProxy.SpanId,
                spanContextProxy.SamplingPriority,
                spanContextProxy.ServiceName,
                origin: null),
            _ => GetISpanContext(context),
        };

    private static InternalSpanContext GetISpanContext<T>(T parent)
    {
        var context = parent.DuckCast<ISpanContextProxy>();
        return new InternalSpanContext(
            new TraceId(Upper: 0, Lower: context.TraceId),
            context.SpanId,
            samplingPriority: null,
            context.ServiceName,
            origin: null);
    }
}
