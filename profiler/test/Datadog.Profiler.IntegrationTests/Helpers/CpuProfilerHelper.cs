// <copyright file="CpuProfilerHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datadog.Profiler.IntegrationTests.Xunit;

namespace Datadog.Profiler.IntegrationTests.Helpers
{
    internal static class CpuProfilerHelper
    {
        private const string DowngradeMarker = "Falling back to the manual CPU profiler";

        /// <summary>
        /// Reads the lines of every native profiler log found in the given directory.
        /// </summary>
        /// <param name="logDirectory">The log directory of the run.</param>
        /// <returns>The lines of the native profiler logs.</returns>
        public static IEnumerable<string> ReadNativeLogs(string logDirectory)
        {
            return Directory.EnumerateFiles(logDirectory, "DD-DotNet-Profiler-Native*.log", SearchOption.AllDirectories)
                            .SelectMany(File.ReadLines);
        }

        /// <summary>
        /// Skips the calling test when the profiler gave up on timer_create.
        /// RLIMIT_SIGPENDING is accounted per user id and shared by every process of that user, so a
        /// neighbour on the same machine can exhaust the signal queue and leave this process with no
        /// choice but the manual CPU profiler. That is a property of the host, not a defect in the
        /// code under test.
        /// </summary>
        /// <param name="logDirectory">The log directory of the run.</param>
        public static void SkipIfTimerCreateWasDowngraded(string logDirectory)
        {
            SkipIfTimerCreateWasDowngraded(ReadNativeLogs(logDirectory));
        }

        /// <summary>
        /// Skips the calling test when the profiler gave up on timer_create.
        /// </summary>
        /// <param name="nativeLogLines">The lines of the native profiler log.</param>
        public static void SkipIfTimerCreateWasDowngraded(IEnumerable<string> nativeLogLines)
        {
            if (nativeLogLines.Any(line => line.Contains(DowngradeMarker)))
            {
                throw new SkipTestException(
                    "The host signal queue is exhausted (see RLIMIT_SIGPENDING), so timer_create CPU profiling was downgraded to the manual CPU profiler.");
            }
        }
    }
}
