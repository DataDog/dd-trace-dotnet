// <copyright file="RemoteConfigurationCache.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;

namespace Datadog.Trace.RemoteConfigurationManagement
{
    internal sealed class RemoteConfigurationCache
    {
        public RemoteConfigurationCache(RemoteConfigurationPath path, long length, Dictionary<string, string> hashes, long version)
        {
            Path = path;
            Length = length;
            Hashes = hashes;
            Version = version;
        }

        public RemoteConfigurationPath Path { get; }

        public long Length { get; }

        public Dictionary<string, string> Hashes { get; }

        public long Version { get; }

        public ulong ApplyState { get; private set; } = ApplyStates.UNACKNOWLEDGED;

        public string Error { get; private set; }

        public void Applied()
        {
            ApplyState = ApplyStates.ACKNOWLEDGED;
            Error = null;
        }

        public void ErrorOccured(string error)
        {
            ApplyState = ApplyStates.ERROR;
            Error = error;
        }
    }
}
