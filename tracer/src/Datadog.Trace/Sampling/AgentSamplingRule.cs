// <copyright file="AgentSamplingRule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;

namespace Datadog.Trace.Sampling
{
    // These "default" sampling rule contains the mapping of service/env names to sampling rates.
    // These rates are received in http responses from the trace agent after we send a trace payload.
    internal sealed class AgentSamplingRule : ISamplingRule
    {
        private const string DefaultKey = "service:,env:";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<AgentSamplingRule>();
        private static readonly TimeSpan RampUpInterval = TimeSpan.FromSeconds(1);

        private Dictionary<SampleRateKey, float> _sampleRates = new();
        private float? _defaultSamplingRate;
        private DateTime _lastCapped;

        // if there are no rules, this normally means we haven't sent any payloads to the Agent yet (aka cold start), so the mechanism is "Default".
        // if there are rules, there should always be at least one match (the fallback "service:,env:") and the mechanism is "AgentRate".
        public string SamplingMechanism => _sampleRates.Count == 0 && _defaultSamplingRate == null ?
                                            Datadog.Trace.Sampling.SamplingMechanism.Default :
                                            Datadog.Trace.Sampling.SamplingMechanism.AgentRate;

        // Agent sampling rules do not depend on span resource names, only service and environment names.
        public bool IsResourceBasedSamplingRule => false;

        public bool IsMatch(Span span) => true;

        public float GetSamplingRate(Span span)
        {
            if (_sampleRates.Count > 0)
            {
                var service = span.ServiceName;
                var env = span.Context.TraceContext.Environment ?? string.Empty;
                var key = new SampleRateKey(service, env);

                if (_sampleRates.TryGetValue(key, out var matchingRate))
                {
                    return matchingRate;
                }
            }

            if (_defaultSamplingRate is { } defaultRate)
            {
                return defaultRate;
            }

            // we don't have sampling rates from the agent yet (cold start),
            // fallback to 100% sampling rate, don't add "_dd.agent_psr" numeric tag
            return 1;
        }

        /// <summary>
        /// Returns a rate that is at most 2x the old rate when increasing.
        /// Rate decreases and transitions from zero are applied immediately.
        /// When <paramref name="canIncrease"/> is false (cooldown not elapsed), increases are held at <paramref name="oldRate"/>.
        /// </summary>
        internal static bool CappedRate(float oldRate, float newRate, bool canIncrease, out float effectiveRate)
        {
            if (newRate <= oldRate || oldRate == 0)
            {
                effectiveRate = newRate;
                return false;
            }

            if (!canIncrease)
            {
                effectiveRate = oldRate;
                return fase;
            }

            effectiveRate = Math.Min(oldRate * 2, newRate);
            return true;
        }

        public void SetDefaultSampleRates(IReadOnlyDictionary<string, float> sampleRates)
        {
            if (sampleRates is not { Count: > 0 })
            {
                Log.Debug("Sampling rates received from the agent are empty.");
                return;
            }

            // to avoid locking if writers and readers can access the dictionary at the same time,
            // build the new dictionary first, then replace the old one
            var rates = new Dictionary<SampleRateKey, float>(sampleRates.Count);
            var defaultSamplingRate = _defaultSamplingRate;

            var now = Clock.UtcNow;
            var canIncrease = (now - _lastCapped) >= RampUpInterval;
            var capApplied = false;

            foreach (var pair in sampleRates)
            {
                if (string.Equals(pair.Key, DefaultKey, StringComparison.OrdinalIgnoreCase))
                {
                    var oldDefault = _defaultSamplingRate ?? 1f;
                    capApplied = CappedRate(oldDefault, pair.Value, canIncrease, out defaultSamplingRate) || capApplied;
                    continue;
                }

                var key = SampleRateKey.Parse(pair.Key);

                if (key == null)
                {
                    Log.Warning("Could not parse sampling rate key {SampleRateKey}", pair.Key);
                    continue;
                }

                if (!_sampleRates.TryGetValue(key.Value, out var oldRate))
                {
                    oldRate = _defaultSamplingRate ?? 1f;
                }

                capApplied = CappedRate(oldRate, pair.Value, canIncrease, out var effectiveRate) || capApplied;
                rates.Add(key.Value, effectiveRate);
            }

            if (capApplied)
            {
                _lastCapped = now;
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
                // Expected format: "service:{service},env:{env}"
                var parts = key.Split(PartSeparator);

                if (parts.Length != 2)
                {
                    return null;
                }

                var serviceParts = parts[0].Split(ValueSeparator, 2);

                if (serviceParts.Length != 2 || !string.Equals(serviceParts[0], "service", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                var envParts = parts[1].Split(ValueSeparator, 2);

                if (envParts.Length != 2 || !string.Equals(envParts[0], "env", StringComparison.OrdinalIgnoreCase))
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
