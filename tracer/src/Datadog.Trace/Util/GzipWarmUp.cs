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
    private static readonly object Lock = new();
    private static bool _done;
#endif

    /// <summary>
    /// Runs the warm-up once per process. Must be called before starting threads that compress concurrently.
    /// </summary>
    public static void RunOnce()
    {
#if !NETFRAMEWORK
        // Later callers wait for the first one to finish, so they never start compressing during the warm-up.
        lock (Lock)
        {
            if (_done)
            {
                return;
            }

            _done = true;

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
