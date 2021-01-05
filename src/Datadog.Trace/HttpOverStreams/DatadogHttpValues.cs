using System;

namespace Datadog.Trace.HttpOverStreams
{
    internal static class DatadogHttpValues
    {
        /// <summary>
        /// Some sane limit for responses from the agent.
        /// Most responses will be a few hundred or less.
        /// Oversized to account for future expansion or errors.
        /// </summary>
        public const int MaximumResponseBufferSize = 10_240;

        /// <summary>
        /// Some limit for requests to the agent.
        /// Maximum throughput for one request is 50kb.
        /// This number is based on local testing.
        /// However, failing before the send means that behavior is changed and logs are different, so this is slightly oversized.
        /// This value will only be used if the Content-Length is not specified, which is never in the case of the current named pipes implementation.
        /// </summary>
        public const int DefaultMaximumRequestBufferSize = 52_431_485;

        public const char CarriageReturn = '\r';
        public static readonly string NewLine = Environment.NewLine;
        public static readonly int CrLfLength = NewLine.Length;
    }
}
