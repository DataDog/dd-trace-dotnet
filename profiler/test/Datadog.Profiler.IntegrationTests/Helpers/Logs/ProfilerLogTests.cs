// <copyright file="ProfilerLogTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;
using Xunit.Sdk;

namespace Datadog.Profiler.IntegrationTests.Helpers.Logs
{
    [Trait("Category", "TestInfrastructure")]
    public class ProfilerLogTests : IDisposable
    {
        private readonly string _directory;

        public ProfilerLogTests()
        {
            _directory = Path.Combine(Path.GetTempPath(), "dd-profiler-logs-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_directory))
                {
                    Directory.Delete(_directory, true);
                }
            }
            catch (IOException)
            {
            }
        }

        [Fact]
        public void TypedGroups_SelectNativeLoaderAndXUnitFiles()
        {
            Write("DD-DotNet-Profiler-Native-app-1.log", "native");
            Write("DD-DotNet-Profiler-Managed-app-1.log", "managed");
            Write("DD-DotNet-Common-ManagedLoader-app-1.log", "loader");
            Write("xunit.txt", "xunit");
            Write("xunitAtxt", "not the xunit log");

            var logs = ProfilerLogSet.Read(_directory);

            logs.NativeProfiler.Files.Should().ContainSingle()
                .Which.FileName.Should().Be("DD-DotNet-Profiler-Native-app-1.log");
            logs.ManagedLoader.Files.Should().ContainSingle()
                .Which.FileName.Should().Be("DD-DotNet-Common-ManagedLoader-app-1.log");
            logs.XUnit.Files.Should().ContainSingle()
                .Which.FileName.Should().Be("xunit.txt");
            logs.Matching(ProfilerLogSet.ManagedProfilerPattern).Files.Should().ContainSingle()
                .Which.FileName.Should().Be("DD-DotNet-Profiler-Managed-app-1.log");
            logs.Matching("xunit.txt").Files.Should().ContainSingle()
                .Which.FileName.Should().Be("xunit.txt");
        }

        [Fact]
        public void ManagedGroups_ZeroMatchingFiles_DoNotContainFiles()
        {
            Write("DD-DotNet-Profiler-Native-app-1.log", "native");

            var logs = ProfilerLogSet.Read(_directory);

            logs.ManagedLoader.Files.Should().BeEmpty();
            logs.ManagedLoader.Should().NotContainFiles();
            logs.Matching(ProfilerLogSet.ManagedProfilerPattern).Should().NotContainFiles();
        }

        [Fact]
        public void NativeProfiler_OneFile_ContainsTheLine()
        {
            Write("DD-DotNet-Profiler-Native-app-1.log", "[time | info |] Configuration: DD_PROFILING_LOCK_ENABLED=1");

            var logs = ProfilerLogSet.Read(_directory);

            logs.NativeProfiler.Files.Should().ContainSingle();
            logs.NativeProfiler.Should().ContainLine("Configuration: DD_PROFILING_LOCK_ENABLED");
            logs.NativeProfiler.Should().NotContainErrors();
        }

        [Fact]
        public void NativeProfiler_SeveralFiles_ContainLineSearchesEachFile()
        {
            Write("DD-DotNet-Profiler-Native-app-1.log", "started");
            Write("nested/DD-DotNet-Profiler-Native-app-2.log", "Configuration: DD_PROFILING_LOCK_ENABLED=0");

            var logs = ProfilerLogSet.Read(_directory);

            logs.NativeProfiler.Files.Should().HaveCount(2);
            logs.NativeProfiler.Should().ContainLine("Configuration: DD_PROFILING_LOCK_ENABLED");
        }

        [Fact]
        public void NativeProfiler_RequiredFileMissing_NamesThePatternAndDirectory()
        {
            Write("xunit.txt", "only the test log");

            var logs = ProfilerLogSet.Read(_directory);
            var message = AssertFailure(() => logs.NativeProfiler.Should().ContainLine("Configuration: DD_PROFILING_LOCK_ENABLED"));

            message.Should().Contain($"no file found for {ProfilerLogSet.NativeProfilerPattern} in {_directory}");
        }

        [Fact]
        public void NativeProfiler_NotContainErrors_RequiresAMatchingFile()
        {
            var logs = ProfilerLogSet.Read(_directory);
            const string pattern = "DD-DotNet-Profiler-Native*-9.log";

            var message = AssertFailure(() => logs.NativeProfiler.Matching(pattern).Should().NotContainErrors());

            message.Should().Contain($"no file found for {pattern} in {_directory}");
        }

        [Fact]
        public void NativeProfiler_RequiredLineMissing_ListsEveryFileRead()
        {
            var first = "DD-DotNet-Profiler-Native-app-1.log";
            var second = Path.Combine("nested", "DD-DotNet-Profiler-Native-app-2.log");
            Write(first, "info | started");
            Write(second, "info | running");

            var logs = ProfilerLogSet.Read(_directory);
            var message = AssertFailure(() => logs.NativeProfiler.Should().ContainLine("Configuration: DD_PROFILING_LOCK_ENABLED"));

            message.Should().Contain(Path.Combine(_directory, first));
            message.Should().Contain(Path.Combine(_directory, second));
            message.Should().NotContain("no file found");
        }

