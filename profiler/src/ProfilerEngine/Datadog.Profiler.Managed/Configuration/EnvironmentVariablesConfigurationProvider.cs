// <copyright file="EnvironmentVariablesConfigurationProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Profiler;

namespace Datadog.Configuration
{
    /// <summary>
    /// The Tracer public docs to the environment variables read here are at:
    /// https://docs.datadoghq.com/tracing/setup_overview/setup/dotnet-framework/?tab=environmentvariables#configuration-settings
    /// (as of Mar 2021)
    ///
    /// This Provider is meant to be as a fluent API in a chain after <c>DefaultReleaseProductConfigurationProvider</c> (or <c>DevProductConfigurationProvider</c>).
    /// It modifies only the config value for which an environment variable has been set, so prior defaults need to be set in a fluent chain.
    /// </summary>
    internal static class EnvironmentVariablesConfigurationProvider
    {
        private const string LogComponentMoniker = nameof(EnvironmentVariablesConfigurationProvider);

        private static readonly char[] DDTagsListSeparator = new char[] { ',' };

        public static IProductConfiguration ApplyEnvironmentVariables(this IProductConfiguration config)
        {
            if (config == null)
            {
                return null;
            }

            var mutableConfig = new MutableProductConfiguration(config);

            if (TryGetEnvironmentVariable("DD_PROFILING_UPLOAD_PERIOD", out string envProfilesExportDefaultInterval))
            {
                if (int.TryParse(envProfilesExportDefaultInterval, out var profilesExportDefaultInterval))
                {
                    mutableConfig.ProfilesExport_DefaultInterval = TimeSpan.FromSeconds(profilesExportDefaultInterval);
                }
                else
                {
                    Log.Error(
                        Log.WithCallInfo(LogComponentMoniker),
                        "The environment variable \"DD_PROFILING_UPLOAD_PERIOD\" is specified (\"",
                        envProfilesExportDefaultInterval,
                        "\") but cannot be parsed as an int. Using original value: ",
                        mutableConfig.ProfilesExport_DefaultInterval);
                }
            }

            if (TryGetEnvironmentVariable("DD_PROFILING_OUTPUT_DIR", out string directory))
            {
                mutableConfig.ProfilesExport_LocalFiles_Directory = directory;
            }

            // If Ingestion Endpoint Url is specified, the Host, Port and ApiPath are ignored.
            // However, that logic is inside the export loop that actually interprets those values.
            // At this point we just extract all of the info that is contained in the environment variables.
            // If both, URL and Host-Port-Etc are specified, we will extract them all and leave the
            // priorization logic of what to use to the config consumer.
            if (TryGetEnvironmentVariable("DD_TRACE_AGENT_URL", out string ddTraceAgentUrl))
            {
                mutableConfig.ProfilesIngestionEndpoint_Url = ddTraceAgentUrl;
            }

            if (TryGetEnvironmentVariable("DD_AGENT_HOST", out string ddTraceAgentHost))
            {
                mutableConfig.ProfilesIngestionEndpoint_Host = ddTraceAgentHost;
            }

            if (TryGetEnvironmentVariable("DD_TRACE_AGENT_PORT", out string szTraceAgentPort))
            {
                if (int.TryParse(szTraceAgentPort, out int port))
                {
                    mutableConfig.ProfilesIngestionEndpoint_Port = port;
                }
                else
                {
                    Log.Error(
                        Log.WithCallInfo(LogComponentMoniker),
                        "The environment variable \"DD_TRACE_AGENT_PORT\" is specified (",
                        szTraceAgentPort,
                        ") but cannot be parsed as a number and will be ignored.");
                }
            }

            // Api Key is not required for agent-based ingestion scnarios; it IS required for agent-less ingestion.
            if (TryGetEnvironmentVariable("DD_API_KEY", out string ddApiKey))
            {
                mutableConfig.ProfilesIngestionEndpoint_DatadogApiKey = ddApiKey;
            }

            if (TryGetEnvironmentVariable("DD_HOSTNAME", out string ddHostName))
            {
                mutableConfig.DDDataTags_Host = ddHostName;
            }

            if (TryGetEnvironmentVariable("DD_SERVICE", out string ddServiceName))
            {
                mutableConfig.DDDataTags_Service = ddServiceName;
            }

            if (TryGetEnvironmentVariable("DD_ENV", out string ddEnvironment))
            {
                mutableConfig.DDDataTags_Env = ddEnvironment;
            }

            if (TryGetEnvironmentVariable("DD_VERSION", out string ddServiceVersion))
            {
                mutableConfig.DDDataTags_Version = ddServiceVersion;
            }

            if (TryGetEnvironmentVariable("DD_TAGS", out string ddTagsStr))
            {
                mutableConfig.DDDataTags_CustomTags = ParseAndMergeDdTags(mutableConfig.DDDataTags_CustomTags, ddTagsStr);
            }

            if (TryGetEnvironmentVariable("DD_TRACE_DEBUG", out string envIsTraceDebugEnabled))
            {
                const bool isTraceDebugEnabled = true;
                if (
                    ConfigurationProviderUtils.TryParseBooleanSettingStr(
                        envIsTraceDebugEnabled,
                        isTraceDebugEnabled,
                        out bool ddIsTraceDebugEnabledVal))
                {
                    mutableConfig.Log_IsDebugEnabled = ddIsTraceDebugEnabledVal;
                }
                else
                {
                    Log.Error(
                        Log.WithCallInfo(LogComponentMoniker),
                        "The environment variable \"DD_TRACE_DEBUG\" is specified (",
                        envIsTraceDebugEnabled,
                        $") but cannot be parsed as a boolean. Using {isTraceDebugEnabled.ToString()} as default");

                    mutableConfig.Log_IsDebugEnabled = isTraceDebugEnabled;
                }
            }

            if (TryGetEnvironmentVariable("DD_PROFILING_LOG_DIR", out string ddTraceLogDirectory))
            {
                mutableConfig.Log_PreferredLogFileDirectory = ddTraceLogDirectory;
            }

            if (TryGetEnvironmentVariable("DD_INTERNAL_OPERATIONAL_METRICS_ENABLED", out string envIsEnabled))
            {
                const bool isOperationalMetricsEnabled = false;
                if (ConfigurationProviderUtils.TryParseBooleanSettingStr(envIsEnabled, isOperationalMetricsEnabled, out bool isEnabled))
                {
                    mutableConfig.Metrics_Operational_IsEnabled = isEnabled;
                }
                else
                {
                    Log.Error(
                        Log.WithCallInfo(LogComponentMoniker),
                        "The environment variable \"DD_INTERNAL_OPERATIONAL_METRICS_ENABLED\" is specified (",
                        envIsEnabled,
                        $") but cannot be parsed as a boolean. Using {isOperationalMetricsEnabled.ToString()} as default");

                    mutableConfig.Log_IsDebugEnabled = isOperationalMetricsEnabled;
                }
            }

            if (TryGetEnvironmentVariable("DD_PROFILING_FRAMES_NATIVE_ENABLED", out string envFramesNativeIsEnabled))
            {
                const bool isFramesNativeEnabled = false;
                if (ConfigurationProviderUtils.TryParseBooleanSettingStr(envFramesNativeIsEnabled, isFramesNativeEnabled, out bool isEnabled))
                {
                    mutableConfig.FrameKinds_Native_IsEnabled = isEnabled;
                }
                else
                {
                    Log.Error(
                        Log.WithCallInfo(LogComponentMoniker),
                        "The environment variable \"DD_PROFILING_FRAMES_NATIVE_ENABLED\" is specified (",
                        envFramesNativeIsEnabled,
                        $") but cannot be parsed as a boolean. Using isFramesNativeEnabled.ToString() as default");

                    mutableConfig.FrameKinds_Native_IsEnabled = isFramesNativeEnabled;
                }
            }

            return mutableConfig.CreateImmutableSnapshot();
        }

