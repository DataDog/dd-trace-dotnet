using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
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

            int agentPort = TcpPortProvider.GetOpenPort();
            int aspNetCorePort = TcpPortProvider.GetOpenPort(); // unused for now
            Output.WriteLine($"Assigning port {agentPort} for the agentPort.");
            Output.WriteLine($"Assigning port {aspNetCorePort} for the aspNetCorePort.");

            ProcessResult result;

            using (var agent = new MockTracerAgent(agentPort))
            {
                agent.ShouldDeserializeTraces = shouldDeserializeTraces;

                // Using the following code to avoid possible hangs on WaitForExit due to synchronous reads: https://stackoverflow.com/questions/139593/processstartinfo-hanging-on-waitforexit-why
                using (var outputWaitHandle = new AutoResetEvent(false))
                using (var errorWaitHandle = new AutoResetEvent(false))
                using (var process = new Process())
                {
                    // Initialize StartInfo
                    process.StartInfo.FileName = executable;
                    EnvironmentHelper.SetEnvironmentVariables(agentPort, aspNetCorePort, executable, process.StartInfo.EnvironmentVariables);
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

                    // Set up buffered output for stdout and stderr
                    var outputBuffer = new StringBuilder();
                    var errorBuffer = new StringBuilder();
                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (e.Data == null)
                        {
                            outputWaitHandle.Set();
                        }
                        else
                        {
                            outputBuffer.AppendLine(e.Data);
                        }
                    };
                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (e.Data == null)
                        {
                            errorWaitHandle.Set();
                        }
                        else
                        {
                            errorBuffer.AppendLine(e.Data);
                        }
                    };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    var ranToCompletion = process.WaitForExit(MaxTestRunMilliseconds) && outputWaitHandle.WaitOne(MaxTestRunMilliseconds / 2) && errorWaitHandle.WaitOne(MaxTestRunMilliseconds / 2);
                    var standardOutput = outputBuffer.ToString();
                    var standardError = errorBuffer.ToString();

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

                    int exitCode = process.ExitCode;

                    result = new ProcessResult(process, standardOutput, standardError, exitCode);
                }
            }

            var successCode = 0;
            Assert.True(successCode == result.ExitCode, $"Non-success exit code {result.ExitCode}");
            Assert.True(string.IsNullOrEmpty(result.StandardError), $"Expected no errors in smoke test: {result.StandardError}");
        }
    }
}
