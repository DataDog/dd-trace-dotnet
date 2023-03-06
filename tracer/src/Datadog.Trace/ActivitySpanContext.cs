// <copyright file="ActivitySpanContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>


namespace Datadog.Trace;

internal class ActivitySpanContext
{
    public ActivitySpanContext(System.Diagnostics.ActivityContext context)
    {
        Context = context;
    }

    public ActivitySpanContext(System.Diagnostics.ActivityContext context, ActivitySpanContext parent, TraceContext traceContext)
        : this(context)
    {
        Parent = parent;
        TraceContext = traceContext;
    }

    internal System.Diagnostics.ActivityContext Context { get; }

    internal TraceContext TraceContext { get; }

    internal ActivitySpanContext Parent { get; }
}
