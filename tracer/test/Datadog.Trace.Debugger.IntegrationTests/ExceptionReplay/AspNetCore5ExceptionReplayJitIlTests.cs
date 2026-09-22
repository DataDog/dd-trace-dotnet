// <copyright file="AspNetCore5ExceptionReplayJitIlTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER

using System;
using System.IO;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Debugger.ExceptionAutoInstrumentation;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Samples.Probes.TestRuns.ExceptionReplay;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Debugger.IntegrationTests.ExceptionReplay;

/// <summary>
/// APMS-20228: EndAsyncMethodProbe used to memcpy the instruction before SetException.
/// When that opcode is <c>ldfld</c> (this adversarial fixture, not stock Roslyn),
/// the rewritten MoveNext was rejected at <c>AsyncMethodBuilderCore.Start</c>.
/// Fail-closed must abort the rewrite instead. Own fixture so this cannot
/// contaminate the customer-shaped C# tests.
/// </summary>
[Collection(nameof(AspNetCore5ExceptionReplayInvalidProgramTests))]
public class AspNetCore5ExceptionReplayJitIlTests : AspNetBase, IClassFixture<AspNetCoreTestFixture>
{
    private const string ExceptionReplayPhaseTag = "_dd.di._er";
    private const string UnsupportedCompletionValueLoadLog = "EndAsyncMethodProbe: instruction before SetException is not a standalone value load";
    private const int MaxAttempts = 8;
    private readonly string _logPath;

    public AspNetCore5ExceptionReplayJitIlTests(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
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
        SetEnvironmentVariable("COMPLUS_ForceEnc", "0");

        _logPath = Path.Combine(LogDirectory, nameof(AspNetCore5ExceptionReplayJitIlTests));
        Directory.CreateDirectory(_logPath);
        SetEnvironmentVariable(ConfigurationKeys.LogDirectory, _logPath);

        Fixture = fixture;
        Fixture.SetOutput(outputHelper);
    }

    protected AspNetCoreTestFixture Fixture { get; }

    public override void Dispose()
    {
        base.Dispose();
        Fixture.SetOutput(null);
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public async Task SetExceptionLdfldPrev_DoesNotThrowInvalidProgramException()
    {
        var testType = typeof(SetExceptionLdfldPrevTest);
        var url = $"/RunTest/{testType.FullName}";
        var invalidProgram = typeof(InvalidProgramException).FullName;
        var expectedErrorType = typeof(InvalidOperationException).FullName;

        IncludeAllHttpSpans = true;
        using var logEntryWatcher = new LogEntryWatcher("dotnet-tracer-native-*", _logPath, Output);
        await Fixture.TryStartApp(this);
        SetHttpPort(Fixture.HttpPort);

        var agent = Fixture.Agent;

        try
        {
            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                var spans = await SendRequestsAsync(agent, [url]);

                Fixture.Process.HasExited.Should().BeFalse(
                    "the sample process should stay alive after request {0}",
                    attempt);

                var erroredSpan = spans.Should().Contain(x => x.Tags != null && x.Tags.ContainsKey(Tags.ErrorStack)).Which;
                var errorType = erroredSpan.GetTag(Tags.ErrorType);
                var errorStack = erroredSpan.GetTag(Tags.ErrorStack);
                var exceptionReplayPhase = erroredSpan.GetTag(ExceptionReplayPhaseTag);

                Output.WriteLine($"Attempt {attempt}: error.type={errorType} {ExceptionReplayPhaseTag}={exceptionReplayPhase}");
                Output.WriteLine(errorStack);

                errorType.Should().NotBe(
                    invalidProgram,
                    "memcpy of ldfld before SetException must abort instead of producing InvalidProgramException.{0}{1}",
                    Environment.NewLine,
                    errorStack);

                errorType.Should().Be(expectedErrorType);
                await Task.Delay(250);
            }

            var guardLog = await logEntryWatcher.WaitForLogEntry(UnsupportedCompletionValueLoadLog);
            guardLog.Should().Contain("LdfldSm.MoveNext");
        }
        finally
        {
            agent.ClearSnapshots();
        }
    }
}

#endif
