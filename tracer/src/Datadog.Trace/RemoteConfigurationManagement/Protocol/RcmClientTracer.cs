// <copyright file="RcmClientTracer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Datadog.Trace.Vendors.Newtonsoft.Json;

#nullable enable

namespace Datadog.Trace.RemoteConfigurationManagement.Protocol
{
    internal sealed class RcmClientTracer
    {
        // Don't change this constructor - it's used by Newtonsoft.JSON for deserialization
        // and that can mean the provided properties are not _really_ nullable, even though we "require" them to be
        public RcmClientTracer(string runtimeId, string tracerVersion, string service, string env, string? appVersion, List<string> tags, List<string>? processTags)
        {
            RuntimeId = runtimeId;
            Language = TracerConstants.Language;
            TracerVersion = tracerVersion;
            Service = service;
            ProcessTags = processTags;
            Env = env;
            AppVersion = appVersion;
            Tags = tags ?? [];
        }

        [JsonIgnore]
        public bool IsGitMetadataAddedToRequestTags { get; set; }

        [JsonProperty("runtime_id")]
        public string? RuntimeId { get; }

        [JsonProperty("language")]
        public string Language { get; }

        [JsonProperty("tracer_version")]
        public string? TracerVersion { get; }

        [JsonProperty("service")]
        public string? Service { get; }

        [JsonProperty("process_tags")]
        public List<string>? ProcessTags { get; }

        [JsonProperty("extra_services")]
        public string[]? ExtraServices { get; set; }

        [JsonProperty("env")]
        public string? Env { get; }

        [JsonProperty("app_version")]
        public string? AppVersion { get; }

        [JsonProperty("tags")]
        public List<string> Tags { get; }

        public static RcmClientTracer Create(string runtimeId, string tracerVersion, string service, string env, string? appVersion, ReadOnlyDictionary<string, string> globalTags, List<string>? processTags)
            => new(runtimeId, tracerVersion, service, env, appVersion, GetTags(env, service, globalTags), processTags);

        private static List<string> GetTags(string? environment, string? serviceVersion, ReadOnlyDictionary<string, string>? globalTags)
        {
            var tags = globalTags?.Count > 0
                           ? globalTags.Select(pair => pair.Key + ":" + pair.Value).ToList()
                           : [];

            if (!string.IsNullOrEmpty(environment))
            {
                tags.Add($"env:{environment}");
            }

            if (!string.IsNullOrEmpty(serviceVersion))
            {
                tags.Add($"version:{serviceVersion}");
            }

            tags.Add($"tracer_version:{TracerConstants.ThreePartVersion}");

            var hostName = PlatformHelpers.HostMetadata.Instance?.Hostname;
            if (!string.IsNullOrEmpty(hostName))
            {
                tags.Add($"host_name:{hostName}");
            }

            return tags;
        }
    }
}
