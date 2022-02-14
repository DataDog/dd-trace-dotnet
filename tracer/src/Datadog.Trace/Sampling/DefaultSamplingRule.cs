// <copyright file="DefaultSamplingRule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.Sampling
{
    internal class DefaultSamplingRule : ISamplingRule
    {
        private const string DefaultKey = "service:,env:";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<DefaultSamplingRule>();

        private Dictionary<SampleRateKey, float> _sampleRates = new();
        private float _defaultSamplingRate = 1;

        public string RuleName => "default-rule";

        public int SamplingMechanism => Datadog.Trace.Sampling.SamplingMechanism.AgentRate;

        /// <summary>
        /// Gets the lowest possible priority
        /// </summary>
        public int Priority => int.MinValue;

        public bool IsMatch(Span span)
        {
            return true;
        }

        public float GetSamplingRate(Span span)
        {
            Log.Debug("Using the default sampling logic");

            if (_sampleRates.Count == 0)
            {
                span.SetMetric(Metrics.SamplingAgentDecision, 1); // Keep it to ease investigations
                return 1;
            }

            string env;

            if (span.Tags is CommonTags tags)
            {
                env = tags.Environment;
            }
            else
            {
                env = span.GetTag(Tags.Env);
            }

            var service = span.ServiceName;

            var key = new SampleRateKey(service, env);

            if (_sampleRates.TryGetValue(key, out var sampleRate))
            {
                span.SetMetric(Metrics.SamplingAgentDecision, sampleRate);
                return sampleRate;
            }

            Log.Debug("Could not establish sample rate for trace {TraceId}. Using default rate instead: {rate}", span.TraceId, _defaultSamplingRate);
            span.SetMetric(Metrics.SamplingAgentDecision, _defaultSamplingRate);
            return _defaultSamplingRate;
        }

        public void SetDefaultSampleRates(IReadOnlyDictionary<string, float> sampleRates)
        {
            if (sampleRates is null || sampleRates.Count == 0)
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

        private readonly struct SampleRateKey : IEquatable<SampleRateKey>
        {
            private static readonly char[] PartSeparator = new[] { ',' };
            private static readonly char[] ValueSeparator = new[] { ':' };

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

            public override bool Equals(object obj)
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
