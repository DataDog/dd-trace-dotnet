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
    }
}
