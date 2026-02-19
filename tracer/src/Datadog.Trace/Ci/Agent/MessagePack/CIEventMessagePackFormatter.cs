// <copyright file="CIEventMessagePackFormatter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.Ci.Agent.Payloads;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;
using Datadog.Trace.Vendors.MessagePack;

namespace Datadog.Trace.Ci.Agent.MessagePack;

internal sealed class CIEventMessagePackFormatter : EventMessagePackFormatter<CIVisibilityProtocolPayload>
{
    // Runtime values
    private readonly byte[] _runtimeIdValueBytes = StringEncoding.UTF8.GetBytes(Tracer.RuntimeId);
    private readonly byte[] _libraryVersionValueBytes = StringEncoding.UTF8.GetBytes(TracerConstants.AssemblyVersion);
    private readonly byte[]? _environmentValueBytes;
    private readonly byte[]? _testSessionNameValueBytes;

    private readonly ArraySegment<byte> _envelopBytes;

    public CIEventMessagePackFormatter(TracerSettings tracerSettings)
    {
        // we don't subscribe, because we assume this _can't_ change in CI Visibility
        if (!string.IsNullOrEmpty(tracerSettings.Manager.InitialMutableSettings.Environment))
        {
            _environmentValueBytes = StringEncoding.UTF8.GetBytes(tracerSettings.Manager.InitialMutableSettings.Environment);
        }

        var testOptimization = TestOptimization.Instance;
        if (!string.IsNullOrWhiteSpace(testOptimization.Settings.TestSessionName))
        {
            _testSessionNameValueBytes = StringEncoding.UTF8.GetBytes(testOptimization.Settings.TestSessionName);
        }

        _envelopBytes = GetEnvelopeArraySegment();
    }

    public override int Serialize(ref byte[] bytes, int offset, CIVisibilityProtocolPayload? value, IFormatterResolver formatterResolver)
    {
        if (value is null)
        {
            return 0;
        }

        var originalOffset = offset;

        // Write envelope
        MessagePackBinary.EnsureCapacity(ref bytes, offset, _envelopBytes.Count);
        Buffer.BlockCopy(_envelopBytes.Array!, _envelopBytes.Offset, bytes, offset, _envelopBytes.Count);
        offset += _envelopBytes.Count;

        // Write events
        if (value.Events.Lock())
        {
            var data = value.Events.Data;
            MessagePackBinary.EnsureCapacity(ref bytes, offset, data.Count);
            Buffer.BlockCopy(data.Array!, data.Offset, bytes, offset, data.Count);
            offset += data.Count;
        }
        else
        {
            Log.Error<int>("Error while locking the events buffer with {Count} events.", value.Events.Count);
            offset += MessagePackBinary.WriteNil(ref bytes, offset);
        }

        return offset - originalOffset;
    }

    private ArraySegment<byte> GetEnvelopeArraySegment()
    {
        var offset = 0;
        // Default size o the array, in case we don't have enough space MessagePackBinary will resize it
        var bytes = new byte[2048];

        offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, 3);

        // # Version

        // Key
        offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, VersionBytes);
        // Value
        offset += MessagePackBinary.WriteInt32(ref bytes, offset, 1);

        // # Metadata

        // Key
        offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.MetadataBytes);

        // Value
        var metadataValuesCount = _testSessionNameValueBytes is not null ? 5 : 1;
        offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, metadataValuesCount);

        // ->  * : {}

        // Key
        offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.AsteriskBytes);

        // Value (RuntimeId, Language, library_version, Env?)
        int valuesCount = 3;
        if (_environmentValueBytes is not null)
        {
            valuesCount++;
        }

        offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, valuesCount);

        offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.RuntimeIdBytes);
        offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _runtimeIdValueBytes);

        offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.LanguageBytes);
        offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.DotnetLanguageValueBytes);

        offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.LibraryVersionBytes);
        offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _libraryVersionValueBytes);

        if (_environmentValueBytes is not null)
        {
            offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.EnvBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _environmentValueBytes);
        }

        if (_testSessionNameValueBytes is not null)
        {
            // -> test : {}
            offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.TestBytes);
            offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, 1);
            // -> test_session.name : "value"
            offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.TestSessionNameBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _testSessionNameValueBytes);

            // -> test_suite_end : {}
            offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.TestSuiteBytes);
            offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, 1);
            // -> test_session.name : "value"
            offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.TestSessionNameBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _testSessionNameValueBytes);

            // -> test_module_end : {}
            offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.TestModuleBytes);
            offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, 1);
            // -> test_session.name : "value"
            offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.TestSessionNameBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _testSessionNameValueBytes);

            // -> test_session_end : {}
            offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.TestSessionBytes);
            offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, 1);
            // -> test_session.name : "value"
            offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.TestSessionNameBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _testSessionNameValueBytes);
        }

        // # Events

        // Key
        offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.EventsBytes);

        return new ArraySegment<byte>(bytes, 0, offset);
    }
}
