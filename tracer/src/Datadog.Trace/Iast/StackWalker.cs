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
using Datadog.Trace.Util;

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
    /// Captures the current stack and selects the frame a vulnerability should be reported at.
    /// </summary>
    /// <param name="captureSourceInfoForAllFrames">
    /// Whether to resolve source info (a PDB lookup per frame) for the whole stack. Only needed when the
    /// full stack is going to be reported; the selected frame keeps its file info either way.
    /// </param>
    /// <param name="stack">The captured stack, or <c>null</c> when the stack was not walked.</param>
    /// <param name="targetFrame">The frame to report, or <c>null</c> when no suitable frame was found.</param>
    /// <returns><c>false</c> when no location should be reported at all.</returns>
    // Not inlined, and the capture and the frame lookup live together on purpose: the index returned by
    // the capture only maps to a StackFrame created at the very same stack depth.
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool TryGetStackTraceAndFrame(bool captureSourceInfoForAllFrames, out StackTrace? stack, out StackFrame? targetFrame)
    {
        stack = null;
        targetFrame = null;

        // The runtime walks the stack on this very thread, so walking one that is already nearly
        // exhausted is what pushes a deeply recursive request over the guard page.
        if (!ExecutionStackGuard.HasSufficientStack())
        {
            return false;
        }

        var capture = new StackTrace(DefaultSkipFrames, captureSourceInfoForAllFrames);
        if (!TryGetFrameIndex(capture, out var index))
        {
            return false;
        }

        stack = capture;
        if (index >= 0)
        {
            targetFrame = captureSourceInfoForAllFrames
                              ? capture.GetFrame(index)
                              : new StackFrame(DefaultSkipFrames + index, true);
        }

        return true;
    }

    public static bool TryGetFrame(StackTrace stackTrace, out StackFrame? targetFrame)
    {
        targetFrame = null;
        if (!TryGetFrameIndex(stackTrace, out var index))
        {
            return false;
        }

        if (index >= 0)
        {
            targetFrame = stackTrace.GetFrame(index);
        }

        return true;
    }

    private static bool TryGetFrameIndex(StackTrace stackTrace, out int index)
    {
        index = -1;
        var frames = stackTrace.GetFrames() ?? [];
        for (var i = 0; i < frames.Length; i++)
        {
            var declaringType = frames[i]?.GetMethod()?.DeclaringType;

            foreach (var excludeType in ExcludeSpanGenerationTypes)
            {
                if (excludeType == declaringType?.FullName)
                {
                    return false;
                }
            }

            var assembly = declaringType?.Assembly.GetName().Name;
            if (assembly != null && !MustSkipAssembly(assembly))
            {
                index = i;
                return true;
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
