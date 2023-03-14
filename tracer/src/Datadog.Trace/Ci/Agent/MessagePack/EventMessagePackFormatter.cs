// <copyright file="EventMessagePackFormatter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.MessagePack;
using Datadog.Trace.Vendors.MessagePack.Formatters;

#pragma warning disable SA1402

namespace Datadog.Trace.Ci.Agent.MessagePack
{
    internal abstract class EventMessagePackFormatter
    {
#if NETCOREAPP
        protected static ReadOnlySpan<byte> TypeBytes => "type"u8;

        protected static ReadOnlySpan<byte> VersionBytes => "version"u8;

        protected static ReadOnlySpan<byte> ContentBytes => "content"u8;
#else
        protected static byte[] TypeBytes { get; } = StringEncoding.UTF8.GetBytes("type");

        protected static byte[] VersionBytes { get; } = StringEncoding.UTF8.GetBytes("version");

        protected static byte[] ContentBytes { get; } = StringEncoding.UTF8.GetBytes("content");
#endif
    }

    internal abstract class EventMessagePackFormatter<T> : EventMessagePackFormatter, IMessagePackFormatter<T>
    {
        private readonly IDatadogLogger _log;

        protected EventMessagePackFormatter()
        {
            _log = DatadogLogging.GetLoggerFor(GetType());
        }

        protected IDatadogLogger Log => _log;

        public virtual T Deserialize(byte[] bytes, int offset, IFormatterResolver formatterResolver, out int readSize)
        {
            throw new NotImplementedException();
        }

        public abstract int Serialize(ref byte[] bytes, int offset, T value, IFormatterResolver formatterResolver);
    }
}
