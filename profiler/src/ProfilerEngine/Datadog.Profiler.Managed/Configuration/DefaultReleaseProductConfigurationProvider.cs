// <copyright file="DefaultReleaseProductConfigurationProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;

namespace Datadog.Configuration
{
    internal static class DefaultReleaseProductConfigurationProvider
    {
        public static IProductConfiguration ApplyReleaseDefaults(this IProductConfiguration config)
        {
            if (config == null)
            {
                return null;
            }

            var mutableConfig = new MutableProductConfiguration(config);

            mutableConfig.ProfilesExport_DefaultInterval = TimeSpan.FromSeconds(60);

            // These bytes are counted as encoded in the buffer segment, i.e. each snapshot takes (26 + 9 per frame) bytes (see StackSnapshotResult.h).
            // Snapshots with 100 frames: 1 snapshot uses 926 bytes, 1MB holds 1132 such snapshots.
            // Snapshots with 50 frames:  1 snapshot uses 476 bytes, 1MB holds 2202 such snapshots
            // Snapshots with 35 frames:  1 snapshot uses 341 bytes, 1MB holds 3075 such snapshots.
            //
            // We will not sample 5 threads more often than once every 9 milliseconds.
            // Assuming 50 frames per snapshot on average (based on some ad-hoc tests with Computer01 demo
            // it is a safe upper bound for the average), we don't expect to generate more than ~33333 (= 5 * 60 * 1000 / 9) snapshots per minute
            // --> which requires 33333 / 2202 ~= 15 MB.
            // A 500 MB buffer will work for ~33 minutes
            // 1,000,000 samples currespond to ~454 MB.
            //
            // Based on the above, we go with the magic numbers that will limit the buffer to 500 MB and 1 Mio samples
            // by triggering the profiles export if those thresholds are met.
            // These are round numbers that will keep the impact on the customer app in check during early beta stages.
            // During the public beta we need to validate these assumptions and see if the numbers need to be tweaked in either direction.
            mutableConfig.ProfilesExport_EarlyTriggerOnCollectedStackSnapshotsBytes = 500 * 1024 * 1024;  // 500 MBytes
            mutableConfig.ProfilesExport_EarlyTriggerOnCollectedStackSnapshotsCount = 1000000;            // 1,000,000 stack samples

            mutableConfig.ProfilesExport_LocalFiles_Directory = null;

            mutableConfig.ProfilesIngestionEndpoint_Url = null;                       // If Ingestion Endpoint Url is specified, the Host, Port and ApiPath are ignored.
            mutableConfig.ProfilesIngestionEndpoint_Host = "127.0.0.1";               // Local agent (avoids the IPv4 wait that can occur when using "localhost")
            mutableConfig.ProfilesIngestionEndpoint_Port = 8126;                      // Local agent's default port
            mutableConfig.ProfilesIngestionEndpoint_ApiPath = "profiling/v1/input";   // Local agent's API path.

            // Api Key is not required for agent-based ingestion scnarios; it IS required for agent-less ingestion.
            mutableConfig.ProfilesIngestionEndpoint_DatadogApiKey = null;

            // For RELEASE, the we use defaults below, and better values need to be created by setting respective environment variables
            // (see the .ApplyEnvironmentVariables() API in EnvironmentVariablesConfigurationProvider).
            string ddService = ConfigurationProviderUtils.GetDdServiceFallback();
            mutableConfig.DDDataTags_Host = ConfigurationProviderUtils.GetMachineName();
            mutableConfig.DDDataTags_Service = string.IsNullOrWhiteSpace(ddService) ? "Unspecified-Service" : ddService;
            mutableConfig.DDDataTags_Env = "Unspecified-Environment";
            mutableConfig.DDDataTags_Version = "Unspecified-Version";
            mutableConfig.DDDataTags_CustomTags = new KeyValuePair<string, string>[0];

            // When we get to public beta, Debug-Logging should be DISABLED by default.
            // However, while we are still working towards that level of maturity, debug logs are almost always helpful.
            mutableConfig.Log_IsDebugEnabled = true;
            mutableConfig.Log_PreferredLogFileDirectory = ConfigurationProviderUtils.GetOsSpecificDefaultLogDirectory();

            mutableConfig.Metrics_Operational_IsEnabled = false;

            mutableConfig.Metrics_StatsdAgent_Port = 8125;

            // For now, the default installation does not collect native frames.
            // The user can, however, enable it using an environment variable (see 'EnvironmentVariablesConfigurationProvider').
            // This should be changed once the UI supports appropriate filtering.
            mutableConfig.FrameKinds_Native_IsEnabled = false;

            return mutableConfig.CreateImmutableSnapshot();
        }
    }
}