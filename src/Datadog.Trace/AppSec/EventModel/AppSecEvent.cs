// <copyright file="AppSecEvent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.AppSec.EventModel
{
    internal class AppSecEvent : IEvent
    {
        [JsonProperty("event_version")]
        public string EventVersion => "0.1.0";

        [JsonProperty("event_id")]
        public string EventId { get; set; }

        [JsonProperty("detected_at")]
        public DateTime DetectedAt { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("blocked")]
        public bool Blocked { get; set; }
    }
}
