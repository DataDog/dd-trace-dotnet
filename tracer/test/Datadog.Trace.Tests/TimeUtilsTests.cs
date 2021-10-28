// <copyright file="TimeUtilsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.ExtensionMethods;
using Xunit;

namespace Datadog.Trace.Tests
{
    public class TimeUtilsTests
    {
        [Fact]
        public void ToUnixTimeNanoseconds_UnixEpoch_Zero()
        {
            var date = DateTimeOffset.FromUnixTimeMilliseconds(0);
            Assert.Equal(0, date.ToUnixTimeNanoseconds());
        }

        [Fact]
        public void ToUnixTimeNanoseconds_Now_CorrectMillisecondRoundedValue()
        {
            var date = DateTimeOffset.UtcNow;
            Assert.Equal(date.ToUnixTimeMilliseconds(), date.ToUnixTimeNanoseconds() / 1000000);
        }
    }
}
