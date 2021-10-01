// <copyright file="LogRateLimiterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using NUnit.Framework;

namespace Datadog.Trace.Tests.Logging
{
    [NonParallelizable]
    public class LogRateLimiterTests : IDisposable
    {
        private const int SecondsBetweenLogs = 60;
        private LogRateLimiter _rateLimiter;
        private SimpleClock _clock;
        private IDisposable _clockDisposable;

        public static IEnumerable<object[]> GetSecondIntervals(int count, int increment)
            => Enumerable.Range(1, count).Select(x => new object[] { x * increment });

        [SetUp]
        public void Setup()
        {
            _rateLimiter = new LogRateLimiter(SecondsBetweenLogs);
            _clock = new SimpleClock();
            _clockDisposable = Clock.SetForCurrentThread(_clock);
        }

        public void Dispose() => _clockDisposable?.Dispose();

        [Test]
        public void IdenticalLogs_WhenFasterThanAllowedRate_DoesNotWriteSubsequentLogs()
        {
            const string filePath = @"C:\some\path";
            const int lineNo = 123;

            // first log is always true
            var shouldLogFirstMessage = _rateLimiter.ShouldLog(filePath, lineNo, out _);
            Assert.True(shouldLogFirstMessage);

            // All at same time instant
            for (int i = 0; i < 100; i++)
            {
                var shouldLog = _rateLimiter.ShouldLog(filePath, lineNo, out _);
                Assert.False(shouldLog);
            }
        }

        [Test]
        public void IdenticalLogs_WhenFasterThanAllowedRate_OnlyRecordsOneLogPerTimePeriod()
        {
            const string filePath = @"C:\some\path";
            const int lineNo = 123;

            var messagesLogged = 0;

            // first log is always true
            var shouldLogFirstMessage = _rateLimiter.ShouldLog(filePath, lineNo, out _);
            Assert.True(shouldLogFirstMessage);

            // only 1 of subsequent logs should be true
            // can't guarantee which one, as depends when time bucket rolls over
            for (int i = 0; i < SecondsBetweenLogs; i++)
            {
                _clock.UtcNow = _clock.UtcNow.AddSeconds(1);
                var shouldLog = _rateLimiter.ShouldLog(filePath, lineNo, out _);
                if (shouldLog)
                {
                    messagesLogged++;
                }
            }

            Assert.AreEqual(1, messagesLogged);
        }

        [TestCaseSource(nameof(GetSecondIntervals), new object[] { 10, SecondsBetweenLogs })]
        public void IdenticalLogs_WhenSlowerThanAllowedRate_AreNotFiltered(int secondsPassed)
        {
            const string filePath = @"C:\some\path";
            const int lineNo = 123;

            var shouldLogFirstMessage = _rateLimiter.ShouldLog(filePath, lineNo, out _);
            Assert.True(shouldLogFirstMessage);

            _clock.UtcNow = _clock.UtcNow.AddSeconds(secondsPassed);
            var shouldLogSecondMessage = _rateLimiter.ShouldLog(filePath, lineNo, out _);
            Assert.True(shouldLogSecondMessage);
        }

        [Test]
        public void IdenticalLogs_WhenUnfiltered_ReturnsNumberOfSkippedMessages()
        {
            const string filePath = @"C:\some\path";
            const int lineNo = 123;
            const uint expectedSkipCount = 10u;

            _rateLimiter.ShouldLog(filePath, lineNo, out var initialSkipCount);
            Assert.AreEqual(0u, initialSkipCount);

            for (var i = 0; i < expectedSkipCount; i++)
            {
                _rateLimiter.ShouldLog(filePath, lineNo, out _);
            }

            _clock.UtcNow = _clock.UtcNow.AddSeconds(SecondsBetweenLogs);

            _rateLimiter.ShouldLog(filePath, lineNo, out var actualSkipCount);
            Assert.AreEqual(expectedSkipCount, actualSkipCount);
        }

        [TestCase(@"C:\some\path")]
        [TestCase(@"C:\some\other_path")]
        [TestCase(@"C:\some\Path")]
        [TestCase(@"note%aR34LPath")] // not valid, but shouldn't throw
        public void LogsFromDifferentFiles_AreAlwaysLogged(string filePath)
        {
            const int lineNo = 123;

            var shouldLog = _rateLimiter.ShouldLog(filePath, lineNo, out _);
            Assert.True(shouldLog);
        }

        [Test]
        public void LogsFromDifferentFiles_WithinTimePeriod_AreAlwaysLogged()
        {
            const int lineNo = 123;
            var paths = new[]
            {
                @"C:\some\path",
                @"C:\some\other_path",
                @"C:\some\Path",
                @"note%aR34LPath"
            };

            foreach (var path in paths)
            {
                var shouldLog = _rateLimiter.ShouldLog(path, lineNo, out _);
                Assert.True(shouldLog);
            }
        }

        [TestCase(0)]
        [TestCase(10)]
        [TestCase(123)]
        [TestCase(-1)] // not valid, but shouldn't throw
        public void LogsFromDifferentLines_AreAlwaysLogged(int lineNo)
        {
            const string filePath = @"C:\some\path";

            var shouldLog = _rateLimiter.ShouldLog(filePath, lineNo, out _);
            Assert.True(shouldLog);
        }

        [Test]
        public void LogsFromDifferentLines_WithinTimePeriod_AreAlwaysLogged()
        {
            const string filePath = @"C:\some\path";
            var lineNos = new[] { 0, 10, 123, -1 };

            foreach (var lineNo in lineNos)
            {
                var shouldLog = _rateLimiter.ShouldLog(filePath, lineNo, out _);
                Assert.True(shouldLog);
            }
        }

        [Test]
        public void WhenCallerLineNumberAndCallerFilePathHaveDefaults_AlwaysLogs()
        {
            const int lineNo = 0;
            const string filePath = "";

            for (var i = 0; i < 10; i++)
            {
                var shouldLog = _rateLimiter.ShouldLog(filePath, lineNo, out _);
                Assert.True(shouldLog);
            }
        }
    }
}
