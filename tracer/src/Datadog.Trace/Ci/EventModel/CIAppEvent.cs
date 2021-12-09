// <copyright file="CIAppEvent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Ci.EventModel
{
    internal class CIAppEvent<T> : IEvent
    {
        internal CIAppEvent(string type, T content)
        {
            Type = type;
            Content = content;
        }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("content")]
        public T Content { get; set; }
    }
}
