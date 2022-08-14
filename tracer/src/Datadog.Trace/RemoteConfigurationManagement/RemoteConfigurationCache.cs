// <copyright file="RemoteConfigurationCache.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

namespace Datadog.Trace.RemoteConfigurationManagement
{
    internal class RemoteConfigurationCache
    {
        public RemoteConfigurationCache(RemoteConfigurationPath path, int length, Dictionary<string, string> hashes, int version)
        {
            Path = path;
            Length = length;
            Hashes = hashes;
            Version = version;
        }

        public RemoteConfigurationPath Path { get; }

        public int Length { get; }

        public Dictionary<string, string> Hashes { get; }

        public int Version { get; }
    }
}
