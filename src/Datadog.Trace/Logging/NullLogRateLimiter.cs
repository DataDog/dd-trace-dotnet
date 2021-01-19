namespace Datadog.Trace.Logging
{
    internal class NullLogRateLimiter : ILogRateLimiter
    {
        /// <inheritdoc/>
        public bool ShouldLog(string filePath, int lineNumber, out uint skipCount)
        {
            skipCount = 0;
            return true;
        }
    }
}
