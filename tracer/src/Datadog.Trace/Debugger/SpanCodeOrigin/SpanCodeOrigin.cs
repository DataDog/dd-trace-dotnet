// <copyright file="SpanCodeOrigin.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Diagnostics;
using System.Reflection;
using Datadog.Trace.Debugger.Caching;
using Datadog.Trace.Debugger.Configurations;
using Datadog.Trace.Debugger.Symbols;
using Datadog.Trace.Logging;
using Datadog.Trace.Pdb;
using Datadog.Trace.Tagging;
using Datadog.Trace.VendoredMicrosoftCode.System.Buffers;
using Datadog.Trace.VendoredMicrosoftCode.System.Collections.Immutable;

namespace Datadog.Trace.Debugger.SpanCodeOrigin
{
    internal class SpanCodeOrigin
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SpanCodeOrigin));

        private readonly ConcurrentAdaptiveCache<Assembly, AssemblyPdbInfo?> _assemblyPdbCache = new();
        private readonly DebuggerSettings _settings;
        private readonly CodeOriginTags _tags;

        internal SpanCodeOrigin()
        {
            _settings = LiveDebugger.Instance?.Settings ?? DebuggerSettings.FromDefaultSource();
            _tags = new CodeOriginTags(_settings.CodeOriginMaxUserFrames);
        }

        internal static SpanCodeOriginManager Instance { get; } = new();
		
		public void UpdateConfiguration(DebuggerSettings newSettings)
        {
            _settings = newSettings;
        }

        internal void SetCodeOriginForExitSpan(Span? span)
        {
            if (span == null)
            {
                return;
            }

            if (span.Tags is WebTags { SpanKind: SpanKinds.Server })
            {
                // entry span
                Log.Debug("Skipping span {SpanID}, we will add entry span code origin for it later. Resource: {ResourceName}, Operation: {OperationName}", span.SpanId, span.ResourceName, span.OperationName);
                return;
            }

            if (span.GetTag(_tags.Type) != null)
            {
                Log.Debug("Span {SpanID} has already code origin tags. Resource: {ResourceName}, Operation: {OperationName}", span.SpanId, span.ResourceName, span.OperationName);
                return;
            }

            AddExitSpanTags(span);
        }

        internal void SetCodeOriginForEntrySpan(Span? span, Type? type, MethodInfo? method)
        {
            if (span == null ||
                type == null ||
                method == null ||
                !_settings.CodeOriginForSpansEnabled)
            {
                return;
            }

            if (span.GetTag(_tags.Type) != null)
            {
                Log.Debug("Span {SpanID} has already code origin tags. Resource: {ResourceName}, Operation: {OperationName}", span.SpanId, span.ResourceName, span.OperationName);
                return;
            }

            AddEntrySpanTags(span, type, method);
        }

        private void AddEntrySpanTags(Span span, Type type, MethodInfo method)
        {
            try
            {
                var methodName = method.Name;
                var typeFullName = type.FullName;
                if (typeFullName == null)
                {
                    return;
                }

                var assembly = type.Assembly;
                if (AssemblyFilter.ShouldSkipAssembly(assembly, this._settings.SymDbThirdPartyDetectionExcludes, this._settings.SymDbThirdPartyDetectionIncludes))
                {
                    // use cache when this will be merged: https://github.com/DataDog/dd-trace-dotnet/pull/6093
                    return;
                }

                span.Tags.SetTag(_tags.Type, "entry");
                span.Tags.SetTag(_tags.Index[0], "0");
                span.Tags.SetTag(_tags.Method[0], methodName);
                span.Tags.SetTag(_tags.TypeName[0], typeFullName);

                var sp = GetPdbInfo(assembly, method!);
                if (sp != null)
                {
                    span.Tags.SetTag(_tags.File[0], sp.Value.URL);
                    span.Tags.SetTag(_tags.Line[0], sp.Value.StartLine.ToString());
                    span.Tags.SetTag(_tags.Column[0], sp.Value.StartColumn.ToString());
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to create entry span tag for {Span}. Resource: {ResourceName}, Operation: {OperationName}", span.SpanId, span.ResourceName, span.OperationName);
            }
        }

        private DatadogMetadataReader.DatadogSequencePoint? GetPdbInfo(Assembly assembly, MethodInfo method)
        {
            var pdbInfo = _assemblyPdbCache.GetOrAdd(
                assembly,
                asm =>
                {
                    using var reader = DatadogMetadataReader.CreatePdbReader(asm);
                    if (reader is not { IsPdbExist: true })
                    {
                        return null;
                    }

                    try
                    {
                        var endpointMethodTokens = EndpointDetector.GetEndpointMethodTokens(reader);

                        // Build dictionary of sequence points only for endpoint methods
                        var builder = ImmutableDictionary.CreateBuilder<int, DatadogMetadataReader.DatadogSequencePoint?>();

                        foreach (var token in endpointMethodTokens)
                        {
                            try
                            {
                                var sequencePoint = reader.GetMethodSourceLocation(token);
                                builder.Add(token, sequencePoint);
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, "Failed to get sequence point for method token {Token} in assembly {AssemblyName}", property0: token, asm.FullName);
                            }
                        }

                        return new AssemblyPdbInfo(builder.ToImmutable());
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error while getting endpoints for assembly: {AssemblyName}", asm.Location);
                        return null;
                    }
                });

            return pdbInfo?.MethodSequencePoints.GetValueOrDefault<DatadogMetadataReader.DatadogSequencePoint?>(method.MetadataToken);
        }

        private void AddExitSpanTags(Span span)
        {
            var frames = ArrayPool<FrameInfo>.Shared.Rent(settings.CodeOriginMaxUserFrames);
            try
            {
                var framesLength = PopulateUserFrames(frames);
                if (framesLength == 0)
                {
                    return;
                }

                for (var i = 0; i < framesLength; i++)
                {
                    ref var info = ref frames[i];

                    // PopulateUserFrames returns only frames that have method
                    var method = info.Frame.GetMethod()!;
                    var typeName = method.DeclaringType?.FullName ?? method.DeclaringType?.Name;
                    if (string.IsNullOrEmpty(typeName))
                    {
                        continue;
                    }

                    span.Tags.SetTag(_tags.Type, "exit");
                    span.Tags.SetTag(_tags.Index[i], info.FrameIndex.ToString());
                    span.Tags.SetTag(_tags.Method[i], method.Name);
                    span.Tags.SetTag(_tags.TypeName[i], typeName);

                    var fileName = info.Frame.GetFileName();
                    if (string.IsNullOrEmpty(fileName))
                    {
                        continue;
                    }

                    span.Tags.SetTag(_tags.File[i], fileName);

                    var line = info.Frame.GetFileLineNumber();
                    span.Tags.SetTag(_tags.Line[i], line.ToString());

                    var column = info.Frame.GetFileColumnNumber();
                    span.Tags.SetTag(_tags.Column[i], column.ToString());
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to create exit span tag for {Span}. Resource: {ResourceName}, Operation: {OperationName}", span.SpanId, span.ResourceName, span.OperationName);
            }
            finally
            {
                ArrayPool<FrameInfo>.Shared.Return(frames);
            }
        }

        private int PopulateUserFrames(FrameInfo[] frames)
        {
            var stackTrace = new StackTrace(fNeedFileInfo: true);
            var stackFrames = stackTrace.GetFrames();

            if (stackFrames == null)
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

        private sealed class AssemblyPdbInfo(ImmutableDictionary<int, DatadogMetadataReader.DatadogSequencePoint?> sequencePoints)
        {
            public ImmutableDictionary<int, DatadogMetadataReader.DatadogSequencePoint?> MethodSequencePoints { get; } = sequencePoints;
        }

        /// <summary>
        /// avoid string concatenations and reduce GC pressure in hot path
        /// </summary>
        internal class CodeOriginTags
        {
            private const string CodeOriginTag = "_dd.code_origin";
            private const string FramesPrefix = "frames";

#pragma warning disable SA1401
            internal readonly string Type = $"{CodeOriginTag}.type";
            internal readonly string[] Index;
            internal readonly string[] Method;
            internal readonly string[] TypeName;
            internal readonly string[] File;
            internal readonly string[] Line;
            internal readonly string[] Column;
#pragma warning restore SA1401

            internal CodeOriginTags(int maxFrames)
            {
                Index = new string[maxFrames];
                Method = new string[maxFrames];
                TypeName = new string[maxFrames];
                File = new string[maxFrames];
                Line = new string[maxFrames];
                Column = new string[maxFrames];

                for (var i = 0; i < maxFrames; i++)
                {
                    Index[i] = $"{CodeOriginTag}.{FramesPrefix}.{i}.index";
                    Method[i] = $"{CodeOriginTag}.{FramesPrefix}.{i}.method";
                    TypeName[i] = $"{CodeOriginTag}.{FramesPrefix}.{i}.type";
                    File[i] = $"{CodeOriginTag}.{FramesPrefix}.{i}.file";
                    Line[i] = $"{CodeOriginTag}.{FramesPrefix}.{i}.line";
                    Column[i] = $"{CodeOriginTag}.{FramesPrefix}.{i}.column";
                }
            }
        }
    }
}
