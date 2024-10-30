// <copyright file="SpanCodeOriginManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Symbols;
using Datadog.Trace.Logging;
using Datadog.Trace.VendoredMicrosoftCode.System.Buffers;
using Datadog.Trace.VendoredMicrosoftCode.System.Collections.Immutable;

namespace Datadog.Trace.Debugger.SpanCodeOrigin
{
    internal class SpanCodeOriginManager
    {
        private const string CodeOriginTag = "_dd.code_origin";
        private const string FramesPrefix = "frames";
        private static readonly DebuggerSettings Settings = LiveDebugger.Instance?.Settings ?? DebuggerSettings.FromDefaultSource();
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SpanCodeOriginManager));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void SetCodeOrigin(Span? span)
        {
            if (span == null || !Settings.CodeOriginForSpansEnabled)
            {
                return;
            }

            AddExitSpanTag(span);
        }

        private static void AddExitSpanTag(Span span)
        {
            var frames = ArrayPool<StackFrame>.Shared.Rent(Settings.CodeOriginMaxUserFrames);
            try
            {
                var framesLength = PopulateUserFrames(frames);
                if (framesLength == 0)
                {
                    Log.Warning("No user frames has founded");
                    return;
                }

                span.Tags.SetTag($"{CodeOriginTag}.type", "exit");

                for (int i = 0; i < framesLength; i++)
                {
                    var frame = frames[i];
                    var fileName = frame.GetFileName(); // todo: should we normalize?
                    if (!string.IsNullOrEmpty(fileName))
                    {
                        span.Tags.SetTag($"{CodeOriginTag}.{FramesPrefix}.{i}.file", fileName);
                    }

                    var line = frame.GetFileLineNumber();
                    if (line > 0)
                    {
                        span.Tags.SetTag($"{CodeOriginTag}.{FramesPrefix}.{i}.line", line.ToString());
                    }

                    int column = frame.GetFileColumnNumber();
                    if (column > 0)
                    {
                        span.Tags.SetTag($"{CodeOriginTag}.{FramesPrefix}.{i}.column", column.ToString());
                    }

                    // PopulateUserFrames returns only frames that have method
                    var method = frame.GetMethod()!;
                    var type = method.DeclaringType?.FullName ?? method.DeclaringType?.Name;
                    if (!string.IsNullOrEmpty(type))
                    {
                        span.Tags.SetTag($"{CodeOriginTag}.{FramesPrefix}.{i}.method", method.Name);
                        span.Tags.SetTag($"{CodeOriginTag}.{FramesPrefix}.{i}.type", type);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to create exit span tag for {Span}", span.SpanId);
            }
            finally
            {
                if (frames != null)
                {
                    ArrayPool<StackFrame>.Shared.Return(frames, true);
                }
            }
        }

        private static int PopulateUserFrames(StackFrame[] frames)
        {
            var stackTrace = new StackTrace(true);
            var stackFrames = stackTrace.GetFrames();

            if (stackFrames == null)
            {
                return 0;
            }

            var count = 0;
            for (int walkIndex = 2; walkIndex < stackFrames.Length && count < Settings.CodeOriginMaxUserFrames; walkIndex++)
            {
                var frame = stackFrames[walkIndex];

                var assembly = frame?.GetMethod()?.DeclaringType?.Module.Assembly;
                if (assembly == null)
                {
                    continue;
                }

                if (AssemblyFilter.ShouldSkipAssembly(assembly, LiveDebugger.Instance.Settings.ThirdPartyDetectionExcludes, LiveDebugger.Instance.Settings.ThirdPartyDetectionIncludes))
                {
                    // use cache when this will be merged: https://github.com/DataDog/dd-trace-dotnet/pull/6093
                    continue;
                }

                frames[count++] = frame!;
            }

            return count;
        }

        private static void InstrumentSpanOriginProbes(StackFrame[] frames, ISpan rootSpan)
        {
            var probes = ArrayPool<SpanOriginProbe>.Shared.Rent(Settings.CodeOriginMaxUserFrames);
            try
            {
                for (var i = 0; i < frames.Length; i++)
                {
                    var probe = CreateSpanOriginProbe(frames[i].GetMethod());
                    if (probe != null)
                    {
                        probes[i] = probe;
                        rootSpan.SetTag(probe.Id, string.Empty);
                    }
                }
            }
            finally
            {
                if (probes != null)
                {
                    ArrayPool<SpanOriginProbe>.Shared.Return(probes, true);
                }
            }

            LiveDebugger.Instance.UpdateAddedProbeInstrumentations(probes);
        }

        private static SpanOriginProbe? CreateSpanOriginProbe(MethodBase? method)
        {
            if (method == null)
            {
                return null;
            }

            var type = method.DeclaringType;
            if (type == null)
            {
                return null;
            }

            return new SpanOriginProbe
            {
                Id = Guid.NewGuid().ToString(),
                EvaluateAt = EvaluateAt.Exit,
                Where = new Where
                {
                    MethodName = method.Name,
                    TypeName = type.FullName ?? type.Name,
                },
            };
        }
    }
}
