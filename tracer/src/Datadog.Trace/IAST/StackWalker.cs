// <copyright file="StackWalker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Diagnostics;
using System.Linq;

namespace Datadog.Trace.Iast
{
    internal static class StackWalker
    {
        public static readonly string[] AssemblyNamesToSkip = { "Datadog.Trace", "System.Security.Cryptography.Primitives" };

        private const int DefaultSkipFrames = 2;

        public static StackFrame? GetFrame()
        {
            var stackTrace = new StackTrace(DefaultSkipFrames, true);

            foreach (var frame in stackTrace.GetFrames())
            {
                var assembly = frame?.GetMethod()?.DeclaringType?.Assembly?.GetName()?.Name;
                if (assembly != null && !AssemblyNamesToSkip.Contains(assembly))
                {
                    return frame;
                }
            }

            return null;
        }
    }
}
