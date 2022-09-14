// <copyright file="ConfigurationKeys.Rcm.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.RemoteConfigurationManagement;

namespace Datadog.Trace.Configuration
{
    internal static partial class ConfigurationKeys
    {
        internal static class Rcm
        {
            /// <summary>
            /// Configuration key for RCM poll interval (in milliseconds).
            /// Default value is 5000 ms
            /// Maximum value is 5000 ms
            /// </summary>
            /// <seealso cref="RemoteConfigurationSettings.PollInterval"/>
            public const string PollInterval = "DD_INTERNAL_RCM_POLL_INTERVAL";
        }
    }
}
