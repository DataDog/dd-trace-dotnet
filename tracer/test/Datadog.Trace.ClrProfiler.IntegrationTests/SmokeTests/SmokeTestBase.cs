// <copyright file="SmokeTestBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    public abstract class SmokeTestBase
    {
        protected SmokeTestBase(
            ITestOutputHelper output,
            string smokeTestName,
            int maxTestRunSeconds = 60)
        {
            Output = output;

            MaxTestRunMilliseconds = maxTestRunSeconds * 1000;
            EnvironmentHelper = new EnvironmentHelper(
                smokeTestName,
                this.GetType(),
                output,
                samplesDirectory: "test/test-applications/regression",
                prependSamplesToAppName: false);
        }

        protected ITestOutputHelper Output { get; }

        protected EnvironmentHelper EnvironmentHelper { get; }

        protected int MaxTestRunMilliseconds { get; }

        protected bool AssumeSuccessOnTimeout { get; set; }

        protected IImmutableList<MockSpan> Spans { get; private set; } = ImmutableList<MockSpan>.Empty;

        protected void SetEnvironmentVariable(string key, string value)
        {
            EnvironmentHelper.CustomEnvironmentVariables.Add(key, value);
        }

        /// <summary>
        /// Method to execute a smoke test.
        /// </summary>
        /// <param name="shouldDeserializeTraces">Optimization parameter, pass false when the resulting traces aren't being verified</param>
        /// <param name="expectedExitCode">Expected exit code</param>
        /// <returns>Async operation</returns>
        protected async Task CheckForSmoke(bool shouldDeserializeTraces = true, int expectedExitCode = 0)
        {
            var applicationPath = EnvironmentHelper.GetSampleApplicationPath().Replace(@"\\", @"\");
            Output.WriteLine($"Application path: {applicationPath}");
            var executable = EnvironmentHelper.GetSampleExecutionSource();
            Output.WriteLine($"Executable path: {executable}");

            if (!System.IO.File.Exists(applicationPath))
            {
                throw new Exception($"Smoke test file does not exist: {applicationPath}");
            }

            // clear all relevant environment variables to start with a clean slate
            EnvironmentHelper.ClearProfilerEnvironmentVariables();

            int aspNetCorePort = TcpPortProvider.GetOpenPort(); // unused for now
            Output.WriteLine($"Assigning port {aspNetCorePort} for the aspNetCorePort.");

            ProcessResult result;
            string standardError = null;

            using (var agent = EnvironmentHelper.GetMockAgent())
            {
                agent.ShouldDeserializeTraces = shouldDeserializeTraces;

                // Command becomes: dotnet.exe <applicationPath>
                var args = EnvironmentHelper.IsCoreClr() ? applicationPath : null;
                // Using the following code to avoid possible hangs on WaitForExit due to synchronous reads: https://stackoverflow.com/questions/139593/processstartinfo-hanging-on-waitforexit-why
                using (var process = await ProfilerHelper.StartProcessWithProfiler(executable, EnvironmentHelper, agent, arguments: args, aspNetCorePort: aspNetCorePort, processToProfile: executable))
                {
                    using var helper = new ProcessHelper(process);

                    var ranToCompletion = process.WaitForExit(MaxTestRunMilliseconds) && helper.Drain(MaxTestRunMilliseconds / 2);
                    var standardOutput = helper.StandardOutput;
                    standardError = helper.ErrorOutput;

                    if (!ranToCompletion)
                    {
                        if (!process.HasExited)
                        {
                            try
                            {
                                process.Kill();
                            }
                            catch
                            {
                                // Do nothing
                            }
                        }

                        if (AssumeSuccessOnTimeout)
                        {
                            Assert.True(true, "No smoke is a good sign for this case, even on timeout.");
                            return;
                        }
                        else
                        {
                            Output.WriteLine("The smoke test is running for too long or was lost.");
                            Output.WriteLine($"StandardOutput:{Environment.NewLine}{standardOutput}");
                            Output.WriteLine($"StandardError:{Environment.NewLine}{standardError}");

                            throw new TimeoutException("The smoke test is running for too long or was lost.");
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(standardOutput))
                    {
                        Output.WriteLine($"StandardOutput:{Environment.NewLine}{standardOutput}");
                    }

                    if (!string.IsNullOrWhiteSpace(standardError))
                    {
                        Output.WriteLine($"StandardError:{Environment.NewLine}{standardError}");
                    }

                    result = new ProcessResult(process, standardOutput, standardError, process.ExitCode);
                }

                Spans = agent.Spans;
            }

            ErrorHelpers.CheckForKnownSkipConditions(Output, result.ExitCode, result.StandardError, EnvironmentHelper);

#if !NET5_0_OR_GREATER
            if (result.StandardOutput.Contains("App completed successfully")
                && Regex.IsMatch(result.StandardError, @"open\(/proc/\d+/mem\) FAILED 2 \(No such file or directory\)"))
            {
                // The above message is the last thing set before we exit.
                // We can still get flake on shutdown (which we can't isolate), but for some reason
                // the crash dump _also_ fails. As this doesn't give us any useful information,
                // Skip the test instead of giving flake
                throw new SkipException("Error during shutting down but crash dump failed");
            }
#endif
            ExitCodeException.ThrowIfNonExpected(result.ExitCode, expectedExitCode, result.StandardError);

            if (expectedExitCode == 0)
            {
                Assert.True(string.IsNullOrEmpty(result.StandardError), $"Expected no errors in smoke test: {result.StandardError}");
            }
        }
    }
}
