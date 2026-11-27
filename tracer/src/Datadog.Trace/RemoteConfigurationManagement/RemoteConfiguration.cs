// <copyright file="RemoteConfiguration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.RemoteConfigurationManagement
{
    internal sealed class RemoteConfiguration
    {
        public RemoteConfiguration(
            RemoteConfigurationPath path,
            RemoteConfigurationContent contents,
            long version)
        {
            Path = path;
            Contents = contents;
            Version = version;
        }

        public RemoteConfigurationPath Path { get; }

        public RemoteConfigurationContent Contents { get; }

        public long Version { get; }
    }
}
