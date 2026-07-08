// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace Samples.LiveDebuggerPoc.Viewer;

internal static class Program
{
    private static int Main(string[] args)
    {
        var path = args.FirstOrDefault(arg => !arg.StartsWith("--", StringComparison.Ordinal));
        if (string.IsNullOrWhiteSpace(path))
        {
            System.Console.Error.WriteLine("Usage: Samples.LiveDebuggerPoc.Viewer <flow-events.dflp> [--html [output.html]] [--site <dd-site>] [--apm-base-url <url>]");
            return 1;
        }

        var file = FlowEventReader.Read(path);
        if (file.Events.Length == 0)
        {
            System.Console.WriteLine("No flow events found.");
            return 0;
        }

        var report = FlowReport.Build(file, path);
        RenderConsole(report);

        var htmlIndex = Array.IndexOf(args, "--html");
        if (htmlIndex >= 0)
        {
            var outputPath = htmlIndex + 1 < args.Length && !args[htmlIndex + 1].StartsWith("--", StringComparison.Ordinal)
                                 ? args[htmlIndex + 1]
                                 : Path.ChangeExtension(path, ".html");
            var apmBaseUrl = ResolveApmBaseUrl(args);
            File.WriteAllText(outputPath, HtmlReportRenderer.Render(report, apmBaseUrl), Encoding.UTF8);
            System.Console.WriteLine();
            System.Console.WriteLine("HTML report: " + outputPath);
            System.Console.WriteLine("APM links:   " + apmBaseUrl);
        }

        return 0;
    }

    private static string ResolveApmBaseUrl(string[] args)
    {
        var explicitUrl = GetOption(args, "--apm-base-url");
        if (!string.IsNullOrWhiteSpace(explicitUrl))
        {
            return explicitUrl.Trim().TrimEnd('/');
        }

        var site = GetOption(args, "--site");
        if (string.IsNullOrWhiteSpace(site))
        {
            site = Environment.GetEnvironmentVariable("DD_SITE");
        }

        if (string.IsNullOrWhiteSpace(site))
        {
            site = "datadoghq.com";
        }

        site = site.Trim().TrimEnd('/');
        site = site.Replace("https://", string.Empty, StringComparison.OrdinalIgnoreCase)
                   .Replace("http://", string.Empty, StringComparison.OrdinalIgnoreCase);

        // Region-specific sites (us3., us5., ap1., ap2., ...) already resolve to the app host;
        // the global sites (datadoghq.com, datadoghq.eu, ddog-gov.com) need the "app." prefix.
        var firstLabel = site.Split('.')[0];
        var hasRegionPrefix = firstLabel.Length >= 3
                              && (firstLabel.StartsWith("us", StringComparison.OrdinalIgnoreCase) || firstLabel.StartsWith("ap", StringComparison.OrdinalIgnoreCase))
                              && char.IsDigit(firstLabel[firstLabel.Length - 1]);
        var host = hasRegionPrefix ? site : "app." + site;
        return "https://" + host;
    }

    private static string GetOption(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        if (index >= 0 && index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            return args[index + 1];
        }

        return null;
    }

    private static void RenderConsole(FlowReport report)
    {
        System.Console.WriteLine("Live Debugger Flow Recorder");
        System.Console.WriteLine(report.CaptureName + " | " + report.Events.Length + " events | " + report.Frames.Count + " frames | " + report.AsyncOperations.Count + " async operations | " + report.Operations.Count + " recorder operations");
        System.Console.WriteLine();

        foreach (var operation in report.Operations.OrderBy(operation => operation.OperationId))
        {
            System.Console.WriteLine("Operation " + operation.OperationId + " trigger=" + operation.TriggerReason + " root=" + operation.Root);
            if (operation.TraceId is not null)
            {
                System.Console.WriteLine("  Trace: " + operation.TraceId + ", root span: " + operation.RootSpanId + ", active span: " + operation.ActiveSpanId);
            }
        }

        if (report.Operations.Count > 0)
        {
            System.Console.WriteLine();
        }

        foreach (var marker in report.Markers.OrderBy(marker => marker.Timestamp))
        {
            System.Console.WriteLine(marker.Kind + ": " + marker.Reason + " operation=" + marker.OperationId + " flow=" + marker.FlowId);
        }

        if (report.Markers.Count > 0)
        {
            System.Console.WriteLine();
        }

        foreach (var flow in report.Flows.OrderBy(flow => flow.FlowId))
        {
            System.Console.WriteLine("Flow " + flow.FlowId + " (" + Format(flow.DurationMs) + " ms, " + flow.Events.Length + " events)");
            if (flow.TraceId is not null)
            {
                System.Console.WriteLine("  Trace: " + flow.TraceId + ", root span: " + flow.RootSpanId + ", active span: " + flow.ActiveSpanId);
            }

            foreach (var frame in flow.RootFrames.OrderBy(frame => frame.StartTimestamp))
            {
                RenderConsoleFrame(report, frame, indent: 1);
            }
        }

        if (report.AsyncOperations.Count > 0)
        {
            System.Console.WriteLine();
            System.Console.WriteLine("Async logical operations");
            foreach (var edge in report.AsyncEdges.OrderBy(edge => edge.Timestamp))
            {
                System.Console.WriteLine("  edge " + edge.ParentFlowId + " -> " + edge.ChildFlowId + " via " + edge.MethodDisplayName);
            }

            foreach (var operation in report.AsyncOperations.OrderBy(operation => operation.StartTimestamp))
            {
                var stepText = operation.Steps.Count == 1 ? " step" : " steps";
                System.Console.WriteLine("  - " + operation.LogicalName + " flow=" + operation.FlowId + " " + operation.Steps.Count + stepText + " duration=" + Format(operation.DurationMs) + "ms");
            }
        }
    }

    private static void RenderConsoleFrame(FlowReport report, Frame frame, int indent)
    {
        var prefix = new string(' ', indent * 2);
        var exception = frame.ExceptionTypeId == 0 ? string.Empty : " exception=" + (frame.ExceptionType ?? ("type-id#" + frame.ExceptionTypeId));
        System.Console.WriteLine(prefix + "- " + frame.DisplayName + " frame=" + frame.FrameId + " duration=" + Format(frame.DurationMs) + "ms" + exception);
        if (!string.IsNullOrWhiteSpace(frame.ExceptionMessage))
        {
            System.Console.WriteLine(prefix + "  ! " + frame.ExceptionMessage);
        }

        foreach (var value in frame.Values)
        {
            var reason = value.NotCaptured == FlowNotCapturedReason.None ? string.Empty : " (" + value.NotCaptured + ")";
            System.Console.WriteLine(prefix + "  value " + value.Phase + " " + value.Kind + " " + value.Name + ": " + value.Value + reason + " [" + value.TypeName + "]");
        }

        foreach (var child in report.GetChildren(frame).OrderBy(child => child.StartTimestamp))
        {
            RenderConsoleFrame(report, child, indent + 1);
        }
    }

    private static string TryGetAsyncLogicalName(IReadOnlyDictionary<int, string> methodNames, int methodMetadataIndex)
    {
        if (!methodNames.TryGetValue(methodMetadataIndex, out var displayName))
        {
            return null;
        }

        const string stateMachinePrefix = "+<";
        const string standaloneStateMachinePrefix = "<";
        const string stateMachineSuffix = ">d__";
        const string moveNextSuffix = ".MoveNext";

        var prefixIndex = displayName.IndexOf(stateMachinePrefix, StringComparison.Ordinal);
        if (prefixIndex < 0 || !displayName.EndsWith(moveNextSuffix, StringComparison.Ordinal))
        {
            if (displayName.StartsWith(standaloneStateMachinePrefix, StringComparison.Ordinal) && displayName.EndsWith(moveNextSuffix, StringComparison.Ordinal))
            {
                var standaloneSuffixIndex = displayName.IndexOf(stateMachineSuffix, standaloneStateMachinePrefix.Length, StringComparison.Ordinal);
                if (standaloneSuffixIndex >= 0)
                {
                    return displayName.Substring(standaloneStateMachinePrefix.Length, standaloneSuffixIndex - standaloneStateMachinePrefix.Length);
                }
            }

            return displayName.EndsWith("Async", StringComparison.Ordinal) ? displayName : null;
        }

        var methodNameStart = prefixIndex + stateMachinePrefix.Length;
        var suffixIndex = displayName.IndexOf(stateMachineSuffix, methodNameStart, StringComparison.Ordinal);
        if (suffixIndex < 0)
        {
            return null;
        }

        var declaringType = displayName.Substring(0, prefixIndex);
        var methodName = displayName.Substring(methodNameStart, suffixIndex - methodNameStart);
        return declaringType + "." + methodName;
    }

    private static string ShortName(string displayName)
    {
        var logicalName = TryGetAsyncLogicalName(new Dictionary<int, string> { [0] = displayName }, 0);
        if (logicalName is not null)
        {
            displayName = logicalName;
        }

        var index = displayName.LastIndexOf('.');
        return index >= 0 && index + 1 < displayName.Length ? displayName.Substring(index + 1) : displayName;
    }

    private static double ToMilliseconds(long elapsedTimestamp)
    {
        return elapsedTimestamp * 1000.0 / Stopwatch.Frequency;
    }

