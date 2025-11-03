// <copyright file="Startup.NetFramework.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

#nullable enable

using System;
using System.IO;

namespace Datadog.Trace.ClrProfiler.Managed.Loader
{
    /// <summary>
    /// A class that attempts to load the Datadog.Trace .NET assembly.
    /// </summary>
    public partial class Startup
    {
        internal static string ComputeTfmDirectory(string tracerHomeDirectory)
        {
            return Path.Combine(Path.GetFullPath(tracerHomeDirectory), "net461");
        }

        internal static string GetProfilerPathEnvVarNameForArch()
        {
            return Environment.Is64BitProcess ? "COR_PROFILER_PATH_64" : "COR_PROFILER_PATH_32";
        }

        internal static string GetProfilerPathEnvVarNameFallback()
        {
            return "COR_PROFILER_PATH";
        }
    }
}

#endif
