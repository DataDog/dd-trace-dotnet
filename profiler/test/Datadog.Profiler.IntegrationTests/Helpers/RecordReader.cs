// <copyright file="RecordReader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Runtime.InteropServices;

#pragma warning disable SA1649 // File name should match first type name

namespace Datadog.Profiler.IntegrationTests
{
    // used for both register and unregister commands
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct RegistrationCommand
    {
        public IpcHeader Header;
        public UInt64 Pid;
    }

    // Common part of each record | size = 17 + 80 + 2 = 99
    // The size of the CLR event payload byte[] is given by EtwUserDataLength
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct AgentEventRecord
    {
        public IpcHeader Header;
        public EVENT_HEADER EventHeader;
        public UInt16 EtwUserDataLength;

        public AgentEventRecord()
        {
            EtwUserDataLength = 0;
        }
    }

    // TODO: check that it is possible to use .NET 8 InlineArray in integration tests
    [System.Runtime.CompilerServices.InlineArray(14)]
    public struct MagicVersion
    {
        public byte _element;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct IpcHeader
    {
        public static UInt16 HeaderSize = 17;
        public static string MagicValue = "DD_ETW_IPC_V1";

        public MagicVersion Magic;  // should be "DD_ETW_IPC_V1" in ASCII
        public UInt16 Size;
        public byte CommandIdOrResponseCode;
    }

    public struct ClrEventPayload
    {
        public UInt16 EtwUserDataLength;

        // the size of this payload is given by EtwUserDataLength
        public byte EtwPayload; // array of bytes
    }

    // from C:\Program Files (x86)\Windows Kits\10\Include\10.0.19041.0\um\evntcons.h
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct EVENT_RECORD
    {
        public EVENT_HEADER EventHeader;       // Event header
        public ETW_BUFFER_CONTEXT BufferContext;   // Buffer context
        public UInt16 ExtendedDataCount;           // Number of extended data items
        public UInt16 UserDataLength;              // User data length
        public UIntPtr ExtendedData;               // Pointer to an array of extended data items
        public UIntPtr UserData;                   // Pointer to user data
        public UIntPtr UserContext;                // Context from OpenTrace
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct EVENT_HEADER
    {
        public UInt16 Size;                            // Event Size
        public UInt16 HeaderType;                      // Header Type
        public UInt16 Flags;                           // Flags
        public UInt16 EventProperty;                   // User given event property
        public UInt32 ThreadId;                        // Thread Id
        public UInt32 ProcessId;                       // Process Id
        public UInt64 TimeStamp;                       // Event Timestamp
        public Guid ProviderId;                        // Provider Id
        public ETW_EVENT_DESCRIPTOR EventDescriptor;   // Event Descriptor
        public UInt32 KernelTime;                      // Kernel Mode CPU ticks
        public UInt32 UserTime;                        // User mode CPU ticks
        public Guid ActivityId;                        // Activity Id
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ETW_BUFFER_CONTEXT
    {
        public byte ProcessorNumber;
        public byte Alignment;
        public UInt16 LoggerId;
    }

    // from C:\Program Files (x86)\Windows Kits\10\Include\10.0.19041.0\shared\evntprov.h
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ETW_EVENT_DESCRIPTOR
    {
        public UInt16 Id;
        public byte Version;
        public byte Channel;
        public byte Level;
        public byte Opcode;
        public UInt16 Task;
        public UInt64 Keyword;
    }

#pragma warning disable SA1401 // Fields should be private
#pragma warning disable CA2211 // Non-constant fields should not be visible
#pragma warning disable SA1402 // File may only contain a single type
    public static class AgentCommands
    {
        public static byte Register = 1;
        public static byte Unregister = 2;
        public static byte ClrEvent = 16;
        public static byte KeepAlive = 17;
    }

    public static class ResponseCodes
    {
        public static byte Success = 0;
        public static byte Failure = 0xFF;
    }
#pragma warning restore SA1402 // File may only contain a single type
#pragma warning restore CA2211 // Non-constant fields should not be visible
#pragma warning restore SA1401 // Fields should be private

    public class RecordReader
    {
        // IpcHeader + EVENT_HEADER + ClrEventPayload.Size
        private const int CommonRecordSize = 17 + 80 + 2; // = 99
        private readonly BinaryReader _reader;
        private readonly IRecordDumper _recordDumper;
        private readonly IEventDumper _eventDumper;
        private byte[] _recordBuffer;

        public RecordReader(BinaryReader reader, IRecordDumper recordDumper, IEventDumper eventDumper)
        {
            _reader = reader;
            _recordDumper = recordDumper;
            _eventDumper = eventDumper;
            _recordBuffer = new byte[CommonRecordSize + 1024];  // + ClrEventPayload.EtwPayload max size
        }

        // use a large buffer to read the record part by part
        // but keep the record structure to send it as a whole to the dumper
        // i.e.:
        //   - up to the ClrEventPayload could be stored as a struct mapped to the start of the buffer
        //   - the ClrEventPayload.EtwPayload could be passed as a Span<byte> mapped to the rest of read bytes in the buffer
        public void ReadRecord()
        {
            // record =
            //   an IpcHeader
            //   a 80 bytes EVENT_HEADER (its Size field should be ignored)
            //   a ClrEventPayload (UInt16 EtwUserDataLength followed by byte[EtwUserDataLength value] EtwPayload)
            //
            // read the common part of the record
            var sizeRead = _reader.Read(_recordBuffer, 0, CommonRecordSize);
            var span = _recordBuffer.AsSpan();
            AgentEventRecord record = MemoryMarshal.Read<AgentEventRecord>(span);

            // read the rest of the record (i.e. CLR event payload)
            sizeRead = _reader.Read(_recordBuffer, CommonRecordSize, record.EtwUserDataLength);

            if (_recordDumper != null)
            {
                _recordDumper.DumpRecord(_recordBuffer, CommonRecordSize + record.EtwUserDataLength);
            }

            if (_eventDumper != null)
            {
                _eventDumper.DumpEvent(
                    record.EventHeader.TimeStamp,
                    record.EventHeader.ThreadId,
                    record.EventHeader.EventDescriptor.Version,
                    record.EventHeader.EventDescriptor.Keyword,
                    record.EventHeader.EventDescriptor.Level,
                    record.EventHeader.EventDescriptor.Id,
                    span.Slice(CommonRecordSize, record.EtwUserDataLength));
            }
        }

        public Guid ReadGuid(BinaryReader reader)
        {
            byte[] guidBytes = reader.ReadBytes(16);
            return new Guid(guidBytes);
        }
    }
}
#pragma warning restore SA1649 // File name should match first type name
