// <copyright file="StackWalker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Datadog.Trace.Iast;

internal static class StackWalker
{
    private const int DefaultSkipFrames = 2;
    private static readonly string[] ExcludeSpanGenerationTypes = { "Datadog.Trace.Debugger.Helpers.StringExtensions", "Microsoft.AspNetCore.Razor.Language.StreamSourceDocument", "System.Security.IdentityHelper" };
    private static readonly string[] AssemblyNamesToSkip =
    {
        "Datadog.Trace",
        "Dapper",
        "Dapper.",
        "EntityFramework",
        "EntityFramework.",
        "linq2db",
        "Microsoft.",
        "MySql.",
        "MySqlConnector",
        "mscorlib",
        "netstandard",
        "Npgsql",
        "Oracle.",
        "RestSharp",
        "System",
        "System.",
        "xunit.",
        "Azure."
    };

    private static readonly Dictionary<string, bool> ExcludedAssemblyCache = new Dictionary<string, bool>();

    public static StackFrameInfo GetFrame()
    {
        var stackTrace = new StackTrace(DefaultSkipFrames, true);

        foreach (var frame in stackTrace.GetFrames())
        {
            var declaringType = frame?.GetMethod()?.DeclaringType;

            foreach (var excludeType in ExcludeSpanGenerationTypes)
            {
                if (excludeType == declaringType?.FullName)
                {
                    return new StackFrameInfo(null, false);
                }
            }

            if (ExcludeSpanGenerationTypes.Contains(declaringType?.FullName))
            {
                return new StackFrameInfo(null, false);
            }

            var assembly = declaringType?.Assembly.GetName().Name;
            if (assembly != null && !AssemblyExcluded(assembly))
            {
                return new StackFrameInfo(frame, true);
            }
        }

        return new StackFrameInfo(null, true);
    }

    public static bool AssemblyExcluded(string assembly)
    {
        if (ExcludedAssemblyCache.TryGetValue(assembly, out bool excluded))
        {
            return excluded;
        }

        excluded = IsExcluded(assembly);
        ExcludedAssemblyCache[assembly] = excluded;

        return excluded;
    }

    // For performance reasons, we are not supporting wildcards fully. We just need to use '.' at the end for now. We can use regular expressions
    // if in the future we need a more sophisticated wildcard support
    private static bool IsExcluded(string assembly)
    {
        foreach (var assemblyToSkip in AssemblyNamesToSkip)
        {
#if NETCOREAPP3_1_OR_GREATER
            if (assemblyToSkip.EndsWith('.'))
#else
            if (assemblyToSkip.EndsWith("."))
#endif
            {
                if (assembly.StartsWith(assemblyToSkip, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            else
            {
                if (assembly.Equals(assemblyToSkip, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
