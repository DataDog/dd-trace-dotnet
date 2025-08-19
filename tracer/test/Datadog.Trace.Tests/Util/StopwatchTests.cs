// <copyright file="StopwatchTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Diagnostics;
using System.Reflection;
using System.Threading;
using Datadog.Trace.Util;
using Xunit;

namespace Datadog.Trace.Tests.Util
{
    public class StopwatchTests
    {
        [Fact]
        public void ComputesCorrectTimespan()
        {
            var sw = Stopwatch.StartNew();

            Thread.Sleep(50);

            sw.Stop();

            var elapsed = StopwatchHelpers.GetElapsed(sw.ElapsedTicks);

            Assert.Equal(sw.Elapsed, elapsed);
        }
    }
}