    private static string Format(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

        private static string FormatTraceId(ulong traceIdUpper, ulong traceIdLower)
        {
            return traceIdLower != 0 || traceIdUpper != 0
                       ? traceIdUpper.ToString("x16", CultureInfo.InvariantCulture) + traceIdLower.ToString("x16", CultureInfo.InvariantCulture)
                       : null;
        }

    private sealed class FlowReport
    {
        private readonly Dictionary<FrameKey, List<Frame>> _children;
        private readonly Dictionary<FrameKey, Frame> _frameByKey;

        private FlowReport(string capturePath, FlowEventFile file, List<FlowSummary> flows, List<Frame> frames, List<AsyncOperation> asyncOperations, List<AsyncEdge> asyncEdges, List<OperationSummary> operations, List<CaptureMarker> markers, Dictionary<FrameKey, List<Frame>> children, Dictionary<FrameKey, Frame> frameByKey, List<Frame> exceptionFrames)
        {
            CapturePath = capturePath;
            CaptureDisplayPath = ToDisplayPath(capturePath);
            CaptureName = Path.GetFileName(capturePath);
            Events = file.Events;
            Methods = file.Methods;
            Flows = flows;
            Frames = frames;
            AsyncOperations = asyncOperations;
            AsyncEdges = asyncEdges;
            Operations = operations;
            Markers = markers;
            _children = children;
            _frameByKey = frameByKey;
            ExceptionFrames = exceptionFrames;
        }

        public string CapturePath { get; }

        public string CaptureDisplayPath { get; }

        public string CaptureName { get; }

        public FlowEvent[] Events { get; }

        public FlowMethodMetadata[] Methods { get; }

        public List<FlowSummary> Flows { get; }

        public List<Frame> Frames { get; }

        public List<AsyncOperation> AsyncOperations { get; }

        public List<AsyncEdge> AsyncEdges { get; }

        public List<OperationSummary> Operations { get; }

        public List<CaptureMarker> Markers { get; }

        public List<Frame> ExceptionFrames { get; }

        public static FlowReport Build(FlowEventFile file, string capturePath)
        {
            var methodNames = file.Methods.ToDictionary(method => method.MethodMetadataIndex, method => method.DisplayName);
            var frames = BuildFrames(file, methodNames);
            var operations = BuildOperations(file, frames);
            ApplyOperationCorrelation(frames, operations);
            var children = frames.GroupBy(frame => new FrameKey(frame.FlowId, frame.ParentFrameId))
                                 .ToDictionary(group => group.Key, group => group.OrderBy(frame => frame.StartTimestamp).ToList());
            var frameByKey = frames.ToDictionary(frame => new FrameKey(frame.FlowId, frame.FrameId));
            var exceptionFrames = frames.Where(frame => frame.ExceptionType is not null || frame.ExceptionTypeId != 0)
                                        .OrderBy(frame => frame.StartTimestamp)
                                        .ToList();
            var flows = BuildFlows(file.Events, frames, operations);
            var asyncOperations = BuildAsyncOperations(frames, methodNames);
            var asyncEdges = BuildAsyncEdges(file.Events, methodNames);
            var markers = BuildMarkers(file);
            LinkAsyncOperations(asyncOperations, asyncEdges);
            return new FlowReport(capturePath, file, flows, frames, asyncOperations, asyncEdges, operations, markers, children, frameByKey, exceptionFrames);
        }

        private static string ToDisplayPath(string capturePath)
        {
            if (string.IsNullOrWhiteSpace(capturePath))
            {
                return capturePath;
            }

            try
            {
                var fullPath = Path.GetFullPath(capturePath);
                var repoRoot = FindRepositoryRoot(AppContext.BaseDirectory);
                if (repoRoot is null)
                {
                    return capturePath;
                }

                var repoParent = Directory.GetParent(repoRoot)?.FullName;
                if (repoParent is null)
                {
                    return capturePath;
                }

                var relativePath = Path.GetRelativePath(repoParent, fullPath);
                return relativePath.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relativePath)
                           ? capturePath
                           : relativePath;
            }
            catch (Exception)
            {
                return capturePath;
            }
        }

        private static string FindRepositoryRoot(string startDirectory)
        {
            var directory = new DirectoryInfo(startDirectory);
            while (directory is not null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, ".git")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            return null;
        }

        public IReadOnlyList<Frame> GetChildren(Frame frame)
        {
            return _children.TryGetValue(new FrameKey(frame.FlowId, frame.FrameId), out var children) ? children : Array.Empty<Frame>();
        }

        // Root -> throwing frame chain, reconstructed by walking ParentFrameId within the same flow.
        public IReadOnlyList<Frame> GetCallStack(Frame frame)
        {
            var chain = new List<Frame>();
            var current = frame;
            var guard = 0;
            while (current is not null && guard++ < 256)
            {
                chain.Add(current);
                if (current.ParentFrameId == 0 || !_frameByKey.TryGetValue(new FrameKey(current.FlowId, current.ParentFrameId), out var parent))
                {
                    break;
                }

                current = parent;
            }

            chain.Reverse();
            return chain;
        }

        private static List<Frame> BuildFrames(FlowEventFile file, IReadOnlyDictionary<int, string> methodNames)
        {
            var frames = new Dictionary<FrameKey, Frame>();
            foreach (var flowEvent in file.Events)
            {
                if (flowEvent.Kind is FlowEventKind.AsyncEdge or FlowEventKind.Truncated or FlowEventKind.Suppressed)
                {
                    continue;
                }

                var key = new FrameKey(flowEvent.FlowId, flowEvent.FrameId);
                if (!frames.TryGetValue(key, out var frame))
                {
                    var displayName = methodNames.TryGetValue(flowEvent.MethodMetadataIndex, out var methodName)
                                          ? methodName
                                          : "method#" + flowEvent.MethodMetadataIndex;
                    frame = new Frame(flowEvent.FlowId, flowEvent.FrameId, flowEvent.ParentFrameId, flowEvent.MethodMetadataIndex, displayName, flowEvent.Depth, flowEvent.Timestamp);
                    frame.ThreadId = flowEvent.ThreadId;
                    frame.OperationId = flowEvent.OperationId;
                    frames[key] = frame;
                }

                switch (flowEvent.Kind)
                {
                    case FlowEventKind.Enter:
                        frame.StartTimestamp = flowEvent.Timestamp;
                        frame.ThreadId = flowEvent.ThreadId;
                        break;
                    case FlowEventKind.Exit:
                        frame.EndTimestamp = flowEvent.Timestamp;
                        break;
                    case FlowEventKind.Exception:
                        frame.ExceptionTypeId = flowEvent.ExceptionTypeId;
                        break;
                }
            }

            foreach (var exception in file.Exceptions)
            {
                if (frames.TryGetValue(new FrameKey(exception.FlowId, exception.FrameId), out var frame))
                {
                    frame.ExceptionType = GetTableValue(file.Types, exception.TypeId);
                    frame.ExceptionMessage = GetTableValue(file.Strings, exception.MessageId);
                    frame.ExceptionStack = GetTableValue(file.Strings, exception.StackId);
                    frame.ExceptionHResult = exception.HResult;
                }
            }

            foreach (var value in file.Values)
            {
                if (frames.TryGetValue(new FrameKey(value.FlowId, value.FrameId), out var frame))
                {
                    frame.Values.Add(FormatCapturedValue(file, value));
                }
            }

            return frames.Values.OrderBy(frame => frame.StartTimestamp).ToList();
        }

        private static CapturedValueView FormatCapturedValue(FlowEventFile file, FlowCapturedValue value)
        {
            var name = GetTableValue(file.Strings, value.NameId) ?? value.Kind.ToString();
            var type = GetTableValue(file.Types, value.TypeId) ?? "unknown";
            var formatted = value.Tag switch
            {
                FlowValueTag.Null => "null",
                FlowValueTag.Boolean => value.NumberValue == 0 ? "false" : "true",
                FlowValueTag.Int64 => value.NumberValue.ToString(CultureInfo.InvariantCulture),
                FlowValueTag.UInt64 => unchecked((ulong)value.NumberValue).ToString(CultureInfo.InvariantCulture),
                FlowValueTag.String => "\"" + (GetTableValue(file.Strings, value.StringId) ?? string.Empty) + "\"",
                FlowValueTag.TypeSummary => GetTableValue(file.Strings, value.StringId) ?? type,
                FlowValueTag.CollectionSummary => "count=" + (value.ItemCount >= 0 ? value.ItemCount.ToString(CultureInfo.InvariantCulture) : "?") + ", captured=" + value.CapturedItemCount.ToString(CultureInfo.InvariantCulture),
                _ => value.Tag.ToString()
            };

            return new CapturedValueView(value.Phase, value.Kind, name, name, formatted, type, value.NotCapturedReason);
        }

        private static string GetTableValue(string[] table, int id)
        {
            return id >= 0 && id < table.Length ? table[id] : null;
        }

        private static List<FlowSummary> BuildFlows(FlowEvent[] events, List<Frame> frames, List<OperationSummary> operations)
        {
            var framesByFlow = frames.GroupBy(frame => frame.FlowId).ToDictionary(group => group.Key, group => group.ToList());
            var operationsById = operations.ToDictionary(operation => operation.OperationId);
            var flows = new List<FlowSummary>();
            foreach (var flowEvents in events.GroupBy(flowEvent => flowEvent.FlowId).OrderBy(group => group.Key))
            {
                var orderedEvents = flowEvents.ToArray();
                var first = orderedEvents.Min(flowEvent => flowEvent.Timestamp);
                var last = orderedEvents.Max(flowEvent => flowEvent.Timestamp);
                var rootFrames = framesByFlow.TryGetValue(flowEvents.Key, out var flowFrames)
                                     ? flowFrames.Where(frame => frame.ParentFrameId == 0).OrderBy(frame => frame.StartTimestamp).ToList()
                                     : new List<Frame>();
                var operation = orderedEvents.Select(flowEvent => flowEvent.OperationId)
                                             .Where(operationId => operationId != 0)
                                             .Select(operationId => operationsById.TryGetValue(operationId, out var summary) ? summary : null)
                                             .FirstOrDefault(summary => summary is not null);

                flows.Add(new FlowSummary(flowEvents.Key, orderedEvents, rootFrames, ToMilliseconds(last - first), operation?.TraceId, operation?.RootSpanId ?? 0, operation?.ActiveSpanId ?? 0));
            }

            return flows;
        }

        private static List<OperationSummary> BuildOperations(FlowEventFile file, List<Frame> frames)
        {
            var framesByOperation = frames.Where(frame => frame.OperationId != 0)
                                          .GroupBy(frame => frame.OperationId)
                                          .ToDictionary(group => group.Key, group => group.ToList());
            return file.Operations
                       .OrderBy(operation => operation.OperationId)
                       .Select(operation =>
                       {
                           framesByOperation.TryGetValue(operation.OperationId, out var operationFrames);
                           operationFrames ??= new List<Frame>();
                           var traceId = FormatTraceId(operation.TraceIdUpper, operation.TraceIdLower);
                           return new OperationSummary(operation.OperationId, operation.TriggerReason, operation.Root, operation.StartTimestamp, traceId, operation.RootSpanId, operation.ActiveSpanId, operationFrames);
                       })
                       .ToList();
        }

        private static void ApplyOperationCorrelation(List<Frame> frames, List<OperationSummary> operations)
        {
            var operationsById = operations.ToDictionary(operation => operation.OperationId);
            foreach (var frame in frames)
            {
                if (frame.OperationId != 0 && operationsById.TryGetValue(frame.OperationId, out var operation))
                {
                    frame.TraceId = operation.TraceId;
                    frame.RootSpanId = operation.RootSpanId;
                    frame.ActiveSpanId = operation.ActiveSpanId;
                }
            }
        }

        private static List<CaptureMarker> BuildMarkers(FlowEventFile file)
        {
            return file.Events.Where(flowEvent => flowEvent.Kind is FlowEventKind.Truncated or FlowEventKind.Suppressed)
                       .Select(flowEvent => new CaptureMarker(
                           flowEvent.Kind,
                           GetTableValue(file.Strings, flowEvent.MethodMetadataIndex) ?? "unknown budget",
                           flowEvent.OperationId,
                           flowEvent.FlowId,
                           flowEvent.Timestamp))
                       .ToList();
        }

        private static List<AsyncOperation> BuildAsyncOperations(List<Frame> frames, IReadOnlyDictionary<int, string> methodNames)
        {
            return frames.Select(frame => new { Frame = frame, LogicalName = TryGetAsyncLogicalName(methodNames, frame.MethodMetadataIndex) })
                         .Where(item => item.LogicalName is not null)
                         .GroupBy(item => new AsyncOperationKey(item.LogicalName!, item.Frame.FlowId))
                         .Select(group =>
                         {
                             var steps = group.Select(item => item.Frame).OrderBy(frame => frame.StartTimestamp).ToList();
                             return new AsyncOperation(group.Key.LogicalName, group.Key.FlowId, steps);
                         })
                         .OrderBy(operation => operation.StartTimestamp)
                         .ToList();
        }

        private static List<AsyncEdge> BuildAsyncEdges(FlowEvent[] events, IReadOnlyDictionary<int, string> methodNames)
        {
            return events.Where(flowEvent => flowEvent.Kind == FlowEventKind.AsyncEdge)
                         .OrderBy(flowEvent => flowEvent.Timestamp)
                         .Select(edge =>
                         {
                             var method = methodNames.TryGetValue(edge.MethodMetadataIndex, out var methodName)
                                              ? methodName
                                              : "method#" + edge.MethodMetadataIndex;
                             return new AsyncEdge(edge.FlowId, edge.FrameId, method, edge.Timestamp);
                         })
                         .ToList();
        }

        private static void LinkAsyncOperations(List<AsyncOperation> operations, List<AsyncEdge> edges)
        {
            var byFlowId = operations.ToDictionary(operation => operation.FlowId);
            foreach (var edge in edges)
            {
                if (byFlowId.TryGetValue(edge.ParentFlowId, out var parent))
                {
                    edge.Parent = parent;
                }

                if (byFlowId.TryGetValue(edge.ChildFlowId, out var child))
                {
                    edge.Child = child;
                    child.ParentEdge = edge;
                }
            }
        }
    }

    private sealed class FlowSummary
    {
        public FlowSummary(ulong flowId, FlowEvent[] events, List<Frame> rootFrames, double durationMs, string traceId, ulong rootSpanId, ulong activeSpanId)
        {
            FlowId = flowId;
            Events = events;
            RootFrames = rootFrames;
            DurationMs = durationMs;
            TraceId = traceId;
            RootSpanId = rootSpanId;
            ActiveSpanId = activeSpanId;
        }

        public ulong FlowId { get; }

        public FlowEvent[] Events { get; }

        public List<Frame> RootFrames { get; }

        public double DurationMs { get; }

        public string TraceId { get; }

        public ulong RootSpanId { get; }

        public ulong ActiveSpanId { get; }
    }

    private sealed class Frame
    {
        public Frame(ulong flowId, ulong frameId, ulong parentFrameId, int methodMetadataIndex, string displayName, int depth, long startTimestamp)
        {
            FlowId = flowId;
            FrameId = frameId;
            ParentFrameId = parentFrameId;
            MethodMetadataIndex = methodMetadataIndex;
            DisplayName = displayName;
            ShortName = Program.ShortName(displayName);
            Depth = depth;
            StartTimestamp = startTimestamp;
        }

        public ulong FlowId { get; }

        public ulong FrameId { get; }

        public ulong ParentFrameId { get; }

        public int MethodMetadataIndex { get; }

        public string DisplayName { get; }

        public string ShortName { get; }

        public int Depth { get; }

        public long StartTimestamp { get; set; }

        public long EndTimestamp { get; set; }

        public long ExceptionTypeId { get; set; }

        public int ThreadId { get; set; }

        public ulong OperationId { get; set; }

        public string TraceId { get; set; }

        public ulong RootSpanId { get; set; }

        public ulong ActiveSpanId { get; set; }

        public string ExceptionType { get; set; }

        public string ExceptionMessage { get; set; }

        public string ExceptionStack { get; set; }

        public int ExceptionHResult { get; set; }

        public List<CapturedValueView> Values { get; } = new();

        public double DurationMs => EndTimestamp == 0 ? 0 : ToMilliseconds(EndTimestamp - StartTimestamp);
    }

    private sealed class OperationSummary
    {
        public OperationSummary(ulong operationId, string triggerReason, string root, long startTimestamp, string traceId, ulong rootSpanId, ulong activeSpanId, List<Frame> frames)
        {
            OperationId = operationId;
            TriggerReason = triggerReason;
            Root = root;
            StartTimestamp = startTimestamp;
            TraceId = traceId;
            RootSpanId = rootSpanId;
            ActiveSpanId = activeSpanId;
            Frames = frames;
        }

        public ulong OperationId { get; }

        public string TriggerReason { get; }

        public string Root { get; }

        public long StartTimestamp { get; }

        public string TraceId { get; }

        public ulong RootSpanId { get; }

        public ulong ActiveSpanId { get; }

        public List<Frame> Frames { get; }
    }

    private sealed class CaptureMarker
    {
        public CaptureMarker(FlowEventKind kind, string reason, ulong operationId, ulong flowId, long timestamp)
        {
            Kind = kind;
            Reason = reason;
            OperationId = operationId;
            FlowId = flowId;
            Timestamp = timestamp;
        }

        public FlowEventKind Kind { get; }

        public string Reason { get; }

        public ulong OperationId { get; }

        public ulong FlowId { get; }

        public long Timestamp { get; }
    }

    private sealed class AsyncOperation
    {
        public AsyncOperation(string logicalName, ulong flowId, List<Frame> steps)
        {
            LogicalName = logicalName;
            ShortName = Program.ShortName(logicalName);
            FlowId = flowId;
            Steps = steps;
        }

        public string LogicalName { get; }

        public string ShortName { get; }

        public ulong FlowId { get; }

        public List<Frame> Steps { get; }

        public AsyncEdge ParentEdge { get; set; }

        public ulong ActiveSpanId => Steps.Select(step => step.ActiveSpanId).FirstOrDefault(id => id != 0);

        public ulong RootSpanId => Steps.Select(step => step.RootSpanId).FirstOrDefault(id => id != 0);

        public long StartTimestamp => Steps.Min(step => step.StartTimestamp);

        public long EndTimestamp => Steps.Max(step => step.EndTimestamp == 0 ? step.StartTimestamp : step.EndTimestamp);

        public double DurationMs => ToMilliseconds(EndTimestamp - StartTimestamp);
    }

    private sealed class AsyncEdge
    {
        public AsyncEdge(ulong parentFlowId, ulong childFlowId, string methodDisplayName, long timestamp)
        {
            ParentFlowId = parentFlowId;
            ChildFlowId = childFlowId;
            MethodDisplayName = methodDisplayName;
            MethodShortName = Program.ShortName(methodDisplayName);
            Timestamp = timestamp;
        }

        public ulong ParentFlowId { get; }

        public ulong ChildFlowId { get; }

        public string MethodDisplayName { get; }

        public string MethodShortName { get; }

        public long Timestamp { get; }

        public AsyncOperation Parent { get; set; }

        public AsyncOperation Child { get; set; }
    }

