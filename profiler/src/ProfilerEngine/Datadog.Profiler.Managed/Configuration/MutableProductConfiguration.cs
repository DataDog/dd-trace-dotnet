// <copyright file="MutableProductConfiguration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;

namespace Datadog.Configuration
{
    internal sealed class MutableProductConfiguration : ImmutableProductConfiguration, IProductConfiguration
    {
        public MutableProductConfiguration(
            TimeSpan profilesExport_DefaultInterval,
            long profilesExport_EarlyTriggerOnCollectedStackSnapshotsBytes,
            long profilesExport_EarlyTriggerOnCollectedStackSnapshotsCount,
            string profilesExport_LocalFiles_Directory,
            string profilesIngestionEndpoint_Url,
            string profilesIngestionEndpoint_Host,
            int profilesIngestionEndpoint_Port,
            string profilesIngestionEndpoint_ApiPath,
            string profilesIngestionEndpoint_DatadogApiKey,
            string ddDataTags_Host,
            string ddDataTags_Service,
            string ddDataTags_Env,
            string ddDataTags_Version,
            IEnumerable<KeyValuePair<string, string>> ddDataTags_CustomTags,
            bool log_IsDebugEnabled,
            string log_PreferredLogFileDirectory,
            bool metrics_Operational_IsEnabled,
            int metrics_StatsdAgent_Port,
            bool frameKinds_Native_IsEnabled)
            : base(
                  profilesExport_DefaultInterval,
                  profilesExport_EarlyTriggerOnCollectedStackSnapshotsBytes,
                  profilesExport_EarlyTriggerOnCollectedStackSnapshotsCount,
                  profilesExport_LocalFiles_Directory,
                  profilesIngestionEndpoint_Url,
                  profilesIngestionEndpoint_Host,
                  profilesIngestionEndpoint_Port,
                  profilesIngestionEndpoint_ApiPath,
                  profilesIngestionEndpoint_DatadogApiKey,
                  ddDataTags_Host,
                  ddDataTags_Service,
                  ddDataTags_Env,
                  ddDataTags_Version,
                  ddDataTags_CustomTags,
                  log_IsDebugEnabled,
                  log_PreferredLogFileDirectory,
                  metrics_Operational_IsEnabled,
                  metrics_StatsdAgent_Port,
                  frameKinds_Native_IsEnabled)
        {
        }

        public MutableProductConfiguration(IProductConfiguration otherConfig)
            : base(otherConfig)
        {
        }

        public new TimeSpan ProfilesExport_DefaultInterval
        {
            get { return base.ProfilesExport_DefaultInterval; }
            set { _profilesExport_DefaultInterval = value; }
        }

        public new long ProfilesExport_EarlyTriggerOnCollectedStackSnapshotsBytes
        {
            get { return base.ProfilesExport_EarlyTriggerOnCollectedStackSnapshotsBytes; }
            set { _profilesExport_EarlyTriggerOnCollectedStackSnapshotsBytes = value; }
        }

        public new long ProfilesExport_EarlyTriggerOnCollectedStackSnapshotsCount
        {
            get { return base.ProfilesExport_EarlyTriggerOnCollectedStackSnapshotsCount; }
            set { _profilesExport_EarlyTriggerOnCollectedStackSnapshotsCount = value; }
        }

        public new string ProfilesExport_LocalFiles_Directory
        {
            get { return base.ProfilesExport_LocalFiles_Directory; }
            set { _profilesExport_LocalFiles_Directory = value; }
        }

        public new string ProfilesIngestionEndpoint_Url
        {
            get { return base.ProfilesIngestionEndpoint_Url; }
            set { _profilesIngestionEndpoint_Url = value; }
        }

        public new string ProfilesIngestionEndpoint_Host
        {
            get { return base.ProfilesIngestionEndpoint_Host; }
            set { _profilesIngestionEndpoint_Host = value; }
        }

        public new int ProfilesIngestionEndpoint_Port
        {
            get { return base.ProfilesIngestionEndpoint_Port; }
            set { _profilesIngestionEndpoint_Port = value; }
        }

        public new string ProfilesIngestionEndpoint_ApiPath
        {
            get { return base.ProfilesIngestionEndpoint_ApiPath; }
            set { _profilesIngestionEndpoint_ApiPath = value; }
        }

        public new string ProfilesIngestionEndpoint_DatadogApiKey
        {
            get { return base.ProfilesIngestionEndpoint_DatadogApiKey; }
            set { _profilesIngestionEndpoint_DatadogApiKey = value; }
        }

        public new string DDDataTags_Host
        {
            get { return base.DDDataTags_Host; }
            set { _ddDataTags_Host = value; }
        }

        public new string DDDataTags_Service
        {
            get { return base.DDDataTags_Service; }
            set { _ddDataTags_Service = value; }
        }

        public new string DDDataTags_Env
        {
            get { return base.DDDataTags_Env; }
            set { _ddDataTags_Env = value; }
        }

        public new string DDDataTags_Version
        {
            get { return base.DDDataTags_Version; }
            set { _ddDataTags_Version = value; }
        }

        public new IEnumerable<KeyValuePair<string, string>> DDDataTags_CustomTags
        {
            get { return base.DDDataTags_CustomTags; }
            set { _ddDataTags_CustomTags = value; }
        }

        public new bool Log_IsDebugEnabled
        {
            get { return base.Log_IsDebugEnabled; }
            set { _log_IsDebugEnabled = value; }
        }

        public new string Log_PreferredLogFileDirectory
        {
            get { return base.Log_PreferredLogFileDirectory; }
            set { _log_PreferredLogFileDirectory = value; }
        }

        public new bool Metrics_Operational_IsEnabled
        {
            get { return base.Metrics_Operational_IsEnabled; }
            set { _metrics_Operational_IsEnabled = value; }
        }

        public new int Metrics_StatsdAgent_Port
        {
            get { return base.Metrics_StatsdAgent_Port; }
            set { _metrics_StatsdAgent_Port = value; }
        }

        public new bool FrameKinds_Native_IsEnabled
        {
            get { return base.FrameKinds_Native_IsEnabled; }
            set { _frameKinds_Native_IsEnabled = value; }
        }
    }
}
