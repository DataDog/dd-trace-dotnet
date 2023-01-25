// <copyright file="SnapshotSummary.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Debugger.Models;
using Datadog.Trace.Util;

namespace Datadog.Trace.Debugger.Snapshots
{
    internal class SnapshotSummary
    {
        public static string FormatMessage(Snapshot snapshot)
        {
            var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);
            sb.Append(FormatMethod(snapshot.Debugger.Snapshot.Stack, snapshot.Debugger.Snapshot.Probe.Location));
            sb.Append('(')
              .Append(FormatCapturedValues(GetArguments(snapshot)) ?? string.Empty)
              .Append(')');
            var returnValue = GetReturnValue(snapshot);
            if (returnValue != null)
            {
                sb.Append(": ").Append(returnValue);
            }

            var locals = FormatCapturedValues(GetLocals(snapshot)) ?? string.Empty;
            if (!string.IsNullOrEmpty(locals))
            {
                sb.Append(Environment.NewLine).Append(locals);
            }

            return StringBuilderCache.GetStringAndRelease(sb);
        }

        private static string FormatMethod(IReadOnlyList<StackInfo> frames, ProbeLocation probeLocation)
        {
            if (frames?.Count > 0)
            {
                // we first try to use the top frame on the stacktrace, if available
                return FormatMethod(frames[0]);
            }

            // if the stacktrace is not available we use the probe location
            return FormatMethod(probeLocation);
        }

        private static string FormatMethod(StackInfo stackFrame)
        {
            if (stackFrame.Function != null)
            {
                var classAndMethod = GetClassAndMethod(stackFrame.Function);
                if (classAndMethod.Count == 2)
                {
                    return classAndMethod[1] + "." + classAndMethod[0];
                }
                else if (classAndMethod.Count == 1)
                {
                    return classAndMethod[0];
                }
                else
                {
                    return stackFrame.Function;
                }
            }
            else
            {
                return stackFrame.FileName;
            }
        }

        private static string FormatMethod(ProbeLocation probeLocation)
        {
            if (probeLocation.Type != null && probeLocation.Method != null)
            {
                // parse out the class name
                var fqn = probeLocation.Type;
                var className = fqn.Substring(fqn.LastIndexOf('.') + 1);
                return className + "." + probeLocation.Method;
            }

            if (probeLocation.Method != null)
            {
                return probeLocation.Method;
            }

            return probeLocation.File + ":" + probeLocation.Lines;
        }

        private static List<string> GetClassAndMethod(string stackFrameFunction)
        {
            int firstParenIdx = stackFrameFunction.IndexOf('(');
            if (firstParenIdx >= 0)
            {
                stackFrameFunction = stackFrameFunction.Substring(0, firstParenIdx);
            }

            int lastDotIdx = stackFrameFunction.LastIndexOf('.');
            if (lastDotIdx == -1)
            {
                return new List<string> { stackFrameFunction };
            }

            var results = new List<string>();
            while (lastDotIdx > -1 && results.Count < 2)
            {
                var part = stackFrameFunction.Substring(lastDotIdx + 1);
                results.Add(part);
                stackFrameFunction = stackFrameFunction.Substring(0, lastDotIdx);
                lastDotIdx = stackFrameFunction.LastIndexOf('.');
            }

            return results;
        }

        private static CapturedValue[] GetArguments(Snapshot snapshot)
        {
            return snapshot.Debugger.Snapshot.Captures.Entry?.Arguments ??
                   snapshot.Debugger.Snapshot.Captures.Lines?.Captured?.Arguments;
        }

        private static string GetReturnValue(Snapshot snapshot)
        {
            return snapshot.Debugger.Snapshot.Captures.Return?.Locals?.FirstOrDefault(local => local.Name == "@return")?.Value;
        }

        private static CapturedValue[] GetLocals(Snapshot snapshot)
        {
            return GetLastCapture(snapshot)?.Locals;
        }

        private static string FormatCapturedValues(CapturedValue[] capturedValues)
        {
            if (capturedValues == null || capturedValues.Length == 0)
            {
                return null;
            }

            Array.Sort(capturedValues);
            return string.Join(", ", capturedValues.Select(cv => cv?.Name + "=" + cv?.Value));
        }

        private static CapturedContext GetLastCapture(Snapshot snapshot)
        {
            return snapshot.Debugger.Snapshot.Captures.Return ??
                   snapshot.Debugger.Snapshot.Captures.Lines?.Captured ??
                   snapshot.Debugger.Snapshot.Captures.Entry;
        }
    }
}
