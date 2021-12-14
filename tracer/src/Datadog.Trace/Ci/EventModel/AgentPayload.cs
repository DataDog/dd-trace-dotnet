// <copyright file="AgentPayload.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Ci.EventModel
{
    // AgentPayload represents payload the agent sends to the intake.
    // https://github.com/DataDog/datadog-agent/blob/main/pkg/trace/pb/agent_payload.proto#L9-L25
    internal readonly ref struct AgentPayload
    {
        // hostName specifies hostname of where the agent is running.
        [JsonProperty("hostName")]
        public readonly string HostName;
        // env specifies `env` set in agent configuration.
        [JsonProperty("env")]
        public readonly string Env;
        // tracerPayloads specifies list of the payloads received from tracers.
        [JsonProperty("tracerPayloads")]
        public readonly List<TracerPayload> TracerPayloads;
        // tags specifies tags common in all `tracerPayloads`.
        [JsonProperty("tags")]
        public readonly Dictionary<string, string> Tags;
        // agentVersion specifies version of the agent.
        [JsonProperty("agentVersion")]
        public readonly string AgentVersion;
        // targetTPS holds `TargetTPS` value in AgentConfig.
        [JsonProperty("targetTPS")]
        public readonly double TargetTPS;
        // errorTPS holds `ErrorTPS` value in AgentConfig.
        [JsonProperty("errorTPS")]
        public readonly double ErrorTPS;
    }

    // TracerPayload represents a payload the trace agent receives from tracers.
    // https://github.com/DataDog/datadog-agent/blob/main/pkg/trace/pb/tracer_payload.proto#L22-L44
    internal readonly struct TracerPayload
    {
        // containerID specifies the ID of the container where the tracer is running on.
        [JsonProperty("container_id")]
        public readonly string ContainerId;
        // languageName specifies language of the tracer.
        [JsonProperty("language_name")]
        public readonly string LanguageName;
        // languageVersion specifies language version of the tracer.
        [JsonProperty("language_version")]
        public readonly string LanguageVersion;
        // tracerVersion specifies version of the tracer.
        [JsonProperty("tracer_version")]
        public readonly string TracerVersion;
        // runtimeID specifies V4 UUID representation of a tracer session.
        [JsonProperty("runtime_id")]
        public readonly string RuntimeId;
        // chunks specifies list of containing trace chunks.
        [JsonProperty("chunks")]
        public readonly List<TraceChunk> Chunks;
        // tags specifies tags common in all `chunks`.
        [JsonProperty("tags")]
        public readonly Dictionary<string, string> Tags;
        // env specifies `env` tag that set with the tracer.
        [JsonProperty("env")]
        public readonly string Env;
        // hostname specifies hostname of where the tracer is running.
        [JsonProperty("hostName")]
        public readonly string HostName;
        // version specifies `version` tag that set with the tracer.
        [JsonProperty("app_version")]
        public readonly string AppVersion;
    }

    // TraceChunk represents a list of spans with the same trace id.
    // https://github.com/DataDog/datadog-agent/blob/main/pkg/trace/pb/tracer_payload.proto#L8-L20
    internal readonly struct TraceChunk
    {
        // priority specifies sampling priority of the trace.
        [JsonProperty("priority")]
        public readonly int Priority;
        // origin specifies origin product ("lambda", "rum", etc.) of the trace.
        [JsonProperty("origin")]
        public readonly string Origin;
        // spans specifies list of containing spans.
        [JsonProperty("spans")]
        public readonly List<TraceSpan> Spans;
        // tags specifies tags common in all `spans`.
        [JsonProperty("tags")]
        public readonly Dictionary<string, string> Tags;
        // droppedTrace specifies whether the trace was dropped by samplers or not.
        [JsonProperty("dropped_trace")]
        public readonly bool DroppedTrace;
    }

    // https://github.com/DataDog/datadog-agent/blob/main/pkg/trace/pb/span.proto
    internal readonly struct TraceSpan
    {
        [JsonProperty("service")]
        public readonly string Service;
        [JsonProperty("name")]
        public readonly string Name;
        [JsonProperty("resource")]
        public readonly string Resource;
        [JsonProperty("trace_id")]
        public readonly ulong TraceId;
        [JsonProperty("span_id")]
        public readonly ulong SpanId;
        [JsonProperty("parent_id")]
        public readonly ulong ParentId;
        [JsonProperty("start")]
        public readonly long Start;
        [JsonProperty("duration")]
        public readonly long Duration;
        [JsonProperty("error")]
        public readonly int Error;
        [JsonProperty("meta")]
        public readonly Dictionary<string, string> Meta;
        [JsonProperty("metrics")]
        public readonly Dictionary<string, double> Metrics;
        [JsonProperty("type")]
        public readonly string Type;
    }
}
