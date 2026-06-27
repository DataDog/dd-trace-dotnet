// <copyright file="FlowEventBinaryFormat.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Datadog.Trace.Util;

namespace Datadog.Trace.Debugger.LiveDebuggerPoc
{
    internal static class FlowEventBinaryFormat
    {
        public const int Version = 1;
        private const int Magic = 0x44464c50; // DFLP

        public static void Write(string path, FlowEvent[] events)
        {
            var directory = Path.GetDirectoryName(path);
            if (!StringUtil.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            Write(stream, events);
        }

        public static void Write(Stream stream, FlowEvent[] events)
        {
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            writer.Write(Magic);
            writer.Write(Version);
            writer.Write(events.Length);

            foreach (var flowEvent in events)
            {
                Write(writer, flowEvent);
            }
        }

        public static FlowEvent[] Read(string path)
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return Read(stream);
        }

        public static FlowEvent[] Read(Stream stream)
        {
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            var magic = reader.ReadInt32();
            if (magic != Magic)
            {
                throw new InvalidDataException("The flow recorder file header is invalid.");
            }

            var version = reader.ReadInt32();
            if (version != Version)
            {
                throw new InvalidDataException("The flow recorder file version is not supported.");
            }

            var count = reader.ReadInt32();
            if (count < 0)
            {
                throw new InvalidDataException("The flow recorder event count is invalid.");
            }

            var events = new List<FlowEvent>(count);
            for (var i = 0; i < count; i++)
            {
                events.Add(ReadEvent(reader));
            }

            return events.ToArray();
        }

        private static void Write(BinaryWriter writer, in FlowEvent flowEvent)
        {
            writer.Write((byte)flowEvent.Kind);
            writer.Write(flowEvent.Timestamp);
            writer.Write(flowEvent.MethodMetadataIndex);
            writer.Write(flowEvent.FlowId);
            writer.Write(flowEvent.FrameId);
            writer.Write(flowEvent.ParentFrameId);
            writer.Write(flowEvent.Depth);
            writer.Write(flowEvent.ThreadId);
            writer.Write(flowEvent.TraceIdUpper);
            writer.Write(flowEvent.TraceIdLower);
            writer.Write(flowEvent.RootSpanId);
            writer.Write(flowEvent.ActiveSpanId);
            writer.Write(flowEvent.ExceptionTypeId);
        }

        private static FlowEvent ReadEvent(BinaryReader reader)
        {
            return new FlowEvent(
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
    }
}
