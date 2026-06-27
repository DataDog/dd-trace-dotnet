// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Linq;

namespace Samples.LiveDebuggerPoc.Viewer;

internal static class Program
{
    private static int Main(string[] args)
    {
        var path = args.Length > 0 ? args[0] : null;
        if (string.IsNullOrWhiteSpace(path))
        {
            System.Console.Error.WriteLine("Usage: Samples.LiveDebuggerPoc.Viewer <flow-events.dflp>");
            return 1;
        }

        var events = FlowEventReader.Read(path);
        if (events.Length == 0)
        {
            System.Console.WriteLine("No flow events found.");
            return 0;
        }

        foreach (var flow in events.GroupBy(flowEvent => flowEvent.FlowId).OrderBy(group => group.Key))
        {
            RenderFlow(flow.Key, flow.ToArray());
        }

        return 0;
    }

    private static void RenderFlow(ulong flowId, FlowEvent[] events)
    {
        var frames = BuildFrames(events);
        var first = events.Min(flowEvent => flowEvent.Timestamp);
        var last = events.Max(flowEvent => flowEvent.Timestamp);
        var duration = ToMilliseconds(last - first);
        var trace = events.FirstOrDefault(flowEvent => flowEvent.TraceIdLower != 0 || flowEvent.TraceIdUpper != 0);

        System.Console.WriteLine("Flow " + flowId + " (" + Format(duration) + " ms, " + events.Length + " events)");
        if (trace.TraceIdLower != 0 || trace.TraceIdUpper != 0)
        {
            System.Console.WriteLine("  Trace: " + trace.TraceIdUpper.ToString("x16", CultureInfo.InvariantCulture) + trace.TraceIdLower.ToString("x16", CultureInfo.InvariantCulture) + ", root span: " + trace.RootSpanId + ", active span: " + trace.ActiveSpanId);
        }

        foreach (var frame in frames.Values.Where(frame => frame.ParentFrameId == 0).OrderBy(frame => frame.StartTimestamp))
        {
            RenderFrame(frame, frames, indent: 1);
        }
    }

    private static Dictionary<ulong, Frame> BuildFrames(FlowEvent[] events)
    {
        var frames = new Dictionary<ulong, Frame>();
        foreach (var flowEvent in events)
        {
            if (!frames.TryGetValue(flowEvent.FrameId, out var frame))
            {
                frame = new Frame(flowEvent.FrameId, flowEvent.ParentFrameId, flowEvent.MethodMetadataIndex, flowEvent.Depth, flowEvent.Timestamp);
                frames[flowEvent.FrameId] = frame;
            }

            switch (flowEvent.Kind)
            {
                case FlowEventKind.Enter:
                    frame.StartTimestamp = flowEvent.Timestamp;
                    break;
                case FlowEventKind.Exit:
                    frame.EndTimestamp = flowEvent.Timestamp;
                    break;
                case FlowEventKind.Exception:
                    frame.ExceptionTypeId = flowEvent.ExceptionTypeId;
                    break;
            }
        }

        return frames;
    }

    private static void RenderFrame(Frame frame, Dictionary<ulong, Frame> frames, int indent)
    {
        var prefix = new string(' ', indent * 2);
        var duration = frame.EndTimestamp == 0 ? 0 : ToMilliseconds(frame.EndTimestamp - frame.StartTimestamp);
        var exception = frame.ExceptionTypeId == 0 ? string.Empty : " exception-type-id=" + frame.ExceptionTypeId;
        System.Console.WriteLine(prefix + "- method#" + frame.MethodMetadataIndex + " frame=" + frame.FrameId + " duration=" + Format(duration) + "ms" + exception);

        foreach (var child in frames.Values.Where(candidate => candidate.ParentFrameId == frame.FrameId).OrderBy(candidate => candidate.StartTimestamp))
        {
            RenderFrame(child, frames, indent + 1);
        }
    }

    private static double ToMilliseconds(long elapsedTimestamp)
    {
        return elapsedTimestamp * 1000.0 / Stopwatch.Frequency;
    }

    private static string Format(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private sealed class Frame
    {
        public Frame(ulong frameId, ulong parentFrameId, int methodMetadataIndex, int depth, long startTimestamp)
        {
            FrameId = frameId;
            ParentFrameId = parentFrameId;
            MethodMetadataIndex = methodMetadataIndex;
            Depth = depth;
            StartTimestamp = startTimestamp;
        }

        public ulong FrameId { get; }

        public ulong ParentFrameId { get; }

        public int MethodMetadataIndex { get; }

        public int Depth { get; }

        public long StartTimestamp { get; set; }

        public long EndTimestamp { get; set; }

        public long ExceptionTypeId { get; set; }
    }

    private static class FlowEventReader
    {
        private const int Magic = 0x44464c50;
        private const int Version = 1;

        public static FlowEvent[] Read(string path)
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new BinaryReader(stream);

            if (reader.ReadInt32() != Magic)
            {
                throw new InvalidDataException("The flow recorder file header is invalid.");
            }

            if (reader.ReadInt32() != Version)
            {
                throw new InvalidDataException("The flow recorder file version is not supported.");
            }

            var count = reader.ReadInt32();
            var events = new FlowEvent[count];
            for (var i = 0; i < count; i++)
            {
                events[i] = new FlowEvent(
                    (FlowEventKind)reader.ReadByte(),
                    reader.ReadInt64(),
                    reader.ReadInt32(),
                    reader.ReadUInt64(),
                    reader.ReadUInt64(),
                    reader.ReadUInt64(),
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    reader.ReadUInt64(),
                    reader.ReadUInt64(),
                    reader.ReadUInt64(),
                    reader.ReadUInt64(),
                    reader.ReadInt64());
            }

            return events;
        }
    }

    private enum FlowEventKind : byte
    {
        Enter = 1,
        Exit = 2,
        Exception = 3
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
        ulong TraceIdUpper,
        ulong TraceIdLower,
        ulong RootSpanId,
        ulong ActiveSpanId,
        long ExceptionTypeId);
}