    private static class HtmlReportRenderer
    {
        public static string Render(FlowReport report, string apmBaseUrl)
        {
            var captureStart = report.Events.Length == 0 ? 0L : report.Events.Min(flowEvent => flowEvent.Timestamp);
            var captureEnd = report.Events.Length == 0 ? 0L : report.Events.Max(flowEvent => flowEvent.Timestamp);
            var spanMs = ToMilliseconds(captureEnd - captureStart);
            var rootDuration = report.Flows.Count == 0 ? 0 : report.Flows.Max(flow => flow.DurationMs);
            if (spanMs <= 0)
            {
                spanMs = rootDuration <= 0 ? 1 : rootDuration;
            }

            var rows = BuildTimelineRows(report, captureStart);
            var maxRowMs = rows.Count == 0 ? 1 : Math.Max(1, rows.Max(row => row.DurationMs));
            var maxFrameMs = report.Frames.Count == 0 ? 1 : Math.Max(1, report.Frames.Max(frame => frame.DurationMs));
            var threadCount = report.Events.Select(flowEvent => flowEvent.ThreadId).Distinct().Count();
            var traceId = report.Flows.Select(flow => flow.TraceId).FirstOrDefault(id => !string.IsNullOrEmpty(id));
            var spanFlow = report.Flows.FirstOrDefault(flow => flow.RootSpanId != 0 || flow.ActiveSpanId != 0);
            var spanSummaries = BuildSpanSummaries(report);

            var html = new StringBuilder();
            html.AppendLine("<!doctype html>");
            html.AppendLine("<html lang=\"en\">");
            html.AppendLine("<head>");
            html.AppendLine("<meta charset=\"utf-8\">");
            html.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
            html.AppendLine("<title>Live Debugger Flow Recorder</title>");
            html.AppendLine("<style>");
            html.AppendLine(Styles);
            html.AppendLine("</style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            html.AppendLine("<main class=\"shell\">");

            html.AppendLine("<header class=\"masthead\">");
            html.AppendLine("<div class=\"masthead-text\">");
            html.AppendLine("<p class=\"eyebrow\">Datadog Live Debugger POC</p>");
            html.AppendLine("<h1>Flow Recorder</h1>");
            html.AppendLine("<p class=\"subtitle\">A single execution capture shown as a <strong>timeline</strong>, <strong>async call flow</strong>, and the <strong>work segments</strong> that ran between awaits.</p>");
            html.AppendLine("</div>");
            html.AppendLine("<div class=\"capture-card\">");
            html.AppendLine("<span class=\"capture-label\">Capture</span>");
            html.AppendLine("<strong>" + H(report.CaptureName) + "</strong>");
            html.AppendLine("<code title=\"" + H(report.CaptureDisplayPath) + "\">" + H(report.CaptureDisplayPath) + "</code>");
            if (!string.IsNullOrEmpty(traceId) || spanFlow is not null)
            {
                html.AppendLine("<div class=\"capture-ids\">");
                if (!string.IsNullOrEmpty(traceId))
                {
                    html.AppendLine(TraceChip(apmBaseUrl, "trace-chip", traceId, "trace " + H(Shorten(traceId)), "Linked APM trace: " + H(traceId)));
                }

                if (spanFlow is not null && spanFlow.RootSpanId != 0)
                {
                    html.AppendLine(SpanChip(apmBaseUrl, "trace-chip", traceId, spanFlow.RootSpanId, "span " + H(SpanShort(spanFlow.RootSpanId)), "Root APM span: " + spanFlow.RootSpanId));
                }

                html.AppendLine("</div>");
            }

            html.AppendLine("</div>");
            html.AppendLine("</header>");

            RenderNav(html, report, spanSummaries.Count > 0, rows.Count > 0);

            html.AppendLine("<section class=\"metrics\" id=\"overview\">");
            Metric(html, "Events", report.Events.Length.ToString(CultureInfo.InvariantCulture), "recorded runtime facts", "Raw records written by the recorder, such as method enter/exit, async edges, values, exceptions, and capture markers.");
            Metric(html, "Frames", report.Frames.Count.ToString(CultureInfo.InvariantCulture), "work segments", "Method execution segments reconstructed from enter and exit events. Async methods can produce multiple frames as they resume after awaits.");
            Metric(html, "Recorder ops", report.Operations.Count.ToString(CultureInfo.InvariantCulture), "captured operations", "Operation-scoped capture windows opened by the recorder.");
            Metric(html, "Async ops", report.AsyncOperations.Count.ToString(CultureInfo.InvariantCulture), "async methods", "Logical async methods stitched together from their resume segments.");
            Metric(html, "APM spans", spanSummaries.Count.ToString(CultureInfo.InvariantCulture), "correlated spans", "Distinct active APM span ids seen during the capture.");
            Metric(html, "Threads", threadCount.ToString(CultureInfo.InvariantCulture), "OS threads", "Distinct operating system threads that ran recorded work.");
            Metric(html, "Wall time", Format(spanMs) + " ms", "capture duration", "Elapsed time between the first and last recorded event.");
            html.AppendLine("</section>");

            RenderLegend(html);
            RenderOperationSummary(html, report, apmBaseUrl);
            RenderCaptureMarkers(html, report);
            RenderExceptions(html, report, apmBaseUrl);
            RenderSpanSummary(html, spanSummaries, apmBaseUrl);
            RenderTimeline(html, rows, spanMs, maxRowMs, maxFrameMs, apmBaseUrl);
            RenderThreadLanes(html, report, captureStart, spanMs, maxFrameMs);

            html.AppendLine("<section class=\"grid\">");
            RenderFlowSection(html, report, maxFrameMs, apmBaseUrl);
            RenderHotSegments(html, report, maxFrameMs);
            html.AppendLine("</section>");

            html.AppendLine("</main>");
            html.AppendLine("<script>");
            html.AppendLine(Script);
            html.AppendLine("</script>");
            html.AppendLine("</body>");
            html.AppendLine("</html>");
            return html.ToString();
        }

        private static void RenderLegend(StringBuilder html)
        {
            html.AppendLine("<section class=\"legend\">");
            html.AppendLine("<div class=\"legend-item\"><span class=\"chip flow\">Flow</span><p>A recorded execution lane. Async work may continue in a new flow after an await, and the viewer links those flows back together.</p></div>");
            html.AppendLine("<div class=\"legend-item\"><span class=\"chip frame\">Frame</span><p>One uninterrupted run of a method: from enter to exit, exception, or the next await.</p></div>");
            html.AppendLine("<div class=\"legend-item\"><span class=\"chip async\">Async op</span><p>The original async method view, reconstructed from all of its resume frames.</p></div>");
            html.AppendLine("<div class=\"legend-item heat-legend\"><span class=\"chip\">Heat</span><div class=\"heat-scale\"><i class=\"heat-1\"></i><i class=\"heat-2\"></i><i class=\"heat-3\"></i><i class=\"heat-4\"></i><i class=\"heat-5\"></i></div><p>fast &rarr; slow, relative to the longest segment.</p></div>");
            html.AppendLine("</section>");
        }

        private static void RenderOperationSummary(StringBuilder html, FlowReport report, string apmBaseUrl)
        {
            if (report.Operations.Count == 0)
            {
                return;
            }

            html.AppendLine("<section class=\"panel span-panel\" id=\"operations\">");
            html.AppendLine("<div class=\"section-heading\"><div><p class=\"eyebrow\">Recorder scope</p><h2>Operation context</h2></div><span>The recorder operation is the capture gate; trace context is optional correlation.</span></div>");
            html.AppendLine("<div class=\"span-list\">");
            foreach (var operation in report.Operations.OrderBy(operation => operation.OperationId))
            {
                html.Append("<article class=\"span-card\">");
                html.Append("<div class=\"span-main\">");
                html.Append("<span class=\"span-chip\">operation " + operation.OperationId + "</span>");
                html.Append("<strong>" + H(operation.TriggerReason) + "</strong>");
                html.Append("<code>" + H(operation.Root) + "</code>");
                html.Append("</div>");
                html.Append("<div class=\"span-stats\"><span>" + operation.Frames.Count + " frames</span>");
                if (!string.IsNullOrEmpty(operation.TraceId))
                {
                    html.Append(TraceChip(apmBaseUrl, "flow-chip", operation.TraceId, "trace " + H(Shorten(operation.TraceId)), "trace " + H(operation.TraceId)));
                }

                if (operation.ActiveSpanId != 0)
                {
                    html.Append(SpanChip(apmBaseUrl, "flow-chip", operation.TraceId, operation.ActiveSpanId, "span " + H(SpanShort(operation.ActiveSpanId)), "active APM span " + operation.ActiveSpanId));
                }

                html.Append("</div>");
                html.AppendLine("</article>");
            }

            html.AppendLine("</div>");
            html.AppendLine("</section>");
        }

        private static void RenderCaptureMarkers(StringBuilder html, FlowReport report)
        {
            if (report.Markers.Count == 0)
            {
                return;
            }

            html.AppendLine("<section class=\"panel span-panel\" id=\"markers\">");
            html.AppendLine("<div class=\"section-heading\"><div><p class=\"eyebrow\">Capture limits</p><h2>Suppressed and truncated regions</h2></div><span>Bounded capture is expected; these markers explain why part of the flow is missing.</span></div>");
            html.AppendLine("<div class=\"span-list\">");
            foreach (var marker in report.Markers.OrderBy(marker => marker.Timestamp))
            {
                html.Append("<article class=\"span-card\">");
                html.Append("<div class=\"span-main\">");
                html.Append("<span class=\"span-chip\">" + H(marker.Kind.ToString().ToLowerInvariant()) + "</span>");
                html.Append("<strong>" + H(marker.Reason) + "</strong>");
                html.Append("<code>operation " + marker.OperationId + " / flow " + marker.FlowId + "</code>");
                html.Append("</div>");
                html.AppendLine("</article>");
            }

            html.AppendLine("</div>");
            html.AppendLine("</section>");
        }

        private static void RenderNav(StringBuilder html, FlowReport report, bool hasSpans, bool hasTimeline)
        {
            html.AppendLine("<nav class=\"topnav\" id=\"topnav\">");
            html.AppendLine("<div class=\"nav-links\">");
            html.Append("<a href=\"#overview\">Overview</a>");
            if (report.Operations.Count > 0)
            {
                html.Append("<a href=\"#operations\">Operations</a>");
            }

            if (report.Markers.Count > 0)
            {
                html.Append("<a href=\"#markers\">Limits <b>" + report.Markers.Count + "</b></a>");
            }

            if (report.ExceptionFrames.Count > 0)
            {
                html.Append("<a class=\"nav-exc\" href=\"#exceptions\">Exceptions <b>" + report.ExceptionFrames.Count + "</b></a>");
            }

            if (hasSpans)
            {
                html.Append("<a href=\"#spans\">Spans</a>");
            }

            if (hasTimeline)
            {
                html.Append("<a href=\"#timeline\">Timeline</a>");
            }

            html.Append("<a href=\"#lane-panel\">Threads</a>");
            html.Append("<a href=\"#flows\">Call flow</a>");
            html.Append("<a href=\"#hotspots\">Hot spots</a>");
            html.AppendLine("</div>");
            html.AppendLine("<div class=\"nav-tools\">");
            html.AppendLine("<input type=\"search\" id=\"global-search\" class=\"nav-search\" placeholder=\"Search methods\u2026\" autocomplete=\"off\">");
            html.AppendLine("<button type=\"button\" id=\"theme-toggle\" class=\"nav-toggle\" title=\"Toggle light / dark\" aria-label=\"Toggle light or dark theme\">&#9680;</button>");
            html.AppendLine("</div>");
            html.AppendLine("</nav>");
        }

        private static void RenderExceptions(StringBuilder html, FlowReport report, string apmBaseUrl)
        {
            if (report.ExceptionFrames.Count == 0)
            {
                return;
            }

            html.AppendLine("<section class=\"panel exc-panel\" id=\"exceptions\">");
            html.AppendLine("<div class=\"section-heading\"><div><p class=\"eyebrow\">Root cause</p><h2>Exceptions</h2></div><span>Each throw, with the reconstructed call stack and the runtime values captured on every frame in it.</span></div>");

            foreach (var frame in report.ExceptionFrames)
            {
                var typeName = frame.ExceptionType ?? ("type-id#" + frame.ExceptionTypeId);
                html.Append("<article class=\"exc-card\">");

                html.Append("<div class=\"exc-head\">");
                html.Append("<span class=\"exc-badge\">exception</span>");
                html.Append("<strong class=\"exc-type\" title=\"" + H(typeName) + "\">" + H(ShortTypeName(typeName)) + "</strong>");
                if (frame.ActiveSpanId != 0)
                {
                    html.Append(SpanChip(apmBaseUrl, "exc-chip", frame.TraceId, frame.ActiveSpanId, "span " + H(SpanShort(frame.ActiveSpanId)), "active APM span " + frame.ActiveSpanId));
                }

                html.Append("<a class=\"exc-jump\" href=\"#flow-" + frame.FlowId + "\">flow " + frame.FlowId + " &middot; frame " + frame.FrameId + " &#8599;</a>");
                html.Append("</div>");

                if (!string.IsNullOrWhiteSpace(frame.ExceptionMessage))
                {
                    html.Append("<p class=\"exc-message\">" + H(frame.ExceptionMessage) + "</p>");
                }

                var meta = H(typeName);
                if (frame.ExceptionHResult != 0)
                {
                    meta += " &middot; HRESULT 0x" + frame.ExceptionHResult.ToString("x8", CultureInfo.InvariantCulture);
                }

                html.Append("<div class=\"exc-fulltype\">" + meta + "</div>");

                var stack = report.GetCallStack(frame);
                html.Append("<div class=\"exc-stack\">");
                html.Append("<div class=\"exc-stack-title\">Call stack &middot; runtime state <small>(outermost first)</small></div>");
                var depthIndex = 0;
                foreach (var stackFrame in stack)
                {
                    var isThrow = stackFrame.FlowId == frame.FlowId && stackFrame.FrameId == frame.FrameId;
                    html.Append("<div class=\"exc-frame" + (isThrow ? " throwing" : string.Empty) + "\" style=\"--sf-depth:" + depthIndex + "\">");
                    html.Append("<div class=\"exc-frame-head\">");
                    html.Append("<span class=\"exc-frame-name\">" + H(stackFrame.ShortName) + "</span>");
                    if (isThrow)
                    {
                        html.Append("<span class=\"exc-frame-tag\">threw here</span>");
                    }

                    html.Append("<span class=\"exc-frame-full\">" + H(stackFrame.DisplayName) + "</span>");
                    html.Append("<span class=\"exc-frame-dur\">" + Format(stackFrame.DurationMs) + " ms</span>");
                    html.Append("</div>");
                    if (stackFrame.Values.Count > 0)
                    {
                        AppendValueRows(html, stackFrame.Values);
                    }
                    else
                    {
                        html.Append("<div class=\"exc-frame-novalues\">no captured values on this frame</div>");
                    }

                    html.Append("</div>");
                    depthIndex++;
                }

                html.Append("</div>");

                if (!string.IsNullOrWhiteSpace(frame.ExceptionStack))
                {
                    html.Append("<details class=\"exc-raw\"><summary>Raw stack trace</summary><pre>" + H(frame.ExceptionStack) + "</pre></details>");
                }

                html.AppendLine("</article>");
            }

            html.AppendLine("</section>");
        }

        private static List<SpanSummary> BuildSpanSummaries(FlowReport report)
        {
            return report.Frames.Where(frame => frame.ActiveSpanId != 0)
                         .GroupBy(frame => frame.ActiveSpanId)
                         .Select(group =>
                         {
                             var frames = group.ToArray();
                             var rootSpanId = frames.Select(frame => frame.RootSpanId).FirstOrDefault(id => id != 0);
                             var traceId = frames.Select(frame => frame.TraceId).FirstOrDefault(id => !string.IsNullOrEmpty(id));
                             return new SpanSummary(
                                 group.Key,
                                 rootSpanId,
                                 traceId,
                                 frames.Length,
                                 frames.Select(frame => frame.FlowId).Distinct().Count(),
                                 frames.Sum(frame => frame.DurationMs),
                                 frames.Min(frame => frame.StartTimestamp));
                         })
                         .OrderBy(summary => summary.FirstTimestamp)
                         .ToList();
        }

