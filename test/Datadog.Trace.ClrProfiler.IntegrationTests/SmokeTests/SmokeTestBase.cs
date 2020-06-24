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
            int maxTestRunSeconds = 30)
        {
            Output = output;
            MaxTestRunMilliseconds = maxTestRunSeconds * 1000;
            EnvironmentHelper = new EnvironmentHelper(
                smokeTestName,
                this.GetType(),
                output,
                samplesDirectory: "reproductions",
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

                    var cancellationTokenSource = new CancellationTokenSource();

                    // Drain and store the output
                    var stdoutReader = new OutputReader(process.StandardOutput, cancellationTokenSource.Token);
                    var stderrReader = new OutputReader(process.StandardError, cancellationTokenSource.Token);

                    var ranToCompletion = process.WaitForExit(MaxTestRunMilliseconds);

                    if (AssumeSuccessOnTimeout && !ranToCompletion)
                    {
                        process.Kill();
                        Assert.True(true, "No smoke is a good sign for this case, even on timeout.");
                        return;
                    }

                    if (!ranToCompletion)
                    {
                        Output.WriteLine("The smoke test is running for too long or was lost.");
                        Output.WriteLine($"StandardOutput:{Environment.NewLine}{stdoutReader.GetOutput()}");
                        Output.WriteLine($"StandardError:{Environment.NewLine}{stderrReader.GetOutput()}");

                        cancellationTokenSource.Cancel();

                        throw new TimeoutException("The smoke test is running for too long or was lost.");
                    }

                    var standardOutput = stdoutReader.GetOutput(waitForCompletion: true);
                    var standardError = stderrReader.GetOutput(waitForCompletion: true);

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

        private class OutputReader
        {
            private readonly StreamReader _reader;
            private readonly StringBuilder _buffer = new StringBuilder();
            private readonly CancellationToken _cancellationToken;
            private readonly Thread _thread;

            public OutputReader(StreamReader reader, CancellationToken token)
            {
                _reader = reader;
                _cancellationToken = token;

                _thread = new Thread(Drain) { IsBackground = true };
                _thread.Start();
            }

            public string GetOutput(bool waitForCompletion = false)
            {
                if (waitForCompletion)
                {
                    _thread.Join();
                }

                lock (_buffer)
                {
                    return _buffer.ToString();
                }
            }

            private void Drain()
            {
                while (!_reader.EndOfStream && !_cancellationToken.IsCancellationRequested)
                {
                    var line = _reader.ReadLine();

                    lock (_buffer)
                    {
                        _buffer.AppendLine(line);
                    }
                }
            }
        }
    }
}
