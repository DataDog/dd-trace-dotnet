using System;
using System.Collections.Generic;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Sampling
{
    internal class DefaultSamplingRule : ISamplingRule
    {
        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.For<DefaultSamplingRule>();

        private Dictionary<SampleRateKey, float> _sampleRates = new Dictionary<SampleRateKey, float>();

        public string RuleName => "default-rule";

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
                return 1;
            }

            var env = span.GetTag(Tags.Env);
            var service = span.ServiceName;

            var key = new SampleRateKey(service, env);

            if (_sampleRates.TryGetValue(key, out var sampleRate))
            {
                span.SetMetric(Metrics.SamplingAgentDecision, sampleRate);
                return sampleRate;
            }

            Log.Debug("Could not establish sample rate for trace {TraceId}", span.TraceId);

            return 1;
        }

        public void SetDefaultSampleRates(IEnumerable<KeyValuePair<string, float>> sampleRates)
        {
            // to avoid locking if writers and readers can access the dictionary at the same time,
            // build the new dictionary first, then replace the old one
            var rates = new Dictionary<SampleRateKey, float>();

            if (sampleRates != null)
            {
                foreach (var pair in sampleRates)
                {
                    // No point in adding default rates
                    if (pair.Value == 1.0f)
                    {
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
            }

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
