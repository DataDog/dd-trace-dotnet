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

    protected RcmBase(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper, bool? enableSecurity, string testName)
        : base("AspNetCore5", outputHelper, "/shutdown", testName: testName)
    {
        Fixture = fixture;
        EnableSecurity = enableSecurity;
        SetEnvironmentVariable(ConfigurationKeys.Rcm.PollInterval, "500");

        // the directory would be created anyway, but in certain case a delay can lead to an exception from the LogEntryWatcher
        Directory.CreateDirectory(LogDirectory);
        SetEnvironmentVariable(ConfigurationKeys.LogDirectory, LogDirectory);
    }

    protected AspNetCoreTestFixture Fixture { get; }

    protected bool? EnableSecurity { get; }

    protected string LogDirectory => Path.Combine(DatadogLoggingFactory.GetLogDirectory(), $"{GetType().Name}Logs");

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
}
