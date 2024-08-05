// <copyright file="Metrics.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace
{
    internal static class Metrics
    {
        /// <summary>
        /// Tag set to specify the sampling decision that was taken
        /// <seealso cref="SamplingPriorityValues"/>
        /// </summary>
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

        /// <summary>
        /// Span classified by the RareSampler
        /// </summary>
        internal const string RareSpan = "_dd.rare";

        /// <summary>
        /// The process id.
        /// </summary>
        internal const string ProcessId = "process_id";

        /// <summary>
        /// The database index redis is connected to
        /// </summary>
        internal const string RedisDatabaseIndex = "db.redis.database_index";

        /// <summary>
        /// A boolean indicating whether APM tracing is disabled. When APM is disabled, this metric is set to 0.
        /// </summary>
        internal const string ApmEnabled = "_dd.apm.enabled";

        /// <summary>
        /// Whether the libraries application security features are enabled.
        /// </summary>
        public const string AppSecEnabled = "_dd.appsec.enabled";

        /// <summary>
        /// The number of AppSec traces ignored by the AppSec rate limiter
        /// </summary>
        public const string AppSecRateLimitDroppedTraces = "_dd.appsec.rate_limit.dropped_traces";

        /// <summary>
        /// Total cumulative waf duration across spans for one request
        /// </summary>
        public const string AppSecWafDuration = "_dd.appsec.waf.duration";

        /// <summary>
        /// Total cumulative waf duration across spans for one request, including parameters encoding, bindings, for non managed waf
        /// </summary>
        public const string AppSecWafAndBindingsDuration = "_dd.appsec.waf.duration_ext";

        /// <summary>
        /// Total cumulative waf duration for RASP calls across spans for one request
        /// </summary>
        public const string RaspWafDuration = "_dd.appsec.rasp.duration";

        /// <summary>
        /// Total cumulative waf duration for RASP calls across spans for one request, including parameters encoding, bindings, for non managed waf
        /// </summary>
        public const string RaspWafAndBindingsDuration = "_dd.appsec.rasp.duration_ext";

        /// <summary>
        /// Is set to 1 if there was a RASP timeout.
        /// </summary>
        public const string RaspWafTimeout = "_dd.appsec.rasp.timeout";

        /// <summary>
        /// Counts the number of times a rule type is evaluated.
        /// </summary>
        public const string RaspRuleEval = "_dd.appsec.rasp.rule.eval";

        /// <summary>
        /// Float representing the number of rules loaded successfully
        /// </summary>
        public const string AppSecWafInitRulesLoaded = "_dd.appsec.event_rules.loaded";

        /// <summary>
        /// Float representing the number of rules which failed to load
        /// </summary>
        public const string AppSecWafInitRulesErrorCount = "_dd.appsec.event_rules.error_count";

        /// <summary>
        /// Contains tag names that are associated with Single Span Sampling.
        /// </summary>
        internal static class SingleSpanSampling
        {
            /// <summary>
            /// Contains the <see cref="Sampling.SamplingMechanism"/> by which the individual span was sampled.
            /// </summary>
            internal const string SamplingMechanism = "_dd.span_sampling.mechanism";

            /// <summary>
            /// Contains the configured sampling probability for the <see cref="Sampling.ISpanSamplingRule"/> applied.
            /// </summary>
            /// <seealso cref="Sampling.ISpanSamplingRule.SamplingRate"/>
            internal const string RuleRate = "_dd.span_sampling.rule_rate";

            /// <summary>
            /// Contains the configured limit of maximum number of sampled spans for the <see cref="Sampling.ISpanSamplingRule"/> applied.
            /// <para>If there is no configured limit for the matched rule, this tag is omitted.</para>
            /// </summary>
            /// <seealso cref="Sampling.ISpanSamplingRule.MaxPerSecond"/>
            internal const string MaxPerSecond = "_dd.span_sampling.max_per_second";
        }
    }
}
