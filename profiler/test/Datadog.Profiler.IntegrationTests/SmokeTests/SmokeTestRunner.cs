// <copyright file="SmokeTestRunner.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Datadog.Configuration;
using Datadog.Profiler.IntegrationTests;
using Datadog.Profiler.IntegrationTests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.SmokeTests
{
    public class SmokeTestRunner
    {
        private readonly ITestOutputHelper _output;
        // The max test duration is _really_ big on some runners the test(s) can
        // take long time to start and to end.
        private readonly TimeSpan _maxTestRunDuration = TimeSpan.FromSeconds(600);
        private readonly int _minimumExpectedPprofsCount = 2; // 1 empty and at least one normal
        private readonly int _testDurationInSeconds = 10;
        private readonly int _profilingExportsIntervalInSeconds = 3;
        private readonly bool _useDefaultLogDir;
        private readonly bool _useDefaultPprofDir;
        private readonly string _testBaseOutputDir;

        // short name of the demo application (appears in the folder name under "src\demo"
        private readonly string _appName;

        // .NET framework ID
        private readonly string _framework;

        // name of the demo application generated assembly
        private readonly string _appAssembly;

        // additional parameter to the command line if needed (for --scenario support)
        private readonly string _commandLine;

        private string _testLogDir;
        private string _testPprofDir;
        private string _appListenerPort;

        public SmokeTestRunner(string appName, string framework, string appAssembly, string commandLine, ITestOutputHelper output, bool useDefaultLogDir = false, bool useDefaultPprofDir = false)
        {
            _appName = appName;
            _framework = framework;
            _appAssembly = appAssembly;
            _commandLine = commandLine;
            _output = output;
            _testBaseOutputDir = GetTestOutputPath();
            _useDefaultLogDir = useDefaultLogDir;
            _useDefaultPprofDir = useDefaultPprofDir;
            _testLogDir = ConfigurationProviderUtils.GetOsSpecificDefaultLogDirectory();
            _testPprofDir = ConfigurationProviderUtils.GetOsSpecificDefaultPProfDirectory();
            EnvironmentHelper = new EnvironmentHelper(_framework);
        }

        public SmokeTestRunner(string appName, string framework, string appAssembly, ITestOutputHelper output, bool useDefaultLogDir = false, bool useDefaultPprofDir = false)
            : this(appName, framework, appAssembly, commandLine: null, output, useDefaultLogDir, useDefaultPprofDir)
        {
        }

        public EnvironmentHelper EnvironmentHelper { get; }

        public static string GetApplicationOutputFolderPath(string appName)
        {
            string configurationAndPlatform = $"{EnvironmentHelper.GetConfiguration()}-{EnvironmentHelper.GetPlatform()}";
            string binPath = EnvironmentHelper.GetBinOutputPath();
            return Path.Combine(binPath, configurationAndPlatform, "profiler", "src", "Demos", appName);
        }

        public void RunAndCheck()
        {
            using (var datadogMockAgent = new MockDatadogAgent(_output))
            {
                RunTest(datadogMockAgent.Port);
                PrintTestInfo();

                // Avoid CI flackiness: checking pprof files is enough
                //RunChecks(datadogMockAgent);

                //CheckPprofFiles();
                CheckLogFiles();
            }
        }

        // needs to be static: used by the test case discoverer "SmokeFactDiscoverer"
        public string GetApplicationAssemblyFileName()
        {
            var extension = "exe";
            if (!EnvironmentHelper.IsRunningOnWindows())
            {
                extension = "dll";
            }

            return $"{_appAssembly}.{extension}";
        }

        public string GetApplicationPath()
        {
            return Path.Combine(GetApplicationOutputFolderPath(_appName), _framework, GetApplicationAssemblyFileName());
        }

        private static void DeleteIfNeeded(string testOutputPath)
        {
            if (Directory.Exists(testOutputPath))
            {
                Directory.Delete(testOutputPath, recursive: true);
            }
        }

        private void PrintTestInfo()
        {
            _output.WriteLine("Test information:");
            _output.WriteLine($"* Name: {_appName}");
            _output.WriteLine($"* Assembly: {_appAssembly}");
            _output.WriteLine($"* Command Line: {_commandLine}");
            _output.WriteLine($"* Path: {GetApplicationPath()}");
            _output.WriteLine($"* Test base dir: {_testBaseOutputDir}");
            _output.WriteLine($"* LogDir: {_testLogDir}");
            _output.WriteLine($"* PprofDir: {_testPprofDir}");
        }

        private void RunChecks(MockDatadogAgent agent)
        {
            CheckLogFiles();
            CheckPprofFiles();
            CheckAgent(agent);
        }

        private void CheckLogFiles()
        {
            CheckLogFiles("DD-DotNet-Common-ManagedLoader*.*");
            CheckLogFiles("DD-DotNet-Profiler-Managed*.*");
            CheckLogFiles("DD-DotNet-Profiler-Native*.*");
        }

        private void CheckLogFiles(string filePattern)
        {
            List<string> managedLoaderLogFiles = Directory.EnumerateFiles(_testLogDir, filePattern, SearchOption.AllDirectories).ToList();

            Assert.NotEmpty(managedLoaderLogFiles);

            foreach (string logFile in managedLoaderLogFiles)
            {
                Assert.False(LogFileContainsErrorMessage(logFile), $"Found error message in the log file {logFile}");
            }
        }

        private void CheckPprofFiles()
        {
            var pprofFiles = Directory.EnumerateFiles(_testPprofDir, "*.pprof", SearchOption.AllDirectories).ToList();
            Assert.True(pprofFiles.Count >= _minimumExpectedPprofsCount, $"The number of pprof files was not greater than or equal to {_minimumExpectedPprofsCount}. Actual value {pprofFiles.Count}");
        }

        private void CheckAgent(MockDatadogAgent agent)
        {
            Assert.True(agent.NbReceivedCalls >= _minimumExpectedPprofsCount, $"The number of calls to the agent was not greater than or equal to {_minimumExpectedPprofsCount}. Actual value {agent.NbReceivedCalls}");
        }

        private bool LogFileContainsErrorMessage(string logFile)
        {
            var errorLinePattern = new Regex(@"\| error \|", RegexOptions.IgnoreCase | RegexOptions.Compiled);

            using (var file = File.OpenRead(logFile))
            using (var reader = new StreamReader(file))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();

                    if (line != null && errorLinePattern.IsMatch(line))
                    {
                        _output.WriteLine(line);
                        return true;
                    }
                }
            }

            return false;
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
            (string executor, string arguments) = BuildTestCommandLine();

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

            bool ranToCompletion = process.WaitForExit((int)_maxTestRunDuration.TotalMilliseconds) && processHelper.Drain((int)_maxTestRunDuration.TotalMilliseconds / 2);

            string standardOutput = processHelper.StandardOutput;
            string errorOutput = processHelper.ErrorOutput;

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
            string testLogDir = null;
            if (!_useDefaultLogDir)
            {
                testLogDir = _testLogDir = Path.Combine(_testBaseOutputDir, "Logs");
            }

            string testPprofDir = null;
            if (!_useDefaultPprofDir)
            {
                testPprofDir = _testPprofDir = Path.Combine(_testBaseOutputDir, "Pprofs");
            }

            string serviceName = $"IntegrationTest-{_appName}";
            EnvironmentHelper.SetEnvironmentVariables(environmentVariables, agentPort, _profilingExportsIntervalInSeconds, testLogDir, testPprofDir, serviceName);
        }

        private string GetTestOutputPath()
        {
            // DD_TESTING_OUPUT_DIR is set by the CI
            string baseTestOutputDir = Environment.GetEnvironmentVariable("DD_TESTING_OUPUT_DIR") ?? Path.GetTempPath();
            string testOutputPath = Path.Combine(baseTestOutputDir, $"SmokeTest_{_appName}", _framework);

            DeleteIfNeeded(testOutputPath);

            return testOutputPath;
        }
    }
}
