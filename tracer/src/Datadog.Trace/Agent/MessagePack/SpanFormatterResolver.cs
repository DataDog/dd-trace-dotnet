// <copyright file="SpanFormatterResolver.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Vendors.MessagePack;
using Datadog.Trace.Vendors.MessagePack.Formatters;
using Datadog.Trace.Vendors.MessagePack.Resolvers;

namespace Datadog.Trace.Agent.MessagePack
{
    internal sealed class SpanFormatterResolver : IFormatterResolver
    {
        public static readonly IFormatterResolver Instance = new SpanFormatterResolver();

        private readonly object _formatter;

        private SpanFormatterResolver()
        {
            _formatter = SpanMessagePackFormatter.Instance;
        }

        public IMessagePackFormatter<T> GetFormatter<T>()
        {
            if (typeof(T) == typeof(TraceChunkModel))
            {
                return (IMessagePackFormatter<T>)_formatter;
            }

            if (typeof(T) == typeof(Span) || typeof(T) == typeof(SpanModel))
            {
                // We block IMessagePackFormatter<Span> and IMessagePackFormatter<SpanModel> since it
                // can return wrong results and we want to catch any tests trying to use it.
                // Do not serialize individual spans. Only serialize entire trace chunks.
                // Note that this also covers collections, like Span[] and ArraySegment<Span>.
                throw new NotSupportedException("Serializing Span with IMessagePackFormatter<Span> is not supported. Use IMessagePackFormatter<SpanModel> instead.");
            }

            return StandardResolver.Instance.GetFormatter<T>();
        }
    }
}
