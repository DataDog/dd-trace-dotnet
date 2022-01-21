// <copyright file="TraceEventMessagePackFormatter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Ci.EventModel;
using Datadog.Trace.Vendors.MessagePack;

namespace Datadog.Trace.Ci.Agent.MessagePack
{
    internal class TraceEventMessagePackFormatter : EventMessagePackFormatter<TraceEvent>
    {
        private readonly byte[] _spansBytes = StringEncoding.UTF8.GetBytes("spans");

        public override int Serialize(ref byte[] bytes, int offset, TraceEvent value, IFormatterResolver formatterResolver)
        {
            if (value is null)
            {
                return 0;
            }

            var originalOffset = offset;

            offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, 3);

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, TypeBytes);
            offset += MessagePackBinary.WriteString(ref bytes, offset, value.Type);

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, VersionBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, Version100ValueBytes);

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, ContentBytes);
            offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, 1);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _spansBytes);
            offset += formatterResolver.GetFormatter<ArraySegment<Span>>().Serialize(ref bytes, offset, value.Content, formatterResolver);

            return offset - originalOffset;
        }
    }
}
