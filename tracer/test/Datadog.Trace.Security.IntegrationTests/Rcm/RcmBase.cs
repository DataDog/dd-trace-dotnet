// <copyright file="RcmBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Configuration;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.Rcm;

public class RcmBase : AspNetBase
{
    protected const string LogFileNamePrefix = "dotnet-tracer-managed-";

#pragma warning disable SA1401
    protected readonly TimeSpan logEntryWatcherTimeout = TimeSpan.FromSeconds(20);
#pragma warning restore SA1401

    public RcmBase(ITestOutputHelper outputHelper, string testName)
        : base("AspNetCore5", outputHelper, "/shutdown", testName: testName)
    {
        SetEnvironmentVariable(ConfigurationKeys.Rcm.PollInterval, "500");
    }

    protected string AppSecDisabledMessage() => $"AppSec is now Disabled, _settings.Enabled is false, coming from remote config: true  {{ MachineName: \".\", Process: \"[{SampleProcessId}";

    protected string AppSecEnabledMessage() => $"AppSec is now Enabled, _settings.Enabled is true, coming from remote config: true  {{ MachineName: \".\", Process: \"[{SampleProcessId}";

    protected string RulesUpdatedMessage() => $"rules have been updated and waf status is \"DDWAF_OK\"  {{ MachineName: \".\", Process: \"[{SampleProcessId}";
}
