// <copyright file="RaspTelemetryHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.AppSec.Rasp;
using Datadog.Trace.Tagging;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests.RASP
{
    public class RaspTelemetryHelperTests
    {
        [Fact]
        public void GivenARaspTelemetryHelper_WhenGenerateRaspSpanMetricTagsIsCalledWithTimeout_ThenTagIsSet()
        {
            var raspTelemetryHelper = new RaspTelemetryHelper();
            var tags = new CommonTags();
            raspTelemetryHelper.AddRaspSpanMetrics(1000, 2000, true);
            raspTelemetryHelper.GenerateRaspSpanMetricTags(tags);
            tags.GetMetric(Metrics.RaspWafTimeout).Should().Be(1);
        }
    }
}
