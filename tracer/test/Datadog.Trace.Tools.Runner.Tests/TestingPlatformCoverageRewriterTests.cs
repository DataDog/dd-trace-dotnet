// <copyright file="TestingPlatformCoverageRewriterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.Coverage.Attributes;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Util;
using FluentAssertions;
using Mono.Cecil;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

public class TestingPlatformCoverageRewriterTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dd-mtp-rewriter-" + Path.GetRandomFileName());
    private readonly string _tracerHome;
    private readonly string[] _modules;
    private readonly List<string> _backupDirectories = [];
    private readonly TestOptimizationSettings _settings = new(NullConfigurationSource.Instance, NullConfigurationTelemetry.Instance);

    public TestingPlatformCoverageRewriterTests()
    {
        // The rewrite copies Datadog.Trace.dll from the folder that matches the target framework of the assembly, netstandard2.0 for the fixture.
        _modules = ["test", "--test-modules", "**/Sample.Tests.dll", "--root-directory", _root];
        _tracerHome = Path.Combine(_root, "home");
        var tracerFolder = Directory.CreateDirectory(Path.Combine(_tracerHome, "netstandard2.0")).FullName;
        File.Copy("Datadog.Trace.dll", Path.Combine(tracerFolder, "Datadog.Trace.dll"));
        File.Copy("Datadog.Trace.pdb", Path.Combine(tracerFolder, "Datadog.Trace.pdb"));
    }

    public void Dispose()
    {
        foreach (var backupDirectory in _backupDirectories)
        {
            if (Directory.Exists(backupDirectory))
            {
                Directory.Delete(backupDirectory, recursive: true);
            }
        }

        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void RewritesTheTestAssembliesAndRestoresThemWhenDisposed()
    {
        var output = CreateOutputFolder("net10.0");
        var original = Snapshot(output);

        TryRewrite("dotnet", ["test", "--test-modules", "**/Sample.Tests.dll", "--root-directory", _root], _root, _tracerHome, _settings, out var rewriter, out var reason)
                                       .Should().BeTrue(reason);
        using (rewriter)
        {
            reason.Should().BeNull();
            rewriter!.AssembliesProcessed.Should().Be(1);
            HasCoverage(Path.Combine(output, "Sample.Tests.dll")).Should().BeTrue();
            File.Exists(Path.Combine(output, "Datadog.Trace.dll")).Should().BeTrue();
            File.ReadAllText(Path.Combine(output, "Sample.Tests.deps.json")).Should().Contain("Datadog.Trace/");
        }

        Snapshot(output).Should().Equal(original);
    }

    [Fact]
    public void RestoresWhenTheProcessExitsWithoutTheCallerDisposing()
    {
        var output = CreateOutputFolder("net10.0");
        var original = Snapshot(output);
        TryRewrite("dotnet", _modules, _root, _tracerHome, _settings, out var rewriter, out _).Should().BeTrue();
        HasCoverage(Path.Combine(output, "Sample.Tests.dll")).Should().BeTrue();

        rewriter!.SimulateProcessExit();
        rewriter.Dispose();

        Snapshot(output).Should().Equal(original);
    }

    [Fact]
    public void DisposingTwiceIsHarmless()
    {
        var output = CreateOutputFolder("net10.0");
        var original = Snapshot(output);
        TryRewrite("dotnet", _modules, _root, _tracerHome, _settings, out var rewriter, out _).Should().BeTrue();

        rewriter!.Dispose();
        rewriter.Dispose();

        Snapshot(output).Should().Equal(original);
    }

    [Fact]
    public void DoesNotRewriteTheSameFolderTwiceAtTheSameTime()
    {
        var output = CreateOutputFolder("net10.0");
        TryRewrite("dotnet", _modules, _root, _tracerHome, _settings, out var first, out _).Should().BeTrue();
        using (first)
        {
            var rewrittenHash = Hash(Path.Combine(output, "Sample.Tests.dll"));

            TryRewrite("dotnet", _modules, _root, _tracerHome, _settings, out var second, out var reason).Should().BeFalse();

            second.Should().BeNull();
            reason.Should().Contain("Another dd-trace run");
            Hash(Path.Combine(output, "Sample.Tests.dll")).Should().Be(rewrittenHash, "the second run didn't touch the files the first run is using");
        }
    }

    [Fact]
    public void RestoresTheFilesOfARunThatWasKilledBeforeItCouldRestore()
    {
        var output = CreateOutputFolder("net10.0");
        var original = Snapshot(output);
        TryRewrite("dotnet", _modules, _root, _tracerHome, _settings, out var killed, out _).Should().BeTrue();
        killed!.Abandon();
        Snapshot(output).Should().NotEqual(original, "the killed run left the assemblies rewritten");

        TryRewrite("dotnet", _modules, _root, _tracerHome, _settings, out var next, out var reason).Should().BeTrue(reason);
        next!.Dispose();

        Snapshot(output).Should().Equal(original, "the next run put back what the killed run left, then rewrote and restored in turn");
    }

    [Fact]
    public void DoesNotActOnTheRecordOfARunThatWasKilledWhileItWasStillRewriting()
    {
        // Without the completed marker the record can't tell what the killed run had already changed, so it is dropped.
        var output = CreateOutputFolder("net10.0");
        TryRewrite("dotnet", _modules, _root, _tracerHome, _settings, out var killed, out _).Should().BeTrue();
        killed!.Abandon();
        var lockPath = Path.Combine(output, TestingPlatformCoverageRewriter.LockFileName);
        var withoutMarker = string.Join(Environment.NewLine, File.ReadAllLines(lockPath).Where(line => line != "DONE"));
        File.WriteAllText(lockPath, withoutMarker);
        var rewrittenHash = Hash(Path.Combine(output, "Sample.Tests.dll"));

        TryRewrite("dotnet", _modules, _root, _tracerHome, _settings, out var next, out var reason).Should().BeTrue(reason);
        using (next)
        {
            next!.ChangedFiles.Should().BeFalse("the assemblies the killed run left rewritten are taken as they are");
        }

        Hash(Path.Combine(output, "Sample.Tests.dll")).Should().Be(rewrittenHash);
    }

    [Fact]
    public void LeavesAnAssemblyThatWasRewrittenByAnotherToolAsItFoundIt()
    {
        // Rewritten assemblies without a record, for example by the VSTest collector or by a run of an older version.
        var output = CreateOutputFolder("net10.0");
        TryRewrite("dotnet", _modules, _root, _tracerHome, _settings, out var first, out _).Should().BeTrue();
        first!.Abandon();
        File.Delete(Path.Combine(output, TestingPlatformCoverageRewriter.LockFileName));
        var rewrittenHash = Hash(Path.Combine(output, "Sample.Tests.dll"));

        TryRewrite("dotnet", _modules, _root, _tracerHome, _settings, out var second, out var reason).Should().BeTrue(reason);
        using (second)
        {
            second!.ChangedFiles.Should().BeFalse("the assembly already has coverage, so nothing was rewritten");
        }

        Hash(Path.Combine(output, "Sample.Tests.dll")).Should().Be(rewrittenHash);
        HasCoverage(Path.Combine(output, "Sample.Tests.dll")).Should().BeTrue();
    }

    [Fact]
    public void DoesNotRestoreAFileThatSomethingElseChangedAfterTheRewrite()
    {
        var output = CreateOutputFolder("net10.0");
        TryRewrite("dotnet", _modules, _root, _tracerHome, _settings, out var rewriter, out _).Should().BeTrue();
        var depsPath = Path.Combine(output, "Sample.Tests.deps.json");
        File.AppendAllText(depsPath, " ");
        var changed = File.ReadAllText(depsPath);

        rewriter!.Dispose();

        File.ReadAllText(depsPath).Should().Be(changed, "a file changed by something else after the rewrite is not overwritten");
        HasCoverage(Path.Combine(output, "Sample.Tests.dll")).Should().BeFalse("the other files are put back");
    }

    [Fact]
    public void KeepsTheBackupAndSaysSoWhenAFileCannotBeRestored()
    {
        var output = CreateOutputFolder("net10.0");
        var assemblyPath = Path.Combine(output, "Sample.Tests.dll");
        TryRewrite("dotnet", _modules, _root, _tracerHome, _settings, out var rewriter, out _).Should().BeTrue();
        rewriter!.SetRestoreRetryDelay(TimeSpan.FromMilliseconds(1));

        // A test process that still has the assembly open: sharing violation on Windows, so the copy can't replace it.
        using var holder = new FileStream(assemblyPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        rewriter.Dispose();

        if (FrameworkDescription.Instance.IsWindows())
        {
            Directory.Exists(rewriter.BackupDirectory).Should().BeTrue("the copies of the originals are kept when a file could not be restored");
            File.Exists(Path.Combine(output, TestingPlatformCoverageRewriter.LockFileName)).Should().BeTrue("the record is kept so the next run restores the file");
        }
    }

    [Fact]
    public void ReportsWhetherTheRewriteChangedFiles()
    {
        CreateOutputFolder("net10.0");

        TryRewrite("dotnet", _modules, _root, _tracerHome, _settings, out var rewriter, out var reason).Should().BeTrue(reason);
        using (rewriter)
        {
            rewriter!.ChangedFiles.Should().BeTrue();
        }
    }

    [Fact]
    public void KeepsAnExistingTracerAssemblyAsItWas()
    {
        var output = CreateOutputFolder("net10.0");
        File.WriteAllText(Path.Combine(output, "Datadog.Trace.dll"), "existing");
        File.WriteAllText(Path.Combine(output, "Datadog.Trace.pdb"), "existing symbols");
        var original = Snapshot(output);

        TryRewrite("dotnet", _modules, _root, _tracerHome, _settings, out var rewriter, out _).Should().BeTrue();
        rewriter!.Dispose();

        Snapshot(output).Should().Equal(original);
        File.ReadAllText(Path.Combine(output, "Datadog.Trace.dll")).Should().Be("existing");
    }

    [Fact]
    public void RewritesEveryOutputFolderOfTheMatchingAssemblies()
    {
        var net9 = CreateOutputFolder("net9.0");
        var net10 = CreateOutputFolder("net10.0");

        TryRewrite("dotnet", ["test", "--test-modules", "**/Sample.Tests.dll", "--root-directory", _root], _root, _tracerHome, _settings, out var rewriter, out var reason).Should().BeTrue(reason);
        using (rewriter)
        {
            rewriter!.AssembliesProcessed.Should().Be(2);
            HasCoverage(Path.Combine(net9, "Sample.Tests.dll")).Should().BeTrue();
            HasCoverage(Path.Combine(net10, "Sample.Tests.dll")).Should().BeTrue();
        }
    }

    [Fact]
    public void DoesNotRewriteWhenTheCommandBuilds()
    {
        var output = CreateOutputFolder("net10.0");
        var original = Snapshot(output);

        TryRewrite("dotnet", ["test", "Sample.slnx"], _root, _tracerHome, _settings, out var rewriter, out var reason).Should().BeFalse();

        rewriter.Should().BeNull();
        reason.Should().Contain("--no-build");
        Snapshot(output).Should().Equal(original);
    }

    [Theory]
    [InlineData("--arch", "x64")]
    [InlineData("--os", "linux")]
    [InlineData("--use-current-runtime", null)]
    public void DoesNotRewriteForOptionsThatChangeTheOutputFolder(string option, string? value)
    {
        var output = CreateOutputFolder("net10.0");
        var original = Snapshot(output);
        string[] arguments = value is null
                                 ? ["test", "--no-build", option]
                                 : ["test", "--no-build", option, value];

        TryRewrite("dotnet", arguments, _root, _tracerHome, _settings, out var rewriter, out var reason).Should().BeFalse();

        rewriter.Should().BeNull();
        reason.Should().Contain(option);
        Snapshot(output).Should().Equal(original);
    }

    [Fact]
    public void DoesNotRewriteWithoutATracerHome()
    {
        var output = CreateOutputFolder("net10.0");
        var original = Snapshot(output);

        TryRewrite("dotnet", ["test", "--test-modules", "**/Sample.Tests.dll", "--root-directory", _root], _root, tracerHome: null, _settings, out var rewriter, out var reason).Should().BeFalse();

        rewriter.Should().BeNull();
        reason.Should().NotBeNullOrEmpty();
        Snapshot(output).Should().Equal(original);
    }

    [Fact]
    public void DoesNotRewriteWhenNoAssemblyMatches()
    {
        TryRewrite("dotnet", ["test", "--test-modules", "**/Missing.dll", "--root-directory", _root], _root, _tracerHome, _settings, out var rewriter, out var reason).Should().BeFalse();

        rewriter.Should().BeNull();
        reason.Should().Contain("output folders");
    }

    [Fact]
    public void DoesNotRewriteAnAssemblyWithoutSymbols()
    {
        var output = CreateOutputFolder("net10.0");
        File.Delete(Path.Combine(output, "Sample.Tests.pdb"));
        var original = Snapshot(output);

        TryRewrite("dotnet", ["test", "--test-modules", "**/Sample.Tests.dll", "--root-directory", _root], _root, _tracerHome, _settings, out var rewriter, out var reason).Should().BeFalse();

        rewriter.Should().BeNull();
        reason.Should().Contain("symbols");
        Snapshot(output).Should().Equal(original);
    }

    private static bool HasCoverage(string assemblyPath)
    {
        using var assembly = AssemblyDefinition.ReadAssembly(assemblyPath, new ReaderParameters { InMemory = true });
        return assembly.CustomAttributes.Any(attribute => attribute.AttributeType.FullName == typeof(CoveredAssemblyAttribute).FullName);
    }

    private static string Hash(string path)
    {
        using var sha = SHA256.Create();
        return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));
    }

    private bool TryRewrite(string program, IReadOnlyList<string> arguments, string workingDirectory, string? tracerHome, TestOptimizationSettings settings, out TestingPlatformCoverageRewriter? rewriter, out string? reason)
    {
        var result = TestingPlatformCoverageRewriter.TryRewrite(program, arguments, workingDirectory, tracerHome, settings, out rewriter, out reason);
        if (rewriter is not null)
        {
            // A run that is abandoned or whose files can't be restored keeps its copies: remove them with the test.
            _backupDirectories.Add(rewriter.BackupDirectory);
        }

        return result;
    }

    private string[] Snapshot(string folder)
        => Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .Select(path => $"{path.Substring(folder.Length)}:{Hash(path)}")
                    .ToArray();

    private string CreateOutputFolder(string framework)
    {
        var folder = Directory.CreateDirectory(Path.Combine(_root, "project", "bin", "Release", framework)).FullName;
        File.Copy("CoverageRewriterAssembly.dll", Path.Combine(folder, "Sample.Tests.dll"));
        File.Copy("CoverageRewriterAssembly.pdb", Path.Combine(folder, "Sample.Tests.pdb"));
        File.WriteAllText(
            Path.Combine(folder, "Sample.Tests.deps.json"),
            """{ "runtimeTarget": { "name": ".NETCoreApp,Version=v10.0" }, "targets": { ".NETCoreApp,Version=v10.0": { "Sample.Tests/1.0.0": { "runtime": { "Sample.Tests.dll": { } } } } }, "libraries": { "Sample.Tests/1.0.0": { "type": "project", "serviceable": false, "sha512": "" } } }""");
        return folder;
    }
}
