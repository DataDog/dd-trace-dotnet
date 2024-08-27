// <copyright file="AspNetCore5ExceptionReplay.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Debugger.IntegrationTests.Helpers;
using Datadog.Trace.Iast.Telemetry;
using Datadog.Trace.TestHelpers;
using Samples.Probes.TestRuns;
using VerifyTests;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Debugger.IntegrationTests.ExceptionReplay;

public class AspNetCore5ExceptionReplayEnabledDynamicInstrumentationEnabled : AspNetCore5ExceptionReplay
{
    public AspNetCore5ExceptionReplayEnabledDynamicInstrumentationEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
    : base(fixture, outputHelper, enableDynamicInstrumentation: true, captureFullCallStack: false)
    {
    }
}

public class AspNetCore5ExceptionReplayEnabledDynamicInstrumentationEnabled_FullCallStack : AspNetCore5ExceptionReplay
{
    public AspNetCore5ExceptionReplayEnabledDynamicInstrumentationEnabled_FullCallStack(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper, enableDynamicInstrumentation: true, captureFullCallStack: true)
    {
    }
}

public class AspNetCore5ExceptionReplayEnabledDynamicInstrumentationDisabled : AspNetCore5ExceptionReplay
{
    public AspNetCore5ExceptionReplayEnabledDynamicInstrumentationDisabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
    : base(fixture, outputHelper, enableDynamicInstrumentation: false, captureFullCallStack: false)
    {
    }
}

public class AspNetCore5ExceptionReplayEnabledDynamicInstrumentationDisabled_FullCallStack : AspNetCore5ExceptionReplay
{
    public AspNetCore5ExceptionReplayEnabledDynamicInstrumentationDisabled_FullCallStack(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper, enableDynamicInstrumentation: false, captureFullCallStack: true)
    {
    }
}

public abstract class AspNetCore5ExceptionReplay : AspNetBase, IClassFixture<AspNetCoreTestFixture>
{
    private static readonly string[] KnownPropertiesToReplace = { "duration", "timestamp", "dd.span_id", "dd.trace_id", "id", "Id", "lineNumber", "thread_name", "thread_id", "<>t__builder", "s_taskIdCounter", "<>u__1", "stack", "m_task" };
    private static readonly string[] KnownPropertiesToRemove = { "CachedReusableFilters", "MaxStateDepth", "MaxValidationDepth", "Empty", "Revision", "_active", "Items", "asyncRun", "run", "tasks" };
    private static readonly string[] KnownClassNamesToRemoveFromExceptionReplayFrame = { "<<Configure>b__5_2>d" };

    // This class is used to test Exception Replay with Dynamic Instrumentation enabled or disabled.
    public AspNetCore5ExceptionReplay(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper, bool enableDynamicInstrumentation, bool captureFullCallStack)
        : base("AspNetCore5", outputHelper, "/shutdown")
    {
        SetEnvironmentVariable(ConfigurationKeys.Debugger.ExceptionReplayEnabled, "true");
        SetEnvironmentVariable(ConfigurationKeys.Debugger.Enabled, enableDynamicInstrumentation.ToString().ToLower());
        SetEnvironmentVariable(ConfigurationKeys.Debugger.ExceptionReplayCaptureFullCallStackEnabled, captureFullCallStack.ToString().ToLower());
        SetEnvironmentVariable(ConfigurationKeys.Debugger.ExceptionReplayEnabled, "true");
        SetEnvironmentVariable(ConfigurationKeys.Rcm.PollInterval, "100");
        SetEnvironmentVariable(ConfigurationKeys.Debugger.MaxDepthToSerialize, "5");
        SetEnvironmentVariable(ConfigurationKeys.Debugger.DiagnosticsInterval, "1");
        SetEnvironmentVariable(ConfigurationKeys.Debugger.MaxTimeToSerialize, "1000");
        SetEnvironmentVariable("DD_CLR_ENABLE_INLINING", "0");

        // See https://github.com/dotnet/runtime/issues/91963
        SetEnvironmentVariable("COMPLUS_ForceEnc", "0");

        DynamicInstrumentationEnabled = enableDynamicInstrumentation;
        CaptureFullCallStackEnabled = captureFullCallStack;
        Fixture = fixture;
        Fixture.SetOutput(outputHelper);
    }

    protected bool DynamicInstrumentationEnabled { get; }

    protected bool CaptureFullCallStackEnabled { get; }

    protected AspNetCoreTestFixture Fixture { get; }

