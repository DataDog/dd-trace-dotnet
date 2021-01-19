using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Xunit;

namespace Datadog.Trace.Tests.Logging
{
    [Collection(nameof(Datadog.Trace.Tests.Logging))]
    public class LogRateLimiterTests : IDisposable
    {
        private const int SecondsBetweenLogs = 60;
        private readonly LogRateLimiter _rateLimiter;

        public LogRateLimiterTests()
        {
            _rateLimiter = new LogRateLimiter(SecondsBetweenLogs);
        }

        public static IEnumerable<object[]> GetSecondIntervals(int count, int increment)
            => Enumerable.Range(1, count).Select(x => new object[] { x * increment });

        public void Dispose() => Clock.Reset();

        [Fact]
        public void IdenticalLogs_WhenFasterThanAllowedRate_DoesNotWriteSubsequentLogs()
        {
            const string filePath = @"C:\some\path";
            const int lineNo = 123;

            Clock.SetForCurrentThread(new SimpleClock());

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

        [Fact]
        public void IdenticalLogs_WhenFasterThanAllowedRate_OnlyRecordsOneLogPerTimePeriod()
        {
            const string filePath = @"C:\some\path";
            const int lineNo = 123;

            var clock = new SimpleClock();
            Clock.SetForCurrentThread(clock);
            var messagesLogged = 0;

            // first log is always true
            var shouldLogFirstMessage = _rateLimiter.ShouldLog(filePath, lineNo, out _);
            Assert.True(shouldLogFirstMessage);

            // only 1 of subsequent logs should be true
            // can't guarantee which one, as depends when time bucket rolls over
            for (int i = 0; i < SecondsBetweenLogs; i++)
            {
                clock.UtcNow = clock.UtcNow.AddSeconds(1);
                var shouldLog = _rateLimiter.ShouldLog(filePath, lineNo, out _);
                if (shouldLog)
                {
                    messagesLogged++;
                }
            }

            Assert.Equal(1, messagesLogged);
        }

        [Theory]
        [MemberData(nameof(GetSecondIntervals), 10, SecondsBetweenLogs)]
        public void IdenticalLogs_WhenSlowerThanAllowedRate_AreNotFiltered(int secondsPassed)
        {
            const string filePath = @"C:\some\path";
            const int lineNo = 123;

            var clock = new SimpleClock();
            Clock.SetForCurrentThread(clock);
            var shouldLogFirstMessage = _rateLimiter.ShouldLog(filePath, lineNo, out _);
            Assert.True(shouldLogFirstMessage);

            clock.UtcNow = clock.UtcNow.AddSeconds(secondsPassed);
            var shouldLogSecondMessage = _rateLimiter.ShouldLog(filePath, lineNo, out _);
            Assert.True(shouldLogSecondMessage);
        }

        [Fact]
        public void IdenticalLogs_WhenUnfiltered_ReturnsNumberOfSkippedMessages()
        {
            const string filePath = @"C:\some\path";
            const int lineNo = 123;
            const uint expectedSkipCount = 10u;

            var clock = new SimpleClock();
            Clock.SetForCurrentThread(clock);

            _rateLimiter.ShouldLog(filePath, lineNo, out var initialSkipCount);
            Assert.Equal(0u, initialSkipCount);

            for (var i = 0; i < expectedSkipCount; i++)
            {
                _rateLimiter.ShouldLog(filePath, lineNo, out _);
            }

            clock.UtcNow = clock.UtcNow.AddSeconds(SecondsBetweenLogs);

            _rateLimiter.ShouldLog(filePath, lineNo, out var actualSkipCount);
            Assert.Equal(expectedSkipCount, actualSkipCount);
        }

        [Theory]
        [InlineData(@"C:\some\path")]
        [InlineData(@"C:\some\other_path")]
        [InlineData(@"C:\some\Path")]
        [InlineData(@"note%aR34LPath")] // not valid, but shouldn't throw
        public void LogsFromDifferentFiles_AreAlwaysLogged(string filePath)
        {
            const int lineNo = 123;

            var clock = new SimpleClock();
            Clock.SetForCurrentThread(clock);
            var shouldLog = _rateLimiter.ShouldLog(filePath, lineNo, out _);
            Assert.True(shouldLog);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(10)]
        [InlineData(123)]
        [InlineData(-1)] // not valid, but shouldn't throw
        public void LogsFromDifferentLines_AreAlwaysLogged(int lineNo)
        {
            const string filePath = @"C:\some\path";

            var clock = new SimpleClock();
            Clock.SetForCurrentThread(clock);
            var shouldLog = _rateLimiter.ShouldLog(filePath, lineNo, out _);
            Assert.True(shouldLog);
        }
    }
}
