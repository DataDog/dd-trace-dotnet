// <copyright file="Startup.NetCore.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP

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
            var version = Environment.Version;
            string managedLibrariesDirectory;

            if (version.Major >= 6)
            {
                // version > 6.0
                managedLibrariesDirectory = "net6.0";
            }
            else if (version is { Major: 3, Minor: >= 1 } || version.Major == 5)
            {
                // version is 3.1 or 5.0
                managedLibrariesDirectory = "netcoreapp3.1";
            }
            else
            {
                // version < 3.1 (note: previous versions of .NET Core had major version 4)
                managedLibrariesDirectory = "netstandard2.0";
            }

            var fullPath = Path.Combine(Path.GetFullPath(tracerHomeDirectory), managedLibrariesDirectory);

            // Populate the resolver's cache. The resolver is a separate type so its handler
            // can be invoked from ThreadPool threads without having to wait on Startup..cctor.
            ManagedProfilerAssemblyResolver.PopulateAssemblyCache(fullPath);

            return fullPath;
        }
    }
}

#endif
