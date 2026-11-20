// <copyright file="WafReturnCodeExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.AppSec.Waf;

namespace Datadog.Trace.Telemetry.Metrics;

internal static class WafReturnCodeExtensions
{
    public static MetricTags.WafError? ToWafErrorTag(this WafReturnCode returnCode)
        => returnCode switch
        {
            WafReturnCode.ErrorInternal => MetricTags.WafError.Internal,
            WafReturnCode.ErrorInvalidObject => MetricTags.WafError.InvalidObject,
            WafReturnCode.ErrorInvalidArgument => MetricTags.WafError.InvalidArgument,
            _ => null,
        };
}
