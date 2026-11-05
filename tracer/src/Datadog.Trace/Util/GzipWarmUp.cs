// <copyright file="GzipWarmUp.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if !NETFRAMEWORK
using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using Datadog.Trace.Logging;
#endif

namespace Datadog.Trace.Util;

/// <summary>
/// Works around a race in the zlib-intel compressor that .NET Core ships on Windows before .NET 9.
/// It marks its CPU feature detection as done before publishing the flags, so if several threads create their
/// first compressor at the same time, one can compute a wrong CRC and produce a gzip payload that the receiver rejects.
/// Compressing once on a single thread completes that detection. .NET 9+ uses zlib-ng and other platforms use the system zlib.
/// </summary>
internal static class GzipWarmUp
{
#if !NETFRAMEWORK
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(GzipWarmUp));

    // Keep this an explicit static constructor: without one the type is beforefieldinit, and calling the empty RunOnce()
    // would no longer run the warm-up. The runtime runs it exactly once, and other threads that reach the type wait until it finishes.
    static GzipWarmUp()
    {
        try
        {
            var frameworkDescription = FrameworkDescription.Instance;
            if (frameworkDescription.IsWindows() && frameworkDescription.RuntimeVersion.Major < 9)
            {
                CompressOnce();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error warming up gzip compression");
        }
    }
#endif

    /// <summary>
    /// Runs the warm-up once per process. Must be called before starting threads that compress concurrently.
    /// </summary>
    public static void RunOnce()
    {
        // Intentionally empty: on .NET Core the first call triggers the static constructor, which does the work.
        // There is nothing to do on .NET Framework.
    }

#if !NETFRAMEWORK
    // Kept separate so System.IO.Compression is only loaded on affected runtimes, and a load failure is caught above.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CompressOnce()
    {
        using var gzipStream = new GZipStream(Stream.Null, CompressionLevel.Fastest);
        gzipStream.WriteByte(0);
    }
#endif
}
