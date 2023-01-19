// <copyright file="RcmBase.cs" company="Datadog">
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

public class RcmBase : AspNetBase, IClassFixture<AspNetCoreTestFixture>
{
    protected const string LogFileNamePrefix = "dotnet-tracer-managed-";

    public RcmBase(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper, bool? enableSecurity, string testName)
        : base("AspNetCore5", outputHelper, "/shutdown", testName: testName)
    {
        Fixture = fixture;
        EnableSecurity = enableSecurity;
        SetEnvironmentVariable(ConfigurationKeys.Rcm.PollInterval, "500");
        SetEnvironmentVariable(ConfigurationKeys.LogDirectory, LogDirectory);
    }

    protected AspNetCoreTestFixture Fixture { get; }

    protected bool? EnableSecurity { get; }

    protected TimeSpan LogEntryWatcherTimeout => TimeSpan.FromSeconds(20);

    protected string LogDirectory => Path.Combine(DatadogLoggingFactory.GetLogDirectory(), $"{GetTestName()}Logs");

    public override void Dispose()
    {
        base.Dispose();
        Fixture.SetOutput(null);
    }

    public async Task TryStartApp()
    {
        await Fixture.TryStartApp(this, EnableSecurity);
        SetHttpPort(Fixture.HttpPort);
    }

    internal static void CheckAckState(GetRcmRequest request, string product, uint expectedState, string expectedError, string message)
    {
        var state = request?.Client?.State?.ConfigStates?.SingleOrDefault(x => x.Product == product);

        state.Should().NotBeNull();
        state.ApplyState.Should().Be(expectedState, message);
        state.ApplyError.Should().Be(expectedError, message);
    }

    internal static void CheckCapabilities(GetRcmRequest request, uint expectedState, string message)
    {
#if !NETCOREAPP
        var capabilities = new BigInteger(request?.Client?.Capabilities);
#else
        var capabilities = new BigInteger(request?.Client?.Capabilities, true, true);
#endif
        capabilities.Should().Be(expectedState, message);
    }

    protected string AppSecDisabledMessage() => $"AppSec is now Disabled, _settings.Enabled is false, coming from remote config: true  {{ MachineName: \".\", Process: \"[{Fixture.Process.Id}";

    protected string AppSecEnabledMessage() => $"AppSec is now Enabled, _settings.Enabled is true, coming from remote config: true  {{ MachineName: \".\", Process: \"[{Fixture.Process.Id}";

    protected string RulesUpdatedMessage() => $"rules have been updated and waf status is \"DDWAF_OK\"  {{ MachineName: \".\", Process: \"[{Fixture.Process.Id}";

    protected string WafUpdateRule() => $"DDAS-0015-00: AppSec loaded 1 rules from file RemoteConfig.  {{ MachineName: \".\", Process: \"[{Fixture.Process.Id}";
}
