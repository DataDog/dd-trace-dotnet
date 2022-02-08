// <copyright file="ImmutableProductConfiguration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Util;

namespace Datadog.Configuration
{
    internal class ImmutableProductConfiguration : IProductConfiguration
    {
// private protected is needed so these fields could be used in MutableProductConfiguration
#pragma warning disable SA1401 // Fields should be private
        private protected TimeSpan _profilesExport_DefaultInterval;
        private protected long _profilesExport_EarlyTriggerOnCollectedStackSnapshotsBytes;
        private protected long _profilesExport_EarlyTriggerOnCollectedStackSnapshotsCount;

        private protected string _profilesExport_LocalFiles_Directory;

        private protected string _profilesIngestionEndpoint_Url;
        private protected string _profilesIngestionEndpoint_Host;
        private protected int _profilesIngestionEndpoint_Port;
        private protected string _profilesIngestionEndpoint_ApiPath;
        private protected string _profilesIngestionEndpoint_DatadogApiKey;

        private protected string _ddDataTags_Host;
        private protected string _ddDataTags_Service;
        private protected string _ddDataTags_Env;
        private protected string _ddDataTags_Version;
        private protected IEnumerable<KeyValuePair<string, string>> _ddDataTags_CustomTags;

        private protected bool _log_IsDebugEnabled;
        private protected string _log_PreferredLogFileDirectory;

        private protected bool _metrics_Operational_IsEnabled;

        private protected int _metrics_StatsdAgent_Port;

        private protected bool _frameKinds_Native_IsEnabled;
#pragma warning restore SA1401 // Fields should be private

