// <copyright file="UpstreamService.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Sampling;
using Datadog.Trace.Util;

namespace Datadog.Trace.Tagging.PropagatedTags;

/*
    _dd.p.upstream_services=bWNudWx0eS13ZWI|0|1;dHJhY2Utc3RhdHMtcXVlcnk|2|4

    - Groups are separated by ";" (semicolon)
    - Fields are separated by "|" (vertical bar)

    - Field #1 is always UTF-8 service name encoded with base64
    - Field #2 is always sampling priority
    - Field #3 is always sampling mechanism
    - Field #4 is a sampling rate (rounded up to four decimal places) when the sampling decision was made based on a rate, otherwise empty string
*/
internal readonly struct UpstreamService
{
    public const string TagName = Tags.Propagated.UpstreamServices;
    public const char GroupSeparator = ';';
    public const char FieldSeparator = '|';

    public static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public UpstreamService(string serviceName, SamplingDecision samplingDecision)
        : this(serviceName, samplingDecision.Priority, samplingDecision.Mechanism, samplingDecision.Rate)
    {
    }

    public UpstreamService(string serviceName, int samplingPriority, int samplingMechanism, double? samplingRate)
    {
        ServiceName = serviceName;
        SamplingPriority = samplingPriority;
        SamplingMechanism = samplingMechanism;
        SamplingRate = samplingRate;
    }

    public string ServiceName { get; }

    public int SamplingPriority { get; }

    public int SamplingMechanism { get; }

    public double? SamplingRate { get; }

    public override string ToString()
    {
        // TODO: optimize to avoid heap allocations on each call, especially service name:
        // string -> utf8 bytes -> base64 string
        var serviceNameUtf8Base64 = Convert.ToBase64String(Utf8.GetBytes(ServiceName));

        string field1 = serviceNameUtf8Base64;
        string field2 = SamplingPriority.ToString(CultureInfo.InvariantCulture);
        string field3 = SamplingMechanism.ToString(CultureInfo.InvariantCulture);
        string field4 = SamplingRate.RoundUp(digits: 4)?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

        var totalLength = field1.Length + field2.Length + field3.Length + field4.Length + 3;
        var sb = StringBuilderCache.Acquire(totalLength);

        sb.Append(field1)
          .Append(FieldSeparator)
          .Append(field2)
          .Append(FieldSeparator)
          .Append(field3);

        if (SamplingRate is not null)
        {
            sb.Append(FieldSeparator)
              .Append(field4);
        }

        return StringBuilderCache.GetStringAndRelease(sb);
    }
}