        private static IEnumerable<KeyValuePair<string, string>> ParseAndMergeDdTags(IEnumerable<KeyValuePair<string, string>> existingTags, string ddTagsStr)
        {
            // This method will not be called if ddTagsStr is null.
            // This means that "DD_TAGS" was specified with some value, so the resulting merged tag set should never be null.
            string[] parsedNewTags = ddTagsStr.Split(DDTagsListSeparator, StringSplitOptions.RemoveEmptyEntries);
            if (parsedNewTags == null && parsedNewTags.Length == 0)
            {
                return existingTags ?? new KeyValuePair<string, string>[0];
            }

            // There is no rule that forbids specifying the same tag key more than once.
            // Also, we do not expect many tags. Typically < 100. So we keep them in a list, dit a table and use linear lookups.
            var splitNewTags = new List<KeyValuePair<string, string>>();
            for (int i = 0; i < parsedNewTags.Length; i++)
            {
                string tag = parsedNewTags[i]?.Trim();
                if (string.IsNullOrWhiteSpace(tag))
                {
                    continue;
                }

                int splitPos = tag.IndexOf(':');
                if (splitPos < 0)
                {
                    // "X" => ("X", "Unspecified")
                    splitNewTags.Add(new KeyValuePair<string, string>(tag, null));
                }
                else if (splitPos == 0 && splitPos == tag.Length - 1)
                {
                    // ":" => ("", "")
                    splitNewTags.Add(new KeyValuePair<string, string>(string.Empty, string.Empty));
                }
                else if (splitPos == 0)
                {
                    // ":Y" => ("", "Y")
                    splitNewTags.Add(new KeyValuePair<string, string>(string.Empty, tag.Substring(splitPos + 1).Trim()));
                }
                else if (splitPos == tag.Length - 1)
                {
                    // "X:" => ("X", "")
                    splitNewTags.Add(new KeyValuePair<string, string>(tag.Substring(0, splitPos).Trim(), string.Empty));
                }
                else
                {
                    // "X:Y" => ("X", "Y")
                    splitNewTags.Add(new KeyValuePair<string, string>(tag.Substring(0, splitPos).Trim(), tag.Substring(splitPos + 1).Trim()));
                }
            }

            if (splitNewTags.Count == 0)
            {
                return existingTags ?? new KeyValuePair<string, string>[0];
            }

            if (existingTags == null)
            {
                return splitNewTags;
            }

            // We first include all existing tags EXCEPT if they are included in the new set that came in via the DD_TAGS env var.
            // Existing tags that are also in the new set are considered replaced.
            // We then include all the new tags.
            // Note that a tag key does not need to be unique.
            // See also the above comment about the linear searches in the list.
            var combinedTags = new List<KeyValuePair<string, string>>();
            foreach (KeyValuePair<string, string> existingTag in existingTags)
            {
                if (!IsDdTagExists(existingTag.Key, splitNewTags))
                {
                    combinedTags.Add(existingTag);
                }
            }

            for (int i = 0; i < splitNewTags.Count; i++)
            {
                combinedTags.Add(splitNewTags[i]);
            }

            return combinedTags;
        }

        private static bool IsDdTagExists(string searchTagKey, List<KeyValuePair<string, string>> tagsList)
        {
            if (tagsList == null)
            {
                return false;
            }

            for (int i = 0; i < tagsList.Count; i++)
            {
                string currTagKey = tagsList[i].Key;
                if (searchTagKey == currTagKey || (searchTagKey != null && currTagKey != null && searchTagKey.Equals(currTagKey, StringComparison.Ordinal)))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetEnvironmentVariable(string variableName, out string variableValue)
        {
            try
            {
                variableValue = Environment.GetEnvironmentVariable(variableName);
                return (variableValue != null);
            }
            catch
            {
                variableValue = null;
                return false;
            }
        }
    }
}