        private static void RenderSpanSummary(StringBuilder html, List<SpanSummary> spans, string apmBaseUrl)
        {
            if (spans.Count == 0)
            {
                return;
            }

            html.AppendLine("<section class=\"panel span-panel\" id=\"spans\">");
            html.AppendLine("<div class=\"section-heading\"><div><p class=\"eyebrow\">APM correlation</p><h2>Recorded spans</h2></div><span>Each row groups recorder frames by the active Datadog span at capture time.</span></div>");
            html.AppendLine("<div class=\"span-list\">");
            foreach (var span in spans)
            {
                html.Append("<article class=\"span-card\">");
                html.Append("<div class=\"span-main\">");
                html.Append(SpanChip(apmBaseUrl, "span-chip", span.TraceId, span.SpanId, "span " + H(SpanShort(span.SpanId)), "active APM span " + span.SpanId));
                if (span.SpanId == span.RootSpanId)
                {
                    html.Append("<span class=\"span-root\">root</span>");
                }

                html.Append("<code>" + H(span.TraceId) + "</code>");
                html.Append("</div>");
                html.Append("<div class=\"span-stats\"><span>" + span.FrameCount + " frames</span><span>" + span.FlowCount + " flows</span><span>" + Format(span.CpuMs) + " ms CPU</span></div>");
                html.AppendLine("</article>");
            }

            html.AppendLine("</div>");
            html.AppendLine("</section>");
        }

        private static void RenderTimeline(StringBuilder html, List<TimelineRow> rows, double spanMs, double maxRowMs, double maxFrameMs, string apmBaseUrl)
        {
            html.AppendLine("<section class=\"panel timeline-panel\" id=\"timeline\">");
            html.AppendLine("<div class=\"section-heading\"><div><p class=\"eyebrow\">Wall-clock</p><h2>Execution timeline</h2></div><span>Nested by await causality. Solid blocks are CPU work; gaps are awaited time. Click a bar to jump to its frames.</span></div>");

            if (rows.Count == 0)
            {
                html.AppendLine("<p class=\"empty\">No timeline could be reconstructed from this capture.</p>");
                html.AppendLine("</section>");
                return;
            }

            html.AppendLine("<div class=\"tl-ruler\"><div class=\"tl-ruler-label\">operation</div><div class=\"tl-ruler-track\">");
            for (var tick = 0; tick <= 4; tick++)
            {
                var pct = tick * 25.0;
                var value = spanMs * tick / 4.0;
                html.AppendLine("<span class=\"tl-tick\" style=\"left:" + Format(pct) + "%\">" + Format(value) + " ms</span>");
            }

            html.AppendLine("</div><div class=\"tl-ruler-dur\">duration</div></div>");

            html.AppendLine("<div class=\"tl-body\">");
            foreach (var row in rows)
            {
                RenderTimelineRow(html, row, spanMs, maxRowMs, maxFrameMs, apmBaseUrl);
            }

            html.AppendLine("</div>");
            html.AppendLine("</section>");
        }

        private static void RenderTimelineRow(StringBuilder html, TimelineRow row, double spanMs, double maxRowMs, double maxFrameMs, string apmBaseUrl)
        {
            var rowHeat = Heat(row.DurationMs / maxRowMs);
            var spanLeft = spanMs <= 0 ? 0 : row.StartMs / spanMs * 100;
            var spanWidth = spanMs <= 0 ? 0 : Math.Max(0.4, (row.EndMs - row.StartMs) / spanMs * 100);
            var indent = 12 + (row.Depth * 18);
            var lowerName = (row.FullName ?? row.Name ?? string.Empty).ToLowerInvariant();

            html.Append("<div class=\"tl-row\" data-path=\"" + H(row.Path) + "\" data-flow=\"" + row.FlowId + "\" data-name=\"" + H(lowerName) + "\">");

            html.Append("<div class=\"tl-label\" style=\"padding-left:" + indent.ToString(CultureInfo.InvariantCulture) + "px\">");
            if (row.HasChildren)
            {
                html.Append("<button class=\"tl-toggle\" type=\"button\" data-path=\"" + H(row.Path) + "\" aria-expanded=\"true\" title=\"Collapse subtree\">&#9662;</button>");
            }
            else
            {
                html.Append("<span class=\"tl-bullet\"></span>");
            }

            html.Append("<span class=\"tl-name\" title=\"" + H(row.FullName) + "\">" + H(row.Name) + "</span>");
            html.Append("<span class=\"tl-flow\">flow " + row.FlowId + "</span>");
            if (row.ActiveSpanId != 0)
            {
                html.Append(SpanChip(apmBaseUrl, "tl-spanid", row.TraceId, row.ActiveSpanId, "span " + H(SpanShort(row.ActiveSpanId)), "active APM span " + row.ActiveSpanId));
            }

            if (row.HasException)
            {
                html.Append("<span class=\"tl-exc\" title=\"Exception recorded\">exc</span>");
            }

            html.Append("</div>");

            html.Append("<div class=\"tl-track\" data-flow=\"" + row.FlowId + "\" title=\"" + H(row.Name) + " &middot; " + Format(row.DurationMs) + " ms wall, " + Format(row.SelfMs) + " ms CPU\">");
            html.Append("<span class=\"tl-span\" style=\"left:" + Format(spanLeft) + "%;width:" + Format(spanWidth) + "%\"></span>");
            foreach (var seg in row.Segments)
            {
                var segLeft = spanMs <= 0 ? 0 : seg.StartMs / spanMs * 100;
                var segWidth = spanMs <= 0 ? 0 : Math.Max(0.35, seg.DurationMs / spanMs * 100);
                var segHeat = Heat(seg.DurationMs / maxFrameMs);
                var excClass = seg.HasException ? " exc" : string.Empty;
                html.Append("<span class=\"tl-seg heat-" + segHeat + excClass + "\" style=\"left:" + Format(segLeft) + "%;width:" + Format(segWidth) + "%\" title=\"resume " + seg.Index + " &middot; " + Format(seg.DurationMs) + " ms at +" + Format(seg.StartMs) + " ms\"></span>");
            }

            html.Append("</div>");

            var stepWord = row.StepCount == 1 ? " step" : " steps";
            html.Append("<div class=\"tl-dur\"><strong class=\"heat-text-" + rowHeat + "\">" + Format(row.DurationMs) + " ms</strong><small>&Sigma; " + Format(row.SelfMs) + " ms &middot; " + row.StepCount + stepWord + "</small></div>");

            html.AppendLine("</div>");
        }

        private static void RenderFlowSection(StringBuilder html, FlowReport report, double maxFrameMs, string apmBaseUrl)
        {
            html.AppendLine("<div class=\"panel flows-panel\" id=\"flows\">");
            html.AppendLine("<div class=\"section-heading\"><div><p class=\"eyebrow\">Physical detail</p><h2>Call flow</h2></div><span>Every recorded frame, grouped by flow id.</span></div>");
            html.AppendLine("<div class=\"flow-toolbar\">");
            html.AppendLine("<input type=\"search\" id=\"flow-filter\" class=\"flow-filter\" placeholder=\"Filter methods\u2026\" autocomplete=\"off\">");
            html.AppendLine("<label class=\"flow-toggle\"><input type=\"checkbox\" id=\"hide-trivial\" checked> Hide sub-millisecond flows</label>");
            html.AppendLine("</div>");
            html.AppendLine("<div class=\"flow-list hide-trivial\" id=\"flow-list\">");

            var asyncFlowIds = new HashSet<ulong>(report.AsyncOperations.Select(operation => operation.FlowId));

            foreach (var flow in report.Flows.OrderBy(flow => flow.FlowId))
            {
                var hasCapturedDetails = flow.RootFrames.Any(frame => HasCapturedDetails(report, frame));
                var trivial = flow.DurationMs < 1.0 && !hasCapturedDetails;
                var isAsync = asyncFlowIds.Contains(flow.FlowId);
                html.AppendLine("<article class=\"flow-card" + (trivial ? " trivial" : string.Empty) + "\" id=\"flow-" + flow.FlowId + "\" data-trivial=\"" + (trivial ? "1" : "0") + "\">");
                html.Append("<div class=\"flow-head\" role=\"button\" tabindex=\"0\">");
                html.Append("<span class=\"flow-id\">Flow " + flow.FlowId + "</span>");
                if (isAsync)
                {
                    html.Append("<span class=\"flow-kind\">async</span>");
                }

                html.Append("<span class=\"flow-stat\">" + Format(flow.DurationMs) + " ms</span>");
                html.Append("<span class=\"flow-stat muted\">" + flow.Events.Length + " events</span>");
                if (!string.IsNullOrEmpty(flow.TraceId))
                {
                    html.Append(TraceChip(apmBaseUrl, "flow-chip", flow.TraceId, "trace " + H(Shorten(flow.TraceId)), "trace " + H(flow.TraceId)));
                }

                if (flow.ActiveSpanId != 0)
                {
                    html.Append(SpanChip(apmBaseUrl, "flow-chip", flow.TraceId, flow.ActiveSpanId, "span " + H(SpanShort(flow.ActiveSpanId)), "active APM span " + flow.ActiveSpanId + " &middot; root span " + flow.RootSpanId));
                }

                html.Append("<span class=\"flow-caret\">&#9662;</span>");
                html.AppendLine("</div>");
                html.AppendLine("<div class=\"flow-frames\">");
                var resume = 0;
                foreach (var frame in flow.RootFrames.OrderBy(frame => frame.StartTimestamp))
                {
                    RenderFrame(html, report, frame, depth: 0, maxFrameMs, isAsync ? ++resume : 0, apmBaseUrl);
                }

                html.AppendLine("</div>");
                html.AppendLine("</article>");
            }

            html.AppendLine("</div>");
            html.AppendLine("</div>");
        }

        private static void RenderFrame(StringBuilder html, FlowReport report, Frame frame, int depth, double maxFrameMs, int resumeIndex, string apmBaseUrl)
        {
            var exceptionClass = frame.ExceptionTypeId == 0 ? string.Empty : " exception";
            var heat = Heat(frame.DurationMs / maxFrameMs);
            var barWidth = maxFrameMs <= 0 ? 0 : Math.Max(3, Math.Min(100, frame.DurationMs / maxFrameMs * 100));
            html.Append("<div class=\"frame depth-" + Math.Min(depth, 6) + exceptionClass + "\" data-name=\"" + H(frame.DisplayName.ToLowerInvariant()) + "\">");
            html.Append("<div class=\"frame-main\"><strong>" + H(frame.ShortName));
            if (resumeIndex > 0)
            {
                html.Append("<span class=\"resume\">resume " + resumeIndex + "</span>");
            }

            html.Append("</strong><span>" + H(frame.DisplayName) + "</span></div>");
            html.Append("<div class=\"frame-meta\"><span class=\"frame-bar\"><i class=\"heat-" + heat + "\" style=\"width:" + Format(barWidth) + "%\"></i></span><span class=\"frame-dur heat-text-" + heat + "\">" + Format(frame.DurationMs) + " ms</span>");
            if (frame.ActiveSpanId != 0)
            {
                html.Append(SpanChip(apmBaseUrl, "frame-span", frame.TraceId, frame.ActiveSpanId, "span " + H(SpanShort(frame.ActiveSpanId)), "active APM span " + frame.ActiveSpanId));
            }

            html.Append("<span class=\"frame-id\">frame " + frame.FrameId + "</span></div>");
            if (frame.ExceptionType is not null || frame.Values.Count > 0)
            {
                html.Append("<div class=\"frame-values\">");
                if (frame.ExceptionType is not null)
                {
                    html.Append("<div class=\"frame-exception\"><strong>" + H(frame.ExceptionType) + "</strong>");
                    if (!string.IsNullOrWhiteSpace(frame.ExceptionMessage))
                    {
                        html.Append("<span>" + H(frame.ExceptionMessage) + "</span>");
                    }

                    html.Append("</div>");
                }

                AppendValueRows(html, frame.Values);

                html.Append("</div>");
            }

            html.AppendLine("</div>");
            foreach (var child in report.GetChildren(frame).OrderBy(child => child.StartTimestamp))
            {
                RenderFrame(html, report, child, depth + 1, maxFrameMs, 0, apmBaseUrl);
            }
        }

        private static bool HasCapturedDetails(FlowReport report, Frame frame)
        {
            return frame.ExceptionType is not null ||
                   frame.Values.Count > 0 ||
                   report.GetChildren(frame).Any(child => HasCapturedDetails(report, child));
        }

        private static void AppendValueRows(StringBuilder html, List<CapturedValueView> values)
        {
            if (values.Count == 0)
            {
                return;
            }

            html.Append("<div class=\"val-list\">");
            foreach (var node in BuildValueTree(values))
            {
                AppendValueNode(html, node, depth: 0);
            }

            html.Append("</div>");
        }

        private static void AppendValueNode(StringBuilder html, CapturedValueNode node, int depth)
        {
            var value = node.Value;
            var kindClass = value.Kind switch
            {
                FlowValueKind.Argument => "arg",
                FlowValueKind.Local => "local",
                FlowValueKind.Return => "ret",
                FlowValueKind.This => "this",
                FlowValueKind.Exception => "exc",
                _ => "val"
            };
            var kindLabel = value.Kind switch
            {
                FlowValueKind.Argument => "arg",
                FlowValueKind.Local => "local",
                FlowValueKind.Return => "return",
                FlowValueKind.This => "this",
                FlowValueKind.Exception => "exception",
                _ => value.Kind.ToString().ToLowerInvariant()
            };

            html.Append("<div class=\"val-row depth-" + Math.Min(depth, 4) + "\">");
            html.Append("<span class=\"val-kind k-" + kindClass + "\">" + kindLabel + "</span>");
            html.Append("<span class=\"val-name\">" + H(value.DisplayName) + "</span>");
            html.Append("<span class=\"val-eq\">=</span>");
            html.Append("<span class=\"val-val\">" + H(value.Value) + "</span>");
            html.Append("<span class=\"val-type\" title=\"" + H(value.TypeName) + "\">" + H(ShortTypeName(value.TypeName)) + "</span>");
            if (value.NotCaptured != FlowNotCapturedReason.None)
            {
                html.Append("<span class=\"val-flag\" title=\"value not fully captured\">" + H(value.NotCaptured.ToString()) + "</span>");
            }

            html.Append("</div>");
            foreach (var child in node.Children)
            {
                AppendValueNode(html, child, depth + 1);
            }
        }

        private static List<CapturedValueNode> BuildValueTree(List<CapturedValueView> values)
        {
            var roots = new List<CapturedValueNode>();
            var nodesByName = new Dictionary<string, CapturedValueNode>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                var node = new CapturedValueNode(value);
                nodesByName[value.Name] = node;
            }

            foreach (var value in values)
            {
                var node = nodesByName[value.Name];
                var parentName = GetParentValueName(value.Name);
                if (parentName is not null && nodesByName.TryGetValue(parentName, out var parent))
                {
                    node.Value = value.WithDisplayName(GetValueLeafName(value.Name, parentName));
                    parent.Children.Add(node);
                }
                else
                {
                    roots.Add(node);
                }
            }

            return roots;
        }

        private static string GetValueLeafName(string name, string parentName)
        {
            if (name.Length <= parentName.Length)
            {
                return name;
            }

            var start = parentName.Length;
            if (name[start] == '.')
            {
                start++;
            }

            return name.Substring(start);
        }

