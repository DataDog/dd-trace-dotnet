// <copyright file="DuckTypeAotProcessorsTests.Metadata.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
#if NETCOREAPP2_1
using AssemblyLoadContext = Datadog.Trace.Tools.Runner.Tests.NetCore21AssemblyLoadContext;
#else
using System.Runtime.Loader;
#endif
using Datadog.Trace.Tools.Runner.DuckTypeAot;
using dnlib.DotNet;
using FluentAssertions;
using Xunit;

#pragma warning disable SA1201, SA1202 // Elements should appear in the correct order

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// The metadata layer of the registry emitter: runtime types of metadata types, assembly paths, interface traversal.
/// </summary>
public partial class DuckTypeAotProcessorsTests
{
    /// <summary>
    /// The application's framework assemblies (its runtime pack, the NativeAOT System.Private.CoreLib) are other versions of
    /// the generator's runtime ones, which it can't load: its own stand for them.
    /// </summary>
    [Fact]
    public void RuntimeTypeResolverShouldUseTheRuntimeFrameworkAssemblyForAnotherVersionOfIt()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var module = new ModuleDefUser("System.Private.CoreLib.dll") { Kind = ModuleKind.Dll };
            new AssemblyDefUser("System.Private.CoreLib", new Version(99, 0, 0, 0)).Modules.Add(module);
            var coreLibPath = Path.Combine(tempDirectory, "System.Private.CoreLib.dll");
            module.Write(coreLibPath);