        [Fact]
        public void NotContainLine_ReportsTheFileAndTheMatchingLine()
        {
            const string line = "Configuration: DD_PROFILING_LOCK_ENABLED=1";
            var relativePath = "DD-DotNet-Profiler-Native-app-4.log";
            Write(relativePath, "other", line);

            var logs = ProfilerLogSet.Read(_directory);
            var message = AssertFailure(() => logs.NativeProfiler.Should().NotContainLine("DD_PROFILING_LOCK_ENABLED"));

            message.Should().Contain(Path.Combine(_directory, relativePath));
            message.Should().Contain(line);
        }

        [Fact]
        public void NotContainFiles_ReportsTheMatchingPath()
        {
            var relativePath = "DD-DotNet-Common-ManagedLoader-app-1.log";
            Write(relativePath, "loader started");

            var logs = ProfilerLogSet.Read(_directory);
            var message = AssertFailure(() => logs.ManagedLoader.Should().NotContainFiles());

            message.Should().Contain(Path.Combine(_directory, relativePath));
            message.Should().Contain(ProfilerLogSet.ManagedLoaderPattern);
            message.Should().Contain(_directory);
        }

        [Fact]
        public void NotContainErrors_SkipsAllowedLines_AndReportsTheOthers()
        {
            Write(
                "DD-DotNet-Profiler-Native-app-7.log",
                "[2020-01-01 | error | PId: 7 | TId: 1] operation timed out");
            var rejectedPath = Path.Combine("rejected", "DD-DotNet-Profiler-Native-app-8.log");
            Write(
                rejectedPath,
                "[2020-01-01 | ERROR | PId: 8 | TId: 1] exporter failed for real");

            var logs = ProfilerLogSet.Read(_directory);
            logs.NativeProfiler.Matching("DD-DotNet-Profiler-Native*-7.log")
                .Should().NotContainErrors("operation timed out");

            var message = AssertFailure(() =>
                logs.NativeProfiler.Matching("DD-DotNet-Profiler-Native*-8.log")
                    .Should().NotContainErrors("operation timed out"));

            message.Should().Contain(Path.Combine(_directory, rejectedPath));
            message.Should().Contain("exporter failed for real");
            message.Should().NotContain("operation timed out");
        }

        [Fact]
        public void NotContainErrors_NullAllowedList_RejectsEveryErrorLine()
        {
            const string line = "[2020-01-01 | error | PId: 7 | TId: 1] operation timed out";
            Write("DD-DotNet-Profiler-Native-app-7.log", line);

            var logs = ProfilerLogSet.Read(_directory);
            var message = AssertFailure(() => logs.NativeProfiler.Should().NotContainErrors(null));

            message.Should().Contain(line);
        }

        [Fact]
        public void Matching_ProcessLogGlob_SelectsOneFileAmongSeveral()
        {
            Write("DD-DotNet-Profiler-Native-app-3.log", "[t | error | PId: 3] bad");
            Write("DD-DotNet-Profiler-Native-app-4.log", "[t | info | PId: 4] ok");

            var logs = ProfilerLogSet.Read(_directory);

            logs.NativeProfiler.Matching("DD-DotNet-Profiler-Native*-4.log").Should().NotContainErrors();
            logs.NativeProfiler.Matching("DD-DotNet-Profiler-Native*-4.log").Files.Should().ContainSingle();

            var message = AssertFailure(() =>
                logs.NativeProfiler.Matching("DD-DotNet-Profiler-Native*-3.log").Should().NotContainErrors());

            message.Should().Contain("DD-DotNet-Profiler-Native-app-3.log");
            message.Should().NotContain("DD-DotNet-Profiler-Native-app-4.log");
        }

        [Fact]
        public void Read_WhileAnotherStreamAppends_ReturnsTheFlushedLines()
        {
            var path = Path.Combine(_directory, "xunit.txt");
            using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(stream) { AutoFlush = true })
            {
                writer.WriteLine("first line");

                var log = ProfilerLogFile.Read(path);

                log.Path.Should().Be(path);
                log.Lines.Should().ContainSingle().Which.Should().Be("first line");

                writer.WriteLine("second line");
            }

            ProfilerLogFile.Read(path).Lines.Should().Equal("first line", "second line");
        }

        [Fact]
        public void Search_ByExactLine_Substring_AndRegularExpression()
        {
            const string line = "[time | info |] Configuration: DD_PROFILING_LOCK_ENABLED=1";
            Write("DD-DotNet-Profiler-Native-app-1.log", line);

            var native = ProfilerLogSet.Read(_directory).NativeProfiler;

            native.LinesEqualTo(line).Should().ContainSingle();
            native.LinesEqualTo("Configuration: DD_PROFILING_LOCK_ENABLED").Should().BeEmpty();
            native.LinesContaining("Configuration: DD_PROFILING_LOCK_ENABLED").Should().ContainSingle();
            native.LinesMatching(new Regex("DD_PROFILING_LOCK_ENABLED=1")).Should().ContainSingle();
            native.LinesMatching(new Regex("DD_PROFILING_DOES_NOT_EXIST")).Should().BeEmpty();
            native.Should().ContainLine("Configuration: DD_PROFILING_LOCK_ENABLED");
        }

        private static string AssertFailure(Action action)
        {
            var exception = Assert.Throws<XunitException>(action);
            return exception.Message;
        }

        private void Write(string relativePath, params string[] lines)
        {
            var path = Path.Combine(_directory, relativePath);
            var parent = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(parent))
            {
                Directory.CreateDirectory(parent);
            }

            File.WriteAllLines(path, lines);
        }
    }
}
