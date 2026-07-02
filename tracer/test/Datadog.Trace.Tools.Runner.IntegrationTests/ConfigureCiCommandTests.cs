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
        [EnvironmentRestorer("TMPDIR", "TMP", "TEMP", "XDG_CACHE_HOME")]
        public void ConfigureCiUsesCachedTemporaryTracerHomeWhenReducingPathLength()
        {
            using var setup = ConfigureCiTestSetup.Create(output);
            setup.CreateTrustedCacheHome();
            Directory.CreateDirectory(setup.FixedTempTracerHome);
            setup.UseCacheHome();
            setup.CreateTracerHome();

            var environmentVariables = setup.RunConfigureCi();
            var configuredTracerHome = environmentVariables["DD_DOTNET_TRACER_HOME"];
            var cachedMetadata = Path.Combine(configuredTracerHome, "metadata.txt");

            configuredTracerHome.Should().NotBe(setup.FixedTempTracerHome);
            configuredTracerHome.Should().StartWith(setup.ExpectedCacheRoot);
            File.Exists(cachedMetadata).Should().BeTrue();
            environmentVariables["CORECLR_PROFILER_PATH_64"].Should().StartWith(configuredTracerHome);

            File.WriteAllText(cachedMetadata, "cached");

            var secondEnvironmentVariables = setup.RunConfigureCi();
            secondEnvironmentVariables["DD_DOTNET_TRACER_HOME"].Should().Be(configuredTracerHome);
            File.ReadAllText(cachedMetadata).Should().Be("source");
        }

        [SkippableTheory]
        [Trait("RunOnWindows", "True")]
        [InlineData("profiler")]
        [InlineData("loader-config")]
        [InlineData("native-tracer")]
        [InlineData("managed-dependency")]
        [InlineData("profiler-engine")]
        [InlineData("injected-file")]
        [EnvironmentRestorer("TMPDIR", "TMP", "TEMP", "XDG_CACHE_HOME")]
        public void ConfigureCiRebuildsCachedTracerHomeWhenCachedContentIsModified(string cachedContent)
        {
            using var setup = ConfigureCiTestSetup.Create(output);
            setup.CreateTrustedCacheHome();
            setup.UseCacheHome();
            setup.CreateTracerHome();
            if (cachedContent == "managed-dependency")
            {
                CreateFile(setup.TracerHome, "netstandard2.0", "Datadog.Trace.Dependency.dll");
            }

            var environmentVariables = setup.RunConfigureCi();
            var configuredTracerHome = environmentVariables["DD_DOTNET_TRACER_HOME"];
            var cachedFilePath = GetCachedContentPath(cachedContent, environmentVariables);
            var expectedContent = cachedContent == "injected-file" ? null : File.ReadAllBytes(cachedFilePath);

            File.WriteAllText(cachedFilePath, "tampered");

            var secondEnvironmentVariables = setup.RunConfigureCi();
            secondEnvironmentVariables["DD_DOTNET_TRACER_HOME"].Should().Be(configuredTracerHome);

            if (cachedContent == "injected-file")
            {
                File.Exists(cachedFilePath).Should().BeFalse();
            }
            else
            {
                File.ReadAllBytes(cachedFilePath).Should().Equal(expectedContent);
            }
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        [EnvironmentRestorer("TMPDIR", "TMP", "TEMP", "XDG_CACHE_HOME")]
        public void ConfigureCiFallsBackToOriginalTracerHomeWhenCachedTracerHomePathIsFile()
        {
            using var setup = ConfigureCiTestSetup.Create(output);
            var cachedTracerHome = setup.CreateReadyCachedTracerHome();

            DeleteDirectory(cachedTracerHome);
            File.WriteAllText(cachedTracerHome, "attacker-controlled");

            var environmentVariables = setup.RunConfigureCi();
            environmentVariables["DD_DOTNET_TRACER_HOME"].Should().Be(setup.ExpectedOriginalTracerHome);
            File.ReadAllText(cachedTracerHome).Should().Be("attacker-controlled");
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        [EnvironmentRestorer("TMPDIR", "TMP", "TEMP", "XDG_CACHE_HOME")]
        public void ConfigureCiRebuildsCachedTracerHomeWhenCachedTracerHomeDirectoryWasPrecreated()
        {
            using var setup = ConfigureCiTestSetup.Create(output);
            var cachedTracerHome = setup.CreateReadyCachedTracerHome();

            DeleteDirectory(cachedTracerHome);
            CreatePrivateCacheDirectory(cachedTracerHome);
            File.WriteAllText(Path.Combine(cachedTracerHome, ".dd-trace-runner-cache"), "fake-marker");
            File.WriteAllText(Path.Combine(cachedTracerHome, "metadata.txt"), "tampered");

            var environmentVariables = setup.RunConfigureCi();
            environmentVariables["DD_DOTNET_TRACER_HOME"].Should().Be(cachedTracerHome);
            File.ReadAllText(Path.Combine(cachedTracerHome, "metadata.txt")).Should().Be("source");
        }

        [SkippableFact]
        [EnvironmentRestorer("TMPDIR", "TMP", "TEMP", "XDG_CACHE_HOME")]
        public void ConfigureCiFallsBackToOriginalTracerHomeWhenCachedTracerHomePathIsSymbolicLink()
        {
            SkipOn.Platform(SkipOn.PlatformValue.Windows);

            using var setup = ConfigureCiTestSetup.Create(output);
            var cachedTracerHome = setup.CreateReadyCachedTracerHome();
            var cacheTarget = Path.Combine(setup.TempRoot, "attacker-cache-target");

            DeleteDirectory(cachedTracerHome);
            Directory.CreateDirectory(cacheTarget);
            SetDirectoryMode(cacheTarget, "700");
            RunProcess("ln", "-s", cacheTarget, cachedTracerHome);

            var environmentVariables = setup.RunConfigureCi();
            environmentVariables["DD_DOTNET_TRACER_HOME"].Should().Be(setup.ExpectedOriginalTracerHome);
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        [EnvironmentRestorer("TMPDIR", "TMP", "TEMP", "XDG_CACHE_HOME")]
        public void ConfigureCiFallsBackToOriginalTracerHomeWhenCacheLockPathIsDirectory()
        {
            using var setup = ConfigureCiTestSetup.Create(output);
            var cachedTracerHome = setup.CreateReadyCachedTracerHome();
            var lockPath = cachedTracerHome + ".lock";

            DeleteFile(lockPath);
            Directory.CreateDirectory(lockPath);

            var environmentVariables = setup.RunConfigureCi();
            environmentVariables["DD_DOTNET_TRACER_HOME"].Should().Be(setup.ExpectedOriginalTracerHome);
        }

        [SkippableFact]
        [EnvironmentRestorer("TMPDIR", "TMP", "TEMP", "XDG_CACHE_HOME")]
        public void ConfigureCiFallsBackToOriginalTracerHomeWhenCacheLockPathIsSymbolicLink()
        {
            SkipOn.Platform(SkipOn.PlatformValue.Windows);

            using var setup = ConfigureCiTestSetup.Create(output);
            var cachedTracerHome = setup.CreateReadyCachedTracerHome();
            var lockPath = cachedTracerHome + ".lock";
            var lockTarget = Path.Combine(setup.TempRoot, "attacker-lock");

            DeleteFile(lockPath);
            File.WriteAllText(lockTarget, "attacker-controlled");
            RunProcess("ln", "-s", lockTarget, lockPath);

            var environmentVariables = setup.RunConfigureCi();
            environmentVariables["DD_DOTNET_TRACER_HOME"].Should().Be(setup.ExpectedOriginalTracerHome);
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        [EnvironmentRestorer("TMPDIR", "TMP", "TEMP", "XDG_CACHE_HOME")]
        public void ConfigureCiRetriesConcurrentCacheLock()
        {
            using var setup = ConfigureCiTestSetup.Create(output);
            var cachedTracerHome = setup.CreateReadyCachedTracerHome();
            var lockPath = cachedTracerHome + ".lock";
            using var heldLock = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            var retryCount = 0;

            var configuredTracerHome = TracerHomeCache.GetOrCreateCachedTracerHomeIfShorter(
                setup.TracerHome,
                _ =>
                {
                    retryCount++;
                    heldLock.Dispose();
                });

            retryCount.Should().Be(1);
            configuredTracerHome.Should().Be(cachedTracerHome);
            Directory.Exists(cachedTracerHome).Should().BeTrue();
        }

        [SkippableFact]
        [EnvironmentRestorer("TMPDIR", "TMP", "TEMP", "XDG_CACHE_HOME")]
        public void ConfigureCiFallsBackToOriginalTracerHomeWhenSourceTracerHomeContainsSymbolicLink()
        {
            SkipOn.Platform(SkipOn.PlatformValue.Windows);

            using var setup = ConfigureCiTestSetup.Create(output);
            setup.CreateTrustedCacheHome();
            setup.UseCacheHome();
            setup.CreateTracerHome();

            var sourceMetadata = Path.Combine(setup.TracerHome, "metadata.txt");
            var symlinkTarget = Path.Combine(setup.TempRoot, "attacker-metadata.txt");
            File.WriteAllText(symlinkTarget, "attacker-controlled");
            File.Delete(sourceMetadata);
            RunProcess("ln", "-s", symlinkTarget, sourceMetadata);

            var environmentVariables = setup.RunConfigureCi();
            environmentVariables["DD_DOTNET_TRACER_HOME"].Should().Be(setup.ExpectedOriginalTracerHome);
        }

        [SkippableTheory]
        [Trait("RunOnWindows", "True")]
        [InlineData(".dd-trace-runner-cache")]
        [InlineData(".dd-trace-runner-cache.integrity")]
        [EnvironmentRestorer("TMPDIR", "TMP", "TEMP", "XDG_CACHE_HOME")]
        public void ConfigureCiFallsBackToOriginalTracerHomeWhenSourceTracerHomeContainsReservedCacheMetadata(string reservedFileName)
        {
            using var setup = ConfigureCiTestSetup.Create(output);
            setup.CreateTrustedCacheHome();
            setup.UseCacheHome();
            setup.CreateTracerHome();
            File.WriteAllText(Path.Combine(setup.TracerHome, reservedFileName), "reserved");

            var environmentVariables = setup.RunConfigureCi();
            environmentVariables["DD_DOTNET_TRACER_HOME"].Should().Be(setup.ExpectedOriginalTracerHome);
        }

        [SkippableFact]
        [EnvironmentRestorer("TMPDIR", "TMP", "TEMP", "XDG_CACHE_HOME")]
        public void ConfigureCiCreatesPrivateCachedTracerHomeOnPosix()
        {
            SkipOn.Platform(SkipOn.PlatformValue.Windows);

            using var setup = ConfigureCiTestSetup.Create(output);
            setup.CreateTrustedCacheHome();
            setup.UseCacheHome();
            setup.CreateTracerHome();

            var environmentVariables = setup.RunConfigureCi();
            var configuredTracerHome = environmentVariables["DD_DOTNET_TRACER_HOME"];
            configuredTracerHome.Should().StartWith(setup.ExpectedCacheRoot);
            GetDirectoryMode(configuredTracerHome).Should().Be("700");
        }

        [SkippableFact]
        [EnvironmentRestorer("TMPDIR", "TMP", "TEMP", "XDG_CACHE_HOME", "HOME")]
        public void ConfigureCiUsesHomeDotCacheWhenXdgCacheHomeIsEmptyOnPosix()
        {
            SkipOn.Platform(SkipOn.PlatformValue.Windows);
            SkipIfNativeMetadataHelperUnavailableOnPosix();

            using var setup = ConfigureCiTestSetup.Create(output);
            setup.CreateTracerHome();
            CopyCurrentPlatformNativeTracer(setup.TracerHome);
            EnvironmentHelpers.SetEnvironmentVariable("XDG_CACHE_HOME", string.Empty);
            EnvironmentHelpers.SetEnvironmentVariable("HOME", setup.TempRoot);

            var environmentVariables = setup.RunConfigureCi();
            var configuredTracerHome = environmentVariables["DD_DOTNET_TRACER_HOME"];
            var expectedCacheRoot = Path.GetFullPath(
                Path.Combine(setup.TempRoot, ".cache", "Datadog", "dd-trace", "runner", "tracer-home")) + Path.DirectorySeparatorChar;
            configuredTracerHome.Should().StartWith(expectedCacheRoot);
            GetDirectoryMode(configuredTracerHome).Should().Be("700");
        }

        [SkippableFact]
        [EnvironmentRestorer("TMPDIR", "TMP", "TEMP", "XDG_CACHE_HOME")]
        public void ConfigureCiFallsBackToOriginalTracerHomeWhenNativeMetadataHelperIsUnavailableOnPosix()
        {
            SkipOn.Platform(SkipOn.PlatformValue.Windows);

            using var setup = ConfigureCiTestSetup.Create(output);
            setup.CreateTrustedCacheHome();
            setup.UseCacheHome(requireNativeMetadataHelper: false);
            setup.CreateTracerHome();
            File.WriteAllText(GetCurrentPlatformNativeTracerPath(setup.TracerHome), "not a native library");

            var environmentVariables = setup.RunConfigureCi();
            environmentVariables["DD_DOTNET_TRACER_HOME"].Should().Be(setup.ExpectedOriginalTracerHome);
        }

        [SkippableFact]
        [EnvironmentRestorer("TMPDIR", "TMP", "TEMP", "XDG_CACHE_HOME")]
        public void ConfigureCiFallsBackToOriginalTracerHomeWhenCacheRootIsSharedWritable()
        {
            SkipOn.Platform(SkipOn.PlatformValue.Windows);

            using var setup = ConfigureCiTestSetup.Create(output, "shared-cache");
            Directory.CreateDirectory(setup.CacheHome);
            SetDirectoryMode(setup.CacheHome, "777");
            setup.UseCacheHome();
            setup.CreateTracerHome();

            var environmentVariables = setup.RunConfigureCi();
            environmentVariables["DD_DOTNET_TRACER_HOME"].Should().Be(Path.GetFullPath(setup.TracerHome));
        }

        [SkippableFact]
        [EnvironmentRestorer("TMPDIR", "TMP", "TEMP", "XDG_CACHE_HOME")]
        public void ConfigureCiFallsBackToOriginalTracerHomeWhenCacheRootIsUnderSharedWritableAncestor()
        {
            SkipOn.Platform(SkipOn.PlatformValue.Windows);

            using var setup = ConfigureCiTestSetup.Create(output, Path.Combine("shared-cache-parent", "private-cache"));
            var sharedCacheParent = Path.GetDirectoryName(setup.CacheHome);
            Directory.CreateDirectory(sharedCacheParent);
            SetDirectoryMode(sharedCacheParent, "777");
            CreateTrustedCacheHome(setup.CacheHome);
            setup.UseCacheHome();
            setup.CreateTracerHome();

            var environmentVariables = setup.RunConfigureCi();
            environmentVariables["DD_DOTNET_TRACER_HOME"].Should().Be(Path.GetFullPath(setup.TracerHome));
        }

        [SkippableFact]
        [EnvironmentRestorer("TMPDIR", "TMP", "TEMP", "XDG_CACHE_HOME")]
        public void ConfigureCiFallsBackToOriginalTracerHomeWhenCacheRootIsSymbolicLink()
        {
            SkipOn.Platform(SkipOn.PlatformValue.Windows);

            using var setup = ConfigureCiTestSetup.Create(output, "cache-link");
            var cacheTarget = Path.Combine(setup.TempRoot, "cache-target");
            Directory.CreateDirectory(cacheTarget);
            SetDirectoryMode(cacheTarget, "700");
            RunProcess("ln", "-s", cacheTarget, setup.CacheHome);
            setup.UseCacheHome();
            setup.CreateTracerHome();

            var environmentVariables = setup.RunConfigureCi();
            environmentVariables["DD_DOTNET_TRACER_HOME"].Should().Be(Path.GetFullPath(setup.TracerHome));
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        [EnvironmentRestorer("TMPDIR", "TMP", "TEMP", "XDG_CACHE_HOME")]
        public void ConfigureCiUsesCachedTracerHomeWhenWindowsCacheRootHasInheritOnlyBroadWrite()
        {
            SkipOn.AllExcept(SkipOn.PlatformValue.Windows);

            using var setup = ConfigureCiTestSetup.Create(output, "cache-with-inherit-only-broad-write");
            Skip.If(setup.CacheRootExistedBefore, "Cannot safely modify the real LocalApplicationData runner cache root ACL.");
            setup.CreateTrustedCacheHome();
            GrantWindowsInheritOnlyModifyAccessToEveryone(setup.CacheRoot);
            setup.UseCacheHome();
            setup.CreateTracerHome();

            var environmentVariables = setup.RunConfigureCi();
            environmentVariables["DD_DOTNET_TRACER_HOME"].Should().StartWith(setup.ExpectedCacheRoot);
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        [EnvironmentRestorer("TMPDIR", "TMP", "TEMP", "XDG_CACHE_HOME")]
        public void ConfigureCiFallsBackToOriginalTracerHomeWhenWindowsCacheRootAllowsBroadWrites()
        {
            SkipOn.AllExcept(SkipOn.PlatformValue.Windows);

            using var setup = ConfigureCiTestSetup.Create(output, "shared-cache");
            Skip.If(setup.CacheRootExistedBefore, "Cannot safely modify the real LocalApplicationData runner cache root ACL.");
            Directory.CreateDirectory(setup.CacheRoot);
            GrantWindowsModifyAccessToEveryone(setup.CacheRoot);
            setup.UseCacheHome();
            setup.CreateTracerHome();

            var environmentVariables = setup.RunConfigureCi();
            environmentVariables["DD_DOTNET_TRACER_HOME"].Should().Be(Path.GetFullPath(setup.TracerHome));
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        [EnvironmentRestorer("TMPDIR", "TMP", "TEMP", "XDG_CACHE_HOME")]
        public void ConfigureCiUsesDifferentCachedTracerHomeWhenTracerAssemblyVersionChanges()
        {
            var assemblyTimestamp = DateTime.UtcNow.AddMinutes(-1);
            using var setup = ConfigureCiTestSetup.Create(output);
            setup.CreateTrustedCacheHome();
            setup.UseCacheHome();
            setup.CreateTracerHome(GetDummyAssemblyPath("V1"), assemblyTimestamp);

            var firstEnvironmentVariables = setup.RunConfigureCi();
            var firstConfiguredTracerHome = firstEnvironmentVariables["DD_DOTNET_TRACER_HOME"];

            CopyTracerAssembly(setup.TracerHome, GetDummyAssemblyPath("V2"), assemblyTimestamp);

            var secondEnvironmentVariables = setup.RunConfigureCi();
            var secondConfiguredTracerHome = secondEnvironmentVariables["DD_DOTNET_TRACER_HOME"];
            var secondCachedTracerAssembly = Path.Combine(secondConfiguredTracerHome, "netstandard2.0", "Datadog.Trace.dll");

            secondConfiguredTracerHome.Should().NotBe(firstConfiguredTracerHome);
            AssemblyName.GetAssemblyName(secondCachedTracerAssembly).Version.Should().Be(new Version(2, 0, 0, 0));
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

        private static string GetCachedContentPath(string cachedContent, Dictionary<string, string> environmentVariables)
        {
            var configuredTracerHome = environmentVariables["DD_DOTNET_TRACER_HOME"];
            var cachedProfilerDirectory = Path.GetDirectoryName(environmentVariables["CORECLR_PROFILER_PATH_64"]);

            return cachedContent switch
            {
                "profiler" => environmentVariables["CORECLR_PROFILER_PATH_64"],
                "loader-config" => Path.Combine(cachedProfilerDirectory, "loader.conf"),
                "native-tracer" => Path.Combine(cachedProfilerDirectory, GetNativeTracerFileName()),
                "managed-dependency" => Path.Combine(configuredTracerHome, "netstandard2.0", "Datadog.Trace.Dependency.dll"),
                "profiler-engine" => Path.Combine(configuredTracerHome, "linux-x64", "Datadog.Profiler.Native.so"),
                "injected-file" => Path.Combine(configuredTracerHome, "netstandard2.0", "Injected.Dependency.dll"),
                _ => throw new ArgumentOutOfRangeException(nameof(cachedContent), cachedContent, "Unsupported cached content target.")
            };
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

        private static void CopyCurrentPlatformNativeTracer(string tracerHome)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            var destinationPath = GetCurrentPlatformNativeTracerPath(tracerHome);
            var platformDirectory = Path.GetFileName(Path.GetDirectoryName(destinationPath));
            var nativeTracerFileName = Path.GetFileName(destinationPath);
            var nativeBuildOutputPath = Path.Combine(
                EnvironmentTools.GetSolutionDirectory(),
                "artifacts",
                "native-bin",
                "Datadog.Tracer.Native",
                nativeTracerFileName);
            var monitoringHomePath = Path.Combine(EnvironmentHelper.GetMonitoringHomePath(), platformDirectory, nativeTracerFileName);
            var sourcePath = File.Exists(nativeBuildOutputPath) ? nativeBuildOutputPath : monitoringHomePath;
            File.Exists(sourcePath).Should().BeTrue($"the reduced cache validation requires the real native tracer at {sourcePath}");
            File.Copy(sourcePath, destinationPath, overwrite: true);
        }

        private static void SkipIfNativeMetadataHelperUnavailableOnPosix()
        {
#if !NETCOREAPP3_0_OR_GREATER
            Skip.If(
                !RuntimeInformation.IsOSPlatform(OSPlatform.Windows),
                "Reduced tracer home cache validation on POSIX requires the native metadata helper.");
#endif
        }

        private static string GetCurrentPlatformNativeTracerPath(string tracerHome)
        {
            var nativeLoaderPath = EnvironmentHelper.GetNativeLoaderPath();
            var platformDirectory = Path.GetFileName(Path.GetDirectoryName(nativeLoaderPath));
            return Path.Combine(tracerHome, platformDirectory, GetNativeTracerFileName());
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

        private static void CreatePrivateCacheDirectory(string path)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                WindowsDirectoryAccess.CreatePrivateDirectory(path);
                return;
            }

            Directory.CreateDirectory(path);
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
            RunProcess("chmod", mode, path);
        }

        private static string GetDirectoryMode(string path)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return RunProcess("/usr/bin/stat", "-f", "%Lp", path).Trim();
            }

            return RunProcess("stat", "-c", "%a", path).Trim();
        }

        private static void CreateTrustedCacheHome(string path)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (!Directory.Exists(path))
                {
                    var parentDirectory = Path.GetDirectoryName(Path.GetFullPath(path));
                    if (!string.IsNullOrEmpty(parentDirectory))
                    {
                        Directory.CreateDirectory(parentDirectory);
                    }

                    WindowsDirectoryAccess.CreatePrivateDirectory(path);
                }
                else
                {
                    RestrictWindowsDirectoryToTrustedWriters(path);
                }
            }
            else
            {
                Directory.CreateDirectory(path);
                SetDirectoryMode(path, "700");
            }
        }

        private static void RestrictWindowsDirectoryToTrustedWriters(string path)
        {
            var currentUserSid = GetCurrentWindowsUserSid();
            currentUserSid.Should().NotBeNullOrWhiteSpace("the test cache directory needs an explicit ACE for the current user");

            RunIcacls(
                path,
                "/inheritance:r",
                "/grant:r",
                $"*{currentUserSid}:(OI)(CI)F",
                "*S-1-5-18:(OI)(CI)F",
                "*S-1-5-32-544:(OI)(CI)F");
        }

        private static string GetCurrentWindowsUserSid()
        {
            var output = RunProcess("whoami", "/user");
            var match = Regex.Match(output, @"S-\d(?:-\d+)+");
            match.Success.Should().BeTrue($"the current Windows user SID should be present in whoami output: {output}");
            return match.Value;
        }

        private static void GrantWindowsModifyAccessToEveryone(string path)
        {
            RunIcacls(path, "/grant", "*S-1-1-0:(OI)(CI)M");
        }

        private static void GrantWindowsInheritOnlyModifyAccessToEveryone(string path)
        {
            RunIcacls(path, "/grant", "*S-1-1-0:(OI)(CI)(IO)M");
        }

        private static void RunIcacls(string path, params string[] arguments)
        {
            RunProcess("icacls", [path, .. arguments]);
        }

        private static string RunProcess(string fileName, params string[] arguments)
        {
            var processStartInfo = new ProcessStartInfo(fileName)
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };

            foreach (var argument in arguments)
            {
                processStartInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(processStartInfo);
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            process.ExitCode.Should().Be(0, output + error);
            return output;
        }

        private static void DeleteDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }

        private static void DeleteFile(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
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

        private sealed class ConfigureCiTestSetup : IDisposable
        {
            private readonly ITestOutputHelper _output;
            private readonly List<string> _createdCachedTracerHomePaths = [];
            private readonly bool _cacheRootExistedBefore;
            private bool _copyCurrentPlatformNativeTracer;

            private ConfigureCiTestSetup(ITestOutputHelper output, string cacheDirectoryName)
            {
                _output = output;
                TempRoot = Path.Combine(Path.GetTempPath(), $"dd-trace-runner-temp-{Guid.NewGuid():N}");
                CacheHome = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                                ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                                : Path.Combine(TempRoot, cacheDirectoryName);
                CacheRoot = Path.GetFullPath(Path.Combine(CacheHome, "Datadog", "dd-trace", "runner", "tracer-home"));
                _cacheRootExistedBefore = Directory.Exists(CacheRoot);
                TracerHome = Path.Combine(
                    TempRoot,
                    "long-tracer-home-path-for-reduce-path-length",
                    "very-long-segment-to-force-cache-path-selection",
                    "another-very-long-segment-to-force-cache-path-selection",
                    "home");
            }

            public string TempRoot { get; }

            public string CacheHome { get; }

            public string CacheRoot { get; }

            public string TracerHome { get; }

            public bool CacheRootExistedBefore => _cacheRootExistedBefore;

            public string FixedTempTracerHome => Path.GetFullPath(Path.Combine(TempRoot, "dd"));

            public string ExpectedOriginalTracerHome => Path.GetFullPath(TracerHome);

            public string ExpectedCacheRoot => CacheRoot + Path.DirectorySeparatorChar;

            public static ConfigureCiTestSetup Create(ITestOutputHelper output, string cacheDirectoryName = "cache")
            {
                return new ConfigureCiTestSetup(output, cacheDirectoryName);
            }

            public void CreateTrustedCacheHome()
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    if (!Directory.Exists(CacheRoot))
                    {
                        ConfigureCiCommandTests.CreateTrustedCacheHome(CacheRoot);
                    }

                    return;
                }

                ConfigureCiCommandTests.CreateTrustedCacheHome(CacheHome);
            }

            public void CreateTracerHome(string tracerAssemblyPath = null, DateTime? tracerAssemblyLastWriteTimeUtc = null)
            {
                ConfigureCiCommandTests.CreateTracerHome(TracerHome, tracerAssemblyPath, tracerAssemblyLastWriteTimeUtc);
                if (_copyCurrentPlatformNativeTracer)
                {
                    CopyCurrentPlatformNativeTracer(TracerHome);
                }
            }

            public void UseCacheHome(bool requireNativeMetadataHelper = true)
            {
                if (requireNativeMetadataHelper)
                {
                    SkipIfNativeMetadataHelperUnavailableOnPosix();
                    _copyCurrentPlatformNativeTracer = true;
                }

                EnvironmentHelpers.SetEnvironmentVariable("TMPDIR", TempRoot + Path.DirectorySeparatorChar);
                EnvironmentHelpers.SetEnvironmentVariable("TMP", TempRoot + Path.DirectorySeparatorChar);
                EnvironmentHelpers.SetEnvironmentVariable("TEMP", TempRoot + Path.DirectorySeparatorChar);
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    EnvironmentHelpers.SetEnvironmentVariable("XDG_CACHE_HOME", CacheHome);
                }
            }

            public string CreateReadyCachedTracerHome()
            {
                CreateTrustedCacheHome();
                UseCacheHome();
                CreateTracerHome();
                return RunConfigureCi()["DD_DOTNET_TRACER_HOME"];
            }

            public Dictionary<string, string> RunConfigureCi()
            {
                using var agent = MockTracerAgent.Create(_output, TcpPortProvider.GetOpenPort());
                var commandLine = $"ci configure jenkins --tracer-home {TracerHome} --agent-url http://localhost:{agent.Port}";

                using var console = ConsoleHelper.Redirect();
                Program.Main(commandLine.Split(' ')).Should().Be(0);
                var environmentVariables = GetJenkinsEnvironmentVariables(console.ReadLines());
                TrackCachedTracerHome(environmentVariables);
                return environmentVariables;
            }

            public void Dispose()
            {
                var pathComparer = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
                foreach (var cachedTracerHomePath in _createdCachedTracerHomePaths.Distinct(pathComparer).OrderByDescending(path => path.Length))
                {
                    DeletePath(cachedTracerHomePath + ".lock");
                    DeletePath(cachedTracerHomePath);
                }

                DeleteEmptyCacheRootIfCreatedByTest();
                DeleteDirectory(TempRoot);
            }

            private void TrackCachedTracerHome(Dictionary<string, string> environmentVariables)
            {
                if (environmentVariables.TryGetValue("DD_DOTNET_TRACER_HOME", out var tracerHome))
                {
                    var configuredTracerHome = Path.GetFullPath(tracerHome);
                    var comparison = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                    if (configuredTracerHome.StartsWith(ExpectedCacheRoot, comparison))
                    {
                        _createdCachedTracerHomePaths.Add(configuredTracerHome);
                    }
                }
            }

            private void DeleteEmptyCacheRootIfCreatedByTest()
            {
                if (_cacheRootExistedBefore || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return;
                }

                if (Directory.Exists(CacheRoot) && !Directory.EnumerateFileSystemEntries(CacheRoot).Any())
                {
                    Directory.Delete(CacheRoot);
                }
            }

            private void DeletePath(string path)
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
                else if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }
}
