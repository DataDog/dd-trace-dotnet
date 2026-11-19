// <copyright file="TruncationInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.AppSec.WafEncoding;

/// <summary>
/// What the encoder truncated to fit the WAF safety limits: the largest original size of each kind of truncation,
/// 0 when nothing of that kind was truncated.
/// </summary>
internal struct TruncationInfo
{
    // the truncation_reason flags of the telemetry RFC
    private const int StringTooLongFlag = 1;
    private const int ListOrMapTooLargeFlag = 2;
    private const int ObjectTooDeepFlag = 4;

    /// <summary>
    /// Gets the length, in characters, of the longest string that was truncated
    /// </summary>
    public int StringLength { get; private set; }

    /// <summary>
    /// Gets the element count of the largest container that was truncated
    /// </summary>
    public int ContainerSize { get; private set; }

    /// <summary>
    /// Gets the depth at which containers were dropped because the object was too deep
    /// </summary>
    public int ContainerDepth { get; private set; }

    public readonly bool Truncated => StringLength > 0 || ContainerSize > 0 || ContainerDepth > 0;

    /// <summary>
    /// Gets the truncation_reason of appsec.waf.input_truncated, the flags of the kinds of truncation, null when nothing was truncated
    /// </summary>
    public readonly MetricTags.TruncationReason? Reason
    {
        get
        {
            var flags = (StringLength > 0 ? StringTooLongFlag : 0)
                      | (ContainerSize > 0 ? ListOrMapTooLargeFlag : 0)
                      | (ContainerDepth > 0 ? ObjectTooDeepFlag : 0);

            // the values of TruncationReason are the flags minus one, so that they start at 0
            return flags == 0 ? null : (MetricTags.TruncationReason)(flags - 1);
        }
    }

    public void AddString(int length) => StringLength = Math.Max(StringLength, length);

    public void AddContainer(int count) => ContainerSize = Math.Max(ContainerSize, count);

    /// <summary>
    /// Records that containers were dropped because the object was deeper than the encoding allowed. The depth
    /// reported is where they were dropped rather than the actual depth of the object, an approximation the RFC allows.
    /// </summary>
    /// <param name="startDepth">the depth the encoding started with</param>
    public void AddTooDeep(int startDepth) => AddDepth(startDepth + 1);

    private void AddDepth(int depth) => ContainerDepth = Math.Max(ContainerDepth, depth);

    public void Add(in TruncationInfo other)
    {
        AddString(other.StringLength);
        AddContainer(other.ContainerSize);
        AddDepth(other.ContainerDepth);
    }
}
