// <copyright file="ProfilerAvailabilityHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Util;

namespace Datadog.Trace.ContinuousProfiler;

internal static class ProfilerAvailabilityHelper
{
    // This will never change, so we use a lazy to cache the result.
    // This confirms that we are in an automatic instrumentation environment (and so P/Invokes have been re-written)
    // and that the profiling library has been deployed (which is not the case in some serverless environments).
    // We should add or remove conditions from here as our deployment requirements change.
    // Longer term, we'd like to be able to pass this information from the native side to the managed side, but
    // today that only works on Windows (hence the early short-circuit).
    private static readonly Lazy<bool> ProfilerIsAvailable = new(() => GetIsContinuousProfilerAvailable(EnvironmentHelpersNoLogging.IsClrProfilerAttachedSafe));

    /// <summary>
    /// Gets a value indicating whether returns true if the continuous profiler _should_ be available
    /// </summary>
    public static bool IsContinuousProfilerAvailable => ProfilerIsAvailable.Value;

    [TestingOnly]
    internal static bool IsContinuousProfilerAvailable_TestingOnly(Func<bool> isClrProfilerAttached)
        => GetIsContinuousProfilerAvailable(isClrProfilerAttached);

    private static bool GetIsContinuousProfilerAvailable(Func<bool> isClrProfilerAttached)
    {
        // Profiler is not available on ARM(64)
        var fd = FrameworkDescription.Instance;
        if (!IsSupportedArch(fd))
        {
            return false;
        }

        // This variable is set by the native loader after trying to load the profiler
        // it means the profiler is _there_ though it may not be _loaded_. This only works
        // on Windows at the moment. We assume that the CLR profiler must be attached in this scenario.
        if (fd.IsWindows())
        {
            return !string.IsNullOrEmpty(EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.ContinuousProfiler.InternalProfilingNativeEnginePath));
        }

        // Now we're into fuzzy territory. The CP is not available in some environments
        // - AWS Lambda
        // - Azure Functions where the site extension is _not_ used (Site extension is Windows only, so that's already covered)
        var isUnsupported = EnvironmentHelpers.IsAwsLambda() || EnvironmentHelpers.IsAzureFunctions();

        // As a final check, we check whether the ClrProfiler is attached - if it's not, then the P/Invokes won't
        // have been re-written, and native calls won't work.
        return !isUnsupported && isClrProfilerAttached();

        static bool IsSupportedArch(FrameworkDescription fd)
        {
            return fd.OSPlatform switch
            {
                OSPlatformName.Windows when fd.ProcessArchitecture is ProcessArchitecture.X64 or ProcessArchitecture.X86 => true,
                OSPlatformName.Linux when fd.ProcessArchitecture is ProcessArchitecture.X64 => true,
                _ => false,
            };
        }
    }
}
