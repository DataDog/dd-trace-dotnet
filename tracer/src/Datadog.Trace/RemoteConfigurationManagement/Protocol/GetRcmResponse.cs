// <copyright file="GetRcmResponse.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Internal.RemoteConfigurationManagement.Json;
using Datadog.Trace.Internal.RemoteConfigurationManagement.Protocol.Tuf;
using Datadog.Trace.Internal.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Internal.RemoteConfigurationManagement.Protocol
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
