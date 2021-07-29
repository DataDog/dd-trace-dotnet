// <copyright file="Context.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.AppSec.EventModel
{
    internal class Context
    {
        [JsonProperty("actor")]
        public Actor Actor { get; set; }

        [JsonProperty("host")]
        public Host Host { get; set; }

        [JsonProperty("http")]
        public Http Http { get; set; }

        [JsonProperty("service")]
        public Service Service { get; set; }

        [JsonProperty("service_stack")]
        public ServiceStack ServiceStack { get; set; }

        [JsonProperty("span")]
        public Span Span { get; set; }

        [JsonProperty("trace")]
        public Span Trace { get; set; }

        [JsonProperty("tracer")]
        public Tracer Tracer { get; set; }
    }
}
