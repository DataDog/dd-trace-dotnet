// <copyright file="ManagedProfilerAssemblyLoadContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP
using System.Reflection;
using System.Runtime.Loader;

namespace Datadog.Trace.ClrProfiler.Managed.Loader
{
    internal class ManagedProfilerAssemblyLoadContext : AssemblyLoadContext
    {
        protected override Assembly Load(AssemblyName assemblyName)
        {
            return null;
        }
    }
}
#endif
