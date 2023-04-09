// <copyright file="CIFormatterResolver.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Ci.Agent.Payloads;
using Datadog.Trace.Ci.Coverage.Models.Tests;
using Datadog.Trace.Ci.EventModel;
using Datadog.Trace.Vendors.MessagePack;
using Datadog.Trace.Vendors.MessagePack.Formatters;
using Datadog.Trace.Vendors.MessagePack.Resolvers;

namespace Datadog.Trace.Ci.Agent.MessagePack;

internal class CIFormatterResolver : IFormatterResolver
{
    public static readonly CIFormatterResolver Instance = new();

    private readonly IMessagePackFormatter<Span> _spanFormatter;
    private readonly IMessagePackFormatter<CIVisibilityProtocolPayload> _eventsPayloadFormatter;
    private readonly IMessagePackFormatter<IEvent> _eventFormatter;
    private readonly IMessagePackFormatter<CIVisibilityEvent<Span>> _ciVisibilityEventFormatter;
    private readonly IMessagePackFormatter<CICodeCoveragePayload.CoveragePayload> _coveragePayloadFormatter;
    private readonly IMessagePackFormatter<TestCoverage> _testCoverageFormatter;

    private readonly Type _spanType = typeof(Span);
    private readonly Type _ciVisibilityProtocolPayloadType = typeof(CIVisibilityProtocolPayload);
    private readonly Type _ciVisibilityEventSpanType = typeof(CIVisibilityEvent<Span>);
    private readonly Type _spanEventType = typeof(SpanEvent);
    private readonly Type _testEventType = typeof(TestEvent);
    private readonly Type _testSuiteEventType = typeof(TestSuiteEvent);
    private readonly Type _testModuleEventType = typeof(TestModuleEvent);
    private readonly Type _testSessionEventType = typeof(TestSessionEvent);
    private readonly Type _coveragePayloadType = typeof(CICodeCoveragePayload.CoveragePayload);
    private readonly Type _testCoverageType = typeof(TestCoverage);
    private readonly Type _iEventType = typeof(IEvent);

    private CIFormatterResolver()
    {
        _spanFormatter = SpanMessagePackFormatter.Instance;
        _eventsPayloadFormatter = new CIEventMessagePackFormatter(CIVisibility.Settings.TracerSettings);
        _eventFormatter = new IEventMessagePackFormatter();
        _ciVisibilityEventFormatter = new CIVisibilityEventMessagePackFormatter<Span>();
        _coveragePayloadFormatter = new CoveragePayloadMessagePackFormatter();
        _testCoverageFormatter = new TestCoverageMessagePackFormatter();
    }

    public IMessagePackFormatter<T> GetFormatter<T>()
    {
        var typeOfT = typeof(T);

        if (typeOfT == _spanType)
        {
            return (IMessagePackFormatter<T>)_spanFormatter;
        }

        if (typeOfT == _ciVisibilityProtocolPayloadType)
        {
            return (IMessagePackFormatter<T>)_eventsPayloadFormatter;
        }

        if (typeOfT == _ciVisibilityEventSpanType ||
            typeOfT == _spanEventType ||
            typeOfT == _testEventType ||
            typeOfT == _testSuiteEventType ||
            typeOfT == _testModuleEventType ||
            typeOfT == _testSessionEventType)
        {
            return (IMessagePackFormatter<T>)_ciVisibilityEventFormatter;
        }

        if (typeOfT == _coveragePayloadType)
        {
            return (IMessagePackFormatter<T>)_coveragePayloadFormatter;
        }

        if (typeOfT == _testCoverageType)
        {
            return (IMessagePackFormatter<T>)_testCoverageFormatter;
        }

        if (typeOfT == _iEventType)
        {
            return (IMessagePackFormatter<T>)_eventFormatter;
        }

        return StandardResolver.Instance.GetFormatter<T>();
    }

    public IMessagePackFormatter<CIVisibilityEvent<Span>> GetCiVisibilityEventFormatter()
    {
        return _ciVisibilityEventFormatter;
    }

    public IMessagePackFormatter<TestCoverage> GetTestCoverageFormatter()
    {
        return _testCoverageFormatter;
    }
}
