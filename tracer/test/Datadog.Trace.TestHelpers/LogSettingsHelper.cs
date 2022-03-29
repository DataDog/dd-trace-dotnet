﻿// <copyright file="LogSettingsHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Logging.DirectSubmission.Formatting;
using Datadog.Trace.Logging.DirectSubmission.Sink.PeriodicBatching;

namespace Datadog.Trace.TestHelpers
{
    internal class LogSettingsHelper
    {
        public static LogFormatter GetFormatter() => new(
            GetValidSettings(),
            serviceName: "MyTestService",
            env: "integration_tests",
            version: "1.0.0");

        public static ImmutableDirectLogSubmissionSettings GetValidSettings()
        {
            return ImmutableDirectLogSubmissionSettings.Create(
                host: "some_host",
                source: "csharp",
                intakeUrl: "https://localhost:1234",
                apiKey: "abcdef",
                minimumLevel: DirectSubmissionLogLevel.Debug,
                globalTags: new Dictionary<string, string>(),
                enabledLogShippingIntegrations: ImmutableDirectLogSubmissionSettings.SupportedIntegrations.Select(x => x.ToString()).ToList(),
                batchingOptions: new BatchingSinkOptions(1000, 100_000, TimeSpan.FromSeconds(2)));
        }
    }
}
