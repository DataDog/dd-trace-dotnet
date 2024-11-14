// <copyright file="SpanCodeOriginManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using Datadog.Trace.Debugger.Symbols;
using Datadog.Trace.Logging;
using Datadog.Trace.Pdb;
using Datadog.Trace.Tagging;
using Datadog.Trace.VendoredMicrosoftCode.System.Buffers;

namespace Datadog.Trace.Debugger.SpanCodeOrigin
{
    internal class SpanCodeOriginManager
    {
        private const string CodeOriginTag = "_dd.code_origin";

        private const string FramesPrefix = "frames";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SpanCodeOriginManager));

        private readonly DebuggerSettings _settings = LiveDebugger.Instance?.Settings ?? DebuggerSettings.FromDefaultSource();

        // replace cache when this will be merged: https://github.com/DataDog/dd-trace-dotnet/pull/6093
        private readonly ConcurrentDictionary<string, DatadogMetadataReader.DatadogSequencePoint?> _typeToFileName = new();

        internal static SpanCodeOriginManager Instance { get; } = new();

        internal void SetCodeOriginForExitSpan(Span? span)
        {
            if (span == null ||
                !_settings.CodeOriginForSpansEnabled)
            {
                return;
            }

            if (span.Tags is WebTags { SpanKind: SpanKinds.Server })
            {
                // entry span
                Log.Debug("Skipping span {SpanID}, we will add entry span code origin for it later", span.SpanId);
                return;
            }

            if (span.GetTag($"{CodeOriginTag}.type") != null)
            {
                Log.Debug("Span {SpanID} has already code origin tags", span.SpanId);
                return;
            }

            AddExitSpanTags(span);
        }

        internal void SetCodeOriginForEntrySpan(Span? span, Type? type, MethodInfo? method)
        {
            if (span == null || !_settings.CodeOriginForSpansEnabled)
            {
                return;
            }

            if (span.GetTag($"{CodeOriginTag}.type") != null)
            {
                Log.Debug("Span {SpanID} has already code origin tags", span.SpanId);
                return;
            }

            AddEntrySpanTags(span, type, method);
        }

        private void AddEntrySpanTags(Span span, Type? type, MethodInfo? method)
        {
            var methodName = method?.Name;
            var typeFullName = type?.FullName;
            if (methodName == null || typeFullName == null)
            {
                return;
            }

            var assembly = type!.Assembly;
            if (AssemblyFilter.ShouldSkipAssembly(assembly, _settings.ThirdPartyDetectionExcludes, _settings.ThirdPartyDetectionIncludes))
            {
                // use cache when this will be merged: https://github.com/DataDog/dd-trace-dotnet/pull/6093
                return;
            }

            var sp = this._typeToFileName.GetOrAdd(typeFullName, s => GetFilePath(assembly, method!));
            if (sp != null)
            {
                span.Tags.SetTag($"{CodeOriginTag}.{FramesPrefix}.{0}.index", 0.ToString());
                span.Tags.SetTag($"{CodeOriginTag}.{FramesPrefix}.{0}.method", methodName);
                span.Tags.SetTag($"{CodeOriginTag}.{FramesPrefix}.{0}.type", typeFullName);

                span.Tags.SetTag($"{CodeOriginTag}.{FramesPrefix}.{0}.file", sp.Value.URL);
                span.Tags.SetTag($"{CodeOriginTag}.{FramesPrefix}.{0}.line", sp.Value.StartLine.ToString());
                span.Tags.SetTag($"{CodeOriginTag}.{FramesPrefix}.{0}.line", sp.Value.StartColumn.ToString());
                span.Tags.SetTag($"{CodeOriginTag}.type", "entry");
            }
        }

        private DatadogMetadataReader.DatadogSequencePoint? GetFilePath(Assembly assembly, MethodInfo method)
        {
            using var reader = DatadogMetadataReader.CreatePdbReader(assembly);
            if (reader is { IsPdbExist: true })
            {
                return reader.GetMethodSourceLocation(method!.MetadataToken);
            }

            return null;
        }

        private void AddExitSpanTags(Span span)
        {
            var frames = ArrayPool<FrameInfo>.Shared.Rent(_settings.CodeOriginMaxUserFrames);
            try
            {
                var framesLength = PopulateUserFrames(frames);
                if (framesLength == 0)
                {
                    return;
                }

                bool tagAdded = false;
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

                    var fileName = info.Frame.GetFileName();
                    if (!string.IsNullOrEmpty(fileName))
                    {
                        continue;
                    }

                    span.Tags.SetTag($"{CodeOriginTag}.{FramesPrefix}.{i}.index", info.FrameIndex.ToString());
                    span.Tags.SetTag($"{CodeOriginTag}.{FramesPrefix}.{i}.method", method.Name);
                    span.Tags.SetTag($"{CodeOriginTag}.{FramesPrefix}.{i}.type", type);

                    span.Tags.SetTag($"{CodeOriginTag}.{FramesPrefix}.{i}.file", fileName);

                    var line = info.Frame.GetFileLineNumber();
                    span.Tags.SetTag($"{CodeOriginTag}.{FramesPrefix}.{i}.line", line.ToString());

                    var column = info.Frame.GetFileColumnNumber();
                    span.Tags.SetTag($"{CodeOriginTag}.{FramesPrefix}.{i}.column", column.ToString());
                    tagAdded = true;
                }

                if (tagAdded)
                {
                    span.Tags.SetTag($"{CodeOriginTag}.type", "exit");
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

                var assembly = frame?.GetMethod()?.DeclaringType?.Assembly;
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
