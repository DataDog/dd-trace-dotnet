// <copyright file="RcmCachedTargetFile.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.RemoteConfigurationManagement.Protocol
{
    internal sealed class RcmCachedTargetFile
    {
        public RcmCachedTargetFile(string path, long length, List<RcmCachedTargetFileHash> hashes)
        {
            Path = path;
            Length = length;
            Hashes = hashes;
        }

        [JsonProperty("path")]
        public string Path { get; }

        [JsonProperty("length")]
        public long Length { get; }

        [JsonProperty("hashes")]
        public List<RcmCachedTargetFileHash> Hashes { get; }
    }
}
