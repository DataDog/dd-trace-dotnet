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

namespace Datadog.Profiler.IntegrationTests.Helpers
{
    internal class TestApplicationRunner : IDisposable
    {
        private readonly string _appName;
        private readonly string _framework;
        private readonly string _appAssembly;
        private readonly ITestOutputHelper _output;
        private readonly string _commandLine;
        private readonly string _testBaseOutputDir;

        // The max test duration is _really_ big on some runners the test(s) can
        // take long time to start and to end.
        private readonly TimeSpan _maxTestRunDuration = TimeSpan.FromSeconds(600);

        private readonly int _testDurationInSeconds = 10;
        private readonly int _profilingExportsIntervalInSeconds = 3;
        private string _appListenerPort;
        private bool _testFailed;

        public TestApplicationRunner(
            string appName,
            string framework,
            string appAssembly,
            ITestOutputHelper output,
            string commandLine = null,
            bool enableNewPipeline = false,
            bool enableTracer = false)
        {
            _appName = appName;
            _framework = framework;
            Environment = new EnvironmentHelper(appName, framework, enableNewPipeline, enableTracer);
            _testBaseOutputDir = Environment.GetTestOutputPath();
            _appAssembly = appAssembly;
            _output = output;
            _commandLine = commandLine ?? string.Empty;
            _testFailed = true;
        }

        public EnvironmentHelper Environment { get; }

        public static string GetApplicationOutputFolderPath(string appName)
        {
            var configurationAndPlatform = $"{EnvironmentHelper.GetConfiguration()}-{EnvironmentHelper.GetPlatform()}";
            var binPath = EnvironmentHelper.GetBinOutputPath();
            return Path.Combine(binPath, configurationAndPlatform, "profiler", "src", "Demos", appName);
        }

        public void Dispose()
        {
            var testPath = _testBaseOutputDir;
            if (Directory.Exists(testPath) && !EnvironmentHelper.IsInCI && _testFailed)
            {
                Directory.Delete(testPath, recursive: true);
            }
        }

        public void Run(MockDatadogAgent agent)
        {
            RunTest(agent.Port);
            _testFailed = false;
            PrintTestInfo();
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
            var extension = "exe";
            if (!EnvironmentHelper.IsRunningOnWindows())
            {
                extension = "dll";
            }

            return $"{_appAssembly}.{extension}";
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
            var arguments = $"--timeout {_testDurationInSeconds} --urls {_appListenerPort}";
            if (!string.IsNullOrEmpty(_commandLine))
            {
                arguments += $" {_commandLine}";
            }

            if (!EnvironmentHelper.IsRunningOnWindows())
            {
                // catchsegv is a tool that catches the segmentation fault and displays useful information: callstack, registers...
                return ("catchsegv", $"dotnet {applicationPath} {arguments}");
            }

            return (applicationPath, arguments);
        }

        private void RunTest(int agentPort)
        {
            (var executor, var arguments) = BuildTestCommandLine();

            using var process = new Process();

            SetEnvironmentVariables(process.StartInfo.EnvironmentVariables, agentPort);

            process.StartInfo.FileName = executor;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardInput = false;
            process.Start();

            using var processHelper = new ProcessHelper(process);

            var ranToCompletion = process.WaitForExit((int)_maxTestRunDuration.TotalMilliseconds) && processHelper.Drain((int)_maxTestRunDuration.TotalMilliseconds / 2);

            var standardOutput = processHelper.StandardOutput;
            var errorOutput = processHelper.ErrorOutput;

            if (!ranToCompletion)
            {
                if (!process.HasExited)
                {
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

            if (!string.IsNullOrWhiteSpace(standardOutput))
            {
                _output.WriteLine($"[TestRunner] Standard output: {standardOutput}");
                Assert.False(standardOutput.Contains("[Error]"), "An error occured during the test. See the standard output above.");
            }

            if (!string.IsNullOrWhiteSpace(errorOutput))
            {
                _output.WriteLine($"[TestRunner] Error output: {errorOutput}");
                Assert.False(errorOutput.Contains("[Error]"), "An error occured during the test. See the error output above.");
            }

            Assert.True(
                0 == process.ExitCode,
                $"Exit code of \"{Path.GetFileName(process.StartInfo?.FileName ?? string.Empty)}\" should be 0 instead of {process.ExitCode} (= 0x{process.ExitCode.ToString("X")})");
        }

        private void SetEnvironmentVariables(StringDictionary environmentVariables, int agentPort)
        {
            var serviceName = $"IntegrationTest-{_appName}";
            Environment.PopulateEnvironmentVarialbes(environmentVariables, agentPort, _profilingExportsIntervalInSeconds, serviceName);
        }
    }
}
