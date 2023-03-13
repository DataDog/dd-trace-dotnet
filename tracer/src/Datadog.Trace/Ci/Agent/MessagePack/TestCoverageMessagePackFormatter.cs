// <copyright file="TestCoverageMessagePackFormatter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Ci.Coverage.Models.Tests;
using Datadog.Trace.Vendors.MessagePack;

namespace Datadog.Trace.Ci.Agent.MessagePack;

internal class TestCoverageMessagePackFormatter : EventMessagePackFormatter<TestCoverage>
{
#if NETCOREAPP
    private ReadOnlySpan<byte> TestSessionIdBytes => "test_session_id"u8;

    private ReadOnlySpan<byte> TestSuiteIdBytes => "test_suite_id"u8;

    private ReadOnlySpan<byte> SpanIdBytes => "span_id"u8;

    private ReadOnlySpan<byte> FilesBytes => "files"u8;

    private ReadOnlySpan<byte> FilenameBytes => "filename"u8;

    private ReadOnlySpan<byte> SegmentsBytes => "segments"u8;
#else
    private readonly byte[] _testSessionIdBytes = StringEncoding.UTF8.GetBytes("test_session_id");
    private readonly byte[] _testSuiteIdBytes = StringEncoding.UTF8.GetBytes("test_suite_id");
    private readonly byte[] _spanIdBytes = StringEncoding.UTF8.GetBytes("span_id");
    private readonly byte[] _filesBytes = StringEncoding.UTF8.GetBytes("files");
    private readonly byte[] _filenameBytes = StringEncoding.UTF8.GetBytes("filename");
    private readonly byte[] _segmentsBytes = StringEncoding.UTF8.GetBytes("segments");
#endif

    public override int Serialize(ref byte[] bytes, int offset, TestCoverage value, IFormatterResolver formatterResolver)
    {
        if (value is null)
        {
            return 0;
        }

        var originalOffset = offset;

        var len = 1;
        if (value.SessionId != 0)
        {
            len++;
        }

        if (value.SuiteId != 0)
        {
            len++;
        }

        if (value.SpanId != 0)
        {
            len++;
        }

        offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, len);

        if (value.SessionId != 0)
        {
#if NETCOREAPP
            offset += MessagePackBinary.WriteStringReadOnlySpan(ref bytes, offset, TestSessionIdBytes);
#else
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _testSessionIdBytes);
#endif
            offset += MessagePackBinary.WriteUInt64(ref bytes, offset, value.SessionId);
        }

        if (value.SuiteId != 0)
        {
#if NETCOREAPP
            offset += MessagePackBinary.WriteStringReadOnlySpan(ref bytes, offset, TestSuiteIdBytes);
#else
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _testSuiteIdBytes);
#endif
            offset += MessagePackBinary.WriteUInt64(ref bytes, offset, value.SuiteId);
        }

        if (value.SpanId != 0)
        {
#if NETCOREAPP
            offset += MessagePackBinary.WriteStringReadOnlySpan(ref bytes, offset, SpanIdBytes);
#else
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _spanIdBytes);
#endif
            offset += MessagePackBinary.WriteUInt64(ref bytes, offset, value.SpanId);
        }

#if NETCOREAPP
        offset += MessagePackBinary.WriteStringReadOnlySpan(ref bytes, offset, FilesBytes);
#else
        offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _filesBytes);
#endif
        if (value.Files is { Count: > 0 } files)
        {
            offset += MessagePackBinary.WriteArrayHeader(ref bytes, offset, (uint)files.Count);
            foreach (var file in files)
            {
                offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, 2);

#if NETCOREAPP
                offset += MessagePackBinary.WriteStringReadOnlySpan(ref bytes, offset, FilenameBytes);
#else
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _filenameBytes);
#endif
                offset += MessagePackBinary.WriteString(ref bytes, offset, file.FileName);

#if NETCOREAPP
                offset += MessagePackBinary.WriteStringReadOnlySpan(ref bytes, offset, SegmentsBytes);
#else
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _segmentsBytes);
#endif
                if (file.Segments is { Count: > 0 } segments)
                {
                    offset += MessagePackBinary.WriteArrayHeader(ref bytes, offset, (uint)segments.Count);
                    foreach (var segment in segments)
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
                else
                {
                    offset += MessagePackBinary.WriteNil(ref bytes, offset);
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
