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
            // Probe for various BCL types from different assemblies which we expect to be trimmed in most customer applications
            // We check multiple types to try to avoid false positives in the case where one of them _is_ referenced.
            // Keep these type checks in sync with tracer/src/Datadog.Trace.ClrProfiler.Managed.Loader/Startup.cs
            // and tracer/build/_build/Build.Steps.cs (CreateTrimmingFile target).
            return Type.GetType("System.Net.Mime.SmtpDateTime, System.Net.Mail", throwOnError: false) is null
                || Type.GetType("System.Net.NetworkInformation.PingCompletedEventArgs, System.Net.Ping", throwOnError: false) is null
                || Type.GetType("System.IO.IsolatedStorage.IsolatedStorageScope, System.IO.IsolatedStorage", throwOnError: false) is null;
        }
        catch
        {
            // Shouldn't happen, seeing as we have throwOnError: false

            return false;
        }
    }
}
#endif
