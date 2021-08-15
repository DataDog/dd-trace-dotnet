// <copyright file="Service.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.AppSec.EventModel
{
    internal class Service
    {
        [JsonProperty("context_version")]
        public string ContextVersion => "0.1.0";

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("environment")]
        public string Environment { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonPropertyIgnoreNullValue("when")]
        public DateTimeOffset? When { get; set; }
    }
}
