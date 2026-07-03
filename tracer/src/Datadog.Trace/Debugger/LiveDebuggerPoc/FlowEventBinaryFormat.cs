// <copyright file="FlowEventBinaryFormat.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Datadog.Trace.Util;

namespace Datadog.Trace.Debugger.LiveDebuggerPoc
{
    internal static class FlowEventBinaryFormat
    {
        public const int Version = 6;
        private const int Magic = 0x44464c50; // DFLP
        private const int FlowExceptionDetailsBinarySize = (sizeof(ulong) * 2) + (sizeof(int) * 4);
        private const int FlowCapturedValueBinarySize = (sizeof(ulong) * 2) + (sizeof(byte) * 4) + (sizeof(int) * 5) + sizeof(long);
        private const int MaxFixedRecordBinarySize = FlowEvent.BinarySize;

        public static void Write(string path, FlowEvent[] events)
        {
            Write(path, events, Array.Empty<FlowMethodMetadata>());
        }

        public static void Write(string path, FlowEvent[] events, FlowMethodMetadata[] methods)
        {
            Write(path, new FlowEventFile(events, methods));
        }

        public static void Write(string path, FlowEventFile file)
        {
            var directory = Path.GetDirectoryName(path);
            if (!StringUtil.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            Write(stream, file);
        }

        public static void Write(Stream stream, FlowEvent[] events)
        {
            Write(stream, events, Array.Empty<FlowMethodMetadata>());
        }

        public static void Write(Stream stream, FlowEvent[] events, FlowMethodMetadata[] methods)
        {
            Write(stream, new FlowEventFile(events, methods));
        }

        public static void Write(Stream stream, FlowEventFile file)
        {
            var scratch = new byte[MaxFixedRecordBinarySize];
            var stringScratch = new byte[256];
            WriteInt32(stream, scratch, Magic);
            WriteInt32(stream, scratch, Version);
            WriteInt32(stream, scratch, file.Events.Length);
            foreach (var flowEvent in file.Events)
            {
                Write(stream, flowEvent, scratch);
            }

            WriteInt32(stream, scratch, file.Methods.Length);
            foreach (var method in file.Methods)
            {
                WriteInt32(stream, scratch, method.MethodMetadataIndex);
                WriteString(stream, method.DisplayName, ref stringScratch, scratch);
            }

            WriteInt32(stream, scratch, file.Strings.Count);
            foreach (var value in file.Strings)
            {
                WriteString(stream, value, ref stringScratch, scratch);
            }

            WriteInt32(stream, scratch, file.Types.Count);
            foreach (var value in file.Types)
            {
                WriteString(stream, value, ref stringScratch, scratch);
            }

            WriteInt32(stream, scratch, file.Exceptions.Length);
            foreach (var exception in file.Exceptions)
            {
                Write(stream, exception, scratch);
            }

            WriteInt32(stream, scratch, file.Values.Length);
            foreach (var value in file.Values)
            {
                Write(stream, value, scratch);
            }

            WriteInt32(stream, scratch, file.Operations.Length);
            foreach (var operation in file.Operations)
            {
                Write(stream, operation, scratch, ref stringScratch);
            }
        }

        public static FlowEvent[] Read(string path)
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return Read(stream).Events;
        }

        public static FlowEventFile Read(Stream stream)
        {
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            var magic = reader.ReadInt32();
            if (magic != Magic)
            {
                throw new InvalidDataException("The flow recorder file header is invalid.");
            }

            var version = reader.ReadInt32();
            if (version is < 1 or > Version)
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
                events.Add(ReadEvent(reader, version));
            }

            if (version == 1)
            {
                return new FlowEventFile(events.ToArray(), Array.Empty<FlowMethodMetadata>());
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
                return new FlowEventFile(events.ToArray(), methods);
            }

            var stringCount = ReadNonNegativeCount(reader, "string table");
            var strings = new string[stringCount];
            for (var i = 0; i < stringCount; i++)
            {
                strings[i] = ReadString(reader, version);
            }

