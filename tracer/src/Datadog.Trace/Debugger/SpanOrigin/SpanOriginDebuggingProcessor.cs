// <copyright file="ExceptionDebuggingProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.ExceptionAutoInstrumentation;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Debugger.Instrumentation.Collections;
using Datadog.Trace.Debugger.Snapshots;
using Datadog.Trace.Logging;

#nullable enable
namespace Datadog.Trace.Debugger.SpanOrigin
{
    internal class SpanOriginDebuggingProcessor : IProbeProcessor
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SpanOriginDebuggingProcessor));

        internal SpanOriginDebuggingProcessor(string probeId)
        {
            ProbeId = probeId;
        }

        public string ProbeId { get; }

        public bool ShouldProcess(in ProbeData probeData)
        {
            return true;
        }

        public bool Process<TCapture>(ref CaptureInfo<TCapture> info, IDebuggerSnapshotCreator inSnapshotCreator, in ProbeData probeData)
        {
            var snapshotCreator = (SpanOriginSnapshotCreator)inSnapshotCreator;

            try
            {
                switch (info.MethodState)
                {
                    case MethodState.BeginLine:
                    case MethodState.BeginLineAsync:
                        var shadowStack = ShadowStackHolder.EnsureShadowStackEnabled();
                        var currentFrame = shadowStack.CurrentStackFrameNode;

                        if (currentFrame?.Method != info.Method)
                        {
                            // The current method on top of the stack doesn't match with the line number mapper. Happens in non-async methods due to race between line/method instrumentation.
                            // As a workaround, the line number is kept aside since we know a moment later, on the same thread, the BeginMethod will arrive and pick it up.
                            shadowStack.LineNumberWorkaroundStorage.Value = info.LineCaptureInfo.LineNumber;
                            return true;
                        }

                        currentFrame.LastExecutedLineNumber = info.LineCaptureInfo.LineNumber;
                        break;
                    case MethodState.EntryStart:
                    case MethodState.EntryAsync:
                        break;
                    case MethodState.ExitStart:
                    case MethodState.ExitStartAsync:
                        break;
                    case MethodState.EntryEnd:
                    case MethodState.EndLine:
                    case MethodState.EndLineAsync:
                        break;
                    case MethodState.ExitEnd:
                    case MethodState.ExitEndAsync:
                        break;
                    case MethodState.LogLocal:
                    case MethodState.LogArg:
                        break;
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "ExceptionDebuggingProcessor: Failed to process probe. Probe Id: {ProbeId}", ProbeId);
                return false;
            }

            return true;
        }

        public void LogException(Exception ex, IDebuggerSnapshotCreator inSnapshotCreator)
        {
        }

        public IProbeProcessor UpdateProbeProcessor(ProbeDefinition probe)
        {
            return this;
        }

        public IDebuggerSnapshotCreator CreateSnapshotCreator()
        {
            // ReSharper disable once InconsistentlySynchronizedField
            return new SpanOriginSnapshotCreator(ProbeId);
        }
    }
}
