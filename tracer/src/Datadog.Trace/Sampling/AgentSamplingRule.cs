// <copyright file="AgentSamplingRule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Sampling
{
    // These "default" sampling rule contains the mapping of service/env names to sampling rates.
    // These rates are received in http responses from the trace agent after we send a trace payload.
    internal class AgentSamplingRule : ISamplingRule
    {
        private const string DefaultKey = "service:,env:";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<AgentSamplingRule>();

        private Dictionary<SampleRateKey, float> _sampleRates = new();
        private float? _defaultSamplingRate;

        // if there are no rules, this normally means we haven't sent any payloads to the Agent yet (aka cold start), so the mechanism is "Default".
        // if there are rules, there should always be at least one match (the fallback "service:,env:") and the mechanism is "AgentRate".
        public int SamplingMechanism => _sampleRates.Count == 0 && _defaultSamplingRate == null ?
                                            Datadog.Trace.Sampling.SamplingMechanism.Default :
                                            Datadog.Trace.Sampling.SamplingMechanism.AgentRate;

        /// <summary>
        /// Gets the lowest possible priority
        /// </summary>
        public int Priority => int.MinValue;

        public bool IsMatch(Span span) => true;

        public float GetSamplingRate(Span span)
        {
            Log.Debug("Using the default sampling logic");
            float defaultRate;

            if (_sampleRates.Count == 0)
            {
                // either we don't have sampling rate from the agent yet (cold start),
                // or the only rate we received is for "service:,env:", which is not added to _sampleRates
                defaultRate = _defaultSamplingRate ?? 1;

                // add the _dd.agent_psr tag even if we didn't use rates from the agent (to ease investigations)
                SetSamplingRateTag(span, defaultRate);
                return defaultRate;
            }

            var env = span.Context.TraceContext.Environment ?? string.Empty;
            var service = span.ServiceName;

            var key = new SampleRateKey(service, env);

            if (_sampleRates.TryGetValue(key, out var sampleRate))
            {
                SetSamplingRateTag(span, sampleRate);
                return sampleRate;
            }

            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug("Could not establish sample rate for trace {TraceId}. Using default rate instead: {Rate}", span.Context.RawTraceId, _defaultSamplingRate);
            }

            // no match by service/env, you default rate
            defaultRate = _defaultSamplingRate ?? 1;
            SetSamplingRateTag(span, defaultRate);
            return defaultRate;

            static void SetSamplingRateTag(Span span, float sampleRate)
            {
                if (span.Tags is CommonTags commonTags)
                {
                    commonTags.SamplingAgentDecision = sampleRate;
                }
                else
                {
                    span.SetMetric(Metrics.SamplingAgentDecision, sampleRate);
                }
            }
        }

        public void SetDefaultSampleRates(IReadOnlyDictionary<string, float> sampleRates)
        {
            if (sampleRates is not { Count: > 0 })
            {
                Log.Debug("sampling rates received from the agent are empty");
                return;
            }

            // to avoid locking if writers and readers can access the dictionary at the same time,
            // build the new dictionary first, then replace the old one
            var rates = new Dictionary<SampleRateKey, float>(sampleRates.Count);
            var defaultSamplingRate = _defaultSamplingRate;

            foreach (var pair in sampleRates)
            {
                if (string.Equals(pair.Key, DefaultKey, StringComparison.OrdinalIgnoreCase))
                {
                    defaultSamplingRate = pair.Value;
                    continue;
                }

                var key = SampleRateKey.Parse(pair.Key);

                if (key == null)
                {
                    Log.Warning("Could not parse sample rate key {SampleRateKey}", pair.Key);
                    continue;
                }

                rates.Add(key.Value, pair.Value);
            }

            _defaultSamplingRate = defaultSamplingRate;
            _sampleRates = rates;
        }

        public override string ToString()
        {
            return "AgentSamplingRates";
        }

        private readonly struct SampleRateKey : IEquatable<SampleRateKey>
        {
            private static readonly char[] PartSeparator = [','];
            private static readonly char[] ValueSeparator = [':'];

            private readonly string _service;
            private readonly string _env;

            public SampleRateKey(string service, string env)
            {
                _service = service;
                _env = env;
            }

            public static SampleRateKey? Parse(string key)
            {
                // Expected format:
                // service:{service},env:{env}
                var parts = key.Split(PartSeparator);

                if (parts.Length != 2)
                {
                    return null;
                }

                var serviceParts = parts[0].Split(ValueSeparator, 2);

                if (serviceParts.Length != 2)
                {
                    return null;
                }

                var envParts = parts[1].Split(ValueSeparator, 2);

                if (envParts.Length != 2)
                {
                    return null;
                }

                return new SampleRateKey(serviceParts[1], envParts[1]);
            }

            public bool Equals(SampleRateKey other)
            {
                return _service == other._service && _env == other._env;
            }

            public override bool Equals(object? obj)
            {
                return obj is SampleRateKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((_service != null ? _service.GetHashCode() : 0) * 397) ^ (_env != null ? _env.GetHashCode() : 0);
                }
            }
        }
    }
}