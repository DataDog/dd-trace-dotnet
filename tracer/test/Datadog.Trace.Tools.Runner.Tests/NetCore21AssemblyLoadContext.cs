// <copyright file="NetCore21AssemblyLoadContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP2_1
#nullable enable

using System.Reflection;

namespace Datadog.Trace.Tools.Runner.Tests;

internal sealed class NetCore21AssemblyLoadContext
{
    public NetCore21AssemblyLoadContext(string name, bool isCollectible)
    {
        _ = name;
        _ = isCollectible;
    }

    public Assembly LoadFromAssemblyPath(string assemblyPath)
    {
        return Assembly.LoadFrom(assemblyPath);
    }

    public void Unload()
    {
    }
}
#endif
