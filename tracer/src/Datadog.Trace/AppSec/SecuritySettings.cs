// <copyright file="SecuritySettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.AppSec
{
    internal class SecuritySettings
    {
        public SecuritySettings(IConfigurationSource source)
        {
            // both should default to false
            Enabled = source?.GetBool(ConfigurationKeys.AppSecEnabled) ?? false;
            BlockingEnabled = source?.GetBool(ConfigurationKeys.AppSecBlockingEnabled) ?? false;
            Rules = source?.GetString(ConfigurationKeys.AppSecRules);
            CustomIpHeader = source?.GetString(ConfigurationKeys.AppSecCustomIpHeader);
            var extraHeaders = source?.GetString(ConfigurationKeys.AppSecExtraHeaders);
            ExtraHeaders = !string.IsNullOrEmpty(extraHeaders) ? extraHeaders.Split(',') : Array.Empty<string>();
            KeepTraces = source?.GetBool(ConfigurationKeys.AppSecKeepTraces) ?? true;
            var traceRateLimit = (source?.GetInt32(ConfigurationKeys.AppSecTraceRateLimit)).GetValueOrDefault(0);
            TraceRateLimit = traceRateLimit > 0 ? traceRateLimit : 100;
        }

        public bool Enabled { get; set; }

        public string CustomIpHeader { get; }

        /// <summary>
        /// Gets keys indicating the optional custom appsec headers the user wants to send.
        /// </summary>
        public IReadOnlyList<string> ExtraHeaders { get; }

        public bool BlockingEnabled { get; }

        public string Rules { get; }

        public bool KeepTraces { get; }

        /// <summary>
        /// Gets the limit of AppSec traces sent per second with an integer value, strictly positive.
        /// </summary>
        public int TraceRateLimit { get; }

        public static SecuritySettings FromDefaultSources()
        {
            var source = GlobalSettings.CreateDefaultConfigurationSource();
            return new SecuritySettings(source);
        }
    }
}
