// <copyright file="GetRcmResponse.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.RemoteConfigurationManagement.Json;
using Datadog.Trace.RemoteConfigurationManagement.Protocol.Tuf;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.RemoteConfigurationManagement.Protocol
{
    internal class GetRcmResponse
    {
        [JsonConverter(typeof(TufRootBase64Converter))]
        [JsonProperty("targets")]
        public TufRoot Targets { get; set; }

        [JsonProperty("client_configs")]
        public List<string> ClientConfigs { get; set; } = new();

        [JsonProperty("target_files")]
        public List<RcmFile> TargetFiles { get; set; } = new();
    }
}
