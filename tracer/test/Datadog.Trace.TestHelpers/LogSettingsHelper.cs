// <copyright file="LogSettingsHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Specialized;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Logging.DirectSubmission.Formatting;

namespace Datadog.Trace.TestHelpers
{
    internal class LogSettingsHelper
    {
        public static LogFormatter GetFormatter() => new(
            new ImmutableTracerSettings(new TracerSettings(null, Configuration.Telemetry.NullConfigurationTelemetry.Instance, new OverrideErrorLog())),
            GetValidSettings(),
            aasSettings: null,
            serviceName: "MyTestService",
            env: "integration_tests",
            version: "1.0.0",
            gitMetadataTagsProvider: new NullGitMetadataProvider());

        public static ImmutableDirectLogSubmissionSettings GetValidSettings()
        {
            var tracerSettings = TracerSettings.Create(new()
            {
                { ConfigurationKeys.ApiKey, "abcdef" },
                { ConfigurationKeys.DirectLogSubmission.Host, "some_host" },
                { ConfigurationKeys.DirectLogSubmission.Source, "csharp" },
                { ConfigurationKeys.DirectLogSubmission.Url, "https://localhost:1234" },
                { ConfigurationKeys.DirectLogSubmission.MinimumLevel, "debug" },
                { ConfigurationKeys.DirectLogSubmission.EnabledIntegrations, string.Join(";", ImmutableDirectLogSubmissionSettings.SupportedIntegrations) },
                { ConfigurationKeys.DirectLogSubmission.BatchSizeLimit, "1000" },
                { ConfigurationKeys.DirectLogSubmission.BatchPeriodSeconds, "2" },
                { ConfigurationKeys.DirectLogSubmission.QueueSizeLimit, "100000" }
            });

            return ImmutableDirectLogSubmissionSettings.Create(tracerSettings);
        }
    }
}
