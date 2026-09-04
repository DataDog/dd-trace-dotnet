// <copyright file="OtelTraceState.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Propagators;

namespace Datadog.Trace;

internal sealed class OtelTraceState
{
    internal OtelTraceState(string? headerString)
    {
        CachedHeaderString = headerString;
    }

    public ulong? Threshold { get; set; }

    public ulong? RandomValue { get; set; }

    public string? CachedHeaderString { get; }

    public bool IsModified { get; set; }

    public bool LocallyGeneratedOtelRandomValue { get; set; }

    /// <summary>
    /// Converts the original header into a TraceState object and stores valid "rv" and "th" items
    /// into their first-class properties.
    /// Unknown items remain present in the cached string.
    /// Returns the original string when no rewrite is needed, and null when nothing remains.
    /// </summary>
    internal static OtelTraceState Parse(string? raw)
    {
        var traceState = new OtelTraceState(raw);
        if (StringUtil.IsNullOrEmpty(raw))
        {
            return traceState;
        }

        var remaining = raw!.AsSpan();

        while (true)
        {
            var separatorIndex = remaining.IndexOf(';');
            var item = separatorIndex < 0 ? remaining : remaining.Slice(0, separatorIndex);
            var colonIndex = item.IndexOf(':');
            var key = colonIndex > 0 ? item.Slice(0, colonIndex) : item;
            var value = colonIndex > 0 ? item.Slice(colonIndex + 1) : default;

            if (key.Equals("rv".AsSpan(), StringComparison.Ordinal))
            {
                if (value.Length != OtelTraceStateHelpers.MaxHexDigits || !OtelTraceStateHelpers.TryParseLowercaseHex(value, out ulong randomValue))
                {
                    // Do not store the randomValue
                    // Instead, keep it null and report that the object is modified from the original header
                    traceState.IsModified = true;
                }
                else
                {
                    traceState.RandomValue = randomValue;
                }
            }

            if (key.Equals("th".AsSpan(), StringComparison.Ordinal))
            {
                if (value.Length is < 1 or > OtelTraceStateHelpers.MaxHexDigits || !OtelTraceStateHelpers.TryParseLowercaseHex(value, out ulong threshold))
                {
                    // Do not store the threshold
                    // Instead, keep it null and report that the object is modified from the original header
                    traceState.IsModified = true;
                }
                else
                {
                    traceState.Threshold = threshold;
                }
            }

            if (separatorIndex < 0)
            {
                return traceState;
            }

            remaining = remaining.Slice(separatorIndex + 1);
        }
    }
}
