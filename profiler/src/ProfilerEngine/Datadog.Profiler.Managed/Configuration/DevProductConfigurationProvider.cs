// <copyright file="DevProductConfigurationProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Logging.Emission;
using Datadog.Profiler;

namespace Datadog.Configuration
{
    internal static class DevProductConfigurationProvider
    {
        private static readonly LogSourceInfo LogComponentMoniker = new LogSourceInfo(nameof(DevProductConfigurationProvider));

        public static IProductConfiguration ApplyReleaseOrDevDefaults(this IProductConfiguration config)
        {
            const string EnvVarName = "DD_INTERNAL_USE_DEVELOPMENT_CONFIGURATION";
            const bool ddInternalUseDevelopmentConfigurationValDefault = false;

            string ddInternalUseDevelopmentConfiguration = Environment.GetEnvironmentVariable(EnvVarName);

            if (
                ConfigurationProviderUtils.TryParseBooleanSettingStr(
                    ddInternalUseDevelopmentConfiguration,
                    ddInternalUseDevelopmentConfigurationValDefault,
                    out bool ddInternalUseDevelopmentConfigurationVal))
            {
                Log.Info(
                    LogComponentMoniker,
                    "Use-Dev-Config environment setting found and parsed.",
                    "Env var name",
                    EnvVarName,
                    "Parsed value",
                    ddInternalUseDevelopmentConfigurationVal);
            }
            else
            {
                Log.Info(
                    LogComponentMoniker,
                    "Use-Dev-Config environment setting not found or not parsed. Default will be used.",
                    "Env var name",
                    EnvVarName,
                    "Value",
                    ddInternalUseDevelopmentConfiguration,
                    "Used default value",
                    ddInternalUseDevelopmentConfigurationVal);
            }

            if (ddInternalUseDevelopmentConfigurationVal)
            {
                return DevProductConfigurationProvider.ApplyDevDefaults(config);
            }
            else
            {
                return DefaultReleaseProductConfigurationProvider.ApplyReleaseDefaults(config);
            }
        }

        public static IProductConfiguration ApplyDevDefaults(this IProductConfiguration config)
        {
            if (config == null)
            {
                return null;
            }

            var mutableConfig = new MutableProductConfiguration(config);

            // mutableConfig.ProfilesExport_DefaultInterval = TimeSpan.FromSeconds(20);
            mutableConfig.ProfilesExport_DefaultInterval = TimeSpan.FromSeconds(60);

            // These bytes are counted as encoded in the buffer segment, i.e. each snapshot takes (26 + 9 per frame) bytes (see StackSnapshotResult.h).
            // Snapshots with 100 frames: 1 snapshot uses 926 bytes, 1MB holds 1132 such snapshots.
            // Snapshots with 50 frames:  1 snapshot uses 476 bytes, 1MB holds 2202 such snapshots
            // Snapshots with 35 frames:  1 snapshot uses 341 bytes, 1MB holds 3075 such snapshots.
            //
            // We will not sample 5 threads more often than once every 9 milliseconds.
            // Assuming 50 frames per snapshot on average (based on some ad-hoc tests with Computer01 demo
            // it is a safe upper bound for the average), we don't expect to generate more than ~33,333 (= 5 * 60 * 1000 / 9) snapshots per minute
            // --> which requires 33,333 / 2202 ~= 15 MB.
            // A 50 MB buffer will work for ~3+ minutes
            // 50,000 samples currespond to ~22 MB.
            mutableConfig.ProfilesExport_EarlyTriggerOnCollectedStackSnapshotsBytes = 50 * 1024 * 1024;  // 50 MBytes
            mutableConfig.ProfilesExport_EarlyTriggerOnCollectedStackSnapshotsCount = 50000;

            mutableConfig.ProfilesExport_LocalFiles_Directory = ConfigurationProviderUtils.GetOsSpecificDefaultPProfDirectory();

            // If Ingestion Endpoint Url is specified, the Host, Port and ApiPath are ignored.
            // mutableConfig.ProfilesIngestionEndpoint_Url = "https://intake.profile.datadoghq.com/v1/input";

            // --> to local agent that sends to staging
            // mutableConfig.ProfilesIngestionEndpoint_Host = "127.0.0.1";                       // Local agent (avoids the IPv4 wait that can occur when using "localhost")
            // mutableConfig.ProfilesIngestionEndpoint_Host = "localhost";                    // Local agent
            // mutableConfig.ProfilesIngestionEndpoint_Host = "intake.profile.datadoghq.com"; // Main DD ingestion endpoint for agent-less
            // mutableConfig.ProfilesIngestionEndpoint_Host = "intake.profile.datad0g.com";   // Staging DD ingestion endpoint for agent-less

            mutableConfig.ProfilesIngestionEndpoint_Port = 8126;   // Local agent's default port
            // mutableConfig.ProfilesIngestionEndpoint_Port = 0;   // Value <= 0 will result the defaut port for the protocol being used.

            mutableConfig.ProfilesIngestionEndpoint_ApiPath = "profiling/v1/input";  // Local agent's API path.
            // mutableConfig.ProfilesIngestionEndpoint_ApiPath = "v1/input";         // DD ingestion endpoint's API path (for agent-less scenarios)

            // Api Key is not required for agent-based ingestion scenarios; it IS required for agent-less ingestion.
            // mutableConfig.ProfilesIngestionEndpoint_DatadogApiKey = "xxx";
            // mutableConfig.ProfilesIngestionEndpoint_DatadogApiKey = null;

            // --> to preprod
            mutableConfig.ProfilesIngestionEndpoint_Url = "https://intake.profile.datadoghq.com/v1/input";
            mutableConfig.ProfilesIngestionEndpoint_DatadogApiKey = "";

            string ddService = ConfigurationProviderUtils.GetDdServiceFallback();
            mutableConfig.DDDataTags_Host = ConfigurationProviderUtils.GetMachineName();
            mutableConfig.DDDataTags_Service = string.IsNullOrWhiteSpace(ddService) ? ".Net-Profiling-TestService01" : ddService;
            mutableConfig.DDDataTags_Env = "APM-Profiling-Local";
            mutableConfig.DDDataTags_Version = "Demo-Version-11";
            mutableConfig.DDDataTags_CustomTags = new KeyValuePair<string, string>[]
                    {
                        new KeyValuePair<string, string>("CustomTag A", string.Empty),
                        new KeyValuePair<string, string>(null, "Some Value B"),
                        new KeyValuePair<string, string>("CustomTag C", "Some Value C"),
                        new KeyValuePair<string, string>("service", "Foo-Bar")
                    };

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
