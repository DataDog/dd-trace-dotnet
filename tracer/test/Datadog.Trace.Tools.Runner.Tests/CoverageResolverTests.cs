// <copyright file="CoverageResolverTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Coverage.Collector;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Mono.Cecil;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

public class CoverageResolverTests
{
    /// <summary>
    /// Verifies that sibling assembly resolution still finds assemblies in the target output directory.
    /// </summary>
    [Fact]
    public void ResolveCopiedSiblingAssemblySucceeds()
    {
        using var directory = TemporaryDirectory.Create();
        var dependencyPath = CopyCoverageFixture(directory.Path);
        using var resolver = CreateResolver(directory.Path);
        var assemblyName = AssemblyNameReference.Parse(AssemblyName.GetAssemblyName(dependencyPath).FullName);

        var assembly = resolver.Resolve(assemblyName);

        assembly.Name.Name.Should().Be("CoverageRewriterAssembly");
    }

    /// <summary>
    /// Verifies that repeated resolutions reuse the same owned Cecil assembly instead of opening the DLL again.
    /// </summary>
    [Fact]
    public void ResolveCopiedSiblingAssemblyUsesCache()
    {
        using var directory = TemporaryDirectory.Create();
        var dependencyPath = CopyCoverageFixture(directory.Path);
        using var resolver = CreateResolver(directory.Path);
        var assemblyName = AssemblyNameReference.Parse(AssemblyName.GetAssemblyName(dependencyPath).FullName);

        var first = resolver.Resolve(assemblyName);
        var second = resolver.Resolve(assemblyName);

        second.Should().BeSameAs(first);
    }

    /// <summary>
    /// Verifies the Windows failure mode from issue 8592: resolved dependency handles are released.
    /// </summary>
    [SkippableFact]
    public void DisposingResolverReleasesCopiedSiblingAssemblyOnWindows()
    {
        SkipOn.AllExcept(SkipOn.PlatformValue.Windows);

        using var directory = TemporaryDirectory.Create();
        var dependencyPath = CopyCoverageFixture(directory.Path);
        var resolver = CreateResolver(directory.Path);
        var assemblyName = AssemblyNameReference.Parse(AssemblyName.GetAssemblyName(dependencyPath).FullName);

        _ = resolver.Resolve(assemblyName);
        resolver.Dispose();

        AssertCanOpenExclusively(dependencyPath);
    }

    /// <summary>
    /// Verifies that changing the copied tracer location invalidates any earlier cached tracer assembly.
    /// </summary>
    [Fact]
    public void SetTracerAssemblyLocationInvalidatesCachedTracerAssembly()
    {
        using var directory = TemporaryDirectory.Create();
        var firstTracerPath = CopyTracerAssembly(directory.Path, "first");
        var secondTracerPath = CopyTracerAssembly(directory.Path, "second");
        using var resolver = CreateResolver(directory.Path);
        var tracerName = AssemblyNameReference.Parse(AssemblyName.GetAssemblyName(firstTracerPath).FullName);

        resolver.SetTracerAssemblyLocation(firstTracerPath);
        var first = resolver.Resolve(tracerName);
        resolver.SetTracerAssemblyLocation(secondTracerPath);
        var second = resolver.Resolve(tracerName);

        second.Should().NotBeSameAs(first);
    }

    /// <summary>
    /// Verifies that tracer cache invalidation releases the old tracer assembly file on Windows.
    /// </summary>
    [SkippableFact]
    public void SetTracerAssemblyLocationReleasesOldTracerCopyOnWindows()
    {
        SkipOn.AllExcept(SkipOn.PlatformValue.Windows);

        using var directory = TemporaryDirectory.Create();
        var firstTracerPath = CopyTracerAssembly(directory.Path, "first");
        var secondTracerPath = CopyTracerAssembly(directory.Path, "second");
        using var resolver = CreateResolver(directory.Path);
        var tracerName = AssemblyNameReference.Parse(AssemblyName.GetAssemblyName(firstTracerPath).FullName);

        resolver.SetTracerAssemblyLocation(firstTracerPath);
        _ = resolver.Resolve(tracerName);
        resolver.SetTracerAssemblyLocation(secondTracerPath);

        AssertCanOpenExclusively(firstTracerPath);
    }