            var typeCount = ReadNonNegativeCount(reader, "type table");
            var types = new string[typeCount];
            for (var i = 0; i < typeCount; i++)
            {
                types[i] = ReadString(reader, version);
            }

            var exceptionCount = ReadNonNegativeCount(reader, "exception details");
            var exceptions = new FlowExceptionDetails[exceptionCount];
            for (var i = 0; i < exceptionCount; i++)
            {
                exceptions[i] = ReadExceptionDetails(reader);
            }

            var valueCount = ReadNonNegativeCount(reader, "captured value");
            var values = new FlowCapturedValue[valueCount];
            for (var i = 0; i < valueCount; i++)
            {
                values[i] = ReadCapturedValue(reader);
            }

            if (version < 5)
            {
                return new FlowEventFile(events.ToArray(), methods, strings, types, exceptions, values);
            }

            var operationCount = ReadNonNegativeCount(reader, "operation metadata");
            var operations = new FlowOperationMetadata[operationCount];
            for (var i = 0; i < operationCount; i++)
            {
                operations[i] = ReadOperationMetadata(reader, version);
            }

            return new FlowEventFile(events.ToArray(), methods, strings, types, exceptions, values, operations);
        }

        private static void Write(Stream stream, in FlowEvent flowEvent, byte[] scratch)
        {
            var offset = 0;
            WriteByte(scratch, ref offset, (byte)flowEvent.Kind);
            WriteInt64(scratch, ref offset, flowEvent.Timestamp);
            WriteInt32(scratch, ref offset, flowEvent.MethodMetadataIndex);
            WriteUInt64(scratch, ref offset, flowEvent.FlowId);
            WriteUInt64(scratch, ref offset, flowEvent.FrameId);
            WriteUInt64(scratch, ref offset, flowEvent.ParentFrameId);
            WriteInt32(scratch, ref offset, flowEvent.Depth);
            WriteInt32(scratch, ref offset, flowEvent.ThreadId);
            WriteInt64(scratch, ref offset, flowEvent.ExceptionTypeId);
            WriteUInt64(scratch, ref offset, flowEvent.OperationId);
            stream.Write(scratch, 0, FlowEvent.BinarySize);
        }

        private static void Write(Stream stream, in FlowOperationMetadata operation, byte[] scratch, ref byte[] stringScratch)
        {
            WriteUInt64(stream, scratch, operation.OperationId);
            WriteInt64(stream, scratch, operation.Generation);
            WriteString(stream, operation.TriggerReason, ref stringScratch, scratch);
            WriteString(stream, operation.Root, ref stringScratch, scratch);
            WriteInt64(stream, scratch, operation.StartTimestamp);
            WriteUInt64(stream, scratch, operation.TraceIdUpper);
            WriteUInt64(stream, scratch, operation.TraceIdLower);
            WriteUInt64(stream, scratch, operation.RootSpanId);
            WriteUInt64(stream, scratch, operation.ActiveSpanId);
        }

        private static void Write(Stream stream, in FlowExceptionDetails exception, byte[] scratch)
        {
            var offset = 0;
            WriteUInt64(scratch, ref offset, exception.FlowId);
            WriteUInt64(scratch, ref offset, exception.FrameId);
            WriteInt32(scratch, ref offset, exception.TypeId);
            WriteInt32(scratch, ref offset, exception.MessageId);
            WriteInt32(scratch, ref offset, exception.StackId);
            WriteInt32(scratch, ref offset, exception.HResult);
            stream.Write(scratch, 0, FlowExceptionDetailsBinarySize);
        }

        private static void Write(Stream stream, in FlowCapturedValue value, byte[] scratch)
        {
            var offset = 0;
            WriteUInt64(scratch, ref offset, value.FlowId);
            WriteUInt64(scratch, ref offset, value.FrameId);
            WriteByte(scratch, ref offset, (byte)value.Phase);
            WriteByte(scratch, ref offset, (byte)value.Kind);
            WriteInt32(scratch, ref offset, value.NameId);
            WriteInt32(scratch, ref offset, value.TypeId);
            WriteByte(scratch, ref offset, (byte)value.Tag);
            WriteByte(scratch, ref offset, (byte)value.NotCapturedReason);
            WriteInt64(scratch, ref offset, value.NumberValue);
            WriteInt32(scratch, ref offset, value.StringId);
            WriteInt32(scratch, ref offset, value.ItemCount);
            WriteInt32(scratch, ref offset, value.CapturedItemCount);
            stream.Write(scratch, 0, FlowCapturedValueBinarySize);
        }

