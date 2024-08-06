// <copyright file="Tags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Internal.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Internal.AppSec.Waf.ReturnTypes.Managed
{
    internal class Tags
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("category")]
        public string Category { get; set; }
    }
}
