// <copyright file="NativeTelemetryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using FluentAssertions;
using Samples.InstrumentedTests.Iast.Vulnerabilities;
using Xunit;

namespace Samples.InstrumentedTests.Iast
{
    public class NativeTelemetryTests : InstrumentationTestsBase
    {
        private string tainted = "tainted";
        public NativeTelemetryTests() 
        {
            AddTainted(tainted);
        }

        [Fact]
        public void GivenAMethod_WhenInstrumented_InstrumentationPointsAreDetected()
        {
            var sinks = new int[100];
            ReadNativeTelemetry(out var sources, out var propagations, sinks);
            Test();
            ReadNativeTelemetry(out sources, out propagations, sinks);
            int sinksTotal = 0;
            Array.ForEach(sinks, sink => sinksTotal += sink);
            propagations.Should().BeGreaterThanOrEqualTo(2);
            sinksTotal.Should().BeGreaterThanOrEqualTo(1);
        }

        private void Test()
        {
            var tainted2 = (tainted + "added").Trim();
            try
            {
                System.IO.File.ReadAllText(tainted2);
            }
            catch { }
        }
    }
}
