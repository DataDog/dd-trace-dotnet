// <copyright file="ConsoleLoggingTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.Logging;

public class ConsoleLoggingTests : TestHelper
{
    public ConsoleLoggingTests(ITestOutputHelper output)
        : base("Console", output)
    {
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task ConsoleSinkWritesToConsole()
    {
        EnvironmentHelper.CustomEnvironmentVariables["DD_TRACE_LOG_SINKS"] = "console-experimental";
        using var agent = EnvironmentHelper.GetMockAgent();
        using var processResult = await RunSampleAndWaitForExit(agent, "traces 1");

        // split output into lines and exclude lines output from the sample app itself (i.e., not using the console logger)
        var lines = processResult.StandardOutput
                                 .Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries)
                                 .Where(line => !line.StartsWith("Waiting - PID:") &&
                                                line != "Args: traces 1" &&
                                                line != "Sending 1 spans");

        // Expected formats:
        // [yyyy-MM-dd HH:mm:ss.fff +00:00 | DD-TRACE-DOTNET X.Y.Z | LVL] Message
        // [yyyy-MM-dd HH:mm:ss.fff +00:00 | DD-TRACE-DOTNET X.Y.Z | LVL] Message | ExceptionType: ExceptionMessage\nStackTrace1\nStackTrace2\n...
        lines.Should().AllSatisfy(line => line.Should().MatchRegex(@"^\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} \+00:00 \| DD-TRACE-DOTNET \d+\.\d+\.\d+ \| \w{3}\] (.+)( | .+)?$"));
    }

/*
Example output from the Sample.Console app when running the ConsoleLoggingTests test.
Note that a few lines are not from the logger.
----------------------------------------------
[2025-07-10 15:28:26.278 +00:00 | DD-TRACE-DOTNET 3.21.0 | INF] [Assembly metadata] Location: D:\source\datadog\dd-trace-dotnet\shared\bin\monitoring-home\net6.0\Datadog.Trace.dll, HostContext: 0, SecurityRuleSet: None
[2025-07-10 15:28:26.285 +00:00 | DD-TRACE-DOTNET 3.21.0 | INF] CIEnvironmentValues: Loading environment data.
[2025-07-10 15:28:26.299 +00:00 | DD-TRACE-DOTNET 3.21.0 | INF] CIEnvironmentValues: CI could not be detected, using the git folder: "D:\source\datadog\dd-trace-dotnet"
[2025-07-10 15:28:26.302 +00:00 | DD-TRACE-DOTNET 3.21.0 | INF] CODEOWNERS file found: "D:\source\datadog\dd-trace-dotnet\.github\CODEOWNERS"
[2025-07-10 15:28:26.328 +00:00 | DD-TRACE-DOTNET 3.21.0 | INF] AppSec was not activated, its status is enabled=False, AppSec can be remotely enabled=True.
[2025-07-10 15:28:26.370 +00:00 | DD-TRACE-DOTNET 3.21.0 | INF] Using HttpClientRequestFactory for "discovery" transport.
[2025-07-10 15:28:26.382 +00:00 | DD-TRACE-DOTNET 3.21.0 | INF] Using HttpClientRequestFactory for "trace" transport.
[2025-07-10 15:28:26.386 +00:00 | DD-TRACE-DOTNET 3.21.0 | INF] Building automatic tracer
[2025-07-10 15:28:26.388 +00:00 | DD-TRACE-DOTNET 3.21.0 | INF] Runtime id retrieved from native loader: "aa0c382d-d6e3-4ce1-a7cc-58987a3648ac"
[2025-07-10 15:28:26.389 +00:00 | DD-TRACE-DOTNET 3.21.0 | INF] Continuous Profiler is "disabled".
[2025-07-10 15:28:26.414 +00:00 | DD-TRACE-DOTNET 3.21.0 | INF] The profiler has been initialized with 616 definitions.
[2025-07-10 15:28:26.431 +00:00 | DD-TRACE-DOTNET 3.21.0 | INF] Exception Replay is disabled. To enable it, please set DD_EXCEPTION_REPLAY_ENABLED environment variable to '1'/'true'.
[2025-07-10 15:28:26.431 +00:00 | DD-TRACE-DOTNET 3.21.0 | INF] TraceMethods instrumentation enabled with Assembly="Datadog.Trace, Version=3.21.0.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb", Type="Datadog.Trace.ClrProfiler.AutoInstrumentation.TraceAnnotations.TraceAnnotationsIntegration", and Configuration="".
Waiting - PID: 58956 - Main thread: 33928 - Profiler attached: True
Args: traces 1
Sending 1 spans
[2025-07-10 15:28:26.492 +00:00 | DD-TRACE-DOTNET 3.21.0 | INF] PDB file "D:\source\datadog\dd-trace-dotnet\artifacts\bin\Samples.Console\release_net6.0\Samples.Console.pdb" contained SourceLink information, and we successfully parsed it. The mapping uri is https://raw.githubusercontent.com/DataDog/dd-trace-dotnet/a9004a058aeb242e3356cd865b439df636bbc72e/*.
[2025-07-10 15:28:26.492 +00:00 | DD-TRACE-DOTNET 3.21.0 | INF] Found SourceLink information for assembly "Samples.Console": commit "a9004a058aeb242e3356cd865b439df636bbc72e" from "https://github.com/DataDog/dd-trace-dotnet"
[2025-07-10 15:28:26.495 +00:00 | DD-TRACE-DOTNET 3.21.0 | INF] Cache capacity is: 2048
[2025-07-10 15:28:26.518 +00:00 | DD-TRACE-DOTNET 3.21.0 | INF] DATADOG TRACER CONFIGURATION - "{...}"
 */
}
