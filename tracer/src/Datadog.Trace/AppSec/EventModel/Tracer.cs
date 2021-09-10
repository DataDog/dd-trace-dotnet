// <copyright file="Tracer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.AppSec.EventModel
{
    internal class Tracer
    {
        [JsonProperty("context_version")]
        public string ContextVersion => "0.1.0";

        [JsonProperty("runtime_type")]
        public string RuntimeType { get; set; }

        [JsonProperty("runtime_version")]
        public string RuntimeVersion { get; set; }

        [JsonProperty("lib_version")]
        public string LibVersion { get; set; }
    }
}
