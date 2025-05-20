// <copyright file="ConfigurationKeys.Rcm.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.RemoteConfigurationManagement;

namespace Datadog.Trace.Configuration
{
    internal static partial class ConfigurationKeys
    {
        internal static class Rcm
        {
            /// <summary>
            /// Is remote configuration management (RCM) enabled. Defaults to <c>true</c>. RCM requires
            /// the use of the full agent, so will not always be available. This switch is primarily
            /// intended for testing and for explicitly disabling RCM even though it is available.
            /// </summary>
            public const string RemoteConfigurationEnabled = "DD_REMOTE_CONFIGURATION_ENABLED";

            /// <summary>
            /// Configuration key for RCM poll interval (in seconds).
            /// Default value is 5 s
            /// Maximum value is 5 s
            /// </summary>
            /// <seealso cref="RemoteConfigurationSettings.PollInterval"/>
            public const string PollInterval = "DD_REMOTE_CONFIG_POLL_INTERVAL_SECONDS";

            [Obsolete("Use PollInterval instead")]
            public const string PollIntervalInternal = "DD_INTERNAL_RCM_POLL_INTERVAL";
        }
    }
}
