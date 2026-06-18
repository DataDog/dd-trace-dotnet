// <copyright file="ConfigureCiCommandTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.RegularExpressions;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tools.Runner.IntegrationTests
{
    [Collection(nameof(ConsoleTestsCollection))]
    public class ConfigureCiCommandTests(ITestOutputHelper output)
    {
        private static readonly string[] RunScopedBackfillEnvironmentVariables =
        [
            ConfigurationKeys.CIVisibility.TestOptimizationRunId,
            ConfigurationKeys.CIVisibility.TestSessionCommand,
            ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory,
            ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip,
            ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand,
            ConfigurationKeys.CIVisibilityItrCoverageBackfillPath,
            ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder
        ];

        [SkippableTheory]
        [Trait("RunOnWindows", "True")]
        [InlineData("azp", @"##vso\[task.setvariable variable=(?<name>[A-Z1-9_]+);\](?<value>.*)")]
        [InlineData("jenkins", @"(?<name>[A-Z1-9_]+)=(?<value>.*)")]
        [InlineData("github", @"(?<name>[A-Z1-9_]+)=(?<value>.*)", "GITHUB_ENV")]
        [EnvironmentRestorer("GITHUB_ENV")]
        public void ConfigureCi(string ciProviderName, string pattern, string envKeyWithFilePath = null)
        {
            using var agent = MockTracerAgent.Create(output, TcpPortProvider.GetOpenPort());
            var agentUrl = $"http://localhost:{agent.Port}";

            var commandLine = $"ci configure {ciProviderName} --dd-env TestEnv --dd-service TestService --dd-version TestVersion --tracer-home TestTracerHome --agent-url {agentUrl}";

            string envKeyWithFilePathNewValue = null;
            if (!StringUtil.IsNullOrEmpty(envKeyWithFilePath))
            {
                envKeyWithFilePathNewValue = Path.GetTempFileName();
                EnvironmentHelpers.SetEnvironmentVariable(envKeyWithFilePath, envKeyWithFilePathNewValue);
            }

            var originalRunScopedBackfillEnvironment = SetStaleRunScopedBackfillEnvironment();
            try
            {
                using var console = ConsoleHelper.Redirect();

                var result = Program.Main(commandLine.Split(' '));

                result.Should().Be(0);

                var environmentVariables = new Dictionary<string, string>();

                IEnumerable<string> lines = Array.Empty<string>();
                if (!StringUtil.IsNullOrEmpty(envKeyWithFilePathNewValue))
                {
                    lines = File.ReadAllLines(envKeyWithFilePathNewValue);
                }
                else
                {
                    lines = console.ReadLines();
                }

                foreach (var line in lines)
                {
                    var match = Regex.Match(line, pattern);
                    if (match.Success)
                    {
                        environmentVariables.Add(match.Groups["name"].Value, match.Groups["value"].Value);
                    }
                }

                environmentVariables.Should().Contain("DD_ENV", "TestEnv");
                environmentVariables.Should().Contain("DD_SERVICE", "TestService");
                environmentVariables.Should().Contain("DD_VERSION", "TestVersion");
                environmentVariables.Should().Contain("DD_DOTNET_TRACER_HOME", Path.GetFullPath("TestTracerHome"));
                environmentVariables.Should().Contain("DD_TRACE_AGENT_URL", agentUrl);
                foreach (var environmentVariable in RunScopedBackfillEnvironmentVariables)
                {
                    environmentVariables.Should().NotContainKey(environmentVariable);
                }
            }
            finally
            {
                RestoreEnvironment(originalRunScopedBackfillEnvironment);
            }
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        [EnvironmentRestorer("LOCALAPPDATA", "TMPDIR", "TMP", "TEMP", "XDG_CACHE_HOME")]
        public void ConfigureCiUsesCachedTemporaryTracerHomeWhenReducingPathLength()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), $"dd-trace-runner-temp-{Guid.NewGuid():N}");
            var cacheHome = Path.Combine(tempRoot, "cache");
            var tracerHome = Path.Combine(
                tempRoot,
                "long-tracer-home-path-for-reduce-path-length",
                "very-long-segment-to-force-cache-path-selection",
                "another-very-long-segment-to-force-cache-path-selection",
                "home");
            var fixedTempTracerHome = Path.GetFullPath(Path.Combine(tempRoot, "dd"));
            var expectedCacheRoot = Path.GetFullPath(Path.Combine(cacheHome, "Datadog", "dd-trace", "runner", "tracer-home")) + Path.DirectorySeparatorChar;

            try
            {
                CreateTrustedCacheHome(cacheHome);
                Directory.CreateDirectory(fixedTempTracerHome);
                EnvironmentHelpers.SetEnvironmentVariable("LOCALAPPDATA", cacheHome);
                EnvironmentHelpers.SetEnvironmentVariable("TMPDIR", tempRoot + Path.DirectorySeparatorChar);
                EnvironmentHelpers.SetEnvironmentVariable("TMP", tempRoot + Path.DirectorySeparatorChar);
                EnvironmentHelpers.SetEnvironmentVariable("TEMP", tempRoot + Path.DirectorySeparatorChar);
                EnvironmentHelpers.SetEnvironmentVariable("XDG_CACHE_HOME", cacheHome);
                CreateTracerHome(tracerHome);

                using var agent = MockTracerAgent.Create(output, TcpPortProvider.GetOpenPort());
                var agentUrl = $"http://localhost:{agent.Port}";
                var commandLine = $"ci configure jenkins --tracer-home {tracerHome} --agent-url {agentUrl}";

                using var console = ConsoleHelper.Redirect();
                var result = Program.Main(commandLine.Split(' '));
                result.Should().Be(0);

                var environmentVariables = GetJenkinsEnvironmentVariables(console.ReadLines());
                var configuredTracerHome = environmentVariables["DD_DOTNET_TRACER_HOME"];
                var cachedMetadata = Path.Combine(configuredTracerHome, "metadata.txt");

                configuredTracerHome.Should().NotBe(fixedTempTracerHome);
                configuredTracerHome.Should().StartWith(expectedCacheRoot);
                File.Exists(cachedMetadata).Should().BeTrue();
                environmentVariables["CORECLR_PROFILER_PATH_64"].Should().StartWith(configuredTracerHome);

                File.WriteAllText(cachedMetadata, "cached");

                using var secondConsole = ConsoleHelper.Redirect();
                var secondResult = Program.Main(commandLine.Split(' '));
                secondResult.Should().Be(0);

                var secondEnvironmentVariables = GetJenkinsEnvironmentVariables(secondConsole.ReadLines());
                secondEnvironmentVariables["DD_DOTNET_TRACER_HOME"].Should().Be(configuredTracerHome);
                File.ReadAllText(cachedMetadata).Should().Be("source");
            }
            finally
            {
                DeleteDirectory(tempRoot);
            }
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        [EnvironmentRestorer("LOCALAPPDATA", "TMPDIR", "TMP", "TEMP", "XDG_CACHE_HOME")]
        public void ConfigureCiRebuildsCachedTracerHomeWhenCachedProfilerIsModified()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), $"dd-trace-runner-temp-{Guid.NewGuid():N}");
            var cacheHome = Path.Combine(tempRoot, "cache");
            var tracerHome = Path.Combine(
                tempRoot,
                "long-tracer-home-path-for-reduce-path-length",
                "very-long-segment-to-force-cache-path-selection",
                "another-very-long-segment-to-force-cache-path-selection",
                "home");

            try
            {
                CreateTrustedCacheHome(cacheHome);
                EnvironmentHelpers.SetEnvironmentVariable("LOCALAPPDATA", cacheHome);
                EnvironmentHelpers.SetEnvironmentVariable("TMPDIR", tempRoot + Path.DirectorySeparatorChar);
                EnvironmentHelpers.SetEnvironmentVariable("TMP", tempRoot + Path.DirectorySeparatorChar);
                EnvironmentHelpers.SetEnvironmentVariable("TEMP", tempRoot + Path.DirectorySeparatorChar);
                EnvironmentHelpers.SetEnvironmentVariable("XDG_CACHE_HOME", cacheHome);
                CreateTracerHome(tracerHome);

                using var agent = MockTracerAgent.Create(output, TcpPortProvider.GetOpenPort());
                var agentUrl = $"http://localhost:{agent.Port}";
                var commandLine = $"ci configure jenkins --tracer-home {tracerHome} --agent-url {agentUrl}";

                using var console = ConsoleHelper.Redirect();
                var result = Program.Main(commandLine.Split(' '));
                result.Should().Be(0);

                var environmentVariables = GetJenkinsEnvironmentVariables(console.ReadLines());
                var configuredTracerHome = environmentVariables["DD_DOTNET_TRACER_HOME"];
                var cachedProfilerPath = environmentVariables["CORECLR_PROFILER_PATH_64"];

                cachedProfilerPath.Should().StartWith(configuredTracerHome);
                File.WriteAllText(cachedProfilerPath, "tampered");

                using var secondConsole = ConsoleHelper.Redirect();
                var secondResult = Program.Main(commandLine.Split(' '));
                secondResult.Should().Be(0);

                var secondEnvironmentVariables = GetJenkinsEnvironmentVariables(secondConsole.ReadLines());
                secondEnvironmentVariables["DD_DOTNET_TRACER_HOME"].Should().Be(configuredTracerHome);
                File.ReadAllText(cachedProfilerPath).Should().Be("source");
            }
            finally
            {
                DeleteDirectory(tempRoot);
            }
        }

        [SkippableTheory]
        [Trait("RunOnWindows", "True")]
        [InlineData("loader.conf")]
        [InlineData("Datadog.Tracer.Native")]
        [EnvironmentRestorer("LOCALAPPDATA", "TMPDIR", "TMP", "TEMP", "XDG_CACHE_HOME")]
        public void ConfigureCiRebuildsCachedTracerHomeWhenCachedNativeLoaderFileIsModified(string cachedFileName)
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), $"dd-trace-runner-temp-{Guid.NewGuid():N}");
            var cacheHome = Path.Combine(tempRoot, "cache");
            var tracerHome = Path.Combine(
                tempRoot,
                "long-tracer-home-path-for-reduce-path-length",
                "very-long-segment-to-force-cache-path-selection",
                "another-very-long-segment-to-force-cache-path-selection",
                "home");

            try
            {
                CreateTrustedCacheHome(cacheHome);
                EnvironmentHelpers.SetEnvironmentVariable("LOCALAPPDATA", cacheHome);
                EnvironmentHelpers.SetEnvironmentVariable("TMPDIR", tempRoot + Path.DirectorySeparatorChar);
                EnvironmentHelpers.SetEnvironmentVariable("TMP", tempRoot + Path.DirectorySeparatorChar);
                EnvironmentHelpers.SetEnvironmentVariable("TEMP", tempRoot + Path.DirectorySeparatorChar);
                EnvironmentHelpers.SetEnvironmentVariable("XDG_CACHE_HOME", cacheHome);
                CreateTracerHome(tracerHome);

                using var agent = MockTracerAgent.Create(output, TcpPortProvider.GetOpenPort());
                var agentUrl = $"http://localhost:{agent.Port}";
                var commandLine = $"ci configure jenkins --tracer-home {tracerHome} --agent-url {agentUrl}";

                using var console = ConsoleHelper.Redirect();
                var result = Program.Main(commandLine.Split(' '));
                result.Should().Be(0);

                var environmentVariables = GetJenkinsEnvironmentVariables(console.ReadLines());
                var configuredTracerHome = environmentVariables["DD_DOTNET_TRACER_HOME"];
                var cachedProfilerDirectory = Path.GetDirectoryName(environmentVariables["CORECLR_PROFILER_PATH_64"]);
                var cachedLoaderConfigPath = Path.Combine(cachedProfilerDirectory, "loader.conf");
                var cachedNativeTracerPath = Path.Combine(cachedProfilerDirectory, GetNativeTracerFileName());
                var cachedFilePath = cachedFileName == "loader.conf" ? cachedLoaderConfigPath : cachedNativeTracerPath;

                File.WriteAllText(cachedFilePath, "tampered");

                using var secondConsole = ConsoleHelper.Redirect();
                var secondResult = Program.Main(commandLine.Split(' '));
                secondResult.Should().Be(0);

                var secondEnvironmentVariables = GetJenkinsEnvironmentVariables(secondConsole.ReadLines());
                secondEnvironmentVariables["DD_DOTNET_TRACER_HOME"].Should().Be(configuredTracerHome);
                File.ReadAllText(cachedFilePath).Should().Be("source");
            }
            finally
            {
                DeleteDirectory(tempRoot);
            }
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        [EnvironmentRestorer("LOCALAPPDATA", "TMPDIR", "TMP", "TEMP", "XDG_CACHE_HOME")]
        public void ConfigureCiRebuildsCachedTracerHomeWhenCachedManagedDependencyIsModified()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), $"dd-trace-runner-temp-{Guid.NewGuid():N}");
            var cacheHome = Path.Combine(tempRoot, "cache");
            var tracerHome = Path.Combine(
                tempRoot,
                "long-tracer-home-path-for-reduce-path-length",
                "very-long-segment-to-force-cache-path-selection",
                "another-very-long-segment-to-force-cache-path-selection",
                "home");

            try
            {
                CreateTrustedCacheHome(cacheHome);
                EnvironmentHelpers.SetEnvironmentVariable("LOCALAPPDATA", cacheHome);
                EnvironmentHelpers.SetEnvironmentVariable("TMPDIR", tempRoot + Path.DirectorySeparatorChar);
                EnvironmentHelpers.SetEnvironmentVariable("TMP", tempRoot + Path.DirectorySeparatorChar);
                EnvironmentHelpers.SetEnvironmentVariable("TEMP", tempRoot + Path.DirectorySeparatorChar);
                EnvironmentHelpers.SetEnvironmentVariable("XDG_CACHE_HOME", cacheHome);
                CreateTracerHome(tracerHome);
                CreateFile(tracerHome, "netstandard2.0", "Datadog.Trace.Dependency.dll");

                using var agent = MockTracerAgent.Create(output, TcpPortProvider.GetOpenPort());
                var agentUrl = $"http://localhost:{agent.Port}";
                var commandLine = $"ci configure jenkins --tracer-home {tracerHome} --agent-url {agentUrl}";

                using var console = ConsoleHelper.Redirect();
                var result = Program.Main(commandLine.Split(' '));
                result.Should().Be(0);

                var environmentVariables = GetJenkinsEnvironmentVariables(console.ReadLines());
                var configuredTracerHome = environmentVariables["DD_DOTNET_TRACER_HOME"];
                var cachedManagedDependency = Path.Combine(configuredTracerHome, "netstandard2.0", "Datadog.Trace.Dependency.dll");

                File.WriteAllText(cachedManagedDependency, "tampered");

                using var secondConsole = ConsoleHelper.Redirect();
                var secondResult = Program.Main(commandLine.Split(' '));
                secondResult.Should().Be(0);

                var secondEnvironmentVariables = GetJenkinsEnvironmentVariables(secondConsole.ReadLines());
                secondEnvironmentVariables["DD_DOTNET_TRACER_HOME"].Should().Be(configuredTracerHome);
                File.ReadAllText(cachedManagedDependency).Should().Be("source");
            }
            finally
            {
                DeleteDirectory(tempRoot);
            }
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        [EnvironmentRestorer("LOCALAPPDATA", "TMPDIR", "TMP", "TEMP", "XDG_CACHE_HOME")]
        public void ConfigureCiRebuildsCachedTracerHomeWhenCachedProfilerEngineIsModified()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), $"dd-trace-runner-temp-{Guid.NewGuid():N}");
            var cacheHome = Path.Combine(tempRoot, "cache");
            var tracerHome = Path.Combine(
                tempRoot,
                "long-tracer-home-path-for-reduce-path-length",
                "very-long-segment-to-force-cache-path-selection",
                "another-very-long-segment-to-force-cache-path-selection",
                "home");

            try
            {
                CreateTrustedCacheHome(cacheHome);
                EnvironmentHelpers.SetEnvironmentVariable("LOCALAPPDATA", cacheHome);
                EnvironmentHelpers.SetEnvironmentVariable("TMPDIR", tempRoot + Path.DirectorySeparatorChar);
                EnvironmentHelpers.SetEnvironmentVariable("TMP", tempRoot + Path.DirectorySeparatorChar);
                EnvironmentHelpers.SetEnvironmentVariable("TEMP", tempRoot + Path.DirectorySeparatorChar);
                EnvironmentHelpers.SetEnvironmentVariable("XDG_CACHE_HOME", cacheHome);
                CreateTracerHome(tracerHome);

                using var agent = MockTracerAgent.Create(output, TcpPortProvider.GetOpenPort());
                var agentUrl = $"http://localhost:{agent.Port}";
                var commandLine = $"ci configure jenkins --tracer-home {tracerHome} --agent-url {agentUrl}";

                using var console = ConsoleHelper.Redirect();
                var result = Program.Main(commandLine.Split(' '));
                result.Should().Be(0);

                var environmentVariables = GetJenkinsEnvironmentVariables(console.ReadLines());
                var configuredTracerHome = environmentVariables["DD_DOTNET_TRACER_HOME"];
                var cachedProfilerEngine = Path.Combine(configuredTracerHome, "linux-x64", "Datadog.Profiler.Native.so");

                File.WriteAllText(cachedProfilerEngine, "tampered");

                using var secondConsole = ConsoleHelper.Redirect();
                var secondResult = Program.Main(commandLine.Split(' '));
                secondResult.Should().Be(0);

                var secondEnvironmentVariables = GetJenkinsEnvironmentVariables(secondConsole.ReadLines());
                secondEnvironmentVariables["DD_DOTNET_TRACER_HOME"].Should().Be(configuredTracerHome);
                File.ReadAllText(cachedProfilerEngine).Should().Be("source");
            }
            finally
            {
                DeleteDirectory(tempRoot);
            }
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        [EnvironmentRestorer("LOCALAPPDATA", "TMPDIR", "TMP", "TEMP", "XDG_CACHE_HOME")]
        public void ConfigureCiRebuildsCachedTracerHomeWhenExtraLoadableFileIsInjected()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), $"dd-trace-runner-temp-{Guid.NewGuid():N}");
            var cacheHome = Path.Combine(tempRoot, "cache");
            var tracerHome = Path.Combine(
                tempRoot,
                "long-tracer-home-path-for-reduce-path-length",
                "very-long-segment-to-force-cache-path-selection",
                "another-very-long-segment-to-force-cache-path-selection",
                "home");

            try
            {
                CreateTrustedCacheHome(cacheHome);
                EnvironmentHelpers.SetEnvironmentVariable("LOCALAPPDATA", cacheHome);
                EnvironmentHelpers.SetEnvironmentVariable("TMPDIR", tempRoot + Path.DirectorySeparatorChar);
                EnvironmentHelpers.SetEnvironmentVariable("TMP", tempRoot + Path.DirectorySeparatorChar);
                EnvironmentHelpers.SetEnvironmentVariable("TEMP", tempRoot + Path.DirectorySeparatorChar);
                EnvironmentHelpers.SetEnvironmentVariable("XDG_CACHE_HOME", cacheHome);
                CreateTracerHome(tracerHome);

                using var agent = MockTracerAgent.Create(output, TcpPortProvider.GetOpenPort());
                var agentUrl = $"http://localhost:{agent.Port}";
                var commandLine = $"ci configure jenkins --tracer-home {tracerHome} --agent-url {agentUrl}";

                using var console = ConsoleHelper.Redirect();
                var result = Program.Main(commandLine.Split(' '));
                result.Should().Be(0);

                var environmentVariables = GetJenkinsEnvironmentVariables(console.ReadLines());
                var configuredTracerHome = environmentVariables["DD_DOTNET_TRACER_HOME"];
                var injectedFile = Path.Combine(configuredTracerHome, "netstandard2.0", "Injected.Dependency.dll");
                File.WriteAllText(injectedFile, "tampered");

                using var secondConsole = ConsoleHelper.Redirect();
                var secondResult = Program.Main(commandLine.Split(' '));
                secondResult.Should().Be(0);

                var secondEnvironmentVariables = GetJenkinsEnvironmentVariables(secondConsole.ReadLines());
                secondEnvironmentVariables["DD_DOTNET_TRACER_HOME"].Should().Be(configuredTracerHome);
                File.Exists(injectedFile).Should().BeFalse();
            }
            finally
            {
                DeleteDirectory(tempRoot);
            }
        }

        [SkippableFact]
        [EnvironmentRestorer("LOCALAPPDATA", "TMPDIR", "TMP", "TEMP", "XDG_CACHE_HOME")]
        public void ConfigureCiCreatesPrivateCachedTracerHomeOnPosix()
        {
            SkipOn.Platform(SkipOn.PlatformValue.Windows);

            var tempRoot = Path.Combine(Path.GetTempPath(), $"dd-trace-runner-temp-{Guid.NewGuid():N}");
            var cacheHome = Path.Combine(tempRoot, "cache");
            var tracerHome = Path.Combine(
                tempRoot,
                "long-tracer-home-path-for-reduce-path-length",
                "very-long-segment-to-force-cache-path-selection",
                "another-very-long-segment-to-force-cache-path-selection",
                "home");
            var expectedCacheRoot = Path.GetFullPath(Path.Combine(cacheHome, "Datadog", "dd-trace", "runner", "tracer-home")) + Path.DirectorySeparatorChar;

            try
            {
                CreateTrustedCacheHome(cacheHome);
                EnvironmentHelpers.SetEnvironmentVariable("TMPDIR", tempRoot + Path.DirectorySeparatorChar);
                EnvironmentHelpers.SetEnvironmentVariable("TMP", tempRoot + Path.DirectorySeparatorChar);
                EnvironmentHelpers.SetEnvironmentVariable("TEMP", tempRoot + Path.DirectorySeparatorChar);
                EnvironmentHelpers.SetEnvironmentVariable("XDG_CACHE_HOME", cacheHome);
                CreateTracerHome(tracerHome);

                using var agent = MockTracerAgent.Create(output, TcpPortProvider.GetOpenPort());
                var agentUrl = $"http://localhost:{agent.Port}";
                var commandLine = $"ci configure jenkins --tracer-home {tracerHome} --agent-url {agentUrl}";

                using var console = ConsoleHelper.Redirect();
                var result = Program.Main(commandLine.Split(' '));
                result.Should().Be(0);

                var environmentVariables = GetJenkinsEnvironmentVariables(console.ReadLines());
                var configuredTracerHome = environmentVariables["DD_DOTNET_TRACER_HOME"];

                configuredTracerHome.Should().StartWith(expectedCacheRoot);
                GetDirectoryMode(configuredTracerHome).Should().Be("700");
            }
            finally
            {
                DeleteDirectory(tempRoot);
            }
        }

        [SkippableFact]
        [EnvironmentRestorer("LOCALAPPDATA", "TMPDIR", "TMP", "TEMP", "XDG_CACHE_HOME")]
        public void ConfigureCiFallsBackToOriginalTracerHomeWhenCacheRootIsSharedWritable()
        {
            SkipOn.Platform(SkipOn.PlatformValue.Windows);

            var tempRoot = Path.Combine(Path.GetTempPath(), $"dd-trace-runner-temp-{Guid.NewGuid():N}");
            var insecureCacheHome = Path.Combine(tempRoot, "shared-cache");
            var tracerHome = Path.Combine(
                tempRoot,
                "long-tracer-home-path-for-reduce-path-length",
                "very-long-segment-to-force-cache-path-selection",
                "another-very-long-segment-to-force-cache-path-selection",
                "home");

            try
            {
                Directory.CreateDirectory(insecureCacheHome);
                SetDirectoryMode(insecureCacheHome, "777");
                EnvironmentHelpers.SetEnvironmentVariable("TMPDIR", tempRoot + Path.DirectorySeparatorChar);
                EnvironmentHelpers.SetEnvironmentVariable("TMP", tempRoot + Path.DirectorySeparatorChar);
                EnvironmentHelpers.SetEnvironmentVariable("TEMP", tempRoot + Path.DirectorySeparatorChar);
                EnvironmentHelpers.SetEnvironmentVariable("XDG_CACHE_HOME", insecureCacheHome);
                CreateTracerHome(tracerHome);

                using var agent = MockTracerAgent.Create(output, TcpPortProvider.GetOpenPort());
                var agentUrl = $"http://localhost:{agent.Port}";
                var commandLine = $"ci configure jenkins --tracer-home {tracerHome} --agent-url {agentUrl}";

                using var console = ConsoleHelper.Redirect();
                var result = Program.Main(commandLine.Split(' '));
                result.Should().Be(0);

                var environmentVariables = GetJenkinsEnvironmentVariables(console.ReadLines());
                environmentVariables["DD_DOTNET_TRACER_HOME"].Should().Be(Path.GetFullPath(tracerHome));
            }
            finally
            {
                DeleteDirectory(tempRoot);
            }
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        [EnvironmentRestorer("LOCALAPPDATA", "TMPDIR", "TMP", "TEMP", "XDG_CACHE_HOME")]
        public void ConfigureCiFallsBackToOriginalTracerHomeWhenWindowsCacheRootAllowsBroadWrites()
        {
            SkipOn.AllExcept(SkipOn.PlatformValue.Windows);

            var tempRoot = Path.Combine(Path.GetTempPath(), $"dd-trace-runner-temp-{Guid.NewGuid():N}");
            var insecureCacheHome = Path.Combine(tempRoot, "shared-cache");
            var tracerHome = Path.Combine(
                tempRoot,
                "long-tracer-home-path-for-reduce-path-length",
                "very-long-segment-to-force-cache-path-selection",
                "another-very-long-segment-to-force-cache-path-selection",
                "home");

            try
            {
                Directory.CreateDirectory(insecureCacheHome);
                GrantWindowsModifyAccessToEveryone(insecureCacheHome);
                EnvironmentHelpers.SetEnvironmentVariable("LOCALAPPDATA", insecureCacheHome);
                EnvironmentHelpers.SetEnvironmentVariable("TMPDIR", tempRoot + Path.DirectorySeparatorChar);
                EnvironmentHelpers.SetEnvironmentVariable("TMP", tempRoot + Path.DirectorySeparatorChar);
                EnvironmentHelpers.SetEnvironmentVariable("TEMP", tempRoot + Path.DirectorySeparatorChar);
                EnvironmentHelpers.SetEnvironmentVariable("XDG_CACHE_HOME", insecureCacheHome);
                CreateTracerHome(tracerHome);

                using var agent = MockTracerAgent.Create(output, TcpPortProvider.GetOpenPort());
                var agentUrl = $"http://localhost:{agent.Port}";
                var commandLine = $"ci configure jenkins --tracer-home {tracerHome} --agent-url {agentUrl}";

                using var console = ConsoleHelper.Redirect();
                var result = Program.Main(commandLine.Split(' '));
                result.Should().Be(0);

                var environmentVariables = GetJenkinsEnvironmentVariables(console.ReadLines());
                environmentVariables["DD_DOTNET_TRACER_HOME"].Should().Be(Path.GetFullPath(tracerHome));
            }
            finally
            {
                DeleteDirectory(tempRoot);
            }
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        [EnvironmentRestorer("LOCALAPPDATA", "TMPDIR", "TMP", "TEMP", "XDG_CACHE_HOME")]
        public void ConfigureCiUsesDifferentCachedTracerHomeWhenTracerAssemblyVersionChanges()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), $"dd-trace-runner-temp-{Guid.NewGuid():N}");
            var cacheHome = Path.Combine(tempRoot, "cache");
            var tracerHome = Path.Combine(
                tempRoot,
                "long-tracer-home-path-for-reduce-path-length",
                "very-long-segment-to-force-cache-path-selection",
                "another-very-long-segment-to-force-cache-path-selection",
                "home");
            var assemblyTimestamp = DateTime.UtcNow.AddMinutes(-1);

            try
            {
                CreateTrustedCacheHome(cacheHome);
                EnvironmentHelpers.SetEnvironmentVariable("LOCALAPPDATA", cacheHome);
                EnvironmentHelpers.SetEnvironmentVariable("TMPDIR", tempRoot + Path.DirectorySeparatorChar);
                EnvironmentHelpers.SetEnvironmentVariable("TMP", tempRoot + Path.DirectorySeparatorChar);
                EnvironmentHelpers.SetEnvironmentVariable("TEMP", tempRoot + Path.DirectorySeparatorChar);
                EnvironmentHelpers.SetEnvironmentVariable("XDG_CACHE_HOME", cacheHome);
                CreateTracerHome(tracerHome, GetDummyAssemblyPath("V1"), assemblyTimestamp);

                using var agent = MockTracerAgent.Create(output, TcpPortProvider.GetOpenPort());
                var agentUrl = $"http://localhost:{agent.Port}";
                var commandLine = $"ci configure jenkins --tracer-home {tracerHome} --agent-url {agentUrl}";

                using var console = ConsoleHelper.Redirect();
                var result = Program.Main(commandLine.Split(' '));
                result.Should().Be(0);

                var firstEnvironmentVariables = GetJenkinsEnvironmentVariables(console.ReadLines());
                var firstConfiguredTracerHome = firstEnvironmentVariables["DD_DOTNET_TRACER_HOME"];

                CopyTracerAssembly(tracerHome, GetDummyAssemblyPath("V2"), assemblyTimestamp);

                using var secondConsole = ConsoleHelper.Redirect();
                var secondResult = Program.Main(commandLine.Split(' '));
                secondResult.Should().Be(0);

                var secondEnvironmentVariables = GetJenkinsEnvironmentVariables(secondConsole.ReadLines());
                var secondConfiguredTracerHome = secondEnvironmentVariables["DD_DOTNET_TRACER_HOME"];
                var secondCachedTracerAssembly = Path.Combine(secondConfiguredTracerHome, "netstandard2.0", "Datadog.Trace.dll");

                secondConfiguredTracerHome.Should().NotBe(firstConfiguredTracerHome);
                AssemblyName.GetAssemblyName(secondCachedTracerAssembly).Version.Should().Be(new Version(2, 0, 0, 0));
            }
            finally
            {
                DeleteDirectory(tempRoot);
            }
        }

        [SkippableTheory]
        [Trait("RunOnWindows", "True")]
        [InlineData("TF_BUILD", "1", 0, "Detected CI AzurePipelines.")]
        [InlineData("GITHUB_SHA", "1", 0, "Detected CI GithubActions.")]
        [InlineData("Nope", "0", 1, "Failed to autodetect CI.")]
        public void AutodetectCi(string key, string value, int expectedStatusCode, string expectedMessage)
        {
            var originalEnvVars = Environment.GetEnvironmentVariables();

            // Clear all environment variables
            foreach (string envKey in originalEnvVars.Keys)
            {
                Environment.SetEnvironmentVariable(envKey, null);
            }

            try
            {
                Environment.SetEnvironmentVariable(key, value);

                using var agent = MockTracerAgent.Create(output, TcpPortProvider.GetOpenPort());
                var agentUrl = $"http://localhost:{agent.Port}";

                var commandLine = $"ci configure --tracer-home tracerHome --agent-url {agentUrl}";

                using var console = ConsoleHelper.Redirect();

                var result = Program.Main(commandLine.Split(' '));

                result.Should().Be(expectedStatusCode);

                console.Output.Should().Contain(expectedMessage);
            }
            finally
            {
                // Clear all environment variables
                foreach (string envKey in Environment.GetEnvironmentVariables().Keys)
                {
                    Environment.SetEnvironmentVariable(envKey, null);
                }

                // Restore all environment variables
                foreach (string envKey in originalEnvVars.Keys)
                {
                    Environment.SetEnvironmentVariable(envKey, (string)originalEnvVars[envKey]);
                }
            }
        }

        private static Dictionary<string, string> GetJenkinsEnvironmentVariables(IEnumerable<string> lines)
        {
            var environmentVariables = new Dictionary<string, string>();

            foreach (var line in lines)
            {
                var match = Regex.Match(line, @"(?<name>[A-Z1-9_]+)=(?<value>.*)");
                if (match.Success)
                {
                    environmentVariables.Add(match.Groups["name"].Value, match.Groups["value"].Value);
                }
            }

            return environmentVariables;
        }

        private static void CreateTracerHome(string tracerHome, string tracerAssemblyPath = null, DateTime? tracerAssemblyLastWriteTimeUtc = null)
        {
            CreateFile(tracerHome, "metadata.txt");
            CreateFile(tracerHome, "netstandard2.0", "Datadog.Trace.MSBuild.dll");
            if (tracerAssemblyPath is not null)
            {
                CopyTracerAssembly(tracerHome, tracerAssemblyPath, tracerAssemblyLastWriteTimeUtc);
            }

            CreateNativeTracerFiles(tracerHome, "win-x86", "Datadog.Trace.ClrProfiler.Native.dll", "Datadog.Tracer.Native.dll", "Datadog.Profiler.Native.dll");
            CreateNativeTracerFiles(tracerHome, "win-x64", "Datadog.Trace.ClrProfiler.Native.dll", "Datadog.Tracer.Native.dll", "Datadog.Profiler.Native.dll");
            CreateNativeTracerFiles(tracerHome, "win-ARM64EC", "Datadog.Trace.ClrProfiler.Native.dll", "Datadog.Tracer.Native.dll", "Datadog.Profiler.Native.dll");
            CreateNativeTracerFiles(tracerHome, "linux-x64", "Datadog.Trace.ClrProfiler.Native.so", "Datadog.Tracer.Native.so", "Datadog.Profiler.Native.so");
            CreateFile(tracerHome, "linux-x64", "Datadog.Linux.ApiWrapper.x64.so");
            CreateNativeTracerFiles(tracerHome, "linux-musl-x64", "Datadog.Trace.ClrProfiler.Native.so", "Datadog.Tracer.Native.so", "Datadog.Profiler.Native.so");
            CreateFile(tracerHome, "linux-musl-x64", "Datadog.Linux.ApiWrapper.x64.so");
            CreateNativeTracerFiles(tracerHome, "linux-arm64", "Datadog.Trace.ClrProfiler.Native.so", "Datadog.Tracer.Native.so", "Datadog.Profiler.Native.so");
            CreateFile(tracerHome, "linux-arm64", "Datadog.Linux.ApiWrapper.x64.so");
            CreateNativeTracerFiles(tracerHome, "linux-musl-arm64", "Datadog.Trace.ClrProfiler.Native.so", "Datadog.Tracer.Native.so", "Datadog.Profiler.Native.so");
            CreateFile(tracerHome, "linux-musl-arm64", "Datadog.Linux.ApiWrapper.x64.so");
            CreateNativeTracerFiles(tracerHome, "osx", "Datadog.Trace.ClrProfiler.Native.dylib", "Datadog.Tracer.Native.dylib");
        }

        private static void CreateNativeTracerFiles(string tracerHome, string platformDirectory, string profilerFileName, string nativeTracerFileName, string profilerEngineFileName = null)
        {
            CreateFile(tracerHome, platformDirectory, profilerFileName);
            CreateFile(tracerHome, platformDirectory, "loader.conf");
            CreateFile(tracerHome, platformDirectory, nativeTracerFileName);
            if (profilerEngineFileName is not null)
            {
                CreateFile(tracerHome, platformDirectory, profilerEngineFileName);
            }
        }

        private static string GetNativeTracerFileName()
        {
            if (FrameworkDescription.Instance.OSPlatform == OSPlatformName.Windows)
            {
                return "Datadog.Tracer.Native.dll";
            }

            if (FrameworkDescription.Instance.OSPlatform == OSPlatformName.MacOS)
            {
                return "Datadog.Tracer.Native.dylib";
            }

            return "Datadog.Tracer.Native.so";
        }

        private static void CreateFile(string rootPath, params string[] pathParts)
        {
            var path = Path.Combine(new[] { rootPath }.Concat(pathParts).ToArray());
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "source");
        }

        private static void CopyTracerAssembly(string tracerHome, string sourceAssemblyPath, DateTime? lastWriteTimeUtc)
        {
            var targetAssemblyPath = Path.Combine(tracerHome, "netstandard2.0", "Datadog.Trace.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(targetAssemblyPath));
            File.Copy(sourceAssemblyPath, targetAssemblyPath, overwrite: true);
            if (lastWriteTimeUtc.HasValue)
            {
                File.SetLastWriteTimeUtc(targetAssemblyPath, lastWriteTimeUtc.Value);
            }
        }

        private static string GetDummyAssemblyPath(string versionFolder)
        {
            var rootFolder = Path.GetDirectoryName(typeof(ConfigureCiCommandTests).Assembly.Location);
            return Path.GetFullPath(Path.Combine(rootFolder, versionFolder, "DummyLibrary.dll"));
        }

        private static void SetDirectoryMode(string path, string mode)
        {
            var processStartInfo = new ProcessStartInfo("chmod")
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };
            processStartInfo.ArgumentList.Add(mode);
            processStartInfo.ArgumentList.Add(path);

            using var process = Process.Start(processStartInfo);
            process.WaitForExit();
            process.ExitCode.Should().Be(0, process.StandardError.ReadToEnd());
        }

        private static string GetDirectoryMode(string path)
        {
            var processStartInfo = new ProcessStartInfo(RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "/usr/bin/stat" : "stat")
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                processStartInfo.ArgumentList.Add("-f");
                processStartInfo.ArgumentList.Add("%Lp");
            }
            else
            {
                processStartInfo.ArgumentList.Add("-c");
                processStartInfo.ArgumentList.Add("%a");
            }

            processStartInfo.ArgumentList.Add(path);

            using var process = Process.Start(processStartInfo);
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            process.ExitCode.Should().Be(0, error);
            return output.Trim();
        }

        private static void CreateTrustedCacheHome(string path)
        {
            Directory.CreateDirectory(path);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                RestrictWindowsDirectoryToTrustedWriters(path);
            }
            else
            {
                SetDirectoryMode(path, "700");
            }
        }

