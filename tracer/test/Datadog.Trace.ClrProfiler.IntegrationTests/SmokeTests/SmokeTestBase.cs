// <copyright file="SmokeTestBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Immutable;
using System.Diagnostics;
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
        protected void CheckForSmoke(bool shouldDeserializeTraces = true, int expectedExitCode = 0)
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

                // Using the following code to avoid possible hangs on WaitForExit due to synchronous reads: https://stackoverflow.com/questions/139593/processstartinfo-hanging-on-waitforexit-why
                using (var process = new Process())
                {
                    // Initialize StartInfo
                    process.StartInfo.FileName = executable;
                    EnvironmentHelper.SetEnvironmentVariables(agent, aspNetCorePort, process.StartInfo.Environment, processToProfile: executable);
                    if (EnvironmentHelper.IsCoreClr())
                    {
                        // Command becomes: dotnet.exe <applicationPath>
                        process.StartInfo.Arguments = applicationPath;
                    }

                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.RedirectStandardInput = false;

                    process.Start();

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

#if NETCOREAPP2_1
            if (result.ExitCode == 139)
            {
                // Segmentation faults are expected on .NET Core because of a bug in the runtime: https://github.com/dotnet/runtime/issues/11885
                throw new SkipException("Segmentation fault on .NET Core 2.1");
            }
#endif
            if (result.ExitCode == 134
             && standardError?.Contains("System.Threading.AbandonedMutexException: The wait completed due to an abandoned mutex") == true
             && standardError?.Contains("Coverlet.Core.Instrumentation.Tracker") == true)
            {
                // Coverlet occasionally throws AbandonedMutexException during clean up
                throw new SkipException("Coverlet threw AbandonedMutexException during cleanup");
            }

            Assert.True(expectedExitCode == result.ExitCode, $"Expected exit code: {expectedExitCode}, actual exit code: {result.ExitCode}");

            if (expectedExitCode == 0)
            {
                Assert.True(string.IsNullOrEmpty(result.StandardError), $"Expected no errors in smoke test: {result.StandardError}");
            }
        }
    }
}
