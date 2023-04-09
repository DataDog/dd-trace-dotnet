// <copyright file="TaggingUtils.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace;

internal class TaggingUtils
{
    internal static Action<string, string> GetSpanSetter(ISpan span)
    {
        return GetSpanSetter(span, out var _);
    }

    internal static Action<string, string> GetSpanSetter(ISpan span, out Span spanClass)
    {
        TraceContext traceContext = null;
        if (span is Span spanClassTemp)
        {
            spanClass = spanClassTemp;
            traceContext = spanClass.Context.TraceContext;
        }
        else
        {
            spanClass = null;
        }

        Action<string, string> setTag =
            traceContext != null
                ? (name, value) => traceContext.Tags.SetTag(name, value)
                : (name, value) => span.SetTag(name, value);
        return setTag;
    }
}
