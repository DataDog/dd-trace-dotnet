using System;
using System.Collections.Concurrent;

using Datadog.Trace.Util;

namespace Datadog.Trace.Logging
{
    internal class LogRateLimiter : ILogRateLimiter
    {
#if NET45
        private static readonly DateTime _unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
#endif

        private readonly int _secondsBetweenLogs;
        private readonly ConcurrentDictionary<LogRateBucketKey, LogRateBucketInfo> _buckets
            = new ConcurrentDictionary<LogRateBucketKey, LogRateBucketInfo>();

        public LogRateLimiter(int secondsBetweenLogs)
        {
            if (secondsBetweenLogs < 1)
            {
                throw new ArgumentException("Must have positive number of seconds between logs", nameof(secondsBetweenLogs));
            }

            _secondsBetweenLogs = secondsBetweenLogs;
        }

        /// <inheritdoc/>
        public bool ShouldLog(string filePath, int lineNumber, out uint skipCount)
        {
            if (filePath == string.Empty || lineNumber == 0)
            {
                // Shouldn't happen, but playing it safe incase there's a problem with the attributes
                skipCount = 0;
                return true;
            }

#if NET45
            TimeSpan diff = Clock.UtcNow - _unixEpoch;
            var timestamp = diff.TotalSeconds;
#else
            var timestamp = ((DateTimeOffset)Clock.UtcNow).ToUnixTimeSeconds();
#endif

            var currentTimeBucket = (int)(timestamp / _secondsBetweenLogs);
            System.Diagnostics.Debug.Assert(currentTimeBucket > 0, $"Time bucket should be greater than 0");

            // RFC says we should take log context, level, filename and lineNumber into account
            // but we don't currently set the log context name in IDatadogLogger. FilePath and
            // lineNumber should generally sufficient to uniquely identify the log given our API anyway

            // We include the currentTimeBucket as a state inside the key to remove the closure in a possible hotpath.
            // the value is not used neither for GetHashCode or Equality comparison, is just a state used in the updateValueFactory
            // The CLR will use and pass the current calculated key on both factories
            // https://source.dot.net/#System.Collections.Concurrent/System/Collections/Concurrent/ConcurrentDictionary.cs,1342
            // https://referencesource.microsoft.com/#mscorlib/system/Collections/Concurrent/ConcurrentDictionary.cs,1206
            var key = new LogRateBucketKey(filePath, lineNumber, currentTimeBucket);

            var newLogInfo = _buckets.AddOrUpdate(
                key,
                new LogRateBucketInfo(currentTimeBucket, skipCount: 0, 0),
                (key, prev) => GetUpdatedLimitInfo(prev, key.State));

            skipCount = newLogInfo.PreviousSkipCount;
            return newLogInfo.SkipCount == 0;
        }

        private static LogRateBucketInfo GetUpdatedLimitInfo(LogRateBucketInfo previous, int currentTimeBucket)
        {
            if (previous.TimeBucket == currentTimeBucket)
            {
                return new LogRateBucketInfo(currentTimeBucket, previous.SkipCount + 1, previous.PreviousSkipCount);
            }

            return new LogRateBucketInfo(currentTimeBucket, skipCount: 0, previous.SkipCount);
        }

        public readonly struct LogRateBucketInfo
        {
            public readonly int TimeBucket;
            public readonly uint SkipCount;
            public readonly uint PreviousSkipCount;

            public LogRateBucketInfo(int timeBucket, uint skipCount, uint previousSkipCount)
            {
                TimeBucket = timeBucket;
                SkipCount = skipCount;
                PreviousSkipCount = previousSkipCount;
            }
        }

        public readonly struct LogRateBucketKey
        {
            public readonly string FilePath;
            public readonly int LineNo;
            public readonly int State;

            public LogRateBucketKey(string filePath, int lineNo, int state)
            {
                FilePath = filePath;
                LineNo = lineNo;
                State = state;
            }

            public override bool Equals(object obj)
            {
                // We ignore the state explicitly
                return obj is LogRateBucketKey key &&
                       FilePath == key.FilePath &&
                       LineNo == key.LineNo;
            }

            public override int GetHashCode()
            {
                // We ignore the state explicitly
                return HashCode.Combine(FilePath, LineNo);
            }
        }
    }
}
