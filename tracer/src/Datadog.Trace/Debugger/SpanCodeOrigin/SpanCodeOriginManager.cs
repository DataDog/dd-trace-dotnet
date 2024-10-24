// <copyright file="SpanCodeOriginManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Symbols;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.VendoredMicrosoftCode.System.Buffers;

namespace Datadog.Trace.Debugger.SpanCodeOrigin
{
    internal class SpanCodeOriginManager
    {
        private const string CodeOriginTag = "_dd.code_origin";
        private static readonly DebuggerSettings Settings = LiveDebugger.Instance?.Settings ?? DebuggerSettings.FromDefaultSource();
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SpanCodeOriginManager));

        private static readonly HashSet<string> ExitSpanTypes =
        [
            SpanTypes.Http,
            SpanTypes.Sql,
            SpanTypes.Redis,
            SpanTypes.MongoDb,
            SpanTypes.DynamoDb,
            SpanTypes.Db
        ];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsEntrySpan(ISpan? span)
        {
            if (span == null)
            {
                return false;
            }

            if (span.Type == SpanTypes.Web)
            {
                return true;
            }

            if (IsEntryOperation(span.OperationName))
            {
                return true;
            }

            var kind = span?.GetTag(Tags.SpanKind);
            return kind is SpanKinds.Server or SpanKinds.Consumer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsExitSpan(ISpan span)
        {
            if (span.Type != null && ExitSpanTypes.Contains(span.Type))
            {
                return true;
            }

            if (IsExitOperation(span.OperationName))
            {
                return true;
            }

            var kind = span?.GetTag(Tags.SpanKind);
            return kind is SpanKinds.Client or SpanKinds.Producer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsEntryOperation(string? operationName)
        {
            if (string.IsNullOrEmpty(operationName))
            {
                return false;
            }

            return operationName?.StartsWith("aspnet.", StringComparison.OrdinalIgnoreCase) == true ||
                   operationName?.StartsWith("aspnetcore.", StringComparison.OrdinalIgnoreCase) == true ||
                   operationName?.StartsWith("grpc.server.", StringComparison.OrdinalIgnoreCase) == true ||
                   operationName?.StartsWith("webapi.", StringComparison.OrdinalIgnoreCase) == true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsExitOperation(string? operationName)
        {
            if (string.IsNullOrEmpty(operationName))
            {
                return false;
            }

            return operationName?.StartsWith("http.client.", StringComparison.OrdinalIgnoreCase) == true ||
                   operationName?.StartsWith("sql.", StringComparison.OrdinalIgnoreCase) == true ||
                   operationName?.StartsWith("redis.", StringComparison.OrdinalIgnoreCase) == true ||
                   operationName?.StartsWith("mongodb.", StringComparison.OrdinalIgnoreCase) == true ||
                   operationName?.StartsWith("rabbitmq.client.", StringComparison.OrdinalIgnoreCase) == true;
        }

        internal static void SetCodeOrigin(ISpan? span, ISpan rootSpan)
        {
            if (span == null || !Settings.CodeOriginForSpansEnabled)
            {
                return;
            }

            if (IsExitSpan(span))
            {
                CaptureExitSpanLocation(span, rootSpan);
            }
        }

        private static void CaptureExitSpanLocation(ISpan span, ISpan rootSpan)
        {
            var frames = ArrayPool<StackFrame>.Shared.Rent(Settings.CodeOriginMaxUserFrames);
            var probes = ArrayPool<SpanOriginProbe>.Shared.Rent(Settings.CodeOriginMaxUserFrames);
            var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);
            try
            {
                CaptureUserFrames(frames);
                sb.Append("{\"type\":\"").Append("exit").Append("\",\"frames\":[");

                for (int i = 0; i < frames.Length; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(',');
                    }

                    AppendFrame(sb, frames[i]);
                    var probe = CreateSpanOriginProbe(frames[i]);
                    if (probe != null)
                    {
                        probes[i] = probe;
                        rootSpan.SetTag(probe.Id, string.Empty);
                    }
                }

                sb.Append("]}");

                span.SetTag(CodeOriginTag, sb.ToString());

                LiveDebugger.Instance.UpdateAddedProbeInstrumentations(probes);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "");
            }
            finally
            {
                if (sb != null)
                {
                    StringBuilderCache.Release(sb);
                }

                if (frames != null)
                {
                    ArrayPool<StackFrame>.Shared.Return(frames, true);
                }

                if (probes != null)
                {
                    ArrayPool<SpanOriginProbe>.Shared.Return(probes, true);
                }
            }
        }

        private static SpanOriginProbe? CreateSpanOriginProbe(StackFrame frame)
        {
            var method = frame.GetMethod();
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

        private static void CaptureUserFrames(StackFrame[] frames)
        {
            var stackTrace = new StackTrace(true);
            var stackFrames = stackTrace.GetFrames();

            if (stackFrames == null)
            {
                return;
            }

            int count = 0;
            for (int i = 3; i < stackFrames.Length && count < Settings.CodeOriginMaxUserFrames; i++)
            {
                var frame = stackFrames[i];

                var assembly = frame.GetMethod()?.DeclaringType?.Module.Assembly;
                if (assembly == null)
                {
                    continue;
                }

                if (AssemblyFilter.ShouldSkipAssembly(assembly, LiveDebugger.Instance.Settings.ThirdPartyDetectionExcludes, LiveDebugger.Instance.Settings.ThirdPartyDetectionIncludes))
                {
                    // use cache when this will be merged: https://github.com/DataDog/dd-trace-dotnet/pull/6093
                    continue;
                }

                frames[count++] = frame;
            }
        }

        private static void AppendFrame(StringBuilder sb, StackFrame frame)
        {
            sb.Append('{');

            var fileName = frame.GetFileName();
            if (!string.IsNullOrEmpty(fileName))
            {
                sb.Append("\"file\":\"").Append(fileName.Replace("\\", "\\\\")).Append("\",");
                sb.Append("\"line\":").Append(frame.GetFileLineNumber());

                int column = frame.GetFileColumnNumber();
                if (column > 0)
                {
                    sb.Append(",\"column\":").Append(column);
                }
            }

            var method = frame.GetMethod();
            if (method != null)
            {
                sb.Append(",\"method\":\"").Append(method.Name).Append("\"");

                if (method.DeclaringType != null)
                {
                    sb.Append(",\"type\":\"").Append(method.DeclaringType.FullName).Append("\"");
                }
            }

            sb.Append('}');
        }
    }
}
