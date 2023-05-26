// <copyright file="Span.IReadOnlyDictionary.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Datadog.Trace.Util;

namespace Datadog.Trace;

internal partial class Span : IReadOnlyDictionary<string, string>
{
    private static readonly string[] KeyNames =
    {
        Keys.TraceId,
        Keys.ParentId,
        Keys.SamplingPriority,
        Keys.Origin,
        Keys.RawTraceId,
        Keys.RawSpanId,
        Keys.PropagatedTags,
        Keys.AdditionalW3CTraceState,

        // For mismatch version support we need to keep supporting old keys.
        HttpHeaderNames.TraceId,
        HttpHeaderNames.ParentId,
        HttpHeaderNames.SamplingPriority,
        HttpHeaderNames.Origin,
    };

    /// <inheritdoc/>
    int IReadOnlyCollection<KeyValuePair<string, string>>.Count => KeyNames.Length;

    /// <inheritdoc />
    IEnumerable<string> IReadOnlyDictionary<string, string>.Keys => KeyNames;

    /// <inheritdoc/>
    IEnumerable<string> IReadOnlyDictionary<string, string>.Values
    {
        get
        {
            foreach (var key in KeyNames)
            {
                yield return ((IReadOnlyDictionary<string, string>)this)[key];
            }
        }
    }

    /// <inheritdoc/>
    string IReadOnlyDictionary<string, string>.this[string key]
    {
        get
        {
            if (((IReadOnlyDictionary<string, string>)this).TryGetValue(key, out var value))
            {
                return value;
            }

            ThrowHelper.ThrowKeyNotFoundException($"Key not found: {key}");
            return default;
        }
    }

    /// <inheritdoc/>
    IEnumerator<KeyValuePair<string, string>> IEnumerable<KeyValuePair<string, string>>.GetEnumerator()
    {
        var dictionary = (IReadOnlyDictionary<string, string>)this;

        foreach (var key in KeyNames)
        {
            yield return new KeyValuePair<string, string>(key, dictionary[key]);
        }
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IReadOnlyDictionary<string, string>)this).GetEnumerator();
    }

    /// <inheritdoc/>
    bool IReadOnlyDictionary<string, string>.ContainsKey(string key)
    {
        foreach (var k in KeyNames)
        {
            if (k == key)
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    bool IReadOnlyDictionary<string, string>.TryGetValue(string key, out string value)
    {
        var invariant = CultureInfo.InvariantCulture;

        switch (key)
        {
            case Keys.TraceId:
            case HttpHeaderNames.TraceId:
                // use the lower 64-bits for backwards compat, truncate using TraceId128.Lower
                value = TraceId128.Lower.ToString(invariant);
                return true;

            case Keys.ParentId:
            case HttpHeaderNames.ParentId:
                // returns the 64-bit span id in decimal encoding
                value = SpanId.ToString(invariant);
                return true;

            case Keys.SamplingPriority:
            case HttpHeaderNames.SamplingPriority:
                // return the value from TraceContext if available
                var samplingPriority = ((ISpanContextInternal)this).TraceContext?.SamplingPriority ?? ((ISpanContextInternal)this).SamplingPriority;
                value = samplingPriority?.ToString(invariant);
                return true;

            case Keys.Origin:
            case HttpHeaderNames.Origin:
                value = ((ISpanContextInternal)this).Origin;
                return true;

            case Keys.RawTraceId:
                // returns the full 128-bit trace id in hexadecimal encoding
                value = ((ISpanContextInternal)this).RawTraceId;
                return true;

            case Keys.RawSpanId:
                // returns the 64-bit span id in hexadecimal encoding
                value = ((ISpanContextInternal)this).RawSpanId;
                return true;

            case Keys.PropagatedTags:
            case HttpHeaderNames.PropagatedTags:
                value = ((ISpanContextInternal)this).PrepareTagsHeaderForPropagation();
                return true;

            case Keys.AdditionalW3CTraceState:
                // return the value from TraceContext if available
                value = ((ISpanContextInternal)this).TraceContext?.AdditionalW3CTraceState ?? ((ISpanContextInternal)this).AdditionalW3CTraceState;
                return true;

            default:
                value = null;
                return false;
        }
    }

    internal static class Keys
    {
        private const string Prefix = "__DistributedKey-";

        public const string TraceId = $"{Prefix}TraceId";
        public const string ParentId = $"{Prefix}ParentId";
        public const string SamplingPriority = $"{Prefix}SamplingPriority";
        public const string Origin = $"{Prefix}Origin";
        public const string RawTraceId = $"{Prefix}RawTraceId";
        public const string RawSpanId = $"{Prefix}RawSpanId";
        public const string PropagatedTags = $"{Prefix}PropagatedTags";
        public const string AdditionalW3CTraceState = $"{Prefix}AdditionalW3CTraceState";
    }
}
