// <copyright file="Intake.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.AppSec.EventModel.Batch
{
    internal class Intake : IEvent
    {
        [JsonProperty("protocol_version")]
        internal int ProtocolVersion { get; set; }

        [JsonProperty("idempotency_key")]
        internal string IdemPotencyKey { get; set; }

        [JsonProperty("events")]
        internal IEnumerable<IEvent> Events { get; set; }
    }
}
