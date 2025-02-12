// <copyright file="SpanCodeOriginManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Debugger.SpanCodeOrigin;

internal class SpanCodeOriginManager
{
    public static SpanCodeOriginManager Instance { get; } = new();

    public void SetCodeOrigin(Span span)
    {
    }

    public void SetCodeOriginForExitSpan(Span span)
    {
    }
}
