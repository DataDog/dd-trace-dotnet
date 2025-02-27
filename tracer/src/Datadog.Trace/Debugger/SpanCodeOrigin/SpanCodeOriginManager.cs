// <copyright file="SpanCodeOriginManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Diagnostics;
using Datadog.Trace.Debugger.Symbols;
using Datadog.Trace.Logging;

#if NETCOREAPP3_1_OR_GREATER
using System.Buffers;
#else
using Datadog.Trace.VendoredMicrosoftCode.System.Buffers;
#endif

namespace Datadog.Trace.Debugger.SpanCodeOrigin
{
    internal class SpanCodeOriginManager
    {
        private const string CodeOriginTag = "_dd.code_origin";

        private const string FramesPrefix = "frames";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SpanCodeOriginManager));

        private readonly DebuggerSettings _settings = LiveDebugger.Instance?.Settings ?? DebuggerSettings.FromDefaultSource();

        internal static SpanCodeOriginManager Instance { get; } = new();

        internal void SetCodeOrigin(Span? span)
        {
            if (span == null || !_settings.CodeOriginForSpansEnabled)
            {
                return;
            }

            AddExitSpanTag(span);
        }

        private void AddExitSpanTag(Span span)
        {
            var frames = ArrayPool<FrameInfo>.Shared.Rent(_settings.CodeOriginMaxUserFrames);
            try
            {
                var framesLength = PopulateUserFrames(frames);
                if (framesLength == 0)
                {
                    Log.Warning("No user frames were found");
                    return;
                }

                span.Tags.SetTag($"{CodeOriginTag}.type", "exit");
                for (var i = 0; i < framesLength; i++)
                {
                    ref var info = ref frames[i];

                    // PopulateUserFrames returns only frames that have method
                    var method = info.Frame.GetMethod()!;
                    var type = method.DeclaringType?.FullName ?? method.DeclaringType?.Name;
                    if (string.IsNullOrEmpty(type))
                    {
                        continue;
                    }

                    span.Tags.SetTag($"{CodeOriginTag}.{FramesPrefix}.{i}.index", info.FrameIndex.ToString());
                    span.Tags.SetTag($"{CodeOriginTag}.{FramesPrefix}.{i}.method", method.Name);
                    span.Tags.SetTag($"{CodeOriginTag}.{FramesPrefix}.{i}.type", type);

                    var fileName = info.Frame.GetFileName();
                    if (!string.IsNullOrEmpty(fileName))
                    {
                        // todo: should we normalize?
                        span.Tags.SetTag($"{CodeOriginTag}.{FramesPrefix}.{i}.file", fileName);
                    }

                    var line = info.Frame.GetFileLineNumber();
                    if (line > 0)
                    {
                        span.Tags.SetTag($"{CodeOriginTag}.{FramesPrefix}.{i}.line", line.ToString());
                    }

                    var column = info.Frame.GetFileColumnNumber();
                    if (column > 0)
                    {
                        span.Tags.SetTag($"{CodeOriginTag}.{FramesPrefix}.{i}.column", column.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to create exit span tag for {Span}", span.SpanId);
            }
            finally
            {
                if (frames != null!)
                {
                    ArrayPool<FrameInfo>.Shared.Return(frames);
                }
            }
        }

        private int PopulateUserFrames(FrameInfo[] frames)
        {
            var stackTrace = new StackTrace(fNeedFileInfo: true);
            var stackFrames = stackTrace.GetFrames();

            if (stackFrames == null!)
            {
                return 0;
            }

            var count = 0;
            for (var walkIndex = 0; walkIndex < stackFrames.Length && count < _settings.CodeOriginMaxUserFrames; walkIndex++)
            {
                var frame = stackFrames[walkIndex];

                var assembly = frame?.GetMethod()?.DeclaringType?.Module.Assembly;
                if (assembly == null)
                {
                    continue;
                }

                if (AssemblyFilter.ShouldSkipAssembly(assembly, _settings.ThirdPartyDetectionExcludes, _settings.ThirdPartyDetectionIncludes))
                {
                    // use cache when this will be merged: https://github.com/DataDog/dd-trace-dotnet/pull/6093
                    continue;
                }

                frames[count++] = new FrameInfo(walkIndex, frame!);
            }

            return count;
        }

        private readonly record struct FrameInfo(int FrameIndex, StackFrame Frame);
    }
}
