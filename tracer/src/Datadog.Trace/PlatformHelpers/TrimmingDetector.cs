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
    public static readonly TrimState DetectedTrimmingState = DetectTrimming();

    public enum TrimState
    {
        Unknown,
        NoTrimmingDetected,
        TrimmedAppUsingTrimmingFile,
        TrimmedAppMissingTrimmingFile,
    }

    private static TrimState DetectTrimming()
    {
        try
        {
            // Probe for various BCL types from different assemblies which we expect to be trimmed in most customer applications
            // We check multiple types to try to avoid false positives in the case where one of them _is_ referenced.

            // Keep these type checks in sync with tracer/build/_build/Build.Steps.cs (CreateTrimmingFile target).
            if (Type.GetType("System.Resources.ResourceWriter, System.Resources.Writer", throwOnError: false) is null
             || Type.GetType("System.IO.IsolatedStorage.IsolatedStorageScope, System.IO.IsolatedStorage", throwOnError: false) is null)
            {
                // These two are listed in our trimming.xml file. If _either_ of them are missing,
                // that means the app is trimmed, and they haven't used our trimming file.
                return TrimState.TrimmedAppMissingTrimmingFile;
            }

            // This probe is intentionally _not_ listed in CreateTrimmingFile, so that we can detect the case
            // where they're using trimming but have correctly added our trimming file
            return Type.GetType("System.Net.NetworkInformation.PingCompletedEventArgs, System.Net.Ping", throwOnError: false) is null
                       ? TrimState.TrimmedAppUsingTrimmingFile
                       : TrimState.NoTrimmingDetected;
        }
        catch
        {
            // Shouldn't happen, seeing as we have throwOnError: false
            return TrimState.Unknown;
        }
    }
}
#endif
