// <copyright file="SettingsHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging.DirectSubmission;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.Logging
{
    internal class SettingsHelper
    {
        public static DirectLogSubmissionSettings GetValidSettings()
        {
            return DirectLogSubmissionSettings.Create(
                hostname: "some_host",
                source: "csharp",
                transport: "HTTP",
                intakeUrl: "https://localhost:1234",
                apiKey: "abcdef",
                serviceName: "MyTestService",
                minimumLevel: DirectSubmissionLogLevel.Debug,
                globalTags: new Dictionary<string, string>(),
                enabledLogShippingIntegrations: new List<string> { nameof(IntegrationIds.Serilog), nameof(IntegrationIds.ILogger) },
                isLogsInjectionEnabled: true,
                isIntegrationEnabledCallback: x => true);
        }
    }
}
