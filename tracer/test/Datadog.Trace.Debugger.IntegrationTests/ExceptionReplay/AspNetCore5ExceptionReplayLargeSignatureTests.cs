// <copyright file="AspNetCore5ExceptionReplayLargeSignatureTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Samples.Probes.TestRuns;
using Samples.Probes.TestRuns.ExceptionReplay;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Debugger.IntegrationTests.ExceptionReplay;

/// <summary>
/// Exception Replay rewrites the frames of a thrown exception. A frame with a local or a return type whose type
/// signature is larger than the native 500 byte buffers (here an anonymous type with 300 properties) used to
/// overflow a stack buffer in the native tracer and fail fast the process (0xC0000409).
/// </summary>
[CollectionDefinition(nameof(AspNetCore5ExceptionReplayLargeSignatureTests), DisableParallelization = true)]
[Collection(nameof(AspNetCore5ExceptionReplayLargeSignatureTests))]
public class AspNetCore5ExceptionReplayLargeSignatureTests : AspNetBase, IClassFixture<AspNetCoreTestFixture>
{
    private const string FrameFunctionTagSuffix = ".frame_data.function";
    private const string FrameWithLargeLocal = "ThrowWithLargeLocal";
    private const string LambdaReturningLargeTypePrefix = "<ThrowWithLargeLocal>b__";
    private const int Attempts = 4;

    public AspNetCore5ExceptionReplayLargeSignatureTests(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
        : base("AspNetCore5", outputHelper)
    {
        SetEnvironmentVariable(ConfigurationKeys.Debugger.ExceptionReplayEnabled, "true");
        SetEnvironmentVariable(ConfigurationKeys.Debugger.DynamicInstrumentationEnabled, "false");
        SetEnvironmentVariable(ConfigurationKeys.Rcm.PollInterval, "100");
        SetEnvironmentVariable(ConfigurationKeys.Debugger.DiagnosticsInterval, "1");
        SetEnvironmentVariable("DD_CLR_ENABLE_INLINING", "0");
        SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");

        // See https://github.com/dotnet/runtime/issues/91963
        SetEnvironmentVariable("COMPLUS_ForceEnc", "0");

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
    public async Task ExceptionReplay_LargeTypeSignatures_DoesNotCrashProcess()
    {
        var url = $"/RunTest/{typeof(LargeAnonymousTypeTest).FullName}";
        var expectedErrorType = typeof(ExceptionReplayIntentionalException).FullName;

        IncludeAllHttpSpans = true;
        await Fixture.TryStartApp(this);
        SetHttpPort(Fixture.HttpPort);

        var agent = Fixture.Agent;
        var capturedFunctions = new HashSet<string>();

        try
        {
            // The first throw arms Exception Replay, the following ones run the rewritten method
            for (var attempt = 1; attempt <= Attempts; attempt++)
            {
                var spans = await SendRequestsAsync(agent, [url]);

                Fixture.Process.HasExited.Should().BeFalse(
                    "the sample process should stay alive after request {0}, Exception Replay must not crash the process on a large type signature",
                    attempt);

                var erroredSpan = spans.Should().Contain(x => x.Tags != null && x.Tags.ContainsKey(Tags.ErrorStack)).Which;
                erroredSpan.GetTag(Tags.ErrorType).Should().Be(expectedErrorType);

                capturedFunctions.UnionWith(GetCapturedFunctions(erroredSpan));

                await Task.Delay(250);
            }

            // Other frames are captured too, so check these two by name: either could be skipped while the request still succeeds
            capturedFunctions.Should().Contain(FrameWithLargeLocal, "the frame holding the large local should be rewritten and captured, not just survive");
            capturedFunctions.Should().Contain(
                function => function.StartsWith(LambdaReturningLargeTypePrefix, StringComparison.Ordinal),
                "the lambda returning the large type should be rewritten and captured, not just survive");
        }
        finally
        {
            agent.ClearSnapshots();
        }
    }

    private static IEnumerable<string> GetCapturedFunctions(MockSpan span)
    {
        return span.Tags
                   .Where(tag => tag.Key.EndsWith(FrameFunctionTagSuffix, StringComparison.Ordinal))
                   .Select(tag => tag.Value);
    }
}

#endif
