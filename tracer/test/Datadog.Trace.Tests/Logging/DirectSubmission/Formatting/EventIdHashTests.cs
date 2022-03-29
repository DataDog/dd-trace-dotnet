// <copyright file="EventIdHashTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
// based on https://github.com/serilog/serilog-formatting-compact/blob/8393e0ab8c2bc746fc733a4f20731b9e1f20f811/test/Serilog.Formatting.Compact.Tests/EventIdHashTests.cs

using Datadog.Trace.Logging.DirectSubmission.Formatting;
using Xunit;

namespace Datadog.Trace.Tests.Logging.DirectSubmission.Formatting
{
    public class EventIdHashTests
    {
        [Fact]
        public void HashingIsConsistent()
        {
            var h1 = EventIdHash.Compute("Template 1");
            var h2 = EventIdHash.Compute("Template 1");
            Assert.Equal(h1, h2);
        }

        [Fact]
        public void DistinctHashesAreComputed()
        {
            var h1 = EventIdHash.Compute("Template 1");
            var h2 = EventIdHash.Compute("Template 2");
            Assert.NotEqual(h1, h2);
        }
    }
}
