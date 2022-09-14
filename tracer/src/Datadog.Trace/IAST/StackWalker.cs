// <copyright file="StackWalker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Diagnostics;
using System.Linq;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.IAST
{
    internal static class StackWalker
    {
        public static readonly string[] FileNamesToSkip = { "CallTargetInvoker", "BeginMethodHandler" };

        public static StackFrame GetFrame(string[] skipClasses)
        {
            var stackTrace = new StackTrace(2, true);

            foreach (var frame in stackTrace.GetFrames())
            {
                var fileName = frame.GetFileName();
                if (fileName != null && !FileNamesToSkip.Any(x => fileName.Contains(x)) && (skipClasses == null || !skipClasses.Any(x => fileName.Contains(x))))
                {
                    return frame;
                }
            }

            return null;
        }
    }
}