        private static string GetParentValueName(string name)
        {
            if (String.IsNullOrEmpty(name))
            {
                return null;
            }

            var lastDot = name.LastIndexOf('.');
            var lastBracket = name.LastIndexOf('[');
            if (lastDot > 0 && lastDot > lastBracket)
            {
                return name.Substring(0, lastDot);
            }

            if (lastBracket > 0 && name.EndsWith("]", StringComparison.Ordinal))
            {
                return name.Substring(0, lastBracket);
            }

            return null;
        }

        private static string ShortTypeName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                return typeName;
            }

            var generic = typeName.IndexOf('`');
            var trimmed = generic >= 0 ? typeName.Substring(0, generic) : typeName;
            var lastDot = trimmed.LastIndexOf('.');
            return lastDot >= 0 && lastDot + 1 < trimmed.Length ? trimmed.Substring(lastDot + 1) : trimmed;
        }

        private static void RenderHotSegments(StringBuilder html, FlowReport report, double maxFrameMs)
        {
            var topFrames = report.Frames.OrderByDescending(frame => frame.DurationMs).Take(8).ToArray();
            html.AppendLine("<aside class=\"panel sidebar\" id=\"hotspots\">");
            html.AppendLine("<div class=\"section-heading\"><div><p class=\"eyebrow\">Hot spots</p><h2>Slowest segments</h2></div></div>");
            html.AppendLine("<p class=\"sidebar-note\">Synchronous CPU bursts ranked by duration &mdash; the real work happening between awaits.</p>");
            foreach (var frame in topFrames)
            {
                var heat = Heat(frame.DurationMs / maxFrameMs);
                var width = maxFrameMs <= 0 ? 0 : Math.Max(6, Math.Min(100, frame.DurationMs / maxFrameMs * 100));
                html.AppendLine("<a class=\"bar-row\" href=\"#flow-" + frame.FlowId + "\"><div><strong>" + H(frame.ShortName) + "</strong><span>" + H(frame.DisplayName) + "</span></div><em class=\"heat-text-" + heat + "\">" + Format(frame.DurationMs) + " ms</em><i class=\"heat-" + heat + "\" style=\"width:" + Format(width) + "%\"></i></a>");
            }

            html.AppendLine("</aside>");
        }

        private static void RenderThreadLanes(StringBuilder html, FlowReport report, long captureStart, double spanMs, double maxFrameMs)
        {
            double Off(long ticks) => ToMilliseconds(ticks - captureStart);
            var lanes = report.Frames
                              .GroupBy(frame => frame.ThreadId)
                              .Select(group => new
                              {
                                  ThreadId = group.Key,
                                  Frames = group.OrderBy(frame => frame.StartTimestamp).ToList(),
                                  First = group.Min(frame => frame.StartTimestamp),
                                  Cpu = group.Sum(frame => frame.DurationMs),
                              })
                              .OrderBy(lane => lane.First)
                              .ToList();

            html.AppendLine("<section class=\"panel timeline-panel lane-panel\" id=\"lane-panel\">");
            html.AppendLine("<div class=\"section-heading\"><div><p class=\"eyebrow\">Concurrency</p><h2>Thread swim-lanes</h2></div><span>The same time axis, split by OS thread. Watch the workflow hop threads as each await resumes. Click a block to jump to its frames.</span></div>");

            if (lanes.Count == 0)
            {
                html.AppendLine("<p class=\"empty\">No thread activity was recorded.</p>");
                html.AppendLine("</section>");
                return;
            }

            // Stable identity color per logical async operation so a single op can be traced
            // across lanes; everything else (sync calls, accessors) stays neutral.
            var laneFlowIds = new HashSet<ulong>(lanes.SelectMany(lane => lane.Frames).Select(frame => frame.FlowId));
            var legendOps = report.AsyncOperations
                                  .Where(operation => laneFlowIds.Contains(operation.FlowId))
                                  .GroupBy(operation => operation.FlowId)
                                  .Select(group => group.OrderBy(operation => operation.StartTimestamp).First())
                                  .OrderBy(operation => operation.StartTimestamp)
                                  .ToList();
            var opColors = new Dictionary<ulong, string>();
            for (var i = 0; i < legendOps.Count; i++)
            {
                opColors[legendOps[i].FlowId] = OpPalette[i % OpPalette.Length];
            }

            string OpColor(ulong flowId) => opColors.TryGetValue(flowId, out var color) ? color : SyncOpColor;
            var hasSync = lanes.SelectMany(lane => lane.Frames).Any(frame => !opColors.ContainsKey(frame.FlowId));

            html.AppendLine("<div class=\"lane-toolbar\">");
            html.AppendLine("<label class=\"flow-toggle\"><input type=\"checkbox\" id=\"lane-color-op\"> Color by operation</label>");
            html.AppendLine("<span class=\"lane-hint\">Hover any block to trace one logical operation as it hops threads.</span>");
            html.AppendLine("</div>");

            html.AppendLine("<div class=\"lane-legend\">");
            foreach (var operation in legendOps)
            {
                html.Append("<span class=\"lane-key\"><i style=\"background:" + OpColor(operation.FlowId) + "\"></i>" + H(operation.ShortName) + "</span>");
            }

            if (hasSync)
            {
                html.Append("<span class=\"lane-key\"><i style=\"background:" + SyncOpColor + "\"></i>sync / accessor</span>");
            }

            html.AppendLine("</div>");

            html.AppendLine("<div class=\"tl-ruler\"><div class=\"tl-ruler-label\">thread</div><div class=\"tl-ruler-track\">");
            for (var tick = 0; tick <= 4; tick++)
            {
                var pct = tick * 25.0;
                var value = spanMs * tick / 4.0;
                html.AppendLine("<span class=\"tl-tick\" style=\"left:" + Format(pct) + "%\">" + Format(value) + " ms</span>");
            }

            html.AppendLine("</div><div class=\"tl-ruler-dur\">CPU</div></div>");

            html.AppendLine("<div class=\"tl-body\">");
            foreach (var lane in lanes)
            {
                var laneShare = spanMs <= 0 ? 0 : lane.Cpu / spanMs * 100;
                html.Append("<div class=\"tl-row tl-lane\">");
                html.Append("<div class=\"tl-label\" style=\"padding-left:12px\"><span class=\"tl-bullet\"></span><span class=\"tl-name\">thread " + lane.ThreadId + "</span></div>");
                html.Append("<div class=\"tl-track lane\">");
                foreach (var frame in lane.Frames)
                {
                    var end = frame.EndTimestamp == 0 ? frame.StartTimestamp : frame.EndTimestamp;
                    var segLeft = spanMs <= 0 ? 0 : Off(frame.StartTimestamp) / spanMs * 100;
                    var segWidth = spanMs <= 0 ? 0 : Math.Max(0.35, (Off(end) - Off(frame.StartTimestamp)) / spanMs * 100);
                    var heat = Heat(frame.DurationMs / maxFrameMs);
                    var excClass = frame.ExceptionTypeId != 0 ? " exc" : string.Empty;
                    html.Append("<span class=\"tl-seg heat-" + heat + excClass + "\" data-flow=\"" + frame.FlowId + "\" style=\"left:" + Format(segLeft) + "%;width:" + Format(segWidth) + "%;--op-color:" + OpColor(frame.FlowId) + "\" title=\"" + H(frame.ShortName) + " &middot; " + Format(frame.DurationMs) + " ms &middot; flow " + frame.FlowId + " at +" + Format(Off(frame.StartTimestamp)) + " ms\"></span>");
                }

                html.Append("</div>");
                html.Append("<div class=\"tl-dur\"><strong>" + Format(lane.Cpu) + " ms</strong><small>" + lane.Frames.Count + " seg &middot; " + Format(laneShare) + "%</small></div>");
                html.AppendLine("</div>");
            }

            html.AppendLine("</div>");
            html.AppendLine("</section>");
        }

        private static List<TimelineRow> BuildTimelineRows(FlowReport report, long captureStart)
        {
            double Off(long ticks) => ToMilliseconds(ticks - captureStart);
            var rows = new List<TimelineRow>();
            var traceByFlow = report.Flows
                                    .GroupBy(flow => flow.FlowId)
                                    .ToDictionary(group => group.Key, group => group.First().TraceId);

            string TraceForFlow(ulong flowId) => traceByFlow.TryGetValue(flowId, out var id) ? id : null;

            if (report.AsyncOperations.Count > 0)
            {
                var children = new Dictionary<AsyncOperation, List<AsyncOperation>>();
                var roots = new List<AsyncOperation>();
                foreach (var operation in report.AsyncOperations)
                {
                    var parent = operation.ParentEdge?.Parent;
                    if (parent is not null && !ReferenceEquals(parent, operation))
                    {
                        if (!children.TryGetValue(parent, out var list))
                        {
                            list = new List<AsyncOperation>();
                            children[parent] = list;
                        }

                        list.Add(operation);
                    }
                    else
                    {
                        roots.Add(operation);
                    }
                }

                var visited = new HashSet<AsyncOperation>();

                void Walk(AsyncOperation operation, int depth, string path)
                {
                    if (!visited.Add(operation))
                    {
                        return;
                    }

                    var hasChildren = children.TryGetValue(operation, out var kids) && kids.Count > 0;
                    var segments = operation.Steps
                                            .OrderBy(step => step.StartTimestamp)
                                            .Select((step, index) =>
                                            {
                                                var end = step.EndTimestamp == 0 ? step.StartTimestamp : step.EndTimestamp;
                                                return new TimelineSegment(index + 1, Off(step.StartTimestamp), Off(end), step.DurationMs, step.ExceptionTypeId != 0);
                                            })
                                            .ToList();
                    rows.Add(new TimelineRow
                    {
                        Path = path,
                        Depth = depth,
                        Name = operation.ShortName,
                        FullName = operation.LogicalName,
                        FlowId = operation.FlowId,
                        StartMs = Off(operation.StartTimestamp),
                        EndMs = Off(operation.EndTimestamp),
                        DurationMs = operation.DurationMs,
                        SelfMs = operation.Steps.Sum(step => step.DurationMs),
                        StepCount = operation.Steps.Count,
                        HasException = operation.Steps.Any(step => step.ExceptionTypeId != 0),
                        HasChildren = hasChildren,
                        ActiveSpanId = operation.ActiveSpanId,
                        TraceId = TraceForFlow(operation.FlowId),
                        Segments = segments,
                    });

                    if (hasChildren)
                    {
                        foreach (var child in kids.OrderBy(item => item.StartTimestamp))
                        {
                            Walk(child, depth + 1, path + "/" + child.FlowId.ToString(CultureInfo.InvariantCulture));
                        }
                    }
                }

                foreach (var root in roots.OrderBy(operation => operation.StartTimestamp))
                {
                    Walk(root, 0, root.FlowId.ToString(CultureInfo.InvariantCulture));
                }

                return rows;
            }

            var framesByFlow = report.Frames.GroupBy(frame => frame.FlowId)
                                            .ToDictionary(group => group.Key, group => group.OrderBy(frame => frame.StartTimestamp).ToList());
            foreach (var flow in report.Flows.OrderBy(flow => flow.RootFrames.Count == 0 ? long.MaxValue : flow.RootFrames.Min(frame => frame.StartTimestamp)))
            {
                if (!framesByFlow.TryGetValue(flow.FlowId, out var flowFrames) || flowFrames.Count == 0)
                {
                    continue;
                }

                var start = flowFrames.Min(frame => frame.StartTimestamp);
                var end = flowFrames.Max(frame => frame.EndTimestamp == 0 ? frame.StartTimestamp : frame.EndTimestamp);
                var head = flowFrames[0];
                var segments = flowFrames.Select((frame, index) =>
                {
                    var frameEnd = frame.EndTimestamp == 0 ? frame.StartTimestamp : frame.EndTimestamp;
                    return new TimelineSegment(index + 1, Off(frame.StartTimestamp), Off(frameEnd), frame.DurationMs, frame.ExceptionTypeId != 0);
                }).ToList();

                rows.Add(new TimelineRow
                {
                    Path = flow.FlowId.ToString(CultureInfo.InvariantCulture),
                    Depth = 0,
                    Name = head.ShortName,
                    FullName = head.DisplayName,
                    FlowId = flow.FlowId,
                    StartMs = Off(start),
                    EndMs = Off(end),
                    DurationMs = flow.DurationMs,
                    SelfMs = flowFrames.Sum(frame => frame.DurationMs),
                    StepCount = flowFrames.Count,
                    HasException = flowFrames.Any(frame => frame.ExceptionTypeId != 0),
                    HasChildren = false,
                    ActiveSpanId = flow.ActiveSpanId,
                    TraceId = flow.TraceId,
                    Segments = segments,
                });
            }

            return rows;
        }

        private static int Heat(double ratio)
        {
            if (double.IsNaN(ratio) || ratio <= 0)
            {
                return 1;
            }

            var level = (int)Math.Ceiling(ratio * 5);
            return Math.Max(1, Math.Min(5, level));
        }

        private static string Shorten(string traceId)
        {
            if (string.IsNullOrEmpty(traceId) || traceId.Length <= 14)
            {
                return traceId;
            }

            return traceId.Substring(0, 8) + "\u2026" + traceId.Substring(traceId.Length - 4);
        }

        private static string SpanShort(ulong spanId)
        {
            var value = spanId.ToString(CultureInfo.InvariantCulture);
            return value.Length <= 7 ? value : "\u2026" + value.Substring(value.Length - 5);
        }

        private static string ApmTraceUrl(string apmBaseUrl, string traceId)
        {
            return apmBaseUrl + "/apm/trace/" + Uri.EscapeDataString(traceId);
        }

        private static string ApmSpanUrl(string apmBaseUrl, string traceId, ulong spanId)
        {
            var span = spanId.ToString(CultureInfo.InvariantCulture);

            // A trace id lets us deep-link to the trace flame graph and focus the span; otherwise
            // fall back to the trace explorer filtered by span id (reserved attribute, no '@').
            if (!string.IsNullOrEmpty(traceId))
            {
                return apmBaseUrl + "/apm/trace/" + Uri.EscapeDataString(traceId) + "?spanID=" + span;
            }

            return apmBaseUrl + "/apm/traces?query=" + Uri.EscapeDataString("span_id:" + span);
        }

        private static string TraceChip(string apmBaseUrl, string cssClass, string traceId, string label, string title)
        {
            if (string.IsNullOrEmpty(apmBaseUrl) || string.IsNullOrEmpty(traceId))
            {
                return "<span class=\"" + cssClass + "\" title=\"" + title + "\">" + label + "</span>";
            }

            return "<a class=\"" + cssClass + " chip-link\" href=\"" + H(ApmTraceUrl(apmBaseUrl, traceId)) + "\" target=\"_blank\" rel=\"noopener noreferrer\" title=\"" + title + " &middot; open in Datadog\">" + label + "</a>";
        }

        private static string SpanChip(string apmBaseUrl, string cssClass, string traceId, ulong spanId, string label, string title)
        {
            if (string.IsNullOrEmpty(apmBaseUrl) || spanId == 0)
            {
                return "<span class=\"" + cssClass + "\" title=\"" + title + "\">" + label + "</span>";
            }

            return "<a class=\"" + cssClass + " chip-link\" href=\"" + H(ApmSpanUrl(apmBaseUrl, traceId, spanId)) + "\" target=\"_blank\" rel=\"noopener noreferrer\" title=\"" + title + " &middot; open in Datadog\">" + label + "</a>";
        }

        private sealed class TimelineRow
        {
            public string Path { get; set; }

            public int Depth { get; set; }

            public string Name { get; set; }

            public string FullName { get; set; }

            public ulong FlowId { get; set; }

            public double StartMs { get; set; }

            public double EndMs { get; set; }

            public double DurationMs { get; set; }

            public double SelfMs { get; set; }

            public int StepCount { get; set; }

            public bool HasException { get; set; }

            public bool HasChildren { get; set; }

            public ulong ActiveSpanId { get; set; }

            public string TraceId { get; set; }

            public List<TimelineSegment> Segments { get; set; }
        }

        private readonly record struct TimelineSegment(int Index, double StartMs, double EndMs, double DurationMs, bool HasException);

        private readonly record struct SpanSummary(ulong SpanId, ulong RootSpanId, string TraceId, int FrameCount, int FlowCount, double CpuMs, long FirstTimestamp);

        private static void Metric(StringBuilder html, string label, string value, string detail, string tooltip)
        {
            html.AppendLine("<article title=\"" + H(tooltip) + "\" aria-label=\"" + H(label + ": " + value + ". " + tooltip) + "\"><span>" + H(label) + "</span><strong>" + H(value) + "</strong><em>" + H(detail) + "</em></article>");
        }

        private static string H(string value)
        {
            return WebUtility.HtmlEncode(value ?? string.Empty);
        }

        private const string SyncOpColor = "#3a3f52";

        private static readonly string[] OpPalette =
        {
            "#8b7bff", "#46c2ac", "#e3c45c", "#e2665f", "#5aa9e6", "#f29e4c",
            "#9d7bd8", "#5fcf80", "#e87fb0", "#3fc1c9", "#c7d45c", "#b08968",
        };

        private const string Styles = @"
