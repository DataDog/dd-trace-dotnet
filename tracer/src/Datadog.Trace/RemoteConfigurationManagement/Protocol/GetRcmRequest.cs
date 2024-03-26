// <copyright file="GetRcmRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.RemoteConfigurationManagement.Protocol
{
    internal class GetRcmRequest
    {
        public GetRcmRequest(RcmClient client, List<RcmCachedTargetFile> cachedTargetFiles)
        {
            Client = client;
            CachedTargetFiles = cachedTargetFiles;
        }

        [JsonProperty("client")]
        public RcmClient Client { get; }

        [JsonProperty("cached_target_files")]
        public List<RcmCachedTargetFile> CachedTargetFiles { get; }
    }
}
