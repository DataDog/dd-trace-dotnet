// <copyright file="Actor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.AppSec.EventModel
{
    internal class Actor
    {
        [JsonProperty("context_version")]
        public string ContextVersion => "0.1.0";

        [JsonProperty("ip")]
        public Ip Ip { get; set; }

        [JsonProperty("identifiers")]
        public Identifiers Identifiers { get; set; }

        [JsonProperty("_id")]
        public string Id { get; set; }
    }
}
