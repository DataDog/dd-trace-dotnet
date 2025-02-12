// <copyright file="AppSecRequestContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Tagging;

namespace Datadog.Trace.AppSec;

internal class AppSecRequestContext
{
    public void CloseWebSpan(TraceTagCollection tags, Span span)
    {
    }
}
