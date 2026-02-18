// <copyright file="EventMessagePackFormatter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
#pragma warning disable SA1402 // disable check to only have one class per file

using System;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.MessagePack;
using Datadog.Trace.Vendors.MessagePack.Formatters;

namespace Datadog.Trace.Ci.Agent.MessagePack;

internal abstract class EventMessagePackFormatter
{
    private readonly IDatadogLogger _log;

    protected EventMessagePackFormatter()
    {
        _log = DatadogLogging.GetLoggerFor(GetType());
    }

    // Use generated MessagePack constants
    protected static ReadOnlySpan<byte> TypeBytes => MessagePackConstants.TypeBytes;

    protected static ReadOnlySpan<byte> VersionBytes => MessagePackConstants.VersionBytes;

    protected static ReadOnlySpan<byte> ContentBytes => MessagePackConstants.ContentBytes;

    protected IDatadogLogger Log => _log;
}

internal abstract class EventMessagePackFormatter<T> : EventMessagePackFormatter, IMessagePackFormatter<T>
{
    public virtual T Deserialize(byte[] bytes, int offset, IFormatterResolver formatterResolver, out int readSize)
    {
        throw new NotImplementedException();
    }

    public abstract int Serialize(ref byte[] bytes, int offset, T value, IFormatterResolver formatterResolver);
}
