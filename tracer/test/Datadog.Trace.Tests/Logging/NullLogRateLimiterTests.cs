// <copyright file="NullLogRateLimiterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using NUnit.Framework;

namespace Datadog.Trace.Tests.Logging
{
    [NonParallelizable]
    public class NullLogRateLimiterTests : IDisposable
    {
        private readonly NullLogRateLimiter _rateLimiter;

        public NullLogRateLimiterTests()
        {
            _rateLimiter = new NullLogRateLimiter();
        }

        public void Dispose() => Clock.Reset();

        [Test]
        public void ShouldLog_AlwaysReturnsTrue()
        {
            const string filePath = @"C:\some\path";
            const int lineNo = 123;
            Clock.SetForCurrentThread(new ConstantClock());

            for (var i = 0; i < 10; i++)
            {
                var shouldLog = _rateLimiter.ShouldLog(filePath, lineNo, out var skipCount);

                Assert.True(shouldLog, $"{nameof(shouldLog)} was false on iteration {i}");
                Assert.AreEqual(0u, skipCount);
            }
        }

        private class ConstantClock : IClock
        {
            public DateTime UtcNow { get; } = DateTime.UtcNow;
        }
    }
}
