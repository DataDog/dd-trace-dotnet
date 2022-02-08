// <copyright file="ProductConfigurationProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Configuration
{
    internal static class ProductConfigurationProvider
    {
        public static IProductConfiguration CreateDefault()
        {
            return new ImmutableProductConfiguration(
                profilesExport_DefaultInterval: TimeSpan.FromSeconds(60),                   // Send profiles once per minute
                profilesExport_EarlyTriggerOnCollectedStackSnapshotsBytes: int.MaxValue,    // No early trigger, or max value enforced by the actual exporter
                profilesExport_EarlyTriggerOnCollectedStackSnapshotsCount: int.MaxValue,    // No early trigger, or max value enforced by the actual exporter
                profilesExport_LocalFiles_Directory: null,                                  // Specific config providers must set this if local pprofs are enabled
                profilesIngestionEndpoint_Url: null,                                        // If Url was specified, then Host, Port and ApiPath would be ignored.
                profilesIngestionEndpoint_Host: "127.0.0.1",                                // Local agent (avoids the IPv4 wait that can occur when using "localhost")
                profilesIngestionEndpoint_Port: 8126,                                       // Local agent's default port
                profilesIngestionEndpoint_ApiPath: "profiling/v1/input",                    // Local agent's API path.
                profilesIngestionEndpoint_DatadogApiKey: null,                              // Only required for agent-less ingestion.
                ddDataTags_Host: null,                                                      // Data annotation skipped if not set by more specific config provider
                ddDataTags_Service: null,                                                   // Data annotation skipped if not set by more specific config provider
                ddDataTags_Env: null,                                                       // Data annotation skipped if not set by more specific config provider
                ddDataTags_Version: null,                                                   // Data annotation skipped if not set by more specific config provider
                ddDataTags_CustomTags: null,                                                // Data annotation skipped if not set by more specific config provider
                log_IsDebugEnabled: true,                                                   // Unless a more specific config provider is used, we better log everything
                log_PreferredLogFileDirectory: null,                                        // Use hard default if no setting specified
                metrics_Operational_IsEnabled: false,
                metrics_StatsdAgent_Port: 8125,                                             // Statsd Agent port
                frameKinds_Native_IsEnabled: true);                                         // In the most basic configuration we collect everything we can
        }

        public static IProductConfiguration CreateImmutableSnapshot(this IProductConfiguration config)
        {
            if (config == null)
            {
                return null;
            }
            else
            {
                return new ImmutableProductConfiguration(config);
            }
        }
    }
}
