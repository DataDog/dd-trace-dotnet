// <copyright file="IProductConfiguration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;

namespace Datadog.Configuration
{
    public interface IProductConfiguration
    {
        TimeSpan ProfilesExport_DefaultInterval { get; }
        long ProfilesExport_EarlyTriggerOnCollectedStackSnapshotsBytes { get; }
        long ProfilesExport_EarlyTriggerOnCollectedStackSnapshotsCount { get; }

        string ProfilesExport_LocalFiles_Directory { get; }

        string ProfilesIngestionEndpoint_Url { get; }
        string ProfilesIngestionEndpoint_Host { get; }
        int ProfilesIngestionEndpoint_Port { get; }
        string ProfilesIngestionEndpoint_ApiPath { get; }
        string ProfilesIngestionEndpoint_DatadogApiKey { get; }

        string DDDataTags_Host { get; }
        string DDDataTags_Service { get; }
        string DDDataTags_Env { get; }
        string DDDataTags_Version { get; }
        IEnumerable<KeyValuePair<string, string>> DDDataTags_CustomTags { get; }

        bool Log_IsDebugEnabled { get; }
        string Log_PreferredLogFileDirectory { get; }

        bool Metrics_Operational_IsEnabled { get; }

        int Metrics_StatsdAgent_Port { get; }

        bool FrameKinds_Native_IsEnabled { get; }
    }
}