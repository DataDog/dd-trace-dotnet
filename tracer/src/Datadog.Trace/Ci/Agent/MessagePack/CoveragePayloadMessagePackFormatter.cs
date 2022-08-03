// <copyright file="CoveragePayloadMessagePackFormatter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Ci.Coverage.Models;
using Datadog.Trace.Vendors.MessagePack;

namespace Datadog.Trace.Ci.Agent.MessagePack
{
    internal class CoveragePayloadMessagePackFormatter : EventMessagePackFormatter<CoveragePayload>
    {
        private readonly byte[] _traceIdBytes = StringEncoding.UTF8.GetBytes("trace_id");
        private readonly byte[] _spanIdBytes = StringEncoding.UTF8.GetBytes("span_id");
        private readonly byte[] _versionBytes = StringEncoding.UTF8.GetBytes("version");
        private readonly byte[] _filesBytes = StringEncoding.UTF8.GetBytes("files");
        private readonly byte[] _filenameBytes = StringEncoding.UTF8.GetBytes("filename");
        private readonly byte[] _segmentsBytes = StringEncoding.UTF8.GetBytes("segments");

        public override int Serialize(ref byte[] bytes, int offset, CoveragePayload value, IFormatterResolver formatterResolver)
        {
            if (value is null)
            {
                return 0;
            }

            var originalOffset = offset;

            offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, 4);

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _traceIdBytes);
            offset += MessagePackBinary.WriteUInt64(ref bytes, offset, value.TraceId);

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _spanIdBytes);
            offset += MessagePackBinary.WriteUInt64(ref bytes, offset, value.SpanId);

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _versionBytes);
            offset += MessagePackBinary.WriteInt32(ref bytes, offset, value.Version);

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _filesBytes);

            if (value.Files is not null)
            {
                offset += MessagePackBinary.WriteArrayHeader(ref bytes, offset, (uint)value.Files.Count);

                foreach (var file in value.Files)
                {
                    offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, 2);

                    offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _filenameBytes);
                    offset += MessagePackBinary.WriteString(ref bytes, offset, file.FileName);

                    offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _segmentsBytes);
                    if (file.Segments is null)
                    {
                        offset += MessagePackBinary.WriteNil(ref bytes, offset);
                    }
                    else
                    {
                        offset += MessagePackBinary.WriteArrayHeader(ref bytes, offset, (uint)file.Segments.Count);
                        foreach (var segment in file.Segments)
                        {
                            if (segment is null)
                            {
                                offset += MessagePackBinary.WriteNil(ref bytes, offset);
                            }
                            else
                            {
                                offset += MessagePackBinary.WriteArrayHeader(ref bytes, offset, (uint)segment.Length);
                                foreach (var i in segment)
                                {
                                    offset += MessagePackBinary.WriteUInt32(ref bytes, offset, i);
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                offset += MessagePackBinary.WriteNil(ref bytes, offset);
            }

            return offset - originalOffset;
        }
    }
}
