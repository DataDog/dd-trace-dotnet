// <copyright file="ImmutableDynamicSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;

namespace Datadog.Trace.Configuration
{
    internal class ImmutableDynamicSettings : IEquatable<ImmutableDynamicSettings>
    {
        public bool? TraceEnabled { get; init; }

        public bool? AppsecStandaloneEnabled { get; init; }

        public bool? RuntimeMetricsEnabled { get; init; }

        public bool? DataStreamsMonitoringEnabled { get; init; }

        public double? GlobalSamplingRate { get; init; }

        public string? SamplingRules { get; init; }

        public bool? LogsInjectionEnabled { get; init; }

        public IReadOnlyDictionary<string, string>? HeaderTags { get; init; }

        public IReadOnlyDictionary<string, string>? ServiceNameMappings { get; init; }

        public IReadOnlyDictionary<string, string>? GlobalTags { get; init; }

        public bool Equals(ImmutableDynamicSettings? other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return
                TraceEnabled == other.TraceEnabled
             && AppsecStandaloneEnabled == other.AppsecStandaloneEnabled
             && RuntimeMetricsEnabled == other.RuntimeMetricsEnabled
             && DataStreamsMonitoringEnabled == other.DataStreamsMonitoringEnabled
             && Nullable.Equals(GlobalSamplingRate, other.GlobalSamplingRate)
             && SamplingRules == other.SamplingRules
             && LogsInjectionEnabled == other.LogsInjectionEnabled
             && AreEqual(HeaderTags, other.HeaderTags)
             && AreEqual(ServiceNameMappings, other.ServiceNameMappings)
             && AreEqual(GlobalTags, other.GlobalTags);
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((ImmutableDynamicSettings)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                TraceEnabled,
                AppsecStandaloneEnabled,
                RuntimeMetricsEnabled,
                DataStreamsMonitoringEnabled,
                GlobalSamplingRate,
                SamplingRules,
                LogsInjectionEnabled);
        }

        private static bool AreEqual(IReadOnlyDictionary<string, string>? dictionary1, IReadOnlyDictionary<string, string>? dictionary2)
        {
            if (dictionary1 == null || dictionary2 == null)
            {
                return ReferenceEquals(dictionary1, dictionary2);
            }

            if (dictionary1.Count != dictionary2.Count)
            {
                return false;
            }

            foreach (var pair in dictionary1)
            {
                if (dictionary2.TryGetValue(pair.Key, out var value))
                {
                    if (!string.Equals(value, pair.Value))
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }

            return true;
        }
    }
}