        private static void WriteString(Stream stream, string? value, ref byte[] stringScratch, byte[] intScratch)
        {
            value ??= string.Empty;
            var byteCount = Encoding.UTF8.GetByteCount(value);
            WriteInt32(stream, intScratch, byteCount);
            if (byteCount == 0)
            {
                return;
            }

            var buffer = stringScratch;
            if (byteCount > buffer.Length)
            {
                buffer = new byte[byteCount];
                stringScratch = buffer;
            }

            var written = Encoding.UTF8.GetBytes(value, 0, value.Length, buffer, 0);
            stream.Write(buffer, 0, written);
        }

        private static void WriteInt32(Stream stream, byte[] scratch, int value)
        {
            var offset = 0;
            WriteInt32(scratch, ref offset, value);
            stream.Write(scratch, 0, sizeof(int));
        }

        private static void WriteInt64(Stream stream, byte[] scratch, long value)
        {
            var offset = 0;
            WriteInt64(scratch, ref offset, value);
            stream.Write(scratch, 0, sizeof(long));
        }

        private static void WriteUInt64(Stream stream, byte[] scratch, ulong value)
        {
            var offset = 0;
            WriteUInt64(scratch, ref offset, value);
            stream.Write(scratch, 0, sizeof(ulong));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteByte(byte[] buffer, ref int offset, byte value)
        {
            buffer[offset] = value;
            offset++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteInt32(byte[] buffer, ref int offset, int value)
        {
            unchecked
            {
                buffer[offset] = (byte)value;
                buffer[offset + 1] = (byte)(value >> 8);
                buffer[offset + 2] = (byte)(value >> 16);
                buffer[offset + 3] = (byte)(value >> 24);
            }

            offset += sizeof(int);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteInt64(byte[] buffer, ref int offset, long value)
        {
            WriteUInt64(buffer, ref offset, unchecked((ulong)value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteUInt64(byte[] buffer, ref int offset, ulong value)
        {
            unchecked
            {
                buffer[offset] = (byte)value;
                buffer[offset + 1] = (byte)(value >> 8);
                buffer[offset + 2] = (byte)(value >> 16);
                buffer[offset + 3] = (byte)(value >> 24);
                buffer[offset + 4] = (byte)(value >> 32);
                buffer[offset + 5] = (byte)(value >> 40);
                buffer[offset + 6] = (byte)(value >> 48);
                buffer[offset + 7] = (byte)(value >> 56);
            }

            offset += sizeof(ulong);
        }

        private static FlowEvent ReadEvent(BinaryReader reader, int version)
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

            var exceptionTypeId = reader.ReadInt64();
            var operationId = version >= 5 ? reader.ReadUInt64() : 0;
            return new FlowEvent(kind, timestamp, methodMetadataIndex, flowId, frameId, parentFrameId, depth, threadId, exceptionTypeId, operationId);
        }

        private static FlowOperationMetadata ReadOperationMetadata(BinaryReader reader, int version)
        {
            return new FlowOperationMetadata(
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

        private static FlowExceptionDetails ReadExceptionDetails(BinaryReader reader)
        {
            return new FlowExceptionDetails(
                reader.ReadUInt64(),
                reader.ReadUInt64(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32());
        }

        private static string ReadString(BinaryReader reader, int version)
        {
            if (version < 6)
            {
                return reader.ReadString();
            }

            var byteCount = reader.ReadInt32();
            if (byteCount < 0)
            {
                throw new InvalidDataException("The flow recorder string length is invalid.");
            }

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

        private static FlowCapturedValue ReadCapturedValue(BinaryReader reader)
        {
            return new FlowCapturedValue(
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
}
