// <copyright file="CIEventMessagePackFormatter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Ci.Agent.Payloads;
using Datadog.Trace.Configuration;
using Datadog.Trace.Vendors.MessagePack;

namespace Datadog.Trace.Ci.Agent.MessagePack;

internal sealed class CIEventMessagePackFormatter : EventMessagePackFormatter<CIVisibilityProtocolPayload>
{
    private readonly byte[] _runtimeIdValueBytes = StringEncoding.UTF8.GetBytes(Tracer.RuntimeId);
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

#pragma warning disable SA1516 // Elements should be separated by blank line
    private static ReadOnlySpan<byte> MetadataBytes => "metadata"u8;
    private static ReadOnlySpan<byte> AsteriskBytes => "*"u8;
    private static ReadOnlySpan<byte> RuntimeIdBytes => "runtime-id"u8;
    private static ReadOnlySpan<byte> LanguageNameBytes => "language"u8;
    private static ReadOnlySpan<byte> LanguageNameValueBytes => "dotnet"u8;
    private static ReadOnlySpan<byte> LibraryVersionBytes => "library_version"u8;
    private static ReadOnlySpan<byte> LibraryVersionValueBytes => TracerConstants.AssemblyVersionBytes;
    private static ReadOnlySpan<byte> EnvironmentBytes => "env"u8;
    private static ReadOnlySpan<byte> TestBytes => "test"u8;
    private static ReadOnlySpan<byte> TestSuiteEndBytes => "test_suite_end"u8;
    private static ReadOnlySpan<byte> TestModuleEndBytes => "test_module_end"u8;
    private static ReadOnlySpan<byte> TestSessionEndBytes => "test_session_end"u8;
    private static ReadOnlySpan<byte> TestSessionNameBytes => "test_session.name"u8;
    private static ReadOnlySpan<byte> EventsBytes => "events"u8;
#pragma warning restore SA1516

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
        offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, MetadataBytes);

        // Value
        var metadataValuesCount = _testSessionNameValueBytes is not null ? 5 : 1;
        offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, metadataValuesCount);

        // ->  * : {}

        // Key
        offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, AsteriskBytes);

        // Value (RuntimeId, Language, library_version, Env?)
        int valuesCount = 3;
        if (_environmentValueBytes is not null)
        {
            valuesCount++;
        }

        offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, valuesCount);

        offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, RuntimeIdBytes);
        offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _runtimeIdValueBytes);

        offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, LanguageNameBytes);
        offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, LanguageNameValueBytes);

        offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, LibraryVersionBytes);
        offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, LibraryVersionValueBytes);

        if (_environmentValueBytes is not null)
        {
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, EnvironmentBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _environmentValueBytes);
        }

        if (_testSessionNameValueBytes is not null)
        {
            // -> test : {}
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, TestBytes);
            offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, 1);
            // -> test_session.name : "value"
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, TestSessionNameBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _testSessionNameValueBytes);

            // -> test_suite_end : {}
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, TestSuiteEndBytes);
            offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, 1);
            // -> test_session.name : "value"
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, TestSessionNameBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _testSessionNameValueBytes);

            // -> test_module_end : {}
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, TestModuleEndBytes);
            offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, 1);
            // -> test_session.name : "value"
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, TestSessionNameBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _testSessionNameValueBytes);

            // -> test_session_end : {}
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, TestSessionEndBytes);
            offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, 1);
            // -> test_session.name : "value"
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, TestSessionNameBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _testSessionNameValueBytes);
        }

        // # Events

        // Key
        offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, EventsBytes);

        return new ArraySegment<byte>(bytes, 0, offset);
    }
}
