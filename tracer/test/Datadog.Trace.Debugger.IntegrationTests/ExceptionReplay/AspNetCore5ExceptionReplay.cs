// <copyright file="AspNetCore5ExceptionReplay.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Debugger.IntegrationTests.Helpers;
using Datadog.Trace.Iast.Telemetry;
using Datadog.Trace.TestHelpers;
using VerifyTests;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Debugger.IntegrationTests.ExceptionReplay;

public class AspNetCore5ExceptionReplayEnabledDynamicInstrumentationEnabled : AspNetCore5Rasp
{
    public AspNetCore5ExceptionReplayEnabledDynamicInstrumentationEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
    : base(fixture, outputHelper, enableDynamicInstrumentation: true, captureFullCallStack: false)
    {
    }
}

public class AspNetCore5ExceptionReplayEnabledDynamicInstrumentationEnabled_FullCallStack : AspNetCore5Rasp
{
    public AspNetCore5ExceptionReplayEnabledDynamicInstrumentationEnabled_FullCallStack(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper, enableDynamicInstrumentation: true, captureFullCallStack: true)
    {
    }
}

public class AspNetCore5ExceptionReplayEnabledDynamicInstrumentationDisabled : AspNetCore5Rasp
{
    public AspNetCore5ExceptionReplayEnabledDynamicInstrumentationDisabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
    : base(fixture, outputHelper, enableDynamicInstrumentation: false, captureFullCallStack: false)
    {
    }
}

public class AspNetCore5ExceptionReplayEnabledDynamicInstrumentationDisabled_FullCallStack : AspNetCore5Rasp
{
    public AspNetCore5ExceptionReplayEnabledDynamicInstrumentationDisabled_FullCallStack(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper, enableDynamicInstrumentation: false, captureFullCallStack: true)
    {
    }
}

public abstract class AspNetCore5Rasp : AspNetBase, IClassFixture<AspNetCoreTestFixture>
{
    // This class is used to test Exception Replay with Dynamic Instrumentation enabled or disabled.
    public AspNetCore5Rasp(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper, bool enableDynamicInstrumentation, bool captureFullCallStack)
        : base("AspNetCore5", outputHelper, "/shutdown", testName: "AspNetCore5.SecurityEnabled")
    {
        SetEnvironmentVariable(ConfigurationKeys.Debugger.ExceptionDebuggingEnabled, "true");
        SetEnvironmentVariable(ConfigurationKeys.Debugger.Enabled, enableDynamicInstrumentation.ToString().ToLower());
        SetEnvironmentVariable(ConfigurationKeys.Debugger.ExceptionDebuggingCaptureFullCallStackEnabled, captureFullCallStack.ToString().ToLower());
        SetEnvironmentVariable(ConfigurationKeys.Debugger.ExceptionDebuggingEnabled, "true");
        SetEnvironmentVariable(ConfigurationKeys.Rcm.PollInterval, "100");
        SetEnvironmentVariable(ConfigurationKeys.Debugger.MaxDepthToSerialize, "3");
        SetEnvironmentVariable(ConfigurationKeys.Debugger.DiagnosticsInterval, "1");
        SetEnvironmentVariable(ConfigurationKeys.Debugger.MaxTimeToSerialize, "1000");
        DynamicInstrumentationEnabled = enableDynamicInstrumentation;
        CaptureFullCallStackEnabled = captureFullCallStack;
        Fixture = fixture;
        Fixture.SetOutput(outputHelper);
    }

    protected bool DynamicInstrumentationEnabled { get; }

    protected bool CaptureFullCallStackEnabled { get; }

    protected AspNetCoreTestFixture Fixture { get; }

    public override void Dispose()
    {
        base.Dispose();
        Fixture.SetOutput(null);
    }

    public async Task TryStartApp()
    {
        await Fixture.TryStartApp(this);
        SetHttpPort(Fixture.HttpPort);
    }

    [SkippableTheory]
    [InlineData("/Recursive/10", 4, 32)]
    [Trait("RunOnWindows", "True")]
    public async Task TestExceptionReplay(string url, int expectedNumberOfSnapshotsDefault, int expectedNumberOfSnaphotsFull)
    {
        var expectedNumberOfSnapshots = CaptureFullCallStackEnabled ? expectedNumberOfSnaphotsFull : expectedNumberOfSnapshotsDefault;

        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;

        MockSpan erroredSpan = null;

        for (int i = 0; i < 2; i++)
        {
            var spans = await SendRequestsAsync(agent, [url]);
            erroredSpan = spans.Single(x => x.Tags.ContainsKey("error.stack"));
            Assert.True(erroredSpan.Tags.ContainsKey("_dd.di.er"));
        }

        Assert.NotNull(erroredSpan);

        var actualSnapshotsNum = erroredSpan.Tags.Keys.Where(key => key.EndsWith(".snapshot_id"));

        Assert.Equal(expectedNumberOfSnapshots, actualSnapshotsNum.Count());

        // Approve snapshots
        var snapshots = await agent.WaitForSnapshots(expectedNumberOfSnapshots);

        Assert.Equal(expectedNumberOfSnapshots, snapshots?.Length);

        var testName = "ExceptionReplay" + (CaptureFullCallStackEnabled ? ".FullCallStack" : string.Empty) + ".AspNetCore5";
        await Approver.ApproveSnapshots(snapshots, testName, Output, orderPostScrubbing: true);
        agent.ClearSnapshots();
    }
}
#endif
