// <copyright file="CoveragePayloadMessagePackFormatter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Ci.Agent.Payloads;
using Datadog.Trace.Vendors.MessagePack;

namespace Datadog.Trace.Ci.Agent.MessagePack;

internal class CoveragePayloadMessagePackFormatter : EventMessagePackFormatter<CICodeCoveragePayload.CoveragePayload>
{
#if NETCOREAPP
    private ReadOnlySpan<byte> CoveragesBytes => "coverages"u8;
#else
    private byte[] CoveragesBytes { get; } = StringEncoding.UTF8.GetBytes("coverages");
#endif

    public override int Serialize(ref byte[] bytes, int offset, CICodeCoveragePayload.CoveragePayload value, IFormatterResolver formatterResolver)
    {
        var originalOffset = offset;

        offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, 2);

        offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, VersionBytes);
        offset += MessagePackBinary.WriteInt32(ref bytes, offset, 2);

        offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, CoveragesBytes);

        // Write events
        if (value.TestCoverageData.Lock())
        {
            var data = value.TestCoverageData.Data;
            MessagePackBinary.EnsureCapacity(ref bytes, offset, data.Count);
            Buffer.BlockCopy(data.Array!, data.Offset, bytes, offset, data.Count);
            offset += data.Count;
        }
        else
        {
            Log.Error<int>("Error while locking the events buffer with {Count} events.", value.TestCoverageData.Count);
            offset += MessagePackBinary.WriteNil(ref bytes, offset);
        }

        return offset - originalOffset;
    }
}
