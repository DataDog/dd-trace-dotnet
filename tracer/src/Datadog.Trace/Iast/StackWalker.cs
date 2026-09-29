// <copyright file="StackWalker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Datadog.Trace.AppSec;

namespace Datadog.Trace.Iast;

internal static class StackWalker
{
    private const int DefaultSkipFrames = 2;
    private static readonly string[] ExcludeSpanGenerationTypes =
    {
        "Datadog.Trace.Debugger.Helpers.StringExtensions",
        "Microsoft.AspNetCore.Razor.Language.StreamSourceDocument",
        "System.Security.IdentityHelper",
        "Npgsql.Internal.NpgsqlConnector"
    };

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

    private static readonly ConcurrentDictionary<string, bool> ExcludedAssemblyCache = new ConcurrentDictionary<string, bool>();

    /// <summary>
    /// Captures the stack a vulnerability should be reported from, or <c>null</c> when there is not
    /// enough stack left to walk it safely.
    /// </summary>
    // The runtime walks the stack on this very thread, so walking one that is already nearly
    // exhausted is what pushes a deeply recursive request over the guard page.
    // Not inlined because DefaultSkipFrames counts this method and its caller.
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static StackTrace? GetStackTrace()
    {
        return ExecutionStackGuard.HasSufficientStack() ? new StackTrace(DefaultSkipFrames, true) : null;
    }

    /// <summary>
    /// Picks the frame a vulnerability is reported at: the topmost non-excluded frame that has debug
    /// information (file and line), or the topmost non-excluded frame when none of them has it.
    /// <paramref name="identityFrame"/> is always the topmost non-excluded frame, so the vulnerability
    /// hash does not depend on which frames have debug information.
    /// </summary>
    public static bool TryGetFrame(StackTrace stackTrace, out StackFrame? targetFrame, out StackFrame? identityFrame)
    {
        targetFrame = null;
        identityFrame = null;
        var frames = stackTrace.GetFrames() ?? [];
        foreach (var frame in frames)
        {
            var hasDebugInfo = frame?.GetFileLineNumber() > 0;

            // once there is a fallback, only a frame with debug info can replace it, so don't pay
            // for resolving the assembly of the others
            if (targetFrame is not null && !hasDebugInfo)
            {
                continue;
            }

            var declaringType = frame?.GetMethod()?.DeclaringType;

            // only the frames above the first candidate decide whether the vulnerability is reported
            if (targetFrame is null)
            {
                foreach (var excludeType in ExcludeSpanGenerationTypes)
                {
                    if (excludeType == declaringType?.FullName)
                    {
                        return false;
                    }
                }
            }

            var assembly = declaringType?.Assembly.GetName().Name;
            if (assembly != null && !MustSkipAssembly(assembly))
            {
                identityFrame ??= frame;
                if (hasDebugInfo)
                {
                    targetFrame = frame;
                    break;
                }

                targetFrame ??= frame;
            }
        }

        return true;
    }

    public static bool MustSkipAssembly(string assembly)
    {
        if (ExcludedAssemblyCache.TryGetValue(assembly, out bool excluded))
        {
            return excluded;
        }

        excluded = IsExcluded(assembly);
        ExcludedAssemblyCache[assembly] = excluded;

        return excluded;

        // For performance reasons, we are not supporting wildcards fully. We just need to use '.' at the end for now. We can use regular expressions
        // if in the future we need a more sophisticated wildcard support
        static bool IsExcluded(string assembly)
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
}
