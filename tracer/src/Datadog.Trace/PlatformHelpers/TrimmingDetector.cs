// <copyright file="TrimmingDetector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if NET6_0_OR_GREATER
using System;

namespace Datadog.Trace.PlatformHelpers;

internal static class TrimmingDetector
{
    public static readonly bool IsTrimmingDetected = DetectTrimming();

    private static bool DetectTrimming()
    {
        try
        {
            // Probe for internal BCL types from different assemblies.
            // These are internal types that cannot be referenced by customer code,
            // so they should generally be removed by the trimmer. We check two types
            // to try to avoid false positives in the case where one of them _is_ referenced.
            // Keep these type checks in sync with tracer/src/Datadog.Trace.ClrProfiler.Managed.Loader/Startup.cs
            return Type.GetType("System.Net.Mime.SmtpDateTime, System.Net.Mail", throwOnError: false) is null
                || Type.GetType("System.Net.NetworkInformation.IcmpV4MessageConstants, System.Net.Ping", throwOnError: false) is null;
        }
        catch
        {
            // Shouldn't happen, seeing as we have throwOnError: false
            // This is used in both

            return false;
        }
    }
}
#endif