    /// <summary>
    /// Verifies that an active target rewrite lock blocks dependency reads for the same path.
    /// </summary>
    [Fact]
    public void TargetWriteLockBlocksDependencyReadLockForSamePath()
    {
        using var directory = TemporaryDirectory.Create();
        var assemblyPath = CopyCoverageFixture(directory.Path);
        using var writeLock = CoverageAssemblyPathLock.EnterWrite(assemblyPath, TimeSpan.FromSeconds(1));

        var exception = CaptureExceptionFromThread(() =>
        {
            using var readLock = CoverageAssemblyPathLock.EnterRead(assemblyPath, TimeSpan.FromMilliseconds(20));
        });

        exception.Should().BeOfType<IOException>();
    }

    /// <summary>
    /// Verifies that resolver dependency reads wait behind an active rewrite for the same assembly path.
    /// </summary>
    [Fact]
    public async Task ResolverWaitsForTargetWriteLockBeforeReadingDependency()
    {
        using var directory = TemporaryDirectory.Create();
        var dependencyPath = CopyCoverageFixture(directory.Path);
        using var resolver = CreateResolver(directory.Path);
        var assemblyName = AssemblyNameReference.Parse(AssemblyName.GetAssemblyName(dependencyPath).FullName);
        var writeLock = CoverageAssemblyPathLock.EnterWrite(dependencyPath, TimeSpan.FromSeconds(1));

        try
        {
            var resolveTask = Task.Run(() => resolver.Resolve(assemblyName));

            Thread.Sleep(TimeSpan.FromMilliseconds(100));
            resolveTask.IsCompleted.Should().BeFalse();
            writeLock.Dispose();
            var completedTask = await Task.WhenAny(resolveTask, Task.Delay(TimeSpan.FromSeconds(5)));
            completedTask.Should().BeSameAs(resolveTask);
            var assembly = await resolveTask;
            assembly.Name.Name.Should().Be("CoverageRewriterAssembly");
        }
        finally
        {
            writeLock.Dispose();
        }
    }

    /// <summary>
    /// Verifies that target reads load the assembly into memory and release the path lock before processing continues.
    /// </summary>
    [Fact]
    public void ReadTargetAssemblyDoesNotHoldPathLockAfterRead()
    {
        using var directory = TemporaryDirectory.Create();
        var assemblyPath = CopyCoverageFixture(directory.Path);
        using var resolver = CreateResolver(directory.Path);

        using var assembly = AssemblyProcessor.ReadTargetAssembly(assemblyPath, resolver);

        using var writeLock = CoverageAssemblyPathLock.EnterWrite(assemblyPath, TimeSpan.FromMilliseconds(20));
        assembly.Name.Name.Should().Be("CoverageRewriterAssembly");
    }

    /// <summary>
    /// Verifies that target writes still exclude concurrent dependency reads of the same assembly path.
    /// </summary>
    [Fact]
    public async Task WriteTargetAssemblyWaitsForDependencyReadLock()
    {
        using var directory = TemporaryDirectory.Create();
        var assemblyPath = CopyCoverageFixture(directory.Path);
        using var resolver = CreateResolver(directory.Path);
        using var assembly = AssemblyProcessor.ReadTargetAssembly(assemblyPath, resolver);
        var readLock = CoverageAssemblyPathLock.EnterRead(assemblyPath, TimeSpan.FromSeconds(1));

        try
        {
            var writeTask = Task.Run(() => AssemblyProcessor.WriteTargetAssembly(assembly, assemblyPath, strongNameKeyBlob: null));

            Thread.Sleep(TimeSpan.FromMilliseconds(100));
            writeTask.IsCompleted.Should().BeFalse();
            readLock.Dispose();
            var completedTask = await Task.WhenAny(writeTask, Task.Delay(TimeSpan.FromSeconds(5)));
            completedTask.Should().BeSameAs(writeTask);
            await writeTask;
        }
        finally
        {
            readLock.Dispose();
        }
    }

