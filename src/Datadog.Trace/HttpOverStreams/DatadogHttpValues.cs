using System;

namespace Datadog.Trace.HttpOverStreams
{
    internal static class DatadogHttpValues
    {
        /// <summary>
        /// Some sane limit for responses from the agent.
        /// Most responses will be a few hundred or less.
        /// </summary>
        public const int MaximumResponseBufferSize = 5120;

        /// <summary>
        /// Some sane limit for requests to the agent.
        /// Maximum throughput for one request is 50kb.
        /// This number is based on local testing.
        /// </summary>
        public const int MaximumRequestBufferSize = 51_200;

        public const char CarriageReturn = '\r';
        public static readonly string NewLine = Environment.NewLine;
        public static readonly int CrLfLength = NewLine.Length;
    }
}
