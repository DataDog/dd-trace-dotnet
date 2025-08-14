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

#if NET6_0_OR_GREATER
            // Since at least .NET 6, Stopwatch.GetRawElapsedTicks is directly exposed as ElapsedTicks
            // and in .NET 10+ the GetRawElapsedTicks method doesn't exist
            var stopwatchTicks = sw.ElapsedTicks;
#else
            // Extract the internal ticks
            var stopwatchTicks = (long)typeof(Stopwatch)
                .GetMethod("GetRawElapsedTicks", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(sw, null);
#endif
            var elapsed = StopwatchHelpers.GetElapsed(stopwatchTicks);

            Assert.Equal(sw.Elapsed, elapsed);
        }
    }
}
