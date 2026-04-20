// <copyright file="AgentSamplingRule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.Sampling
{
    // These "default" sampling rule contains the mapping of service/env names to sampling rates.
    // These rates are received in http responses from the trace agent after we send a trace payload.
    internal sealed class AgentSamplingRule : ISamplingRule
    {
        private const string DefaultKey = "service:,env:";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<AgentSamplingRule>();

        private Dictionary<SampleRateKey, float> _sampleRates = new();
        private float? _defaultSamplingRate;

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
                    Log.Warning("Could not parse sampling rate key {SampleRateKey}", pair.Key);
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
                var keySpan = key.AsSpan();

                // there must be exactly one comma separating the service and env parts
                var commaIdx = keySpan.IndexOf(',');
                if (commaIdx < 0 || commaIdx != keySpan.LastIndexOf(','))
                {
                    return null;
                }

                var servicePart = keySpan.Slice(0, commaIdx);
                var envPart = keySpan.Slice(commaIdx + 1);

                var serviceColonIdx = servicePart.IndexOf(':');
                if (serviceColonIdx < 0 ||
                    !servicePart.Slice(0, serviceColonIdx).Equals("service".AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                var envColonIdx = envPart.IndexOf(':');
                if (envColonIdx < 0 ||
                    !envPart.Slice(0, envColonIdx).Equals("env".AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                return new SampleRateKey(
                    servicePart.Slice(serviceColonIdx + 1).ToString(),
                    envPart.Slice(envColonIdx + 1).ToString());
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