    public static IEnumerable<object[]> ExceptionReplayTests()
    {
        return DebuggerTestHelper.AllTestDescriptions()
                                 .Where(test => ((ProbeTestDescription)test[0]).TestType.GetCustomAttributes<ExceptionReplayTestDataAttribute>().Any());
    }

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
    [MemberData(nameof(ExceptionReplayTests))]
    [Trait("RunOnWindows", "True")]
    public async Task TestExceptionReplay(ProbeTestDescription testData)
    {
        var data = testData.TestType.GetCustomAttributes<ExceptionReplayTestDataAttribute>().Single();
        var expectedNumberOfSnapshots = CaptureFullCallStackEnabled ? data.ExpectedNumberOfSnaphotsFull : data.ExpectedNumberOfSnapshotsDefault;
        var url = $"/RunTest/{testData.TestType.FullName}";

        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;

        var toApprove = new StringBuilder();

        MockSpan spanOfCapturedException = null;

        for (int i = 0; i < 4; i++)
        {
            var spans = await SendRequestsAsync(agent, [url]);
            var erroredSpan = spans.Single(x => x.Tags.ContainsKey("error.stack"));

            var allTags = erroredSpan.Tags.Where(tag => tag.Key.StartsWith("_dd.di") || tag.Key.StartsWith("_dd.debug")).OrderBy(tag => tag.Key);

            if (allTags.Any(tag => tag.Key == "_dd.di._er" && tag.Value == "NewCase"))
            {
                Output.WriteLine($"Skipped NewCase tag.");
                i -= 1;
                continue;
            }

            toApprove.AppendLine($"Iteration {i}:");

            var classNameSuffix = ".frame_data.class_name";

            var framePrefixesToOmit = allTags
                                         .Where(tag =>
                                          {
                                              if (tag.Key.EndsWith(classNameSuffix) && KnownClassNamesToRemoveFromExceptionReplayFrame.Contains(tag.Value))
                                              {
                                                  return true;
                                              }

                                              if (tag.Key.EndsWith(classNameSuffix) && tag.Value.StartsWith("<"))
                                              {
                                                  var framePrefix = tag.Key.Replace(classNameSuffix, string.Empty);
                                                  return allTags.Any(tag => (tag.Key == framePrefix + ".no_capture_reason") && tag.Value.EndsWith("blacklisted."));
                                              }

                                              return false;
                                          })
                                         .Select(tag => tag.Key.Replace(classNameSuffix, string.Empty))
                                         .ToArray();

            foreach (var tag in allTags)
            {
                var tagValue = tag.Value;

                if (tag.Key.EndsWith("snapshot_id") || tag.Key.EndsWith("exception_id") || tag.Key.EndsWith("exception_hash"))
                {
                    tagValue = "<Redacted>";
                }

                if (framePrefixesToOmit.Any(tag.Key.StartsWith))
                {
                    continue;
                }

                toApprove.AppendLine($"     {tag.Key} : {tagValue}");
            }

            if (allTags.Single(tag => tag.Key == "_dd.di._er").Value == "Eligible")
            {
                spanOfCapturedException = erroredSpan;
            }

            await Task.Delay(250);
        }

        var settings = new VerifySettings();
        settings.DisableRequireUniquePrefix();
        settings.UseFileName($"{nameof(AspNetCore5ExceptionReplay)}{(CaptureFullCallStackEnabled ? ".FullCallStack" : string.Empty)}.{testData.TestType.Name}");
        settings.UseDirectory("Approvals");
        await Verifier.Verify(toApprove.ToString(), settings);

        Assert.NotNull(spanOfCapturedException);

        var actualSnapshotsNum = spanOfCapturedException.Tags.Keys.Where(key => key.EndsWith(".snapshot_id"));

        Assert.Equal(expectedNumberOfSnapshots, actualSnapshotsNum.Count());

        var snapshots = await agent.WaitForSnapshots(expectedNumberOfSnapshots);

        Assert.Equal(expectedNumberOfSnapshots, snapshots!.Length);

        var testName = "ExceptionReplay" + (CaptureFullCallStackEnabled ? ".FullCallStack" : string.Empty) + ".AspNetCore5." + testData.TestType.Name;
        await Approver.ApproveSnapshots(snapshots, testName, Output, KnownPropertiesToReplace, KnownPropertiesToRemove, orderPostScrubbing: true);
        agent.ClearSnapshots();
    }
}
#endif