        public ImmutableProductConfiguration(
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
        {
            _profilesExport_DefaultInterval = profilesExport_DefaultInterval;
            _profilesExport_EarlyTriggerOnCollectedStackSnapshotsBytes = profilesExport_EarlyTriggerOnCollectedStackSnapshotsBytes;
            _profilesExport_EarlyTriggerOnCollectedStackSnapshotsCount = profilesExport_EarlyTriggerOnCollectedStackSnapshotsCount;

            _profilesExport_LocalFiles_Directory = profilesExport_LocalFiles_Directory;

            _profilesIngestionEndpoint_Url = profilesIngestionEndpoint_Url;
            _profilesIngestionEndpoint_Host = profilesIngestionEndpoint_Host;
            _profilesIngestionEndpoint_Port = profilesIngestionEndpoint_Port;
            _profilesIngestionEndpoint_ApiPath = profilesIngestionEndpoint_ApiPath;
            _profilesIngestionEndpoint_DatadogApiKey = profilesIngestionEndpoint_DatadogApiKey;

            _ddDataTags_Host = ddDataTags_Host;
            _ddDataTags_Service = ddDataTags_Service;
            _ddDataTags_Env = ddDataTags_Env;
            _ddDataTags_Version = ddDataTags_Version;
            _ddDataTags_CustomTags = ddDataTags_CustomTags;

            _log_IsDebugEnabled = log_IsDebugEnabled;
            _log_PreferredLogFileDirectory = log_PreferredLogFileDirectory;

            _metrics_Operational_IsEnabled = metrics_Operational_IsEnabled;

            _metrics_StatsdAgent_Port = metrics_StatsdAgent_Port;

            _frameKinds_Native_IsEnabled = frameKinds_Native_IsEnabled;
        }

        public ImmutableProductConfiguration(IProductConfiguration otherConfig)
        {
            Validate.NotNull(otherConfig, nameof(otherConfig));

            _profilesExport_DefaultInterval = otherConfig.ProfilesExport_DefaultInterval;
            _profilesExport_EarlyTriggerOnCollectedStackSnapshotsBytes = otherConfig.ProfilesExport_EarlyTriggerOnCollectedStackSnapshotsBytes;
            _profilesExport_EarlyTriggerOnCollectedStackSnapshotsCount = otherConfig.ProfilesExport_EarlyTriggerOnCollectedStackSnapshotsCount;

            _profilesExport_LocalFiles_Directory = otherConfig.ProfilesExport_LocalFiles_Directory;

            _profilesIngestionEndpoint_Url = otherConfig.ProfilesIngestionEndpoint_Url;
            _profilesIngestionEndpoint_Host = otherConfig.ProfilesIngestionEndpoint_Host;
            _profilesIngestionEndpoint_Port = otherConfig.ProfilesIngestionEndpoint_Port;
            _profilesIngestionEndpoint_ApiPath = otherConfig.ProfilesIngestionEndpoint_ApiPath;
            _profilesIngestionEndpoint_DatadogApiKey = otherConfig.ProfilesIngestionEndpoint_DatadogApiKey;

            _ddDataTags_Host = otherConfig.DDDataTags_Host;
            _ddDataTags_Service = otherConfig.DDDataTags_Service;
            _ddDataTags_Env = otherConfig.DDDataTags_Env;
            _ddDataTags_Version = otherConfig.DDDataTags_Version;
            _ddDataTags_CustomTags = otherConfig.DDDataTags_CustomTags;

            _log_IsDebugEnabled = otherConfig.Log_IsDebugEnabled;
            _log_PreferredLogFileDirectory = otherConfig.Log_PreferredLogFileDirectory;

            _metrics_Operational_IsEnabled = otherConfig.Metrics_Operational_IsEnabled;

            _metrics_StatsdAgent_Port = otherConfig.Metrics_StatsdAgent_Port;

            _frameKinds_Native_IsEnabled = otherConfig.FrameKinds_Native_IsEnabled;
        }

        public TimeSpan ProfilesExport_DefaultInterval
        {
            get { return _profilesExport_DefaultInterval; }
        }

        public long ProfilesExport_EarlyTriggerOnCollectedStackSnapshotsBytes
        {
            get { return _profilesExport_EarlyTriggerOnCollectedStackSnapshotsBytes; }
        }

        public long ProfilesExport_EarlyTriggerOnCollectedStackSnapshotsCount
        {
            get { return _profilesExport_EarlyTriggerOnCollectedStackSnapshotsCount; }
        }

        public string ProfilesExport_LocalFiles_Directory
        {
            get { return _profilesExport_LocalFiles_Directory; }
        }

        public string ProfilesIngestionEndpoint_Url
        {
            get { return _profilesIngestionEndpoint_Url; }
        }

        public string ProfilesIngestionEndpoint_Host
        {
            get { return _profilesIngestionEndpoint_Host; }
        }

        public int ProfilesIngestionEndpoint_Port
        {
            get { return _profilesIngestionEndpoint_Port; }
        }

        public string ProfilesIngestionEndpoint_ApiPath
        {
            get { return _profilesIngestionEndpoint_ApiPath; }
        }

        public string ProfilesIngestionEndpoint_DatadogApiKey
        {
            get { return _profilesIngestionEndpoint_DatadogApiKey; }
        }

        public string DDDataTags_Host
        {
            get { return _ddDataTags_Host; }
        }

        public string DDDataTags_Service
        {
            get { return _ddDataTags_Service; }
        }

        public string DDDataTags_Env
        {
            get { return _ddDataTags_Env; }
        }

        public string DDDataTags_Version
        {
            get { return _ddDataTags_Version; }
        }

        public IEnumerable<KeyValuePair<string, string>> DDDataTags_CustomTags
        {
            get { return _ddDataTags_CustomTags; }
        }

        public bool Log_IsDebugEnabled
        {
            get { return _log_IsDebugEnabled; }
        }

        public string Log_PreferredLogFileDirectory
        {
            get { return _log_PreferredLogFileDirectory; }
        }

        public bool Metrics_Operational_IsEnabled
        {
            get { return _metrics_Operational_IsEnabled; }
        }

        public int Metrics_StatsdAgent_Port
        {
            get { return _metrics_StatsdAgent_Port; }
        }

        public bool FrameKinds_Native_IsEnabled
        {
            get { return _frameKinds_Native_IsEnabled; }
        }
    }
}