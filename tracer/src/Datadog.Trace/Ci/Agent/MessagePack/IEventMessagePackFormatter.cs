// <copyright file="IEventMessagePackFormatter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Ci.Coverage.Models.Tests;
using Datadog.Trace.Ci.EventModel;
using Datadog.Trace.Vendors.MessagePack;
using Datadog.Trace.Vendors.MessagePack.Formatters;

namespace Datadog.Trace.Ci.Agent.MessagePack;

internal class IEventMessagePackFormatter : IMessagePackFormatter<IEvent>
{
    public IEvent Deserialize(byte[] bytes, int offset, IFormatterResolver formatterResolver, out int readSize)
    {
        throw new NotImplementedException();
    }

    public int Serialize(ref byte[] bytes, int offset, IEvent value, IFormatterResolver formatterResolver)
    {
        var formatter = CIFormatterResolver.Instance;

        if (value is CIVisibilityEvent<Span> ciVisibilityEvent)
        {
            return formatter == formatterResolver ?
                       formatter.GetCiVisibilityEventFormatter().Serialize(ref bytes, offset, ciVisibilityEvent, formatterResolver) :
                       formatterResolver.GetFormatter<CIVisibilityEvent<Span>>().Serialize(ref bytes, offset, ciVisibilityEvent, formatterResolver);
        }

        if (value is TestCoverage testCoverageEvent)
        {
            return formatter == formatterResolver ?
                       formatter.GetTestCoverageFormatter().Serialize(ref bytes, offset, testCoverageEvent, formatterResolver) :
                       formatterResolver.GetFormatter<TestCoverage>().Serialize(ref bytes, offset, testCoverageEvent, formatterResolver);
        }

        return 0;
    }
}
