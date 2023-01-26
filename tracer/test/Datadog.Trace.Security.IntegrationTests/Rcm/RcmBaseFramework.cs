// <copyright file="RcmBaseFramework.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.Rcm;

public class RcmBaseFramework : AspNetBase, IClassFixture<AspNetCoreTestFixture>
{
    protected const string LogFileNamePrefix = "dotnet-tracer-managed-";

    public RcmBaseFramework(string sampleName, ITestOutputHelper outputHelper, string shutdownPath, string samplesDir = null, string testName = null)
        : base(sampleName, outputHelper,  shutdownPath, samplesDir, testName)
    {
        SetEnvironmentVariable(ConfigurationKeys.Rcm.PollInterval, "500");

        // the directory would be created anyway, but in certain case a delay can lead to an exception from the LogEntryWatcher
        Directory.CreateDirectory(LogDirectory);
        SetEnvironmentVariable(ConfigurationKeys.LogDirectory, LogDirectory);
    }

    protected TimeSpan LogEntryWatcherTimeout => TimeSpan.FromSeconds(20);

    protected string LogDirectory => Path.Combine(DatadogLoggingFactory.GetLogDirectory(), $"{GetType().Name}Logs");

    protected string RulesUpdatedMessage(int procId) => $"rules have been updated and waf status is \"DDWAF_OK\"  {{ MachineName: \".\", Process: \"[{procId}";
}
