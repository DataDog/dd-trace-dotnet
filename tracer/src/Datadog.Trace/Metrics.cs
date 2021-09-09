// <copyright file="Metrics.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace
{
    internal static class Metrics
    {
        public const string SamplingPriority = "_sampling_priority_v1";

        /// <summary>
        /// To be set when the agent determines the sampling rate for a trace
        /// Read: Agent Priority Sample Rate
        /// </summary>
        public const string SamplingAgentDecision = "_dd.agent_psr";

        /// <summary>
        /// To be set when a sampling rule is applied to a trace
        /// Read: Sampling Rule Priority Sample Rate
        /// </summary>
        public const string SamplingRuleDecision = "_dd.rule_psr";

        /// <summary>
        /// To be set when a rate limiter is applied to a trace.
        /// Read: Rate Limiter Priority Sample Rate
        /// </summary>
        public const string SamplingLimitDecision = "_dd.limit_psr";

        /// <summary>
        /// The length of time a record has been on the queue
        /// </summary>
        public const string MessageQueueTimeMs = "message.queue_time_ms";

        /// <summary>
        /// Identifies top-level spans.
        /// Top-level spans have a different service name from their immediate parent or have no parent.
        /// </summary>
        internal const string TopLevelSpan = "_dd.top_level";

        /// <summary>
        /// Records the keep rate of spans in the tracer, independent of sampling rate
        /// </summary>
        internal const string TracesKeepRate = "_dd.tracer_kr";
    }
}
