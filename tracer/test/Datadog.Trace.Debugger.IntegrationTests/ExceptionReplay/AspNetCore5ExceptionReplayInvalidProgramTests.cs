// <copyright file="AspNetCore5ExceptionReplayInvalidProgramTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Debugger.ExceptionAutoInstrumentation;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Samples.Probes.TestRuns;
using Samples.Probes.TestRuns.ExceptionReplay;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Debugger.IntegrationTests.ExceptionReplay;

/// <summary>
/// APMS-20228: after Exception Replay rewrites an async MoveNext, the next invocation
/// must not throw InvalidProgramException. First throw arms ER; later throws run rewritten IL.
/// Also regresses finally-last EH: injection must bind to the SetException catch, not the
/// last clause, so capture is Eligible rather than EmptyCallStackTreeWhileCollecting.
/// </summary>
[CollectionDefinition(nameof(AspNetCore5ExceptionReplayInvalidProgramTests), DisableParallelization = true)]
[Collection(nameof(AspNetCore5ExceptionReplayInvalidProgramTests))]
public class AspNetCore5ExceptionReplayInvalidProgramTests : AspNetBase, IClassFixture<AspNetCoreTestFixture>
{
    private const string ExceptionReplayPhaseTag = "_dd.di._er";
    private const string DebugInfoCapturedTag = "error.debug_info_captured";
    private const string SnapshotIdTagName = "snapshot_id";
    private const int MaxAttempts = 8;

    // Exception Replay captures once (Eligible + frame tags) then reverts probes.
    private const int RequiredCapturedRequests = 1;

    public AspNetCore5ExceptionReplayInvalidProgramTests(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
        : base("AspNetCore5", outputHelper)
    {
        SetEnvironmentVariable(ConfigurationKeys.Debugger.ExceptionReplayEnabled, "true");
        SetEnvironmentVariable(ConfigurationKeys.Debugger.DynamicInstrumentationEnabled, "false");
        SetEnvironmentVariable(ConfigurationKeys.Rcm.PollInterval, "100");
        SetEnvironmentVariable(ConfigurationKeys.Debugger.MaxDepthToSerialize, "5");
        SetEnvironmentVariable(ConfigurationKeys.Debugger.DiagnosticsInterval, "1");
        SetEnvironmentVariable(ConfigurationKeys.Debugger.MaxTimeToSerialize, "1000");
        SetEnvironmentVariable("DD_CLR_ENABLE_INLINING", "0");
        SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");

        // See https://github.com/dotnet/runtime/issues/91963
        SetEnvironmentVariable("COMPLUS_ForceEnc", "0");

        Fixture = fixture;
        Fixture.SetOutput(outputHelper);
    }

    protected AspNetCoreTestFixture Fixture { get; }

    public static IEnumerable<object[]> InvalidProgramScenarios()
    {
        yield return
        [
            typeof(NestedUsingAwaitGenericOverrideTest),
            "<Handle>d__"
        ];
        yield return [typeof(AwaitUsingCatchFilterFinallyTest), "<RunAsync>d__"];
        // Handwritten MoveNext: finally is last EH; SetException lives in an earlier catch.
        yield return [typeof(FinallyLastEhTest), "FinallyLastSm"];
    }

    public override void Dispose()
    {
        base.Dispose();
        Fixture.SetOutput(null);
    }

    [SkippableTheory]
    [MemberData(nameof(InvalidProgramScenarios))]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public async Task ExceptionReplayRewrite_DoesNotThrowInvalidProgramException(Type testType, string expectedMoveNextClassPrefix)
    {
        var url = $"/RunTest/{testType.FullName}";
        var expectedErrorType = typeof(ExceptionReplayIntentionalException).FullName;
        var invalidProgram = typeof(InvalidProgramException).FullName;

        IncludeAllHttpSpans = true;
        await Fixture.TryStartApp(this);
        SetHttpPort(Fixture.HttpPort);

        var agent = Fixture.Agent;
        var capturedRequests = 0;

        try
        {
            for (var attempt = 1; attempt <= MaxAttempts && capturedRequests < RequiredCapturedRequests; attempt++)
            {
                var spans = await SendRequestsAsync(agent, [url]);

                Fixture.Process.HasExited.Should().BeFalse(
                    "the sample process should stay alive after request {0} to {1}",
                    attempt,
                    testType.Name);

                var erroredSpan = spans.Should().Contain(x => x.Tags != null && x.Tags.ContainsKey(Tags.ErrorStack)).Which;
                var errorType = erroredSpan.GetTag(Tags.ErrorType);
                var errorStack = erroredSpan.GetTag(Tags.ErrorStack);
                var exceptionReplayPhase = erroredSpan.GetTag(ExceptionReplayPhaseTag);

                Output.WriteLine($"Attempt {attempt}: error.type={errorType} {ExceptionReplayPhaseTag}={exceptionReplayPhase}");

                errorType.Should().NotBe(
                    invalidProgram,
                    "Exception Replay rewrite of {0} produced InvalidProgramException on request {1}.{2}{3}",
                    testType.Name,
                    attempt,
                    Environment.NewLine,
                    errorStack);

                errorType.Should().Be(expectedErrorType);

                if (IsSuccessfulCapture(erroredSpan, exceptionReplayPhase, expectedMoveNextClassPrefix))
                {
                    capturedRequests++;
                }

                await Task.Delay(250);
            }

            capturedRequests.Should().BeGreaterThanOrEqualTo(
                RequiredCapturedRequests,
                "Exception Replay should capture {0} MoveNext ({1}) after rewriting (Eligible with snapshot_id frame tags). EmptyCallStackTreeWhileCollecting and InvalidatedCase must not count.",
                testType.Name,
                expectedMoveNextClassPrefix);
        }
        finally
        {
            agent.ClearSnapshots();
        }
    }

    private static bool IsSuccessfulCapture(MockSpan span, string exceptionReplayPhase, string expectedMoveNextClassPrefix)
    {
        if (exceptionReplayPhase != ExceptionReplayDiagnosticTagNames.Eligible || span.Tags is null)
        {
            return false;
        }

        if (!span.Tags.ContainsKey(DebugInfoCapturedTag))
        {
            return false;
        }

        foreach (var tagName in span.Tags.Keys.Where(key => key.EndsWith($".{SnapshotIdTagName}", StringComparison.Ordinal)))
        {
            var framePrefix = tagName.Substring(0, tagName.Length - SnapshotIdTagName.Length);
            if (span.GetTag(framePrefix + "frame_data.function") == "MoveNext"
             && span.GetTag(framePrefix + "frame_data.class_name")?.StartsWith(expectedMoveNextClassPrefix, StringComparison.Ordinal) == true)
            {
                return true;
            }
        }

        return false;
    }
}

#endif
