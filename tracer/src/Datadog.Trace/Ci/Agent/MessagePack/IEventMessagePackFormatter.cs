// <copyright file="IEventMessagePackFormatter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Ci.EventModel;
using Datadog.Trace.Vendors.MessagePack;
using Datadog.Trace.Vendors.MessagePack.Formatters;

namespace Datadog.Trace.Ci.Agent.MessagePack
{
    internal class IEventMessagePackFormatter : IMessagePackFormatter<IEvent>
    {
        public IEvent Deserialize(byte[] bytes, int offset, IFormatterResolver formatterResolver, out int readSize)
        {
            throw new NotImplementedException();
        }

        public int Serialize(ref byte[] bytes, int offset, IEvent value, IFormatterResolver formatterResolver)
        {
            if (value is SpanEvent spanEvent)
            {
                return formatterResolver.GetFormatter<SpanEvent>().Serialize(ref bytes, offset, spanEvent, formatterResolver);
            }

            if (value is TestEvent testEvent)
            {
                return formatterResolver.GetFormatter<TestEvent>().Serialize(ref bytes, offset, testEvent, formatterResolver);
            }

            return 0;
        }
    }
}
