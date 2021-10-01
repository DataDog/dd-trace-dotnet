// <copyright file="StopwatchTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Diagnostics;
using System.Reflection;
using System.Threading;
using Datadog.Trace.Util;
using NUnit.Framework;

namespace Datadog.Trace.Tests.Util
{
    public class StopwatchTests
    {
        [Test]
        public void ComputesCorrectTimespan()
        {
            var sw = Stopwatch.StartNew();

            Thread.Sleep(50);

            sw.Stop();

            // Extract the internal ticks
            var stopwatchTicks = (long)typeof(Stopwatch)
                .GetMethod("GetRawElapsedTicks", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(sw, null);

            var elapsed = StopwatchHelpers.GetElapsed(stopwatchTicks);

            Assert.AreEqual(sw.Elapsed, elapsed);
        }
    }
}
