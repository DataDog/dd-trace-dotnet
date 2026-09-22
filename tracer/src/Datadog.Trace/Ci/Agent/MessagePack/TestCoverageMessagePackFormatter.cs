// <copyright file="TestCoverageMessagePackFormatter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using Datadog.Trace.Ci.Coverage.Models.Tests;
using Datadog.Trace.Vendors.MessagePack;

namespace Datadog.Trace.Ci.Agent.MessagePack;

internal sealed class TestCoverageMessagePackFormatter : EventMessagePackFormatter<TestCoverage>
{
#pragma warning disable SA1516 // Elements should be separated by blank line
    private static ReadOnlySpan<byte> TestSessionIdBytes => "test_session_id"u8;
    private static ReadOnlySpan<byte> TestSuiteIdBytes => "test_suite_id"u8;
    private static ReadOnlySpan<byte> SpanIdBytes => "span_id"u8;
    private static ReadOnlySpan<byte> FilesBytes => "files"u8;
    private static ReadOnlySpan<byte> FilenameBytes => "filename"u8;
    private static ReadOnlySpan<byte> BitmapBytes => "bitmap"u8;
#pragma warning restore SA1516

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
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, TestSessionIdBytes);
            offset += MessagePackBinary.WriteUInt64(ref bytes, offset, value.SessionId);
        }

        if (value.SuiteId != 0)
        {
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, TestSuiteIdBytes);
            offset += MessagePackBinary.WriteUInt64(ref bytes, offset, value.SuiteId);
        }

        if (value.SpanId != 0)
        {
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, SpanIdBytes);
            offset += MessagePackBinary.WriteUInt64(ref bytes, offset, value.SpanId);
        }

        offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, FilesBytes);
        if (value.Files is { Count: > 0 } files)
        {
            offset += MessagePackBinary.WriteArrayHeader(ref bytes, offset, (uint)files.Count);
            foreach (var file in files)
            {
                offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, 2);

                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, FilenameBytes);
                offset += MessagePackBinary.WriteString(ref bytes, offset, file.FileName);

                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, BitmapBytes);
                offset += MessagePackBinary.WriteBytes(ref bytes, offset, file.Bitmap);
            }
        }
        else
        {
            offset += MessagePackBinary.WriteNil(ref bytes, offset);
        }

        return offset - originalOffset;
    }
}