    /// <summary>
    /// Verifies that multiple dependency reads for the same path can proceed together.
    /// </summary>
    [Fact]
    public void DependencyReadLocksAllowConcurrentReads()
    {
        using var directory = TemporaryDirectory.Create();
        var assemblyPath = CopyCoverageFixture(directory.Path);

        using var firstReadLock = CoverageAssemblyPathLock.EnterRead(assemblyPath, TimeSpan.FromSeconds(1));
        using var secondReadLock = CoverageAssemblyPathLock.EnterRead(assemblyPath, TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// Verifies that unrelated assembly paths do not serialize each other.
    /// </summary>
    [Fact]
    public void DifferentPathLocksDoNotBlockEachOther()
    {
        using var directory = TemporaryDirectory.Create();
        var firstPath = CopyCoverageFixture(directory.Path, "first.dll");
        var secondPath = CopyCoverageFixture(directory.Path, "second.dll");
        using var writeLock = CoverageAssemblyPathLock.EnterWrite(firstPath, TimeSpan.FromSeconds(1));

        using var readLock = CoverageAssemblyPathLock.EnterRead(secondPath, TimeSpan.FromMilliseconds(20));
    }

    /// <summary>
    /// Verifies that Windows path casing aliases use the same lock registry entry.
    /// </summary>
    [SkippableFact]
    public void WindowsPathCasingUsesSameLock()
    {
        SkipOn.AllExcept(SkipOn.PlatformValue.Windows);

        using var directory = TemporaryDirectory.Create();
        var assemblyPath = CopyCoverageFixture(directory.Path);
        using var writeLock = CoverageAssemblyPathLock.EnterWrite(assemblyPath.ToUpperInvariant(), TimeSpan.FromSeconds(1));

        var exception = CaptureExceptionFromThread(() =>
        {
            using var readLock = CoverageAssemblyPathLock.EnterRead(assemblyPath.ToLowerInvariant(), TimeSpan.FromMilliseconds(20));
        });

        exception.Should().BeOfType<IOException>();
    }

    private static CoverageAssemblyResolver CreateResolver(string directory)
    {
        var targetPath = Path.Combine(directory, "Target.dll");
        var resolver = new CoverageAssemblyResolver(new ConsoleCollectorLogger(), targetPath);
        resolver.AddSearchDirectory(directory);
        return resolver;
    }

    private static string CopyCoverageFixture(string directory, string fileName = "CoverageRewriterAssembly.dll")
    {
        var targetPath = Path.Combine(directory, fileName);
        File.Copy("CoverageRewriterAssembly.dll", targetPath, overwrite: true);
        File.Copy("CoverageRewriterAssembly.pdb", Path.ChangeExtension(targetPath, ".pdb"), overwrite: true);
        return targetPath;
    }

    private static Exception CaptureExceptionFromThread(ThreadStart action)
    {
        Exception exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });

        thread.Start();
        thread.Join(TimeSpan.FromSeconds(5)).Should().BeTrue();
        return exception;
    }

    private static string CopyTracerAssembly(string directory, string subDirectory)
    {
        var tracerDirectory = Path.Combine(directory, subDirectory);
        Directory.CreateDirectory(tracerDirectory);
        var tracerPath = typeof(Datadog.Trace.Tracer).Assembly.Location;
        var targetPath = Path.Combine(tracerDirectory, Path.GetFileName(tracerPath));
        File.Copy(tracerPath, targetPath, overwrite: true);
        return targetPath;
    }

    private static void AssertCanOpenExclusively(string path)
    {
        using var stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName());
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
