// <copyright file="ExecutedTelemetryHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Iast.Telemetry;
using Datadog.Trace.Tagging;
using FluentAssertions;
using Xunit;
using static Datadog.Trace.Telemetry.Metrics.MetricTags;

namespace Datadog.Trace.Security.Unit.Tests.IAST;

public class ExecutedTelemetryHelperTests
{
    [Fact]
    public void GivenAnIastExecutedTelemetryHelper_WhenGetTags_ResultIsOk()
    {
        var helper = new ExecutedTelemetryHelper();

        helper.AddExecutedPropagation();

        var sources = Enum.GetValues(typeof(IastInstrumentedSources)).Length;

        for (int i = 1; i < sources; i++)
        {
            helper.AddExecutedSource((IastInstrumentedSources)i);
        }

        var sinks = Enum.GetValues(typeof(IastInstrumentedSinks)).Length;

        for (int i = 1; i < sinks; i++)
        {
            helper.AddExecutedSink((IastInstrumentedSinks)i);
        }

        var tags = new TestTags();
        helper.GenerateMetricTags(tags, 33);
        tags.Metrics.Count.Should().Be(sources + sinks - 1);
    }

    public class TestTags : ITags
    {
        public List<string> Metrics { get; set; } = new();

        public double? GetMetric(string key)
        {
            throw new NotImplementedException();
        }

        public string GetTag(string key)
        {
            throw new NotImplementedException();
        }

        public void SetMetric(string key, double? value)
        {
            Metrics.Add(key);
        }

        public void SetTag(string key, string value)
        {
            throw new NotImplementedException();
        }

        void ITags.EnumerateMetrics<TProcessor>(ref TProcessor processor)
        {
            throw new NotImplementedException();
        }

        void ITags.EnumerateTags<TProcessor>(ref TProcessor processor)
        {
            throw new NotImplementedException();
        }

        bool ITags.HasMetaStruct()
        {
            throw new NotImplementedException();
        }

        void ITags.SetMetaStruct(string key, byte[] value)
        {
            throw new NotImplementedException();
        }

        void ITags.EnumerateMetaStruct<TProcessor>(ref TProcessor processor)
        {
            throw new NotImplementedException();
        }
    }
}
