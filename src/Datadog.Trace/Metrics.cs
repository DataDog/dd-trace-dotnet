namespace Datadog.Trace
{
    internal static class Metrics
    {
        public const string SamplingPriority = "_sampling_priority_v1";

        public const string SamplingAgentDecision = "_dd.agent_psr";

        public const string SamplingRuleDecision = "_dd.rule_psr";

        public const string SamplingLimitDecision = "_dd.limit_psr";

        public const string OriginKey = "_dd.origin";
    }
}
