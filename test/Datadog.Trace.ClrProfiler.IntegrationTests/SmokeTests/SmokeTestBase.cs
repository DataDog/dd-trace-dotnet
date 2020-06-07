using System;
using System.Diagnostics;
using Datadog.Core.Tools;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    public abstract class SmokeTestBase
    {
        protected SmokeTestBase(
            ITestOutputHelper output,
            string smokeTestName,
            int maxTestRunSeconds = 30)
        {
            Output = output;
            MaxTestRunMilliseconds = maxTestRunSeconds * 1000;
            EnvironmentHelper = new EnvironmentHelper(
                smokeTestName,
                this.GetType(),
                output,
                samplesDirectory: "reproductions");
        }

        protected ITestOutputHelper Output { get; }

        protected EnvironmentHelper EnvironmentHelper { get; }

        protected int MaxTestRunMilliseconds { get; }

        protected bool AssumeSuccessOnTimeout { get; set; }

        protected void SetEnvironmentVariable(string key, string value)
        {
            EnvironmentHelper.CustomEnvironmentVariables.Add(key, value);
        }

        /// <summary>
        /// Method to execute a smoke test.
        /// </summary>
        /// <param name="shouldDeserializeTraces">Optimization parameter, pass false when the resulting traces aren't being verified</param>
        protected void CheckForSmoke(bool shouldDeserializeTraces = true)
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

            ProcessStartInfo startInfo;

            int agentPort = TcpPortProvider.GetOpenPort();
            int aspNetCorePort = TcpPortProvider.GetOpenPort(); // unused for now
            Output.WriteLine($"Assigning port {agentPort} for the agentPort.");
            Output.WriteLine($"Assigning port {aspNetCorePort} for the aspNetCorePort.");

            if (EnvironmentHelper.IsCoreClr())
            {
                // .NET Core
                startInfo = new ProcessStartInfo(executable, $"{applicationPath}");
            }
            else
            {
                // .NET Framework
                startInfo = new ProcessStartInfo(executable);
            }

            EnvironmentHelper.SetEnvironmentVariables(agentPort, aspNetCorePort, executable, startInfo.EnvironmentVariables);

            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardInput = false;

            ProcessResult result;

            using (var agent = new MockTracerAgent(agentPort))
            {
                agent.ShouldDeserializeTraces = shouldDeserializeTraces;
                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        throw new NullException("We need a reference to the process for this test.");
                    }

                    var ranToCompletion = process.WaitForExit(MaxTestRunMilliseconds);

                    if (AssumeSuccessOnTimeout && !ranToCompletion)
                    {
                        process.Kill();
                        Assert.True(true, "No smoke is a good sign for this case, even on timeout.");
                        return;
                    }

                    if (!ranToCompletion)
                    {
                        throw new TimeoutException("The smoke test is running for too long or was lost.");
                    }

                    string standardOutput = process.StandardOutput.ReadToEnd();
                    string standardError = process.StandardError.ReadToEnd();
                    int exitCode = process.ExitCode;

                    if (!string.IsNullOrWhiteSpace(standardOutput))
                    {
                        Output.WriteLine($"StandardOutput:{Environment.NewLine}{standardOutput}");
                    }

                    if (!string.IsNullOrWhiteSpace(standardError))
                    {
                        Output.WriteLine($"StandardError:{Environment.NewLine}{standardError}");
                    }

                    result = new ProcessResult(process, standardOutput, standardError, exitCode);
                }
            }

            var successCode = 0;
            Assert.True(successCode == result.ExitCode, $"Non-success exit code {result.ExitCode}");
            Assert.True(string.IsNullOrEmpty(result.StandardError), $"Expected no errors in smoke test: {result.StandardError}");
        }
    }
}
