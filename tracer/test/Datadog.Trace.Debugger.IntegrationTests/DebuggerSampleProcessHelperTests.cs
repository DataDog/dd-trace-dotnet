// <copyright file="DebuggerSampleProcessHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.IntegrationTests.Helpers;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Debugger.IntegrationTests
{
    public class DebuggerSampleProcessHelperTests
    {
        [Fact]
        public async Task StopSample_WhenProcessAlreadyExitedWithNonZeroExitCode_SurfacesExitDetails()
        {
            const int ExitCode = 42;
            const string ExpectedStandardOutput = "expected stdout";
            const string ExpectedErrorOutput = "expected stderr";

            using var process = StartProcessThatExits(ExitCode, ExpectedStandardOutput, ExpectedErrorOutput);
            process.WaitForExit(10_000).Should().BeTrue();

            var output = new CapturingTestOutputHelper();
            using var helper = new DebuggerSampleProcessHelper("http://127.0.0.1:1/", process, output);
            var completedTask = await Task.WhenAny(helper.Task, Task.Delay(TimeSpan.FromSeconds(10)));
            completedTask.Should().Be(helper.Task);

            var exception = await Assert.ThrowsAsync<ExitCodeException>(() => helper.StopSample());

            exception.Message.Should().Contain($"actual exit code: {ExitCode}");
            output.Output.Should().Contain($"Process already exited with code {ExitCode}");
            output.Output.Should().Contain(ExpectedStandardOutput);
            output.Output.Should().Contain(ExpectedErrorOutput);
        }

        private static Process StartProcessThatExits(int exitCode, string standardOutput, string errorOutput)
        {
            var startInfo = new ProcessStartInfo
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                startInfo.FileName = "cmd.exe";
                startInfo.Arguments = $"/C \"echo {standardOutput} & echo {errorOutput} 1>&2 & exit /b {exitCode}\"";
            }
            else
            {
                startInfo.FileName = "/bin/sh";
                startInfo.Arguments = $"-c \"printf '%s\\n' '{standardOutput}'; printf '%s\\n' '{errorOutput}' >&2; exit {exitCode}\"";
            }

            return Process.Start(startInfo);
        }

        private class CapturingTestOutputHelper : ITestOutputHelper
        {
            private readonly StringBuilder _output = new();

            public string Output => _output.ToString();

            public void WriteLine(string message)
            {
                _output.AppendLine(message);
            }

            public void WriteLine(string format, params object[] args)
            {
                _output.AppendLine(string.Format(format, args));
            }
        }
    }
}
