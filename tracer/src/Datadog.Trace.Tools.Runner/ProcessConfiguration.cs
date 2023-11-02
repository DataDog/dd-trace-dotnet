// <copyright file="ProcessConfiguration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Tools.Shared;

namespace Datadog.Trace.Tools.Runner
{
    internal static class ProcessConfiguration
    {
        internal static string? GetProcessLogDirectory(int pid)
        {
            ProcessInfo processInfo;

            try
            {
                processInfo = ProcessInfo.GetProcessInfo(pid);
            }
            catch (Exception ex)
            {
                Utils.WriteError("Error while trying to fetch process information: " + ex.Message);
                return null;
            }

            IReadOnlyDictionary<string, string>? applicationConfig;

            try
            {
                applicationConfig = processInfo.LoadApplicationConfig();
            }
            catch (Exception ex)
            {
                Utils.WriteError("Error while trying to load application configuration: " + ex.Message);
                return null;
            }

            IConfigurationSource applicationConfigurationSource = NullConfigurationSource.Instance;

            if (applicationConfig != null)
            {
                applicationConfigurationSource = new DictionaryConfigurationSource(applicationConfig);
            }

            var config = new ConfigurationBuilder(applicationConfigurationSource, NullConfigurationTelemetry.Instance);

            var logDirectory = config.WithKeys(ConfigurationKeys.LogDirectory).AsString();

            if (logDirectory == null)
            {
#pragma warning disable 618 // ProfilerLogPath is deprecated but still supported
                var nativeLogFile = config.WithKeys(ConfigurationKeys.ProfilerLogPath).AsString();
#pragma warning restore 618
                if (!string.IsNullOrEmpty(nativeLogFile))
                {
                    logDirectory = Path.GetDirectoryName(nativeLogFile);
                }
            }

            return logDirectory;
        }
    }
}
