// <copyright file="ClockTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading;
using Datadog.Trace.Util;
using NUnit.Framework;

namespace Datadog.Trace.Tests.Util
{
    [NonParallelizable]
    public class ClockTests
    {
        [Test]
        public void Should_use_real_clock_if_not_overriden()
        {
            // If everything works, the fastpath should be used and this clock ignored
            using var lease = Clock.SetForCurrentThread(new SimpleClock());

            // Reset the override flag
            Clock.Reset();

            var now = Clock.UtcNow;

            Thread.Sleep(100);

            var then = Clock.UtcNow;

            Assert.AreNotEqual(now, then);
        }
    }
}
