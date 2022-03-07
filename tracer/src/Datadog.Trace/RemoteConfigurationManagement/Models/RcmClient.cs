// <copyright file="RcmClient.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.RemoteConfigurationManagement.Models;

internal class RcmClient
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("products")]
    public string[] Products { get; set; }

    [JsonProperty("version")]
    public string Version { get; set; }

    [JsonProperty("state")]
    public object State { get; set; }

    [JsonProperty("is_tracer")]
    public bool IsTracer { get; set; }

    [JsonProperty("tracer_client")]
    public RcmTracerClient TracerClient { get; set; }
}
