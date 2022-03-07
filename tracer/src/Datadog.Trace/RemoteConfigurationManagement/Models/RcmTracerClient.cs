// <copyright file="RcmTracerClient.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.RemoteConfigurationManagement.Models;

internal class RcmTracerClient
{
    [JsonProperty("runtime_id")]
    public string RuntimeId { get; set; }

    [JsonProperty("language")]
    public string Language { get; set; }

    [JsonProperty("tracer_version")]
    public string TracerVersion { get; set; }

    [JsonProperty("service")]
    public string Service { get; set; }

    [JsonProperty("env")]
    public string Env { get; set; }

    [JsonProperty("app_version")]
    public string AppVersion { get; set; }
}
