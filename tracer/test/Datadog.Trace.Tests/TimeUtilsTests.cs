// <copyright file="TimeUtilsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NET45 && !NET451 && !NET452
using System;
using Datadog.Trace.ExtensionMethods;
using NUnit.Framework;

namespace Datadog.Trace.Tests
{
    public class TimeUtilsTests
    {
        [Test]
        public void ToUnixTimeNanoseconds_UnixEpoch_Zero()
        {
            var date = DateTimeOffset.FromUnixTimeMilliseconds(0);
            Assert.AreEqual(0, date.ToUnixTimeNanoseconds());
        }

        [Test]
        public void ToUnixTimeNanoseconds_Now_CorrectMillisecondRoundedValue()
        {
            var date = DateTimeOffset.UtcNow;
            Assert.AreEqual(date.ToUnixTimeMilliseconds(), date.ToUnixTimeNanoseconds() / 1000000);
        }
    }
}
#endif
