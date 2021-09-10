// <copyright file="ResultData.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.AppSec.Waf.ReturnTypes.Managed
{
    internal class ResultData
    {
        [JsonProperty("ret_code")]
        internal int RetCode { get; set; }

        [JsonProperty("flow")]
        internal string Flow { get; set; }

        [JsonProperty("step")]
        internal string Step { get; set; }

        [JsonProperty("rule")]
        internal string Rule { get; set; }

        [JsonProperty("filter")]
        internal List<Filter> Filter { get; set; }
    }
}