#pragma warning disable CA1416 // Windows ACL setup is only called after a RuntimeInformation Windows guard.
        private static void RestrictWindowsDirectoryToTrustedWriters(string path)
        {
            var currentUserSid = WindowsIdentity.GetCurrent().User?.Value;
            currentUserSid.Should().NotBeNullOrEmpty("the test cache directory needs an explicit ACE for the current user");

            RunIcacls(
                path,
                "/inheritance:r",
                "/grant:r",
                $"*{currentUserSid}:(OI)(CI)F",
                "*S-1-5-18:(OI)(CI)F",
                "*S-1-5-32-544:(OI)(CI)F");
        }
#pragma warning restore CA1416

        private static void GrantWindowsModifyAccessToEveryone(string path)
        {
            RunIcacls(path, "/grant", "*S-1-1-0:(OI)(CI)M");
        }

        private static void RunIcacls(string path, params string[] arguments)
        {
            var processStartInfo = new ProcessStartInfo("icacls")
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };

            processStartInfo.ArgumentList.Add(path);
            foreach (var argument in arguments)
            {
                processStartInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(processStartInfo);
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            process.ExitCode.Should().Be(0, output + error);
        }

        private static void DeleteDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }

        private static Dictionary<string, string> SetStaleRunScopedBackfillEnvironment()
        {
            var originalEnvironment = new Dictionary<string, string>();
            foreach (var environmentVariable in RunScopedBackfillEnvironmentVariables)
            {
                originalEnvironment[environmentVariable] = Environment.GetEnvironmentVariable(environmentVariable);
                EnvironmentHelpers.SetEnvironmentVariable(environmentVariable, "stale-run-scoped-value");
            }

            return originalEnvironment;
        }

        private static void RestoreEnvironment(Dictionary<string, string> originalEnvironment)
        {
            foreach (var environmentVariable in originalEnvironment)
            {
                EnvironmentHelpers.SetEnvironmentVariable(environmentVariable.Key, environmentVariable.Value);
            }
        }
    }
}
