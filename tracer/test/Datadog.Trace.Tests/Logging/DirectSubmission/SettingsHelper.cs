// <copyright file="SettingsHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging.DirectSubmission;

namespace Datadog.Trace.Tests.Logging.DirectSubmission
{
    internal class SettingsHelper
    {
        public static DirectLogSubmissionSettings GetValidSettings()
        {
            return DirectLogSubmissionSettings.Create(
                host: "some_host",
                source: "csharp",
                transport: "HTTP",
                intakeUrl: "https://localhost:1234",
                apiKey: "abcdef",
                serviceName: "MyTestService",
                minimumLevel: DirectSubmissionLogLevel.Debug,
                globalTags: new Dictionary<string, string>(),
                enabledLogShippingIntegrations: DirectLogSubmissionSettings.SupportedIntegrations.Select(x => x.ToString()).ToList(),
                isLogsInjectionEnabled: true,
                new ImmutableIntegrationSettingsCollection(
                    new IntegrationSettingsCollection(null),
                    new HashSet<string>()));
        }
    }
}