:root {
  color-scheme: dark;
  --bg: #090b13;
  --panel: rgba(19, 23, 38, 0.72);
  --panel-solid: #131726;
  --border: rgba(255, 255, 255, 0.08);
  --border-strong: rgba(255, 255, 255, 0.16);
  --text: #eef1fb;
  --muted: #99a2bb;
  --faint: #6b7491;
  --accent: #8b7bff;
  --accent-soft: rgba(139, 123, 255, 0.16);
  --mono: ui-monospace, ""Cascadia Code"", ""JetBrains Mono"", ""SF Mono"", Consolas, ""Liberation Mono"", monospace;
  --sans: Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, ""Segoe UI"", sans-serif;
  --h1: #46c2ac;
  --h2: #7cca7c;
  --h3: #e3c45c;
  --h4: #e89b54;
  --h5: #e2665f;
}
* { box-sizing: border-box; }
html { scroll-behavior: smooth; }
body {
  margin: 0;
  min-height: 100vh;
  color: var(--text);
  font-family: var(--sans);
  font-size: 14px;
  line-height: 1.5;
  background:
    radial-gradient(1200px 600px at 12% -5%, rgba(139,123,255,0.10), transparent 60%),
    radial-gradient(900px 500px at 100% 0%, rgba(70,194,172,0.08), transparent 55%),
    linear-gradient(180deg, #090b13 0%, #0b0e1a 60%, #080a11 100%);
  background-attachment: fixed;
}
.shell { width: min(1500px, calc(100% - 56px)); margin: 0 auto; padding: 40px 0 80px; }
a { color: inherit; }

.masthead { display: grid; grid-template-columns: 1fr minmax(320px, 440px); gap: 28px; align-items: end; margin-bottom: 26px; }
.eyebrow { margin: 0 0 8px; color: var(--accent); text-transform: uppercase; letter-spacing: 0.2em; font-size: 11px; font-weight: 700; }
h1 { margin: 0; font-size: clamp(30px, 4.4vw, 52px); line-height: 1.0; letter-spacing: -0.03em; font-weight: 800; }
h2 { margin: 0; font-size: 18px; letter-spacing: -0.01em; font-weight: 700; }
.subtitle { color: var(--muted); font-size: 15px; max-width: 640px; margin: 14px 0 0; }
.subtitle strong { color: var(--text); font-weight: 600; }
.capture-card { border: 1px solid var(--border); background: var(--panel); border-radius: 16px; padding: 16px 18px; display: flex; flex-direction: column; gap: 6px; backdrop-filter: blur(12px); }
.capture-label { color: var(--faint); text-transform: uppercase; letter-spacing: 0.16em; font-size: 10px; font-weight: 700; }
.capture-card strong { font-size: 16px; letter-spacing: -0.01em; }
.capture-card code { font-family: var(--mono); color: var(--muted); font-size: 11px; word-break: break-all; }
.trace-chip { font-family: var(--mono); font-size: 11px; color: var(--accent); background: var(--accent-soft); border: 1px solid rgba(139,123,255,0.3); border-radius: 999px; padding: 3px 10px; }
.capture-ids { display: flex; flex-wrap: wrap; gap: 6px; margin-top: 4px; }
a.chip-link { text-decoration: none; cursor: pointer; transition: filter 0.12s ease, border-color 0.12s ease, box-shadow 0.12s ease; }
a.chip-link::after { content: '\2197'; font-size: 0.82em; opacity: 0.55; margin-left: 3px; }
a.chip-link:hover { filter: brightness(1.3); border-color: var(--accent); box-shadow: 0 0 0 1px rgba(139,123,255,0.35); }
a.chip-link:hover::after { opacity: 1; }
a.chip-link:focus-visible { outline: 2px solid var(--accent); outline-offset: 2px; }

.metrics { display: grid; grid-template-columns: repeat(6, 1fr); gap: 12px; margin-bottom: 16px; }
.metrics article { border: 1px solid var(--border); background: var(--panel); border-radius: 14px; padding: 14px 16px; }
.metrics span { color: var(--faint); text-transform: uppercase; letter-spacing: 0.14em; font-size: 10px; font-weight: 700; }
.metrics strong { display: block; font-size: 26px; letter-spacing: -0.03em; margin: 6px 0 2px; font-variant-numeric: tabular-nums; }
.metrics em { color: var(--muted); font-style: normal; font-size: 12px; }

.legend { display: grid; grid-template-columns: repeat(4, 1fr); gap: 12px; margin-bottom: 22px; }
.legend-item { border: 1px solid var(--border); background: rgba(15,18,30,0.5); border-radius: 12px; padding: 12px 14px; display: flex; flex-direction: column; gap: 8px; }
.legend-item p { margin: 0; color: var(--muted); font-size: 12px; line-height: 1.45; }
.chip { align-self: flex-start; font-size: 11px; font-weight: 700; padding: 3px 9px; border-radius: 7px; letter-spacing: 0.02em; border: 1px solid var(--border-strong); color: var(--text); background: rgba(255,255,255,0.05); }
.chip.flow { color: #d2d6ff; border-color: rgba(139,123,255,0.4); background: var(--accent-soft); }
.chip.frame { color: #c2f0e7; border-color: rgba(70,194,172,0.4); background: rgba(70,194,172,0.14); }
.chip.async { color: #f4e2bc; border-color: rgba(227,196,92,0.4); background: rgba(227,196,92,0.14); }
.heat-legend .heat-scale { display: flex; gap: 3px; }
.heat-scale i { display: block; height: 8px; width: 26px; border-radius: 2px; }

.panel { border: 1px solid var(--border); background: var(--panel); border-radius: 18px; padding: 20px 22px; backdrop-filter: blur(12px); }
.section-heading { display: flex; align-items: baseline; justify-content: space-between; gap: 16px; margin-bottom: 16px; }
.section-heading .eyebrow { margin: 0 0 4px; }
.section-heading > span { color: var(--muted); font-size: 12px; max-width: 480px; text-align: right; }
.empty { color: var(--muted); }

.timeline-panel { margin-bottom: 16px; }
.tl-ruler, .tl-row { display: grid; grid-template-columns: minmax(200px, 320px) 1fr 116px; align-items: center; }
.tl-ruler { height: 26px; margin-bottom: 6px; border-bottom: 1px solid var(--border); }
.tl-ruler-label, .tl-ruler-dur { color: var(--faint); text-transform: uppercase; letter-spacing: 0.12em; font-size: 10px; font-weight: 700; }
.tl-ruler-dur { text-align: right; }
.tl-ruler-track { position: relative; height: 100%; }
.tl-tick { position: absolute; bottom: 4px; transform: translateX(-50%); color: var(--faint); font-size: 10px; font-variant-numeric: tabular-nums; white-space: nowrap; }
.tl-tick:first-child { transform: translateX(0); }
.tl-tick:last-child { transform: translateX(-100%); }
.tl-row { min-height: 34px; border-radius: 8px; }
.tl-row:hover { background: rgba(255,255,255,0.04); }
.tl-row.tl-hidden { display: none; }
.tl-label { display: flex; align-items: center; gap: 8px; min-width: 0; padding-right: 12px; }
.tl-toggle { width: 18px; height: 18px; flex: none; border: 1px solid var(--border-strong); background: rgba(255,255,255,0.05); color: var(--muted); border-radius: 5px; font-size: 10px; line-height: 1; cursor: pointer; padding: 0; transition: transform 0.12s ease; }
.tl-toggle:hover { color: var(--text); }
.tl-toggle.collapsed { transform: rotate(-90deg); }
.tl-bullet { width: 18px; flex: none; }
.tl-name { font-weight: 600; font-size: 13px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.tl-flow { color: var(--faint); font-family: var(--mono); font-size: 10px; flex: none; }
.tl-spanid { color: var(--faint); font-family: var(--mono); font-size: 10px; border: 1px solid var(--border); border-radius: 999px; padding: 0 6px; flex: none; }
.tl-exc { color: var(--h5); font-size: 9px; font-weight: 700; text-transform: uppercase; border: 1px solid rgba(226,102,95,0.5); border-radius: 4px; padding: 1px 5px; flex: none; }
.tl-track { position: relative; height: 22px; cursor: pointer; border-radius: 4px; background: linear-gradient(90deg, var(--border) 0 1px, transparent 1px) repeat-x; background-size: 25% 100%; }
.tl-span { position: absolute; top: 9px; height: 4px; border-radius: 999px; background: rgba(255,255,255,0.16); }
.tl-seg { position: absolute; top: 4px; height: 14px; border-radius: 3px; min-width: 2px; box-shadow: inset 0 0 0 1px rgba(0,0,0,0.28); transition: opacity 0.12s ease; }
.tl-seg.exc { outline: 1px solid var(--h5); outline-offset: 1px; }
.tl-lane .tl-track.lane { cursor: default; }
.tl-lane .tl-seg { cursor: pointer; }
.tl-lane .tl-name { color: var(--muted); font-weight: 600; }

.lane-toolbar { display: flex; gap: 16px; align-items: center; flex-wrap: wrap; margin-bottom: 12px; }
.lane-hint { color: var(--faint); font-size: 11px; }
.lane-legend { display: none; flex-wrap: wrap; gap: 7px 16px; margin-bottom: 14px; padding: 11px 14px; border: 1px solid var(--border); border-radius: 10px; background: rgba(15,18,30,0.5); }
.lane-panel.color-by-op .lane-legend { display: flex; }
.lane-key { display: flex; align-items: center; gap: 7px; font-size: 11px; color: var(--muted); font-family: var(--mono); }
.lane-key i { width: 11px; height: 11px; border-radius: 3px; flex: none; box-shadow: inset 0 0 0 1px rgba(0,0,0,0.35); }
.lane-panel.color-by-op .tl-seg { background: var(--op-color); }
.lane-panel.lens .tl-seg { opacity: 0.16; }
.lane-panel.lens .tl-seg.seg-match { opacity: 1; outline: 1px solid rgba(255,255,255,0.78); outline-offset: 1px; z-index: 3; }
.tl-dur { text-align: right; padding-left: 8px; }
.tl-dur strong { display: block; font-size: 12px; font-variant-numeric: tabular-nums; }
.tl-dur small { display: block; color: var(--faint); font-size: 10px; font-variant-numeric: tabular-nums; }

.heat-1 { background: var(--h1); }
.heat-2 { background: var(--h2); }
.heat-3 { background: var(--h3); }
.heat-4 { background: var(--h4); }
.heat-5 { background: var(--h5); }
.heat-text-1 { color: var(--h1); }
.heat-text-2 { color: var(--h2); }
.heat-text-3 { color: var(--h3); }
.heat-text-4 { color: var(--h4); }
.heat-text-5 { color: var(--h5); }

.span-panel { margin-bottom: 16px; }
.span-list { display: grid; grid-template-columns: repeat(auto-fit, minmax(260px, 1fr)); gap: 10px; }
.span-card { border: 1px solid var(--border); border-radius: 12px; padding: 12px 14px; background: rgba(0,0,0,0.16); display: grid; gap: 8px; }
.span-main { display: flex; align-items: center; gap: 8px; min-width: 0; }
.span-main code { font-family: var(--mono); color: var(--faint); font-size: 10px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.span-chip { font-family: var(--mono); font-size: 11px; color: var(--accent); background: var(--accent-soft); border: 1px solid rgba(139,123,255,0.3); border-radius: 999px; padding: 3px 10px; flex: none; }
.span-root { color: #f4e2bc; border: 1px solid rgba(227,196,92,0.35); background: rgba(227,196,92,0.14); border-radius: 999px; padding: 2px 7px; text-transform: uppercase; letter-spacing: 0.08em; font-size: 9px; font-weight: 700; }
.span-stats { display: flex; flex-wrap: wrap; gap: 8px 12px; color: var(--muted); font-size: 11px; font-variant-numeric: tabular-nums; }

.grid { display: grid; grid-template-columns: minmax(0, 1.6fr) minmax(300px, 0.8fr); gap: 16px; align-items: start; }
.flow-toolbar { display: flex; gap: 12px; align-items: center; margin-bottom: 14px; flex-wrap: wrap; }
.flow-filter { flex: 1; min-width: 180px; background: rgba(0,0,0,0.25); border: 1px solid var(--border-strong); border-radius: 9px; padding: 8px 12px; color: var(--text); font-size: 13px; font-family: var(--sans); }
.flow-filter:focus { outline: none; border-color: var(--accent); }
.flow-toggle { display: flex; align-items: center; gap: 7px; color: var(--muted); font-size: 12px; cursor: pointer; user-select: none; }
.flow-list.hide-trivial .flow-card.trivial { display: none; }
.flow-card { border: 1px solid var(--border); border-radius: 12px; margin-bottom: 10px; background: rgba(0,0,0,0.16); overflow: hidden; }
.flow-card.filtered-out { display: none; }
.flow-card.flash { animation: flash 1.3s ease; }
@keyframes flash { 0% { box-shadow: 0 0 0 1px var(--accent); border-color: var(--accent); } 100% { box-shadow: 0 0 0 0 transparent; } }
.flow-head { width: 100%; display: flex; align-items: center; gap: 12px; padding: 11px 14px; background: none; border: none; color: var(--text); cursor: pointer; text-align: left; font-family: var(--sans); }
.flow-head:hover { background: rgba(255,255,255,0.03); }
.flow-id { font-weight: 700; font-size: 13px; }
.flow-kind { font-size: 9px; font-weight: 700; text-transform: uppercase; letter-spacing: 0.08em; color: #f4e2bc; background: rgba(227,196,92,0.14); border: 1px solid rgba(227,196,92,0.35); border-radius: 5px; padding: 2px 7px; }
.flow-chip { font-family: var(--mono); font-size: 10px; color: var(--accent); background: var(--accent-soft); border: 1px solid rgba(139,123,255,0.28); border-radius: 999px; padding: 2px 8px; }
.flow-stat { font-size: 12px; font-variant-numeric: tabular-nums; }
.flow-stat.muted { color: var(--faint); }
.flow-caret { margin-left: auto; color: var(--muted); transition: transform 0.15s ease; }
.flow-card.collapsed .flow-caret { transform: rotate(-90deg); }
.flow-frames { padding: 2px 14px 12px; }
.flow-card.collapsed .flow-frames { display: none; }
.frame { display: grid; grid-template-columns: minmax(0, 1fr) auto; gap: 14px; align-items: center; padding: 8px 12px; border-radius: 9px; margin: 5px 0; background: rgba(255,255,255,0.03); border: 1px solid var(--border); }
.frame.depth-1 { margin-left: 20px; }
.frame.depth-2 { margin-left: 40px; }
.frame.depth-3 { margin-left: 60px; }
.frame.depth-4 { margin-left: 80px; }
.frame.depth-5 { margin-left: 100px; }
.frame.depth-6 { margin-left: 120px; }
.frame.exception { border-color: rgba(226,102,95,0.5); box-shadow: inset 3px 0 0 var(--h5); }
.frame-main { min-width: 0; }
.frame-main strong { display: flex; align-items: center; gap: 8px; font-size: 13px; font-weight: 600; }
.resume { font-size: 9px; font-weight: 700; text-transform: uppercase; letter-spacing: 0.06em; color: var(--muted); background: rgba(255,255,255,0.06); border: 1px solid var(--border); border-radius: 4px; padding: 1px 6px; }
.frame-main span { display: block; color: var(--faint); font-family: var(--mono); font-size: 11px; margin-top: 3px; word-break: break-all; }
.frame-meta { display: flex; align-items: center; gap: 10px; }
.frame-bar { width: 70px; height: 5px; border-radius: 999px; background: rgba(255,255,255,0.08); overflow: hidden; flex: none; }
.frame-bar i { display: block; height: 100%; border-radius: 999px; }
.frame-dur { font-size: 12px; font-variant-numeric: tabular-nums; white-space: nowrap; min-width: 56px; text-align: right; }
.frame-span { color: var(--faint); font-family: var(--mono); font-size: 10px; border: 1px solid var(--border); border-radius: 999px; padding: 0 6px; white-space: nowrap; }
.frame-id { color: var(--faint); font-size: 10px; font-family: var(--mono); white-space: nowrap; }
.frame-values { grid-column: 1 / -1; display: grid; gap: 5px; padding: 8px 10px; border-radius: 7px; background: rgba(0,0,0,0.18); border: 1px solid var(--border); }
.frame-values code { color: var(--text); font-family: var(--mono); font-size: 11px; white-space: pre-wrap; }
.frame-exception { display: flex; gap: 8px; align-items: baseline; color: #ffd0cc; font-size: 12px; }
.frame-exception span { color: var(--muted); }

.sidebar { position: sticky; top: 20px; }
.sidebar-note { color: var(--muted); font-size: 12px; margin: 0 0 14px; }
.bar-row { position: relative; display: block; padding: 12px 0 16px; border-bottom: 1px solid var(--border); text-decoration: none; color: inherit; }
.bar-row:last-child { border-bottom: none; }
.bar-row:hover strong { color: var(--accent); }
.bar-row div { display: grid; gap: 2px; padding-right: 70px; }
.bar-row strong { font-size: 13px; }
.bar-row span { font-family: var(--mono); font-size: 10px; color: var(--faint); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.bar-row em { position: absolute; right: 0; top: 12px; font-style: normal; font-size: 12px; font-variant-numeric: tabular-nums; }
.bar-row i { position: absolute; left: 0; bottom: 6px; height: 4px; border-radius: 999px; }

.topnav { position: sticky; top: 0; z-index: 30; display: flex; align-items: center; justify-content: space-between; gap: 16px; flex-wrap: wrap; margin: 0 0 18px; padding: 8px 12px; border: 1px solid var(--border); border-radius: 12px; background: rgba(10,12,20,0.82); backdrop-filter: blur(14px); }
.nav-links { display: flex; flex-wrap: wrap; gap: 4px; }
.nav-links a { text-decoration: none; color: var(--muted); font-size: 12px; font-weight: 600; padding: 6px 10px; border-radius: 8px; transition: background 0.12s ease, color 0.12s ease; }
.nav-links a:hover { color: var(--text); background: rgba(255,255,255,0.06); }
.nav-links a.active { color: var(--text); background: var(--accent-soft); }
.nav-links a.nav-exc { color: #ffb4ae; }
.nav-links a.nav-exc b { font-variant-numeric: tabular-nums; margin-left: 4px; padding: 0 6px; border-radius: 999px; background: rgba(226,102,95,0.22); border: 1px solid rgba(226,102,95,0.5); }
.nav-tools { display: flex; align-items: center; gap: 8px; }
.nav-search { background: rgba(0,0,0,0.28); border: 1px solid var(--border-strong); border-radius: 8px; padding: 7px 11px; color: var(--text); font-size: 12px; font-family: var(--sans); width: 200px; }
.nav-search:focus { outline: none; border-color: var(--accent); }
.nav-toggle { width: 32px; height: 32px; border: 1px solid var(--border-strong); border-radius: 8px; background: rgba(255,255,255,0.05); color: var(--muted); cursor: pointer; font-size: 15px; line-height: 1; }
.nav-toggle:hover { color: var(--text); }
.flow-card.search-hide { display: none; }
.tl-row.search-dim { opacity: 0.28; }

.exc-panel { margin-bottom: 16px; border-color: rgba(226,102,95,0.3); }
.exc-card { border: 1px solid rgba(226,102,95,0.35); border-radius: 14px; padding: 16px 18px; margin-bottom: 12px; background: linear-gradient(180deg, rgba(226,102,95,0.10), rgba(0,0,0,0.14)); }
.exc-card:last-child { margin-bottom: 0; }
.exc-head { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
.exc-badge { font-size: 9px; font-weight: 800; letter-spacing: 0.12em; text-transform: uppercase; color: #ffd9d5; background: rgba(226,102,95,0.2); border: 1px solid rgba(226,102,95,0.55); border-radius: 6px; padding: 3px 8px; }
.exc-type { font-size: 18px; letter-spacing: -0.01em; color: #ffb4ae; }
.exc-chip { font-family: var(--mono); font-size: 10px; color: var(--accent); background: var(--accent-soft); border: 1px solid rgba(139,123,255,0.3); border-radius: 999px; padding: 2px 8px; }
.exc-jump { margin-left: auto; font-family: var(--mono); font-size: 11px; color: var(--muted); text-decoration: none; border: 1px solid var(--border); border-radius: 999px; padding: 3px 10px; }
.exc-jump:hover { color: var(--text); border-color: var(--accent); }
.exc-message { margin: 10px 0 4px; font-size: 14px; color: #ffe0dc; }
.exc-fulltype { color: var(--faint); font-family: var(--mono); font-size: 11px; margin-bottom: 12px; }
.exc-stack { display: grid; gap: 6px; }
.exc-stack-title { color: var(--faint); text-transform: uppercase; letter-spacing: 0.12em; font-size: 10px; font-weight: 700; margin-bottom: 2px; }
.exc-stack-title small { text-transform: none; letter-spacing: 0; font-weight: 500; }
.exc-frame { border: 1px solid var(--border); border-radius: 9px; padding: 8px 12px; background: rgba(0,0,0,0.2); margin-left: calc(var(--sf-depth, 0) * 14px); }
.exc-frame.throwing { border-color: rgba(226,102,95,0.6); box-shadow: inset 3px 0 0 var(--h5); background: rgba(226,102,95,0.10); }
.exc-frame-head { display: flex; align-items: baseline; gap: 10px; flex-wrap: wrap; }
.exc-frame-name { font-weight: 700; font-size: 13px; }
.exc-frame-tag { font-size: 9px; font-weight: 700; text-transform: uppercase; letter-spacing: 0.06em; color: #ffd9d5; background: rgba(226,102,95,0.2); border: 1px solid rgba(226,102,95,0.5); border-radius: 5px; padding: 1px 6px; }
.exc-frame-full { color: var(--faint); font-family: var(--mono); font-size: 10px; word-break: break-all; }
.exc-frame-dur { margin-left: auto; color: var(--muted); font-size: 11px; font-variant-numeric: tabular-nums; }
.exc-frame-novalues { color: var(--faint); font-size: 11px; font-style: italic; margin-top: 6px; }
.exc-raw { margin-top: 12px; }
.exc-raw summary { cursor: pointer; color: var(--muted); font-size: 12px; }
.exc-raw pre { margin: 8px 0 0; padding: 12px; border: 1px solid var(--border); border-radius: 9px; background: rgba(0,0,0,0.3); color: var(--muted); font-family: var(--mono); font-size: 11px; overflow-x: auto; white-space: pre-wrap; }

.val-list { grid-column: 1 / -1; display: grid; gap: 4px; margin-top: 6px; }
.exc-frame .val-list { margin-top: 8px; }
.val-row { display: flex; align-items: baseline; gap: 8px; flex-wrap: wrap; font-family: var(--mono); font-size: 11px; padding: 3px 8px; border-radius: 6px; background: rgba(0,0,0,0.2); }
.val-row.depth-1 { margin-left: 18px; }
.val-row.depth-2 { margin-left: 36px; }
.val-row.depth-3 { margin-left: 54px; }
.val-row.depth-4 { margin-left: 72px; }
.val-kind { font-size: 9px; font-weight: 700; text-transform: uppercase; letter-spacing: 0.04em; padding: 1px 6px; border-radius: 4px; border: 1px solid var(--border-strong); color: var(--muted); flex: none; }
.val-kind.k-arg { color: #c2f0e7; border-color: rgba(70,194,172,0.4); background: rgba(70,194,172,0.14); }
.val-kind.k-local { color: #d2d6ff; border-color: rgba(139,123,255,0.4); background: var(--accent-soft); }
.val-kind.k-ret { color: #f4e2bc; border-color: rgba(227,196,92,0.4); background: rgba(227,196,92,0.14); }
.val-kind.k-this { color: #ffd0cc; border-color: rgba(226,102,95,0.4); background: rgba(226,102,95,0.12); }
.val-name { color: var(--text); font-weight: 600; }
.val-eq { color: var(--faint); }
.val-val { color: #b7f7c6; word-break: break-all; }
.val-type { color: var(--faint); }
.val-type::before { content: ':'; margin-right: 4px; }
.val-flag { font-size: 9px; text-transform: uppercase; letter-spacing: 0.04em; color: #f4e2bc; border: 1px solid rgba(227,196,92,0.4); background: rgba(227,196,92,0.12); border-radius: 4px; padding: 0 5px; }

body.light {
  color-scheme: light;
  --bg: #f4f6fc;
  --panel: rgba(255,255,255,0.86);
  --panel-solid: #ffffff;
  --border: rgba(15,20,40,0.12);
  --border-strong: rgba(15,20,40,0.2);
  --text: #121627;
  --muted: #545c74;
  --faint: #7b849c;
  --accent: #6a5af0;
  --accent-soft: rgba(106,90,240,0.12);
  --h1: #14876f; --h2: #2f9e44; --h3: #b8860b; --h4: #c05621; --h5: #d64545;
  background:
    radial-gradient(1200px 600px at 12% -5%, rgba(106,90,240,0.10), transparent 60%),
    radial-gradient(900px 500px at 100% 0%, rgba(20,135,111,0.08), transparent 55%),
    linear-gradient(180deg, #f4f6fc 0%, #eef1f9 100%);
}
body.light .topnav { background: rgba(255,255,255,0.9); }
body.light .legend-item,
body.light .lane-legend { background: rgba(255,255,255,0.78); border-color: rgba(106,90,240,0.14); box-shadow: 0 1px 2px rgba(15,20,40,0.04); }
body.light .nav-search,
body.light .flow-filter { background: #ffffff; border-color: rgba(106,90,240,0.24); color: var(--text); box-shadow: inset 0 1px 2px rgba(15,20,40,0.04); }
body.light .nav-search::placeholder,
body.light .flow-filter::placeholder { color: #747c94; }
body.light .nav-toggle,
body.light .tl-toggle { background: #ffffff; border-color: rgba(106,90,240,0.22); color: #545c74; }
body.light .nav-links a:hover { background: rgba(106,90,240,0.08); }
body.light .span-card,
body.light .flow-card,
body.light .exc-frame { background: rgba(255,255,255,0.72); }
body.light .frame { background: rgba(255,255,255,0.64); }
body.light .frame-bar { background: rgba(106,90,240,0.10); }
body.light .frame-values { background: rgba(106,90,240,0.045); border-color: rgba(106,90,240,0.14); }
body.light .val-row { background: rgba(255,255,255,0.78); border: 1px solid rgba(106,90,240,0.10); box-shadow: 0 1px 0 rgba(15,20,40,0.03); }
body.light .chip { color: #26304f; background: #ffffff; border-color: rgba(84,92,116,0.22); }
body.light .chip.flow,
body.light .trace-chip,
body.light .span-chip,
body.light .flow-chip,
body.light .exc-chip { color: #5141d8; background: rgba(106,90,240,0.12); border-color: rgba(106,90,240,0.28); }
body.light .chip.frame { color: #08745f; background: rgba(20,135,111,0.11); border-color: rgba(20,135,111,0.26); }
body.light .chip.async,
body.light .flow-kind,
body.light .span-root,
body.light .resume,
body.light .val-flag { color: #8a5a00; background: rgba(184,134,11,0.12); border-color: rgba(184,134,11,0.30); }
body.light .frame-span,
body.light .tl-spanid { color: #5141d8; background: rgba(106,90,240,0.08); border-color: rgba(106,90,240,0.18); }
body.light .val-kind.k-arg { color: #08745f; border-color: rgba(20,135,111,0.30); background: rgba(20,135,111,0.10); }
body.light .val-kind.k-local { color: #5141d8; border-color: rgba(106,90,240,0.28); background: rgba(106,90,240,0.10); }
body.light .val-kind.k-ret { color: #8a5a00; border-color: rgba(184,134,11,0.30); background: rgba(184,134,11,0.12); }
body.light .val-kind.k-this,
body.light .exc-badge,
body.light .exc-frame-tag { color: #b42d28; border-color: rgba(214,69,69,0.30); background: rgba(214,69,69,0.10); }
body.light .val-val { color: #197a3a; }
body.light .exc-type { color: #c0362f; }

@media (max-width: 1040px) {
  .masthead, .grid { grid-template-columns: 1fr; }
  .metrics { grid-template-columns: repeat(2, 1fr); }
  .legend { grid-template-columns: 1fr 1fr; }
  .sidebar { position: static; }
  .tl-ruler, .tl-row { grid-template-columns: minmax(150px, 220px) 1fr 96px; }
}
";

        private const string Script = @"
(function () {
  var collapsed = {};
  var rows = Array.prototype.slice.call(document.querySelectorAll('.tl-row'));
  function applyCollapse() {
    rows.forEach(function (row) {
      var path = row.getAttribute('data-path') || '';
      var hidden = false;
      for (var key in collapsed) {
        if (collapsed[key] && path !== key && path.indexOf(key + '/') === 0) { hidden = true; break; }
      }
      row.classList.toggle('tl-hidden', hidden);
    });
  }
  document.querySelectorAll('.tl-toggle').forEach(function (btn) {
    btn.addEventListener('click', function (e) {
      e.stopPropagation();
      var p = btn.getAttribute('data-path');
      collapsed[p] = !collapsed[p];
      btn.classList.toggle('collapsed', !!collapsed[p]);
      btn.setAttribute('aria-expanded', collapsed[p] ? 'false' : 'true');
      applyCollapse();
    });
  });

  function jumpToFlow(id) {
    var card = document.getElementById('flow-' + id);
    if (!card) { return; }
    card.classList.remove('collapsed');
    card.scrollIntoView({ behavior: 'smooth', block: 'center' });
    card.classList.remove('flash');
    void card.offsetWidth;
    card.classList.add('flash');
  }

  document.querySelectorAll('.tl-track[data-flow]').forEach(function (track) {
    track.addEventListener('click', function () { jumpToFlow(track.getAttribute('data-flow')); });
  });

  document.querySelectorAll('.tl-seg[data-flow]').forEach(function (seg) {
    seg.addEventListener('click', function (e) {
      e.stopPropagation();
      jumpToFlow(seg.getAttribute('data-flow'));
    });
  });

  document.querySelectorAll('.flow-head').forEach(function (head) {
    head.addEventListener('click', function () {
      head.parentElement.classList.toggle('collapsed');
    });
    head.addEventListener('keydown', function (e) {
      if (e.key === 'Enter' || e.key === ' ') {
        e.preventDefault();
        head.parentElement.classList.toggle('collapsed');
      }
    });
  });

  document.querySelectorAll('.chip-link').forEach(function (link) {
    link.addEventListener('click', function (e) { e.stopPropagation(); });
  });

  var lanePanel = document.getElementById('lane-panel');
  if (lanePanel) {
    var laneColorOp = document.getElementById('lane-color-op');
    if (laneColorOp) {
      laneColorOp.addEventListener('change', function () {
        lanePanel.classList.toggle('color-by-op', laneColorOp.checked);
      });
    }

    var laneSegs = Array.prototype.slice.call(lanePanel.querySelectorAll('.tl-seg[data-flow]'));
    laneSegs.forEach(function (seg) {
      seg.addEventListener('mouseenter', function () {
        var flow = seg.getAttribute('data-flow');
        lanePanel.classList.add('lens');
        laneSegs.forEach(function (other) {
          other.classList.toggle('seg-match', other.getAttribute('data-flow') === flow);
        });
      });
      seg.addEventListener('mouseleave', function () {
        lanePanel.classList.remove('lens');
        laneSegs.forEach(function (other) { other.classList.remove('seg-match'); });
      });
    });
  }

  var list = document.getElementById('flow-list');
  var hideTrivial = document.getElementById('hide-trivial');
  var filter = document.getElementById('flow-filter');
  var cards = Array.prototype.slice.call(document.querySelectorAll('.flow-card'));

  function refresh() {
    var q = filter ? filter.value.trim().toLowerCase() : '';
    if (list && hideTrivial) { list.classList.toggle('hide-trivial', hideTrivial.checked && q.length === 0); }
    cards.forEach(function (card) {
      if (!q) { card.classList.remove('filtered-out'); return; }
      var match = false;
      card.querySelectorAll('.frame').forEach(function (f) {
        if ((f.getAttribute('data-name') || '').indexOf(q) !== -1) { match = true; }
      });
      card.classList.toggle('filtered-out', !match);
    });
  }

  if (hideTrivial) { hideTrivial.addEventListener('change', refresh); }
  if (filter) { filter.addEventListener('input', refresh); }
  refresh();

  var themeToggle = document.getElementById('theme-toggle');
  if (themeToggle) {
    themeToggle.addEventListener('click', function () {
      document.body.classList.toggle('light');
    });
  }

  var gsearch = document.getElementById('global-search');
  if (gsearch) {
    var searchFlowCards = Array.prototype.slice.call(document.querySelectorAll('.flow-card'));
    var searchTlRows = Array.prototype.slice.call(document.querySelectorAll('#timeline .tl-row'));
    var globalFilter = function () {
      var q = gsearch.value.trim().toLowerCase();
      searchFlowCards.forEach(function (card) {
        if (!q) { card.classList.remove('search-hide'); return; }
        var match = false;
        card.querySelectorAll('.frame').forEach(function (f) {
          if ((f.getAttribute('data-name') || '').indexOf(q) !== -1) { match = true; }
        });
        card.classList.toggle('search-hide', !match);
      });
      searchTlRows.forEach(function (row) {
        if (!q) { row.classList.remove('search-dim'); return; }
        row.classList.toggle('search-dim', (row.getAttribute('data-name') || '').indexOf(q) === -1);
      });
    };
    gsearch.addEventListener('input', globalFilter);
  }

  var navLinks = Array.prototype.slice.call(document.querySelectorAll('.nav-links a'));
  var sectionMap = {};
  navLinks.forEach(function (link) {
    var id = link.getAttribute('href').substring(1);
    var el = document.getElementById(id);
    if (el) { sectionMap[id] = link; }
  });
  if (window.IntersectionObserver && Object.keys(sectionMap).length) {
    var spy = new IntersectionObserver(function (entries) {
      entries.forEach(function (entry) {
        if (entry.isIntersecting) {
          navLinks.forEach(function (l) { l.classList.remove('active'); });
          var link = sectionMap[entry.target.id];
          if (link) { link.classList.add('active'); }
        }
      });
    }, { rootMargin: '-45% 0px -50% 0px', threshold: 0 });
    Object.keys(sectionMap).forEach(function (id) { spy.observe(document.getElementById(id)); });
  }
})();
";
    }

    private readonly record struct AsyncOperationKey(string LogicalName, ulong FlowId);

    private readonly record struct FrameKey(ulong FlowId, ulong FrameId);

    private readonly record struct CapturedValueView(
        FlowCapturePhase Phase,
        FlowValueKind Kind,
        string Name,
        string DisplayName,
        string Value,
        string TypeName,
        FlowNotCapturedReason NotCaptured)
    {
        public CapturedValueView WithDisplayName(string displayName)
        {
            return new CapturedValueView(Phase, Kind, Name, displayName, Value, TypeName, NotCaptured);
        }
    }

    private sealed class CapturedValueNode
    {
        public CapturedValueNode(CapturedValueView value)
        {
            Value = value;
        }

        public CapturedValueView Value { get; set; }

        public List<CapturedValueNode> Children { get; } = new();
    }

    private static class FlowEventReader
    {
        private const int Magic = 0x44464c50;
        private const int Version = 6;

        public static FlowEventFile Read(string path)
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new BinaryReader(stream);

            if (reader.ReadInt32() != Magic)
            {
                throw new InvalidDataException("The flow recorder file header is invalid.");
            }

            var version = reader.ReadInt32();
            if (version is < 1 or > Version)
            {
                throw new InvalidDataException("The flow recorder file version is not supported.");
            }

            var count = reader.ReadInt32();
            var events = new FlowEvent[count];
            for (var i = 0; i < count; i++)
            {
                var kind = (FlowEventKind)reader.ReadByte();
                var timestamp = reader.ReadInt64();
                var methodMetadataIndex = reader.ReadInt32();
                var flowId = reader.ReadUInt64();
                var frameId = reader.ReadUInt64();
                var parentFrameId = reader.ReadUInt64();
                var depth = reader.ReadInt32();
                var threadId = reader.ReadInt32();
                if (version < 6)
                {
                    _ = reader.ReadUInt64();
                    _ = reader.ReadUInt64();
                    _ = reader.ReadUInt64();
                    _ = reader.ReadUInt64();
                }

                events[i] = new FlowEvent(kind, timestamp, methodMetadataIndex, flowId, frameId, parentFrameId, depth, threadId, reader.ReadInt64(), version >= 5 ? reader.ReadUInt64() : 0);
            }

            if (version == 1)
            {
                return new FlowEventFile(events, Array.Empty<FlowMethodMetadata>());
            }

            var methodCount = reader.ReadInt32();
            if (methodCount < 0)
            {
                throw new InvalidDataException("The flow recorder method metadata count is invalid.");
            }

            var methods = new FlowMethodMetadata[methodCount];
            for (var i = 0; i < methodCount; i++)
            {
                methods[i] = new FlowMethodMetadata(reader.ReadInt32(), ReadString(reader, version));
            }

            if (version < 4)
            {
                return new FlowEventFile(events, methods);
            }

            var strings = ReadStringArray(reader, "string table", version);
            var types = ReadStringArray(reader, "type table", version);

            var exceptionCount = ReadNonNegativeCount(reader, "exception details");
            var exceptions = new FlowExceptionDetails[exceptionCount];
            for (var i = 0; i < exceptionCount; i++)
            {
                exceptions[i] = new FlowExceptionDetails(
                    reader.ReadUInt64(),
                    reader.ReadUInt64(),
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    reader.ReadInt32());
            }

            var valueCount = ReadNonNegativeCount(reader, "captured value");
            var values = new FlowCapturedValue[valueCount];
            for (var i = 0; i < valueCount; i++)
            {
                values[i] = new FlowCapturedValue(
                    reader.ReadUInt64(),
                    reader.ReadUInt64(),
                    (FlowCapturePhase)reader.ReadByte(),
                    (FlowValueKind)reader.ReadByte(),
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    (FlowValueTag)reader.ReadByte(),
                    (FlowNotCapturedReason)reader.ReadByte(),
                    reader.ReadInt64(),
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    reader.ReadInt32());
            }

            if (version < 5)
            {
                return new FlowEventFile(events, methods, strings, types, exceptions, values);
            }

            var operationCount = ReadNonNegativeCount(reader, "operation metadata");
            var operations = new FlowOperationMetadata[operationCount];
            for (var i = 0; i < operationCount; i++)
            {
                operations[i] = new FlowOperationMetadata(
                    reader.ReadUInt64(),
                    reader.ReadInt64(),
                    ReadString(reader, version),
                    ReadString(reader, version),
                    reader.ReadInt64(),
                    reader.ReadUInt64(),
                    reader.ReadUInt64(),
                    reader.ReadUInt64(),
                    reader.ReadUInt64());
            }

            return new FlowEventFile(events, methods, strings, types, exceptions, values, operations);
        }

        private static string[] ReadStringArray(BinaryReader reader, string sectionName, int version)
        {
            var count = ReadNonNegativeCount(reader, sectionName);
            var values = new string[count];
            for (var i = 0; i < count; i++)
            {
                values[i] = ReadString(reader, version);
            }

            return values;
        }

        private static string ReadString(BinaryReader reader, int version)
        {
            if (version < 6)
            {
                return reader.ReadString();
            }

            var byteCount = ReadNonNegativeCount(reader, "string length");
            if (byteCount == 0)
            {
                return string.Empty;
            }

            var bytes = reader.ReadBytes(byteCount);
            if (bytes.Length != byteCount)
            {
                throw new EndOfStreamException();
            }

            return Encoding.UTF8.GetString(bytes, 0, bytes.Length);
        }

        private static int ReadNonNegativeCount(BinaryReader reader, string sectionName)
        {
            var count = reader.ReadInt32();
            if (count < 0)
            {
                throw new InvalidDataException("The flow recorder " + sectionName + " count is invalid.");
            }

            return count;
        }
    }

    private enum FlowEventKind : byte
    {
        Enter = 1,
        Exit = 2,
        Exception = 3,
        AsyncEdge = 4,
        Truncated = 5,
        Suppressed = 6
    }

    private readonly record struct FlowEvent(
        FlowEventKind Kind,
        long Timestamp,
        int MethodMetadataIndex,
        ulong FlowId,
        ulong FrameId,
        ulong ParentFrameId,
        int Depth,
        int ThreadId,
        long ExceptionTypeId,
        ulong OperationId);

    private readonly record struct FlowMethodMetadata(int MethodMetadataIndex, string DisplayName);

    private enum FlowCapturePhase : byte
    {
        Entry = 1,
        Exit = 2,
        Exception = 3
    }

    private enum FlowValueKind : byte
    {
        This = 1,
        Argument = 2,
        Local = 3,
        Return = 4,
        Exception = 5
    }

    private enum FlowValueTag : byte
    {
        Null = 1,
        Boolean = 2,
        Int64 = 3,
        UInt64 = 4,
        Double = 5,
        Decimal = 6,
        String = 7,
        TypeSummary = 8,
        CollectionSummary = 9,
        NotCaptured = 10
    }

    private enum FlowNotCapturedReason : byte
    {
        None = 0,
        Depth = 1,
        CollectionSize = 2,
        PayloadLimit = 3,
        Unsupported = 4
    }

    private readonly record struct FlowExceptionDetails(ulong FlowId, ulong FrameId, int TypeId, int MessageId, int StackId, int HResult);

    private readonly record struct FlowCapturedValue(
        ulong FlowId,
        ulong FrameId,
        FlowCapturePhase Phase,
        FlowValueKind Kind,
        int NameId,
        int TypeId,
        FlowValueTag Tag,
        FlowNotCapturedReason NotCapturedReason,
        long NumberValue,
        int StringId,
        int ItemCount,
        int CapturedItemCount);

    private readonly record struct FlowOperationMetadata(
        ulong OperationId,
        long Generation,
        string TriggerReason,
        string Root,
        long StartTimestamp,
        ulong TraceIdUpper,
        ulong TraceIdLower,
        ulong RootSpanId,
        ulong ActiveSpanId);

    private readonly record struct FlowEventFile(
        FlowEvent[] Events,
        FlowMethodMetadata[] Methods,
        string[] Strings,
        string[] Types,
        FlowExceptionDetails[] Exceptions,
        FlowCapturedValue[] Values,
        FlowOperationMetadata[] Operations)
    {
        public FlowEventFile(FlowEvent[] events, FlowMethodMetadata[] methods)
            : this(events, methods, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<FlowExceptionDetails>(), Array.Empty<FlowCapturedValue>(), Array.Empty<FlowOperationMetadata>())
        {
        }

        public FlowEventFile(
            FlowEvent[] events,
            FlowMethodMetadata[] methods,
            string[] strings,
            string[] types,
            FlowExceptionDetails[] exceptions,
            FlowCapturedValue[] values)
            : this(events, methods, strings, types, exceptions, values, Array.Empty<FlowOperationMetadata>())
        {
        }
    }
}
