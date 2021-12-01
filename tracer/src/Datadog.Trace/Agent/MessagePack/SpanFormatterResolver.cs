// <copyright file="SpanFormatterResolver.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Vendors.MessagePack;
using Datadog.Trace.Vendors.MessagePack.Formatters;

namespace Datadog.Trace.Agent.MessagePack
{
    internal class SpanFormatterResolver : IFormatterResolver
    {
        public static readonly IFormatterResolver Instance = new SpanFormatterResolver();

        private readonly ArraySegmentFormatter<Span> _arraySegmentFormatter = new();
        private readonly ArrayFormatter<Span> _arrayFormatter = new();

        private SpanFormatterResolver()
        {
        }

        public IMessagePackFormatter<T> GetFormatter<T>()
        {
            if (typeof(T) == typeof(Span))
            {
                return (IMessagePackFormatter<T>)SpanMessagePackFormatter.Instance;
            }

            if (typeof(T) == typeof(ArraySegment<Span>))
            {
                // double casting to make compiler happy
                return (IMessagePackFormatter<T>)(IMessagePackFormatter<ArraySegment<Span>>)_arraySegmentFormatter;
            }

            // used only in tests
            if (typeof(T) == typeof(Span[]))
            {
                // double casting to make compiler happy
                return (IMessagePackFormatter<T>)(IMessagePackFormatter<Span[]>)_arrayFormatter;
            }

            throw new InvalidOperationException($"Type not supported by {nameof(SpanFormatterResolver)}: {typeof(T).Name}");
        }
    }
}
