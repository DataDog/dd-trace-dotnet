// <copyright file="CIEventMessagePackFormatter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.Ci.EventModel;
using Datadog.Trace.Vendors.MessagePack;
using Datadog.Trace.Vendors.MessagePack.Formatters;
using Datadog.Trace.Vendors.MessagePack.Resolvers;

namespace Datadog.Trace.Ci.Agent.MessagePack
{
    internal class CIEventMessagePackFormatter : IMessagePackFormatter<IEvent>
    {
        public IEvent Deserialize(byte[] bytes, int offset, IFormatterResolver formatterResolver, out int readSize)
        {
            throw new NotImplementedException();
        }

        public int Serialize(ref byte[] bytes, int offset, IEvent value, IFormatterResolver formatterResolver)
        {
            if (value is TraceEvent traceEvent)
            {
                return formatterResolver.GetFormatter<TraceEvent>().Serialize(ref bytes, offset, traceEvent, formatterResolver);
            }
            else if (value is TestEvent testEvent)
            {
                return formatterResolver.GetFormatter<TestEvent>().Serialize(ref bytes, offset, testEvent, formatterResolver);
            }

            return 0;
        }
    }
}
