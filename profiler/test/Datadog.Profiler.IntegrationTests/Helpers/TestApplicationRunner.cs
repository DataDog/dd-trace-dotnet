// <copyright file="TestApplicationRunner.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Datadog.Profiler.IntegrationTests.Helpers
{
    internal class TestApplicationRunner
    {
        private readonly string _appName;
        private readonly string _framework;
        private readonly string _appAssembly;
        private readonly XUnitFileLogger _output;
        private readonly string _commandLine;
        private readonly string _testBaseOutputDir;

        // The max test duration is _really_ big on some runners the test(s) can
        // take long time to start and to end.
        private readonly TimeSpan _maxTestRunDuration = TimeSpan.FromSeconds(600);

        private readonly int _profilingExportsIntervalInSeconds = 3;
        private string _appListenerPort;

        public TestApplicationRunner(
            string appName,
            string framework,
            string appAssembly,
            ITestOutputHelper output,
            string commandLine = null,
            bool enableTracer = false,
            bool enableProfiler = true)
        {
            _appName = appName;
            _framework = framework;
            Environment = new EnvironmentHelper(framework, enableTracer, enableProfiler);
            _testBaseOutputDir = Environment.GetTestOutputPath();
            var logPath = Path.Combine(_testBaseOutputDir, "logs");
            // create the log folder now instead of waiting for the profiler to create it
            Directory.CreateDirectory(_testBaseOutputDir);
            _appAssembly = appAssembly;
            _output = new XUnitFileLogger(output, Path.Combine(logPath, "xunit.txt"));
            _commandLine = commandLine ?? string.Empty;
            ServiceName = $"IntegrationTest-{_appName}";
        }

        public EnvironmentHelper Environment { get; }

        public string ServiceName { get; set; }

        public int TestDurationInSeconds { get; set; } = 10;

        public double TotalTestDurationInMilliseconds { get; set; } = 0;

        public string ProcessOutput { get; set; }

        public ITestOutputHelper XUnitLogger => _output;

        public static string GetApplicationOutputFolderPath(string appName)
        {
            var configurationAndPlatform = $"{EnvironmentHelper.GetConfiguration()}-{EnvironmentHelper.GetPlatform()}";
            var binPath = EnvironmentHelper.GetBinOutputPath();
            return Path.Combine(binPath, configurationAndPlatform, "profiler", "src", "Demos", appName);
        }

        public void Run(MockDatadogAgent agent)
        {
            RunTest(agent);
            PrintTestInfo();
        }

        public bool WaitForExitOrCaptureDump(Process process, int milliseconds)
        {
            var success = process.WaitForExit(milliseconds);

            if (!success)
            {
                process.GetAllThreadsStack(_testBaseOutputDir, _output);
                process.TakeMemoryDump(_testBaseOutputDir, _output);
            }

            return success;
        }

        public ProcessHelper LaunchProcess(MockDatadogAgent agent = null)
        {
            var (executor, arguments) = BuildTestCommandLine();

            var process = new Process();

            SetEnvironmentVariables(process.StartInfo.EnvironmentVariables, agent);

            process.StartInfo.FileName = executor;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardInput = false;
            process.Start();

            return new ProcessHelper(process);
        }

        private void PrintTestInfo()
        {
            _output.WriteLine("Test information:");
            _output.WriteLine($"* Name: {_appName}");
            _output.WriteLine($"* Assembly: {_appAssembly}");
            _output.WriteLine($"* Command Line: {_commandLine}");
            _output.WriteLine($"* Path: {GetApplicationPath()}");
            _output.WriteLine($"* Test base dir: {_testBaseOutputDir}");
            _output.WriteLine($"* LogDir: {Environment.LogDir}");
            _output.WriteLine($"* PprofDir: {Environment.PprofDir}");
        }

        private string GetApplicationAssemblyFileName()
        {
            var extension = string.Empty;
            if (EnvironmentHelper.IsRunningOnWindows())
            {
                extension = ".exe";
            }

            return $"{_appAssembly}{extension}";
        }

        private string GetApplicationPath()
        {
            return Path.Combine(GetApplicationOutputFolderPath(_appName), _framework, GetApplicationAssemblyFileName());
        }

        private (string Executor, string Arguments) BuildTestCommandLine()
        {
            var applicationPath = GetApplicationPath();

            if (!File.Exists(applicationPath))
            {
                throw new Exception($"Unable to find executing assembly at {applicationPath}");
            }

            // Look for a free open port to pass to the ASP.NET Core applications
            // that accept --urls on their command line
            _appListenerPort = $"http://localhost:{TcpPortProvider.GetOpenPort()}";
            var arguments = $"--timeout {TestDurationInSeconds} --urls {_appListenerPort}";
            if (!string.IsNullOrEmpty(_commandLine))
            {
                arguments += $" {_commandLine}";
            }

            return (applicationPath, arguments);
        }

        private void RunTest(MockDatadogAgent agent)
        {
            if (!agent.IsReady)
            {
                throw new XunitException("Agent was not ready to accept connection from profiler");
            }

            (var executor, var arguments) = BuildTestCommandLine();

            using var process = new Process();

            SetEnvironmentVariables(process.StartInfo.EnvironmentVariables, agent);

            process.StartInfo.FileName = executor;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardInput = false;
            var startTime = DateTime.Now;
            process.Start();

            using var processHelper = new ProcessHelper(process);

            var ranToCompletion = process.WaitForExit((int)_maxTestRunDuration.TotalMilliseconds) && processHelper.Drain((int)_maxTestRunDuration.TotalMilliseconds / 2);

            var standardOutput = processHelper.StandardOutput;
            var errorOutput = processHelper.ErrorOutput;
            ProcessOutput = standardOutput;

            if (!ranToCompletion)
            {
                if (!process.HasExited)
                {
                    process.GetAllThreadsStack(_testBaseOutputDir, _output);

                    process.TakeMemoryDump(_testBaseOutputDir, _output);

                    try
                    {
                        process.KillTree();
                    }
                    catch
                    {
                        // do nothing
                    }

                    _output.WriteLine($"The test {_appName} is running for too long (more than {_maxTestRunDuration.TotalSeconds} seconds) or was lost.");
                    _output.WriteLine(standardOutput);
                    _output.WriteLine(errorOutput);
                    throw new TimeoutException($"The test {_appName} is running for too long or was lost");
                }
            }

            var endTime = process.ExitTime;
            TotalTestDurationInMilliseconds = (endTime - startTime).TotalMilliseconds;

            if (standardOutput.Contains("[Error]"))
            {
                _output.WriteLine($"[TestRunner] Standard output: \n{standardOutput}");
                throw new XunitException("An error occured during the test. See the standard output above.");
            }

            if (errorOutput.Contains("[Error]"))
            {
                _output.WriteLine($"[TestRunner] Error output: \n{errorOutput}");
                throw new XunitException("An error occured during the test. See the error output above.");
            }

            Assert.True(
                0 == process.ExitCode,
                $"Exit code of \"{Path.GetFileName(process.StartInfo?.FileName ?? string.Empty)}\" should be 0 instead of {process.ExitCode} (= 0x{process.ExitCode.ToString("X")})");
        }

        private void SetEnvironmentVariables(StringDictionary environmentVariables, MockDatadogAgent agent)
        {
            Environment.PopulateEnvironmentVariables(environmentVariables, agent, _profilingExportsIntervalInSeconds, ServiceName);
        }
    }
}
