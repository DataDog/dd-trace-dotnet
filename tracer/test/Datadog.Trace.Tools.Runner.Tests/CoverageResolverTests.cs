// <copyright file="CoverageResolverTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Runtime.Versioning;
using System.Threading;
using Datadog.Trace.Coverage.Collector;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Mono.Cecil;
using Mono.Cecil.Cil;
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
    /// Verifies that dependencies supplied by a declared shared framework can be resolved outside the test output directory.
    /// </summary>
    [Fact]
    public void ResolveAssemblyFromDeclaredSharedFrameworkSucceeds()
    {
        using var directory = TemporaryDirectory.Create();
        var outputDirectory = Path.Combine(directory.Path, "output");
        var sharedFrameworkRoot = Path.Combine(directory.Path, "shared");
        var stableFrameworkDirectory = Path.Combine(sharedFrameworkRoot, "Microsoft.AspNetCore.App", "10.0.8");
        Directory.CreateDirectory(outputDirectory);
        Directory.CreateDirectory(stableFrameworkDirectory);
        var dependencyPath = CreateAssembly(stableFrameworkDirectory, "SharedFrameworkDependency", new Version(10, 0, 0, 0));
        const string RuntimeConfig = """
                                     {
                                       "runtimeOptions": {
                                         "frameworks": [
                                           { "name": "Microsoft.NETCore.App", "version": "10.0.0" },
                                           { "name": "Microsoft.AspNetCore.App", "version": "10.0.0" }
                                         ]
                                       }
                                     }
                                     """;
        var runtimeConfigPath = Path.Combine(outputDirectory, "Repro.Tests.runtimeconfig.json");
        File.WriteAllText(Path.Combine(outputDirectory, "Malformed.runtimeconfig.json"), "{");
        File.WriteAllText(runtimeConfigPath, RuntimeConfig);
        var targetPath = Path.Combine(outputDirectory, "Repro.Library.dll");
        var assemblyName = new AssemblyNameReference("SharedFrameworkDependency", new Version(10, 0, 0, 0));

        using (var resolver = new CoverageAssemblyResolver(new ConsoleCollectorLogger(), targetPath, sharedFrameworkRoot))
        {
            var assembly = resolver.Resolve(assemblyName);
            assembly.MainModule.FileName.Should().Be(dependencyPath);
        }

        if (EnvironmentTools.IsWindows())
        {
            AssertCanOpenExclusively(dependencyPath);
        }

        File.Delete(runtimeConfigPath);
        using var cachedResolver = new CoverageAssemblyResolver(new ConsoleCollectorLogger(), targetPath, sharedFrameworkRoot);
        cachedResolver.Resolve(assemblyName).MainModule.FileName.Should().Be(dependencyPath);
    }

    /// <summary>
    /// Verifies that shared frameworks can be found when the collector itself is hosted by .NET Framework.
    /// </summary>
    [Fact]
    public void SharedFrameworkRootsUseDotnetRootOutsideCoreClr()
    {
        using var directory = TemporaryDirectory.Create();
        var dotnetRoot = Path.Combine(directory.Path, "dotnet");
        var sharedFrameworkRoot = Path.Combine(dotnetRoot, "shared");
        Directory.CreateDirectory(sharedFrameworkRoot);

        var roots = CoverageAssemblyResolver.SharedFrameworkLocator.GetSharedFrameworkRoots(
            Path.Combine(directory.Path, "mscorlib.dll"),
            name => name == "DOTNET_ROOT" ? dotnetRoot : null);

        roots.Should().Equal(sharedFrameworkRoot);
    }

    /// <summary>
    /// Verifies that an explicitly configured target runtime is searched before the collector runtime.
    /// </summary>
    [Theory]
    [InlineData("DOTNET_HOST_PATH")]
    [InlineData("DOTNET_ROOT")]
    public void SharedFrameworkRootsPreferConfiguredTargetRuntime(string variableName)
    {
        using var directory = TemporaryDirectory.Create();
        var targetDotnetRoot = Path.Combine(directory.Path, "target-dotnet");
        var targetSharedFrameworkRoot = Path.Combine(targetDotnetRoot, "shared");
        var collectorSharedFrameworkRoot = Path.Combine(directory.Path, "collector-dotnet", "shared");
        var coreLibraryPath = Path.Combine(collectorSharedFrameworkRoot, "Microsoft.NETCore.App", "10.0.0", "System.Private.CoreLib.dll");
        Directory.CreateDirectory(targetSharedFrameworkRoot);
        Directory.CreateDirectory(collectorSharedFrameworkRoot);
        var configuredPath = variableName == "DOTNET_HOST_PATH" ? Path.Combine(targetDotnetRoot, "dotnet") : targetDotnetRoot;

        var roots = CoverageAssemblyResolver.SharedFrameworkLocator.GetSharedFrameworkRoots(
            coreLibraryPath,
            name => name == variableName ? configuredPath : null);

        roots.Should().Equal(targetSharedFrameworkRoot, collectorSharedFrameworkRoot);
    }

    /// <summary>
    /// Verifies stable and explicit prerelease roll-forward framework selection.
    /// </summary>
    [Theory]
    [InlineData(false, "10.0.8")]
    [InlineData(true, "10.0.10-servicing.1")]
    public void SharedFrameworkDiscoveryHonorsPrereleaseRollForward(bool rollForwardToPrerelease, string expectedVersion)
    {
        using var directory = TemporaryDirectory.Create();
        var outputDirectory = Path.Combine(directory.Path, "output");
        var sharedFrameworkRoot = Path.Combine(directory.Path, "shared");
        var stableFrameworkDirectory = Path.Combine(sharedFrameworkRoot, "Microsoft.AspNetCore.App", "10.0.8");
        var prereleaseFrameworkDirectory = Path.Combine(sharedFrameworkRoot, "Microsoft.AspNetCore.App", "10.0.10-servicing.1");
        Directory.CreateDirectory(outputDirectory);
        Directory.CreateDirectory(stableFrameworkDirectory);
        Directory.CreateDirectory(prereleaseFrameworkDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, "Repro.Tests.runtimeconfig.json"), CreateRuntimeConfig("framework", "Microsoft.AspNetCore.App", "10.0.0"));

        var directories = CoverageAssemblyResolver.SharedFrameworkLocator.DiscoverSharedFrameworkDirectories(
            outputDirectory,
            [sharedFrameworkRoot],
            rollForwardToPrerelease);

        directories.Should().Equal(Path.Combine(sharedFrameworkRoot, "Microsoft.AspNetCore.App", expectedVersion));
    }

    /// <summary>
    /// Verifies all runtimeconfig framework declaration shapes used by the host.
    /// </summary>
    [Theory]
    [InlineData("framework")]
    [InlineData("includedFrameworks")]
    public void ResolveAssemblySupportsRuntimeConfigFrameworkShape(string propertyName)
    {
        using var directory = TemporaryDirectory.Create();
        var outputDirectory = Path.Combine(directory.Path, "output");
        var sharedFrameworkRoot = Path.Combine(directory.Path, "shared");
        var frameworkDirectory = Path.Combine(sharedFrameworkRoot, "Microsoft.AspNetCore.App", "10.0.8");
        Directory.CreateDirectory(outputDirectory);
        Directory.CreateDirectory(frameworkDirectory);
        _ = CreateAssembly(frameworkDirectory, "SharedFrameworkDependency", new Version(10, 0, 0, 0));
        File.WriteAllText(Path.Combine(outputDirectory, "Repro.Tests.runtimeconfig.json"), CreateRuntimeConfig(propertyName, "Microsoft.AspNetCore.App", "10.0.0"));
        var targetPath = Path.Combine(outputDirectory, "Repro.Library.dll");
        using var resolver = new CoverageAssemblyResolver(new ConsoleCollectorLogger(), targetPath, sharedFrameworkRoot);

        var assembly = resolver.Resolve(new AssemblyNameReference("SharedFrameworkDependency", new Version(10, 0, 0, 0)));

        assembly.Name.Version.Should().Be(new Version(10, 0, 0, 0));
    }

    /// <summary>
    /// Verifies that a candidate from one runtimeconfig cannot satisfy a different requested assembly identity.
    /// </summary>
    [Fact]
    public void ResolveAssemblySkipsSharedFrameworkCandidateWithDifferentIdentity()
    {
        using var directory = TemporaryDirectory.Create();
        var outputDirectory = Path.Combine(directory.Path, "output");
        var sharedFrameworkRoot = Path.Combine(directory.Path, "shared");
        var net8FrameworkDirectory = Path.Combine(sharedFrameworkRoot, "Microsoft.AspNetCore.App", "8.0.10");
        var net9FrameworkDirectory = Path.Combine(sharedFrameworkRoot, "Microsoft.AspNetCore.App", "9.0.10");
        Directory.CreateDirectory(outputDirectory);
        Directory.CreateDirectory(net8FrameworkDirectory);
        Directory.CreateDirectory(net9FrameworkDirectory);
        _ = CreateAssembly(net8FrameworkDirectory, "SharedFrameworkDependency", new Version(8, 0, 0, 0));
        _ = CreateAssembly(net9FrameworkDirectory, "SharedFrameworkDependency", new Version(9, 0, 0, 0));
        File.WriteAllText(Path.Combine(outputDirectory, "A.runtimeconfig.json"), CreateRuntimeConfig("framework", "Microsoft.AspNetCore.App", "8.0.0"));
        File.WriteAllText(Path.Combine(outputDirectory, "B.runtimeconfig.json"), CreateRuntimeConfig("framework", "Microsoft.AspNetCore.App", "9.0.0"));
        var targetPath = Path.Combine(outputDirectory, "Repro.Library.dll");
        using var resolver = new CoverageAssemblyResolver(new ConsoleCollectorLogger(), targetPath, sharedFrameworkRoot);

        var assembly = resolver.Resolve(new AssemblyNameReference("SharedFrameworkDependency", new Version(9, 0, 0, 0)));

        assembly.Name.Version.Should().Be(new Version(9, 0, 0, 0));
    }

    /// <summary>
    /// Verifies that installed frameworks are not probed unless a runtimeconfig declares them.
    /// </summary>
    [Fact]
    public void ResolveAssemblyDoesNotProbeUndeclaredSharedFramework()
    {
        using var directory = TemporaryDirectory.Create();
        var outputDirectory = Path.Combine(directory.Path, "output");
        var sharedFrameworkRoot = Path.Combine(directory.Path, "shared");
        var frameworkDirectory = Path.Combine(sharedFrameworkRoot, "Microsoft.AspNetCore.App", "10.0.8");
        Directory.CreateDirectory(outputDirectory);
        Directory.CreateDirectory(frameworkDirectory);
        _ = CreateAssembly(frameworkDirectory, "SharedFrameworkDependency", new Version(10, 0, 8, 0));
        File.WriteAllText(Path.Combine(outputDirectory, "Repro.Tests.runtimeconfig.json"), CreateRuntimeConfig("framework", "Microsoft.NETCore.App", "10.0.0"));
        var targetPath = Path.Combine(outputDirectory, "Repro.Library.dll");
        using var resolver = new CoverageAssemblyResolver(new ConsoleCollectorLogger(), targetPath, sharedFrameworkRoot);

        var action = () => resolver.Resolve(new AssemblyNameReference("SharedFrameworkDependency", new Version(10, 0, 0, 0)));

        action.Should().Throw<AssemblyResolutionException>();
    }

    /// <summary>
    /// Verifies that an older prerelease framework is not considered compatible with a newer requested prerelease.
    /// </summary>
    [Fact]
    public void ResolveAssemblyDoesNotRollBackToOlderPrereleaseFramework()
    {
        using var directory = TemporaryDirectory.Create();
        var outputDirectory = Path.Combine(directory.Path, "output");
        var sharedFrameworkRoot = Path.Combine(directory.Path, "shared");
        var frameworkDirectory = Path.Combine(sharedFrameworkRoot, "Microsoft.AspNetCore.App", "11.0.0-preview.6");
        Directory.CreateDirectory(outputDirectory);
        Directory.CreateDirectory(frameworkDirectory);
        _ = CreateAssembly(frameworkDirectory, "SharedFrameworkDependency", new Version(11, 0, 0, 6));
        File.WriteAllText(Path.Combine(outputDirectory, "Repro.Tests.runtimeconfig.json"), CreateRuntimeConfig("framework", "Microsoft.AspNetCore.App", "11.0.0-preview.8"));
        var targetPath = Path.Combine(outputDirectory, "Repro.Library.dll");
        using var resolver = new CoverageAssemblyResolver(new ConsoleCollectorLogger(), targetPath, sharedFrameworkRoot);

        var action = () => resolver.Resolve(new AssemblyNameReference("SharedFrameworkDependency", new Version(11, 0, 0, 0)));

        action.Should().Throw<AssemblyResolutionException>();
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
    /// Verifies that a Cecil write failure cannot damage the original assembly or symbols.
    /// </summary>
    [Fact]
    public void WriteTargetAssemblyFailurePreservesOriginalFiles()
    {
        using var directory = TemporaryDirectory.Create();
        var assemblyPath = CopyCoverageFixture(directory.Path);
        var symbolsPath = Path.ChangeExtension(assemblyPath, ".pdb");
        var originalAssembly = File.ReadAllBytes(assemblyPath);
        var originalSymbols = File.ReadAllBytes(symbolsPath);
        using var resolver = CreateResolver(directory.Path);
        using var assembly = AssemblyProcessor.ReadTargetAssembly(assemblyPath, resolver);

        AddOptionalParameterWithUnresolvableEnum(assembly.MainModule);

        var action = () => AssemblyProcessor.WriteTargetAssembly(assembly, assemblyPath, strongNameKeyBlob: null);

        action.Should().Throw<AssemblyResolutionException>();
        File.ReadAllBytes(assemblyPath).Should().Equal(originalAssembly);
        File.ReadAllBytes(symbolsPath).Should().Equal(originalSymbols);
        using var unchangedAssembly = AssemblyDefinition.ReadAssembly(assemblyPath, new ReaderParameters { ReadSymbols = true });
        unchangedAssembly.Name.Name.Should().Be("CoverageRewriterAssembly");
        Directory.GetFiles(directory.Path).Select(Path.GetFileName).Should().BeEquivalentTo("CoverageRewriterAssembly.dll", "CoverageRewriterAssembly.pdb");
        Directory.GetDirectories(directory.Path).Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that a successful staged write publishes readable matching assembly and symbol files.
    /// </summary>
    [Fact]
    public void WriteTargetAssemblySuccessPublishesRewrittenFiles()
    {
        using var directory = TemporaryDirectory.Create();
        var assemblyPath = CopyCoverageFixture(directory.Path);
        using var resolver = CreateResolver(directory.Path);
        using var assembly = AssemblyProcessor.ReadTargetAssembly(assemblyPath, resolver);
        assembly.MainModule.Types.Add(new TypeDefinition("CoverageRewriterAssembly", "AddedByTest", Mono.Cecil.TypeAttributes.Public | Mono.Cecil.TypeAttributes.Class, assembly.MainModule.TypeSystem.Object));
        var transactionPaths = new List<string>();
        string stagedAssemblyPath = null;

        AssemblyProcessor.WriteTargetAssembly(assembly, assemblyPath, strongNameKeyBlob: null, (source, destination, backup) =>
        {
            transactionPaths.Add(source);
            if (destination == assemblyPath)
            {
                stagedAssemblyPath = source;
            }

            if (backup is not null)
            {
                transactionPaths.Add(backup);
            }

            File.Replace(source, destination, backup);
        });

        using var rewrittenAssembly = AssemblyDefinition.ReadAssembly(assemblyPath, new ReaderParameters { ReadSymbols = true });
        rewrittenAssembly.MainModule.GetType("CoverageRewriterAssembly.AddedByTest").Should().NotBeNull();
        Path.GetFileName(stagedAssemblyPath).Should().Be(Path.GetFileName(assemblyPath));
        (stagedAssemblyPath.Length - assemblyPath.Length).Should().BeLessOrEqualTo(20);
        transactionPaths.Where(CoverageCollector.HasAssemblyExtension).Should().BeEmpty();
        Directory.GetFiles(directory.Path).Select(Path.GetFileName).Should().BeEquivalentTo("CoverageRewriterAssembly.dll", "CoverageRewriterAssembly.pdb");
        Directory.GetDirectories(directory.Path).Should().BeEmpty();

        using var stream = File.OpenRead(assemblyPath);
        using var peReader = new PEReader(stream);
        var codeViewEntry = peReader.ReadDebugDirectory().Single(entry => entry.Type == DebugDirectoryEntryType.CodeView);
        var codeViewData = peReader.ReadCodeViewDebugDirectoryData(codeViewEntry);
        Path.GetFileName(codeViewData.Path).Should().Be(Path.GetFileName(Path.ChangeExtension(assemblyPath, ".pdb")));
    }

#if NET7_0_OR_GREATER
    /// <summary>
    /// Verifies that replacing the assembly does not drop Unix mode bits from the original file.
    /// </summary>
    [SkippableFact]
    [UnsupportedOSPlatform("windows")]
    public void WriteTargetAssemblyPreservesUnixMode()
    {
        SkipOn.Platform(SkipOn.PlatformValue.Windows);

        using var directory = TemporaryDirectory.Create();
        var assemblyPath = CopyCoverageFixture(directory.Path, "CoverageRewriterAssembly");
        var expectedMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead;
        File.SetUnixFileMode(assemblyPath, expectedMode);
        using var resolver = CreateResolver(directory.Path);
        using var assembly = AssemblyProcessor.ReadTargetAssembly(assemblyPath, resolver);

        AssemblyProcessor.WriteTargetAssembly(assembly, assemblyPath, strongNameKeyBlob: null);

        File.GetUnixFileMode(assemblyPath).Should().Be(expectedMode);
    }
#endif

    /// <summary>
    /// Verifies that both original files are restored when publishing the DLL reports a failure after replacement.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WriteTargetAssemblyPublicationFailureRestoresOriginalFiles(bool replacementWasPublished)
    {
        using var directory = TemporaryDirectory.Create();
        var assemblyPath = CopyCoverageFixture(directory.Path);
        var symbolsPath = Path.ChangeExtension(assemblyPath, ".pdb");
        var originalAssembly = File.ReadAllBytes(assemblyPath);
        var originalSymbols = File.ReadAllBytes(symbolsPath);
        using var resolver = CreateResolver(directory.Path);
        using var assembly = AssemblyProcessor.ReadTargetAssembly(assemblyPath, resolver);
        assembly.MainModule.Types.Add(new TypeDefinition("CoverageRewriterAssembly", "AddedByTest", Mono.Cecil.TypeAttributes.Public | Mono.Cecil.TypeAttributes.Class, assembly.MainModule.TypeSystem.Object));
        var replaceCallCount = 0;

        var action = () => AssemblyProcessor.WriteTargetAssembly(assembly, assemblyPath, strongNameKeyBlob: null, (source, destination, backup) =>
        {
            replaceCallCount++;
            if (replaceCallCount == 2)
            {
                File.Move(destination, backup!);
                if (replacementWasPublished)
                {
                    File.Move(source, destination);
                }

                throw new IOException("Injected DLL publication failure during replacement.");
            }

            File.Replace(source, destination, backup);
        });

        action.Should().Throw<IOException>();
        File.ReadAllBytes(assemblyPath).Should().Equal(originalAssembly);
        File.ReadAllBytes(symbolsPath).Should().Equal(originalSymbols);
        Directory.GetFiles(directory.Path).Select(Path.GetFileName).Should().BeEquivalentTo("CoverageRewriterAssembly.dll", "CoverageRewriterAssembly.pdb");
        Directory.GetDirectories(directory.Path).Should().BeEmpty();
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

    private static void AddOptionalParameterWithUnresolvableEnum(ModuleDefinition module)
    {
        var missingAssembly = new AssemblyNameReference("Missing.Enums", new Version(1, 0, 0, 0));
        module.AssemblyReferences.Add(missingAssembly);
        var missingEnum = new TypeReference("Missing.Enums", "MissingEnum", module, missingAssembly, true);
        var method = new MethodDefinition("MethodWithMissingEnumDefault", Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static, module.TypeSystem.Void);
        method.Parameters.Add(new ParameterDefinition("value", Mono.Cecil.ParameterAttributes.Optional | Mono.Cecil.ParameterAttributes.HasDefault, missingEnum) { Constant = 0 });
        method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        module.Types.First(type => type.Name != "<Module>").Methods.Add(method);
    }

    private static string CopyCoverageFixture(string directory, string fileName = "CoverageRewriterAssembly.dll")
    {
        var targetPath = Path.Combine(directory, fileName);
        File.Copy("CoverageRewriterAssembly.dll", targetPath, overwrite: true);
        File.Copy("CoverageRewriterAssembly.pdb", Path.ChangeExtension(targetPath, ".pdb"), overwrite: true);
        return targetPath;
    }

    private static string CreateAssembly(string directory, string assemblyName, Version version)
    {
        var assemblyPath = Path.Combine(directory, assemblyName + ".dll");
        using var assembly = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition(assemblyName, version), assemblyName, ModuleKind.Dll);
        assembly.Write(assemblyPath);
        return assemblyPath;
    }

    private static string CreateRuntimeConfig(string propertyName, string frameworkName, string version)
    {
        var framework = $"{{ \"name\": \"{frameworkName}\", \"version\": \"{version}\" }}";
        var value = propertyName == "framework" ? framework : $"[{framework}]";
        return $"{{ \"runtimeOptions\": {{ \"{propertyName}\": {value} }} }}";
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
