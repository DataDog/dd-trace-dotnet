// <copyright file="IastTelemetryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Iast;
using Datadog.Trace.Iast.Telemetry;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.TestHelpers.FluentAssertionsExtensions;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests.IAST
{
    public class IastTelemetryTests
    {
        [Fact]
        public void CheckVulnerabilityTypeAndIastInstrumentedSinksConsistency()
        {
            Enum.GetValues(typeof(MetricTags.IastInstrumentedSinks)).Length.Should().Be(Enum.GetValues(typeof(VulnerabilityType)).Length - 1);
            for (int i = 0; i < Enum.GetValues(typeof(VulnerabilityType)).Length; i++)
            {
                var vulnerabilityType = (VulnerabilityType)i;
                if (vulnerabilityType != VulnerabilityType.None)
                {
                    var tag = (MetricTags.IastInstrumentedSinks)i;
                    tag.ToString().Should().Be(vulnerabilityType.ToString());
                }
            }
        }

        [Fact]
        public void CheckSourceTypeAndIastInstrumentedSourcesConsistency()
        {
            Enum.GetValues(typeof(MetricTags.IastInstrumentedSources)).Length.Should().Be(Enum.GetValues(typeof(SourceTypeName)).Length);
            for (int i = 0; i < Enum.GetValues(typeof(VulnerabilityType)).Length; i++)
            {
                var tag = (MetricTags.IastInstrumentedSources)i;
                tag.ToString().Should().Be(((SourceTypeName)i).ToString());
            }
        }
    }
}
