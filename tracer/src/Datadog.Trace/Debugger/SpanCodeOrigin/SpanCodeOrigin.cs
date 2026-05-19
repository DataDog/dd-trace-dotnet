// <copyright file="SpanCodeOrigin.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using Datadog.Trace.Debugger.Symbols;
using Datadog.Trace.Logging;
using Datadog.Trace.Pdb;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Debugger.SpanCodeOrigin
{
    internal sealed class SpanCodeOrigin
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SpanCodeOrigin));

        // ConditionalWeakTable keys are weak, so a collectible AssemblyLoadContext can unload without being rooted by this cache.
        // Per-assembly Lazy<T> deduplicates concurrent first-touches on the *same* assembly while allowing *different* assemblies to scan in parallel.

        // Per-assembly analysis results (skip decision + sequence points), keyed weakly by Assembly.
        private readonly ConditionalWeakTable<Assembly, Lazy<AssemblyAnalysis>> _assemblyCache = new();

        // Cached factory delegate passed to ConditionalWeakTable.GetValue to avoid allocating a new delegate on each miss.
        private readonly ConditionalWeakTable<Assembly, Lazy<AssemblyAnalysis>>.CreateValueCallback _createAnalysisLazy;
        private readonly CodeOriginTags _tags;

        internal SpanCodeOrigin(DebuggerSettings settings)
        {
            Log.Information("Initializing Code Origin for Spans");
            Settings = settings;
            _tags = new CodeOriginTags(Settings.CodeOriginMaxUserFrames);
            _createAnalysisLazy = assembly => new Lazy<AssemblyAnalysis>(() => ComputeAnalysis(assembly));
        }

        internal DebuggerSettings Settings { get; }

        internal void SetCodeOriginForExitSpan(Span? span)
        {
            if (span?.Tags is WebTags { SpanKind: SpanKinds.Server })
            {
                // entry span
                Log.Debug("SetCodeOriginForExitSpan: Skipping server entry span {SpanID}. Code origin will be added later. Service {ServiceName}, Resource: {ResourceName}, Operation: {OperationName}", span.SpanId, span.ServiceName, span.ResourceName, span.OperationName);
                return;
            }

            if (ShouldSkipExitSpan())
            {
                return;
            }

            if (span == null)
            {
                Log.Debug("Can not add code origin for exit span when span is null");
                return;
            }

            if (span.GetTag(_tags.Type) != null)
            {
                Log.Debug("Span {SpanID} has already code origin tags. Resource: {ResourceName}, Operation: {OperationName}", span.SpanId, span.ResourceName, span.OperationName);
                return;
            }

            AddExitSpanTags(span);
        }

        private bool ShouldSkipExitSpan()
        {
            // Exit span code origin has been disabled since tracer version 3.28.0.
            // The call from Tracer.StartSpan was also removed to avoid per-span overhead.
            // When re-enabling, restore the call in Tracer.StartSpan and update SpanCodeOriginTests.ExitSpanTests.
            return true;
        }

        internal void SetCodeOriginForEntrySpan(Span? span, Type? type, MethodInfo? method)
        {
            if (span == null ||
                type == null ||
                method == null)
            {
                Log.Debug("Can not add code origin when one of the arguments is null");
                return;
            }

            if (span.GetTag(_tags.Type) != null)
            {
                Log.Debug("Span {SpanID} has already code origin tags. Resource: {ResourceName}, Operation: {OperationName}", span.SpanId, span.ResourceName, span.OperationName);
                return;
            }

            AddEntrySpanTags(span, type, method);
        }

        internal bool HasCodeOrigin(Span? span)
        {
            return span?.GetTag(_tags.Type) != null;
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
                var analysis = GetOrComputeAnalysis(assembly);
                if (analysis.ShouldSkip)
                {
                    return;
                }

                var sp = TryGetSequencePoint(analysis, method);

                // Add code origin tags to entry span
                // Adds 4 tags always (type, index, method, typename) + 3 tags if PDB available (file, line, column)
                // Size: ~210-300 bytes without PDB, ~250-500 bytes with PDB
                if (span.Tags is AspNetCoreTags aspNetCoreTags)
                {
                    aspNetCoreTags.CodeOriginType = "entry";
                    aspNetCoreTags.CodeOriginFrameIndex = "0";
                    aspNetCoreTags.CodeOriginFrameMethod = methodName;
                    aspNetCoreTags.CodeOriginFrameType = typeFullName;

                    if (sp.HasValue)
                    {
                        var cached = sp.Value;
                        aspNetCoreTags.CodeOriginFrameFile = cached.Url;
                        aspNetCoreTags.CodeOriginFrameLine = cached.Line;
                        aspNetCoreTags.CodeOriginFrameColumn = cached.Column;
                    }

                    return;
                }

                if (span.Tags is AspNetCoreSingleSpanTags aspNetCoreSingleSpanTags)
                {
                    aspNetCoreSingleSpanTags.CodeOriginType = "entry";
                    aspNetCoreSingleSpanTags.CodeOriginFrameIndex = "0";
                    aspNetCoreSingleSpanTags.CodeOriginFrameMethod = methodName;
                    aspNetCoreSingleSpanTags.CodeOriginFrameType = typeFullName;

                    if (sp.HasValue)
                    {
                        var cached = sp.Value;
                        aspNetCoreSingleSpanTags.CodeOriginFrameFile = cached.Url;
                        aspNetCoreSingleSpanTags.CodeOriginFrameLine = cached.Line;
                        aspNetCoreSingleSpanTags.CodeOriginFrameColumn = cached.Column;
                    }

                    return;
                }

                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug(
                        "Unexpected tags type for entry span {SpanID}: {TagsType}. Skipping code origin tags. Operation: {OperationName}",
                        property0: span.SpanId,
                        property1: span.Tags?.GetType().FullName ?? "<null>",
                        property2: span.OperationName);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to create entry span tag for {Span}. Resource: {ResourceName}, Operation: {OperationName}", span.SpanId, span.ResourceName, span.OperationName);
            }
        }

        // Design Decision: Read ALL detected endpoint sequence points upfront per assembly (one PDB open).
        //
        // Alternatives considered:
        // - Lazy per-method: Would reopen PDB once per unique endpoint (each open prefetches metadata + paying
        //   sequence-point parse cost). Total I/O grows linearly with unique endpoints called.
        // - Keep PDB open across calls: File handle and memory leaks, resource limits, complex lifecycle.
        // - Background/async population: Race conditions, thundering herd, harder testing.
        //
        // Trade-off: Slightly higher first-request latency for simplicity, predictability, and no resource leaks.
        // Memory cost: 50-200 endpoints x ~200 bytes = 10-40 KB per assembly.
        //
        // Per-assembly Lazy<T> serializes the scan for the *same* assembly only - concurrent first-touches on
        // *different* assemblies run in parallel (unlike the previous global write-lock design).
        //
        // ConditionalWeakTable.GetValue already performs the lock-free fast-path lookup internally
        // (TryGetValue first, then create-under-lock on miss), so calling GetValue directly is equivalent
        // to an explicit TryGetValue+GetValue split and saves one redundant lookup on miss.
        private AssemblyAnalysis GetOrComputeAnalysis(Assembly assembly)
        {
            return _assemblyCache.GetValue(assembly, _createAnalysisLazy).Value;
        }

        private AssemblyAnalysis ComputeAnalysis(Assembly assembly)
        {
            // This method is invoked by Lazy<AssemblyAnalysis> with the default
            // ExecutionAndPublication mode, which caches any exception the factory throws.
            // To prevent permanently poisoning the cache entry for an assembly, the entire
            // body is wrapped in a catch-all that falls back to AssemblyAnalysis.Skipped.
            // The two inner try/catches stay in place so we still get diagnostic-rich logs
            // for the two expected failure points (filter evaluation and PDB scan).
            try
            {
                bool shouldSkip;
                try
                {
                    // Single-file assemblies have no Assembly.Location. We still want the
                    // reflection-derived tags; PDB-backed file/line/column tags will be absent.
                    shouldSkip = AssemblyFilter.ShouldSkipAssembly(
                        assembly,
                        Settings.ThirdPartyDetectionExcludes,
                        Settings.ThirdPartyDetectionIncludes,
                        requireAssemblyLocation: false);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to evaluate assembly filter for {AssemblyName}", assembly.FullName);
                    return AssemblyAnalysis.Skipped;
                }

                if (shouldSkip)
                {
                    return AssemblyAnalysis.Skipped;
                }

                Dictionary<int, CachedSequencePoint>? sequencePoints = null;
                try
                {
                    // metadataOnly: true avoids PrefetchEntireImage, which would otherwise read the full DLL into memory.
                    // We only need MetadataReader (for type/method enumeration) and the PDB reader (for sequence points).
                    using var reader = DatadogMetadataReader.CreatePdbReader(assembly, metadataOnly: true);
                    if (reader is { IsPdbExist: true })
                    {
                        sequencePoints = new Dictionary<int, CachedSequencePoint>();
                        var consumer = new SequencePointTokenConsumer(reader, sequencePoints, assembly);
                        EndpointDetector.GetEndpointMethodTokens(reader, ref consumer);
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Error while getting endpoints for {AssemblyName} in location: {AssemblyLocation}", assembly.FullName, assembly.Location);
                    sequencePoints = null;
                }

                return new AssemblyAnalysis(shouldSkip: false, sequencePoints);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error analyzing assembly {AssemblyName}", assembly.FullName);
                return AssemblyAnalysis.Skipped;
            }
        }

        private CachedSequencePoint? TryGetSequencePoint(AssemblyAnalysis analysis, MethodInfo method)
        {
            if (analysis.SequencePoints is null)
            {
                return null;
            }

            int metadataToken;
            try
            {
                metadataToken = method.MetadataToken;
            }
            catch
            {
                // Some MethodInfo implementations do not support MetadataToken.
                return null;
            }

            if (analysis.SequencePoints.TryGetValue(metadataToken, out var sp))
            {
                return sp;
            }

            return null;
        }

        private void AddExitSpanTags(Span span)
        {
            var frames = ArrayPool<FrameInfo>.Shared.Rent(Settings.CodeOriginMaxUserFrames);
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
                if (frames != null)
                {
                    ArrayPool<FrameInfo>.Shared.Return(frames);
                }
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
            for (var walkIndex = 0; walkIndex < stackFrames.Length && count < Settings.CodeOriginMaxUserFrames; walkIndex++)
            {
                var frame = stackFrames[walkIndex];

                var assembly = frame?.GetMethod()?.DeclaringType?.Assembly;
                if (assembly == null)
                {
                    continue;
                }

                if (GetOrComputeAnalysis(assembly).ShouldSkip)
                {
                    continue;
                }

                frames[count++] = new FrameInfo(walkIndex, frame!);
            }

            return count;
        }

        private readonly record struct FrameInfo(int FrameIndex, StackFrame Frame);

        private readonly record struct CachedSequencePoint(string Url, string Line, string Column);

        private readonly struct SequencePointTokenConsumer : EndpointDetector.IEndpointMethodTokenConsumer
        {
            private readonly DatadogMetadataReader _reader;
            private readonly Dictionary<int, CachedSequencePoint> _sequencePoints;
            private readonly Assembly _assembly;

            public SequencePointTokenConsumer(DatadogMetadataReader reader, Dictionary<int, CachedSequencePoint> sequencePoints, Assembly assembly)
            {
                _reader = reader;
                _sequencePoints = sequencePoints;
                _assembly = assembly;
            }

            public void OnEndpointMethodToken(int token)
            {
                try
                {
                    var sequencePoint = _reader.GetMethodSourceLocation(token);
                    if (sequencePoint is { } sp)
                    {
                        // If we don't have a source URL, the sequence point isn't useful for code origin tags
                        // (line/column without a file doesn't provide actionable info).
                        if (StringUtil.IsNullOrEmpty(sp.URL))
                        {
                            return;
                        }

                        // Precompute per-assembly string values to avoid per-span ToString() allocations.
                        // Normalize paths to the same forward-slash form used by Dynamic Instrumentation.
                        var url = sp.URL.IndexOf('\\') >= 0 ? sp.URL.Replace('\\', '/') : sp.URL;
                        _sequencePoints.Add(
                            token,
                            new CachedSequencePoint(
                                url,
                                sp.StartLine.ToString(CultureInfo.InvariantCulture),
                                sp.StartColumn.ToString(CultureInfo.InvariantCulture)));
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to get sequence point for method token {Token} in assembly {AssemblyName}", property0: token, _assembly.FullName);
                }
            }
        }

        private sealed class AssemblyAnalysis
        {
            // Singleton for "skipped" assemblies: no sequence points to remember, just the skip flag.
            // Reuse one instance per filter decision to avoid an allocation per skipped assembly cache entry.
            internal static readonly AssemblyAnalysis Skipped = new(shouldSkip: true, sequencePoints: null);

            internal AssemblyAnalysis(bool shouldSkip, Dictionary<int, CachedSequencePoint>? sequencePoints)
            {
                ShouldSkip = shouldSkip;
                SequencePoints = sequencePoints;
            }

            internal bool ShouldSkip { get; }

            internal Dictionary<int, CachedSequencePoint>? SequencePoints { get; }
        }

        /// <summary>
        /// Avoid string concatenations and reduce GC pressure in hot path
        /// </summary>
        internal sealed class CodeOriginTags
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