            var emitterType = typeof(DuckTypeAotMapping).Assembly.GetType("Datadog.Trace.Tools.Runner.DuckTypeAot.DuckTypeAotRegistryAssemblyEmitter");
            var tryResolveRuntimeType = emitterType!.GetMethod(
                "TryResolveRuntimeType",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(string), typeof(string), typeof(string), typeof(Type).MakeByRefType() },
                modifiers: null);
            var arguments = new object?[] { "System.Private.CoreLib", coreLibPath, "System.ReadOnlyMemory`1[[System.Byte, System.Private.CoreLib]]", null };

            ((bool)tryResolveRuntimeType!.Invoke(obj: null, parameters: arguments)!).Should().BeTrue();
            // The core library's type (Datadog.Trace has its own ReadOnlyMemory<T> for older runtimes).
            arguments[3].Should().Be(typeof(object).Assembly.GetType("System.ReadOnlyMemory`1", throwOnError: true)!.MakeGenericType(typeof(byte)));
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void RuntimeTypeResolverShouldRejectLoadedTypeWhenExplicitAssemblyPathDoesNotMatch()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var loadedAssemblyName = "DuckTypeAotExplicitPathMismatch.Loaded";
            var requestedAssemblyName = "DuckTypeAotExplicitPathMismatch.Requested";
            var loadedAssemblyPath = CreateDuplicateAssignableTargetAssembly(tempDirectory, loadedAssemblyName);
            var requestedAssemblyPath = Path.Combine(tempDirectory, requestedAssemblyName, requestedAssemblyName + ".dll");

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-ExplicitPathMismatch", isCollectible: true);
            try
            {
                _ = loadContext.LoadFromAssemblyPath(loadedAssemblyPath);

                var emitterType = typeof(DuckTypeAotMapping).Assembly.GetType("Datadog.Trace.Tools.Runner.DuckTypeAot.DuckTypeAotRegistryAssemblyEmitter");
                emitterType.Should().NotBeNull();
                var tryResolveRuntimeType = emitterType!.GetMethod(
                    "TryResolveRuntimeType",
                    BindingFlags.Static | BindingFlags.NonPublic,
                    binder: null,
                    types: new[] { typeof(string), typeof(string), typeof(string), typeof(Type).MakeByRefType() },
                    modifiers: null);
                tryResolveRuntimeType.Should().NotBeNull();

                var arguments = new object?[]
                {
                    requestedAssemblyName,
                    requestedAssemblyPath,
                    DuplicateAssignableBaseTargetTypeName,
                    null
                };

                var resolved = (bool)tryResolveRuntimeType!.Invoke(obj: null, parameters: arguments)!;

                resolved.Should().BeFalse("assembly-scoped resolution must not accept a same-named type from an already-loaded unrelated assembly");
                arguments[3].Should().BeNull();
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void RuntimeTypeResolverShouldRejectLoadedGenericArgumentWhenAssemblyCannotBeResolved()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var loadedAssemblyPath = CreateDuplicateAssignableTargetAssembly(tempDirectory, "DuckTypeAotGenericArgumentLoaded");
            var missingAssemblyName = "DuckTypeAotMissingGenericArgument";
            var closedGenericTypeName = $"System.Collections.Generic.List`1[[{DuplicateAssignableBaseTargetTypeName}, {missingAssemblyName}]]";
            var rootAssembly = typeof(List<>).Assembly;
            rootAssembly.Location.Should().NotBeNullOrWhiteSpace();

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-GenericArgumentAssemblyMismatch", isCollectible: true);
            try
            {
                _ = loadContext.LoadFromAssemblyPath(loadedAssemblyPath);

                var resolved = InvokeTryResolveRuntimeType(
                    rootAssembly.GetName().Name!,
                    rootAssembly.Location,
                    closedGenericTypeName,
                    out var runtimeType);

                resolved.Should().BeFalse("assembly-qualified generic arguments must not be satisfied by same-named types from unrelated loaded assemblies");
                runtimeType.Should().BeNull();
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void RuntimeTypeResolverShouldAcceptLoadedAssemblyWhenExplicitPathIsIdenticalCopy()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var loadedAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var copiedDirectory = Path.Combine(tempDirectory, "copy");
            Directory.CreateDirectory(copiedDirectory);
            var copiedAssemblyPath = Path.Combine(copiedDirectory, Path.GetFileName(loadedAssemblyPath));
            File.Copy(loadedAssemblyPath, copiedAssemblyPath);

            var assemblyName = AssemblyName.GetAssemblyName(copiedAssemblyPath).Name;
            assemblyName.Should().NotBeNullOrWhiteSpace();

            var resolved = InvokeTryResolveRuntimeType(
                assemblyName!,
                copiedAssemblyPath,
                typeof(DuckTypeAotProcessorsTests).FullName!,
                out var runtimeType);

            resolved.Should().BeTrue("isolated full-suite copies may be unified by the runtime with an already-loaded assembly that has the same MVID");
            runtimeType.Should().Be(typeof(DuckTypeAotProcessorsTests));
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void RuntimeTypeResolverShouldNotResolveMetadataReferenceByNameFromDifferentLoadedAssembly()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var assemblyName = "DuckTypeAotMetadataReference.SameSimpleName";
            var requestedAssemblyPath = CreateDuplicateAssignableTargetAssembly(Path.Combine(tempDirectory, "requested"), assemblyName, "requested");
            var loadedAssemblyPath = CreateDuplicateAssignableTargetAssembly(Path.Combine(tempDirectory, "loaded"), assemblyName, "loaded");
            var assemblyPathMapField = GetEmitterStaticField("runtimeTypeResolutionAssemblyPathsByName");
            var originalAssemblyPathMap = assemblyPathMapField.GetValue(null);

            using var requestedModule = ModuleDefMD.Load(requestedAssemblyPath);
            var requestedType = requestedModule.Find(DuplicateAssignableBaseTargetTypeName, isReflectionName: false);
            requestedType.Should().NotBeNull();
            var requestedTypeSig = requestedType!.ToTypeSig();

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-MetadataReferenceByName", isCollectible: true);
            try
            {
                _ = loadContext.LoadFromAssemblyPath(loadedAssemblyPath);
                assemblyPathMapField.SetValue(
                    null,
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        [assemblyName] = requestedAssemblyPath
                    });

                var tryResolveMethod = GetEmitterStaticMethod(
                    "TryResolveRuntimeTypeFromTypeDefOrRef",
                    typeof(TypeSig));

                var runtimeType = (Type?)tryResolveMethod.Invoke(obj: null, parameters: new object[] { requestedTypeSig });

                runtimeType.Should().NotBeNull("metadata references with an assembly path should resolve through that identity rather than a same-simple-name loaded assembly");
                runtimeType!.Assembly.Location.Should().Be(requestedAssemblyPath);
            }
            finally
            {
                assemblyPathMapField.SetValue(null, originalAssemblyPathMap);
                loadContext.Unload();
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void RuntimeTypeResolverShouldResolveVoidTypeSignatures()
    {
        using var module = ModuleDefMD.Load(typeof(DuckTypeAotProcessorsTests).Assembly.Location);
        var testType = module.Find(typeof(DuckTypeAotProcessorsTests).FullName, isReflectionName: true);
        testType.Should().NotBeNull();
        var testMethod = testType!.FindMethod(nameof(RuntimeTypeResolverShouldResolveVoidTypeSignatures));
        testMethod.Should().NotBeNull();

        var tryResolveMethod = GetEmitterStaticMethod("TryResolveRuntimeType", typeof(TypeSig));

        var runtimeType = (Type?)tryResolveMethod.Invoke(obj: null, parameters: new object[] { testMethod!.MethodSig.RetType });

        runtimeType.Should().Be(typeof(void), "void return signatures from different metadata assemblies must compare as the same runtime type");
    }

    [Fact]
    public void RuntimeTypeResolverShouldAcceptTypeForwardedBclTypesFromRuntimeFacades()
    {
        var runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location);
        runtimeDirectory.Should().NotBeNullOrWhiteSpace();
        var systemRuntimePath = Path.Combine(runtimeDirectory!, "System.Runtime.dll");
        var netstandardPath = Path.Combine(runtimeDirectory!, "netstandard.dll");
        File.Exists(systemRuntimePath).Should().BeTrue($"expected '{systemRuntimePath}' to exist in the runtime directory");
        File.Exists(netstandardPath).Should().BeTrue($"expected '{netstandardPath}' to exist in the runtime directory");

        var assemblyPathMapField = GetEmitterStaticField("runtimeTypeResolutionAssemblyPathsByName");
        var originalAssemblyPathMap = assemblyPathMapField.GetValue(null);
        try
        {
            assemblyPathMapField.SetValue(
                null,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

            using var module = ModuleDefMD.Load(typeof(DuckTypeAotProcessorsTests).Assembly.Location);
            var testType = module.Find(typeof(DuckTypeAotProcessorsTests).FullName, isReflectionName: true);
            testType.Should().NotBeNull();
            var testMethod = testType!.FindMethod(nameof(DateTimeOffsetReturnForRuntimeTypeResolverTest));
            testMethod.Should().NotBeNull();

            var tryResolveMethod = GetEmitterStaticMethod("TryResolveRuntimeType", typeof(TypeSig));

            var runtimeType = (Type?)tryResolveMethod.Invoke(obj: null, parameters: new object[] { testMethod!.MethodSig.RetType });

            runtimeType.Should().Be(typeof(DateTimeOffset), "BCL facade assemblies can be resolved from the tool runtime directory and type-forward metadata references to the runtime implementation assembly");

            using var systemRuntimeModule = ModuleDefMD.Load(systemRuntimePath);
            using var netstandardModule = ModuleDefMD.Load(netstandardPath);
            var systemRuntimeDateTimeOffsetType = new TypeRefUser(systemRuntimeModule, "System", nameof(DateTimeOffset), new AssemblyRefUser(systemRuntimeModule.Assembly)).ToTypeSig();
            var netstandardDateTimeOffsetType = new TypeRefUser(netstandardModule, "System", nameof(DateTimeOffset), new AssemblyRefUser(netstandardModule.Assembly)).ToTypeSig();
            var areTypesEquivalentMethod = GetEmitterStaticMethod("AreTypesEquivalent", typeof(TypeSig), typeof(TypeSig));

            var equivalent = (bool)areTypesEquivalentMethod.Invoke(obj: null, parameters: new object[] { systemRuntimeDateTimeOffsetType, netstandardDateTimeOffsetType })!;

            equivalent.Should().BeTrue("signature comparison must follow transitive BCL type-forwarder chains such as netstandard -> System.Runtime -> runtime implementation");
        }
        finally
        {
            assemblyPathMapField.SetValue(null, originalAssemblyPathMap);
        }
    }

    [Fact]
    public void RuntimeAssemblyPathResolverShouldPreferRootPathWhenSimpleNameMatches()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var assemblyName = "DuckTypeAotRootPathWins";
            var rootDirectory = Path.Combine(tempDirectory, "root");
            var mappedDirectory = Path.Combine(tempDirectory, "mapped");
            Directory.CreateDirectory(rootDirectory);
            Directory.CreateDirectory(mappedDirectory);
            var rootAssemblyPath = Path.Combine(rootDirectory, assemblyName + ".dll");
            var mappedAssemblyPath = Path.Combine(mappedDirectory, assemblyName + ".dll");
            File.WriteAllBytes(rootAssemblyPath, Array.Empty<byte>());
            File.WriteAllBytes(mappedAssemblyPath, Array.Empty<byte>());

            var assemblyPathMapField = GetEmitterStaticField("runtimeTypeResolutionAssemblyPathsByName");
            var originalAssemblyPathMap = assemblyPathMapField.GetValue(null);
            try
            {
                assemblyPathMapField.SetValue(
                    null,
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        [assemblyName] = mappedAssemblyPath
                    });

                var resolvePathMethod = GetEmitterStaticMethod(
                    "TryResolveRuntimeResolutionAssemblyPath",
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string).MakeByRefType());
                var arguments = new object?[]
                {
                    assemblyName,
                    assemblyName,
                    rootAssemblyPath,
                    null
                };

                var resolved = (bool)resolvePathMethod.Invoke(obj: null, parameters: arguments)!;

                resolved.Should().BeTrue();
                arguments[3].Should().Be(rootAssemblyPath, "the current root assembly path must win over a same-name global path map entry");
            }
            finally
            {
                assemblyPathMapField.SetValue(null, originalAssemblyPathMap);
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    private static string CreateDuplicateAssignableTargetAssembly(string tempDirectory, string assemblyName, string marker = "default")
    {
        var projectDirectory = Path.Combine(tempDirectory, assemblyName);
        Directory.CreateDirectory(projectDirectory);

        var projectPath = Path.Combine(projectDirectory, $"{assemblyName}.csproj");
        var projectContents =
            $"""
             <Project Sdk="Microsoft.NET.Sdk">
               <PropertyGroup>
                 <TargetFramework>{TemporaryTargetAssemblyFramework}</TargetFramework>
                 <AssemblyName>{assemblyName}</AssemblyName>
                 <ImplicitUsings>disable</ImplicitUsings>
                 <Nullable>enable</Nullable>
               </PropertyGroup>
             </Project>
             """;
        File.WriteAllText(projectPath, projectContents);

        var targetsContents =
            $$"""
            namespace Duplicate.Targets
            {
                public sealed class BuildMarker
                {
                    public const string Value = "{{marker}}";
                }

                public class SharedBaseTarget
                {
                    public SharedBaseTarget(string value)
                    {
                        Value = value;
                    }

                    public virtual string Value { get; }
                }

                public class SharedDerivedTarget : SharedBaseTarget
                {
                    public SharedDerivedTarget(string value)
                        : base(value)
                    {
                    }
                }
            }
            """;
        File.WriteAllText(Path.Combine(projectDirectory, "Targets.cs"), targetsContents);

        var buildResult = RunProcess(
            "dotnet",
            projectDirectory,
            timeoutMilliseconds: 120_000,
            captureOutput: true,
            "build",
            projectPath,
            "-c",
            "Release",
            "-f",
            TemporaryTargetAssemblyFramework,
            "/nologo");
        buildResult.ExitCode.Should().Be(
            0,
            $"temporary duplicate target assembly should build.{Environment.NewLine}STDOUT:{Environment.NewLine}{buildResult.StandardOutput}{Environment.NewLine}STDERR:{Environment.NewLine}{buildResult.StandardError}");

        var assemblyPath = Path.Combine(projectDirectory, "bin", "Release", TemporaryTargetAssemblyFramework, $"{assemblyName}.dll");
        File.Exists(assemblyPath).Should().BeTrue($"temporary duplicate target assembly should exist at '{assemblyPath}'");
        return assemblyPath;
    }

    private static DateTimeOffset DateTimeOffsetReturnForRuntimeTypeResolverTest()
        => default;

    private static bool InvokeTryResolveRuntimeType(string assemblyName, string assemblyPath, string typeName, out Type? runtimeType)
    {
        var tryResolveRuntimeType = GetEmitterStaticMethod(
            "TryResolveRuntimeType",
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(Type).MakeByRefType());
        var arguments = new object?[]
        {
            assemblyName,
            assemblyPath,
            typeName,
            null
        };

        var resolved = (bool)tryResolveRuntimeType.Invoke(obj: null, parameters: arguments)!;
        runtimeType = (Type?)arguments[3];
        return resolved;
    }

    private static MethodInfo GetEmitterStaticMethod(string name, params Type[] parameterTypes)
    {
        var emitterType = typeof(DuckTypeAotMapping).Assembly.GetType("Datadog.Trace.Tools.Runner.DuckTypeAot.DuckTypeAotRegistryAssemblyEmitter");
        emitterType.Should().NotBeNull();
        var method = emitterType!.GetMethod(
            name,
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: parameterTypes,
            modifiers: null);
        method.Should().NotBeNull();
        return method!;
    }

    private static FieldInfo GetEmitterStaticField(string name)
    {
        var emitterType = typeof(DuckTypeAotMapping).Assembly.GetType("Datadog.Trace.Tools.Runner.DuckTypeAot.DuckTypeAotRegistryAssemblyEmitter");
        emitterType.Should().NotBeNull();
        var field = emitterType!.GetField(name, BindingFlags.Static | BindingFlags.NonPublic);
        field.Should().NotBeNull();
        return field!;
    }

    private static CommandResult RunProcess(
        string fileName,
        string workingDirectory,
        int timeoutMilliseconds,
        bool captureOutput,
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = captureOutput,
            RedirectStandardError = captureOutput,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start process '{fileName}'.");
        }

        Task<string>? standardOutputTask = null;
        Task<string>? standardErrorTask = null;
        if (captureOutput)
        {
            standardOutputTask = process.StandardOutput.ReadToEndAsync();
            standardErrorTask = process.StandardError.ReadToEndAsync();
        }

        if (!process.WaitForExit(timeoutMilliseconds))
        {
            try
            {
#if NETCOREAPP3_0_OR_GREATER || NET5_0_OR_GREATER
                process.Kill(entireProcessTree: true);
#else
                process.Kill();
#endif
            }
            catch
            {
                // Best-effort cleanup after timeout.
            }

            throw new TimeoutException($"Process '{fileName}' timed out after {timeoutMilliseconds}ms.");
        }

        return new CommandResult(
            process.ExitCode,
            standardOutputTask?.GetAwaiter().GetResult() ?? string.Empty,
            standardErrorTask?.GetAwaiter().GetResult() ?? string.Empty);
    }

    private readonly struct CommandResult
    {
        public CommandResult(int exitCode, string standardOutput, string standardError)
        {
            ExitCode = exitCode;
            StandardOutput = standardOutput;
            StandardError = standardError;
        }

        public int ExitCode { get; }

        public string StandardOutput { get; }

        public string StandardError { get; }
    }
}
