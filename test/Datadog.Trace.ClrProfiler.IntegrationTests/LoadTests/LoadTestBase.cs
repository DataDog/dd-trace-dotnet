using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Datadog.Trace.TestHelpers;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.LoadTests
{
    public abstract class LoadTestBase
    {
        /// <summary>
        /// All of these parts must exit to trigger completion of the test
        /// </summary>
        private readonly List<LoadTestPart> _anchors = new List<LoadTestPart>();

        /// <summary>
        /// All of the parts of the load test
        /// </summary>
        private readonly List<LoadTestPart> _loadTestParts = new List<LoadTestPart>();

        protected LoadTestBase(
            ITestOutputHelper output,
            int maxTestRunSeconds = 240)
        {
            Output = output;
            MaxTestRunMilliseconds = maxTestRunSeconds * 1000;
        }

        protected ITestOutputHelper Output { get; }

        protected int MaxTestRunMilliseconds { get; }

        public void RegisterPart(
            string applicationName,
            string directory,
            bool requiresAgent,
            bool isAnchor = false,
            int? port = null,
            string[] commandLineArgs = null)
        {
            var loadTestPart = new LoadTestPart
            {
                Application = applicationName,
                CommandLineArgs = commandLineArgs,
                Port = port,
                IsAnchor = isAnchor
            };

            var env = new EnvironmentHelper(
                sampleName: applicationName,
                anchorType: this.GetType(),
                output: Output,
                samplesDirectory: directory,
                prependSamplesToAppName: false,
                requiresProfiling: requiresAgent);

            loadTestPart.EnvironmentHelper = env;

            if (requiresAgent)
            {
                // Use different ports for every agent, to mimic individual instances IRL
                int agentPort = TcpPortProvider.GetOpenPort();
                loadTestPart.Agent = new MockTracerAgent(agentPort);
            }

            if (loadTestPart.IsAnchor)
            {
                _anchors.Add(loadTestPart);
            }

            _loadTestParts.Add(loadTestPart);
        }

        protected string GetUrl(int port)
        {
            return $"http://localhost:{port}/";
        }

        protected List<LoadTestPart> RunAllParts()
        {
            // clear all relevant environment variables to start with a clean slate
            EnvironmentHelper.ClearProfilerEnvironmentVariables();

            var threads = new List<Thread>();

            foreach (var part in _loadTestParts)
            {
                var partThread = new Thread(
                    thread =>
                    {
                        RunLoadTestPart(part);
                    });

                threads.Add(partThread);

                partThread.Start();

                // Let the application start
                Thread.Sleep(5000);
            }

            while (AnchorsAreRunning() && threads.Any(t => t.IsAlive))
            {
                Thread.Sleep(2000);
            }

            return _loadTestParts;
        }

        protected void RunLoadTestPart(LoadTestPart loadTestPart)
        {
            var environmentHelper = loadTestPart.EnvironmentHelper;

            var applicationPath = environmentHelper.GetSampleApplicationPath().Replace(@"\\", @"\");
            Output.WriteLine($"Application path: {applicationPath}");
            var executable = environmentHelper.GetSampleExecutionSource();
            Output.WriteLine($"Executable path: {executable}");

            if (!System.IO.File.Exists(applicationPath))
            {
                throw new Exception($"Load test file does not exist: {applicationPath}");
            }

            ProcessStartInfo startInfo;

            int agentPort = loadTestPart.Agent?.Port ?? 0;
            int aspNetPort = loadTestPart.Port ?? 0;

            string commandLineArgs = string.Empty;

            if (loadTestPart.CommandLineArgs != null)
            {
                commandLineArgs = string.Join(" ", loadTestPart.CommandLineArgs);
            }

            if (EnvironmentHelper.IsCoreClr())
            {
                // .NET Core
                startInfo = new ProcessStartInfo(executable, $"{applicationPath} {commandLineArgs}");
                environmentHelper.SetEnvironmentVariableDefaults(agentPort, aspNetPort, executable, startInfo.EnvironmentVariables);
            }
            else
            {
                // .NET Framework
                startInfo = new ProcessStartInfo(executable, $"{commandLineArgs}");
                environmentHelper.SetEnvironmentVariableDefaults(agentPort, aspNetPort, executable, startInfo.EnvironmentVariables);
            }

            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardInput = false;

            Output.WriteLine($"Starting load test part:{environmentHelper.SampleName}");
            Process process = null;
            try
            {
                process = Process.Start(startInfo);

                if (process == null)
                {
                    throw new NullException("We need a reference to the process for this test.");
                }

                loadTestPart.Process = process;

                if (loadTestPart.IsAnchor)
                {
                    process.WaitForExit(MaxTestRunMilliseconds);
                }
                else
                {
                    while (AnchorsAreRunning())
                    {
                        Thread.Sleep(2000);
                    }
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

                loadTestPart.ProcessResult = new ProcessResult(process, standardOutput, standardError, exitCode);

                Output.WriteLine($"Closed load test part:{environmentHelper.SampleName}");
            }
            finally
            {
                loadTestPart.TimeToSetSail = true;

                try
                {
                    if (process != null)
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                        }

                        process.Dispose();
                    }

                    loadTestPart.Agent?.Dispose();
                }
                catch (Exception ex)
                {
                    // Don't care about any of this yet.
                    Output.WriteLine(ex.ToString());
                }
            }
        }

        private bool AnchorsAreRunning()
        {
            return _anchors.Any(anchor => anchor.TimeToSetSail == false);
        }
    }
}
