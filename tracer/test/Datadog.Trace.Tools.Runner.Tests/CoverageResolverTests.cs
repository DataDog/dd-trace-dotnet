// <copyright file="CoverageResolverTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Reflection;
using System.Threading;
using Datadog.Trace.Coverage.Collector;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Mono.Cecil;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

public class CoverageResolverTests
{
    private static readonly TimeSpan LockAcquisitionTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ExpectedContentionTimeout = TimeSpan.FromMilliseconds(200);

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
    public void DisposingResolverReleasesCopiedSiblingAssembly()
    {
        using var directory = TemporaryDirectory.Create();
        var dependencyPath = CopyCoverageFixture(directory.Path);
        var resolver = CreateResolver(directory.Path);
        var assemblyName = AssemblyNameReference.Parse(AssemblyName.GetAssemblyName(dependencyPath).FullName);

        _ = resolver.Resolve(assemblyName);
        resolver.Dispose();

        AssertCanOpenExclusively(dependencyPath);
    }

    /// <summary>
    /// Verifies that changing the copied tracer location invalidates and releases the previous cached tracer assembly.
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
        if (EnvironmentTools.IsWindows())
        {
            AssertCanOpenExclusively(firstTracerPath);
        }
    }

    /// <summary>
    /// Verifies that setting the same copied tracer location keeps the existing cached tracer assembly.
    /// </summary>
    [Fact]
    public void SetTracerAssemblyLocationKeepsCachedTracerAssemblyForSamePath()
    {
        using var directory = TemporaryDirectory.Create();
        var tracerPath = CopyTracerAssembly(directory.Path, "tracer");
        using var resolver = CreateResolver(directory.Path);
        var tracerName = AssemblyNameReference.Parse(AssemblyName.GetAssemblyName(tracerPath).FullName);

        resolver.SetTracerAssemblyLocation(tracerPath);
        var first = resolver.Resolve(tracerName);
        resolver.SetTracerAssemblyLocation(tracerPath);
        var second = resolver.Resolve(tracerName);

        second.Should().BeSameAs(first);
    }

    /// <summary>
    /// Verifies that an active target rewrite lock blocks dependency reads for the same path.
    /// </summary>
    [Fact]
    public void TargetWriteLockBlocksDependencyReadLockForSamePath()
    {
        using var directory = TemporaryDirectory.Create();
        var assemblyPath = CopyCoverageFixture(directory.Path);
        using var writeLock = CoverageAssemblyPathLock.EnterWrite(assemblyPath, LockAcquisitionTimeout);

        var exception = CaptureExceptionFromThread(() =>
        {
            using var readLock = CoverageAssemblyPathLock.EnterRead(assemblyPath, ExpectedContentionTimeout);
        });

        exception.Should().BeOfType<IOException>();
    }

    /// <summary>
    /// Verifies that resolver dependency reads wait behind an active rewrite for the same assembly path.
    /// </summary>
    [Fact]
    public void ResolverCannotReadDependencyWhileTargetWriteLockIsHeld()
    {
        using var directory = TemporaryDirectory.Create();
        var dependencyPath = CopyCoverageFixture(directory.Path);
        using var resolver = CreateResolver(directory.Path);
        var assemblyName = AssemblyNameReference.Parse(AssemblyName.GetAssemblyName(dependencyPath).FullName);
        using var writeLock = CoverageAssemblyPathLock.EnterWrite(dependencyPath, LockAcquisitionTimeout);

        var exception = CaptureExceptionFromThread(() => resolver.Resolve(assemblyName));

        exception.Should().BeOfType<IOException>();
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

        using var writeLock = CoverageAssemblyPathLock.EnterWrite(assemblyPath, LockAcquisitionTimeout);
        assembly.Name.Name.Should().Be("CoverageRewriterAssembly");
    }

    /// <summary>
    /// Verifies that target writes still exclude concurrent dependency reads of the same assembly path.
    /// </summary>
    [Fact]
    public void WriteTargetAssemblyCannotWriteWhileDependencyReadLockIsHeld()
    {
        using var directory = TemporaryDirectory.Create();
        var assemblyPath = CopyCoverageFixture(directory.Path);
        using var resolver = CreateResolver(directory.Path);
        using var assembly = AssemblyProcessor.ReadTargetAssembly(assemblyPath, resolver);
        using var readLock = CoverageAssemblyPathLock.EnterRead(assemblyPath, LockAcquisitionTimeout);

        var exception = CaptureExceptionFromThread(() => AssemblyProcessor.WriteTargetAssembly(assembly, assemblyPath, strongNameKeyBlob: null));

        exception.Should().BeOfType<IOException>();
    }

    /// <summary>
    /// Verifies that multiple dependency reads for the same path can proceed together.
    /// </summary>
    [Fact]
    public void DependencyReadLocksAllowConcurrentReads()
    {
        using var directory = TemporaryDirectory.Create();
        var assemblyPath = CopyCoverageFixture(directory.Path);

        using var firstReadLock = CoverageAssemblyPathLock.EnterRead(assemblyPath, LockAcquisitionTimeout);
        using var secondReadLock = CoverageAssemblyPathLock.EnterRead(assemblyPath, LockAcquisitionTimeout);
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
        using var writeLock = CoverageAssemblyPathLock.EnterWrite(firstPath, LockAcquisitionTimeout);

        using var readLock = CoverageAssemblyPathLock.EnterRead(secondPath, LockAcquisitionTimeout);
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
        using var writeLock = CoverageAssemblyPathLock.EnterWrite(assemblyPath.ToUpperInvariant(), LockAcquisitionTimeout);

        var exception = CaptureExceptionFromThread(() =>
        {
            using var readLock = CoverageAssemblyPathLock.EnterRead(assemblyPath.ToLowerInvariant(), ExpectedContentionTimeout);
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
        thread.Join(LockAcquisitionTimeout).Should().BeTrue();
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
