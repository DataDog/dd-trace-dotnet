// <copyright file="DuckTypeAotNativeAotPublishIntegrationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET8_0
#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Datadog.Trace.Tools.Runner.DuckTypeAot;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

public class DuckTypeAotNativeAotPublishIntegrationTests
{
    private const string RequireNativeAotToolchainEnvironmentVariable = "DD_DUCKTYPE_AOT_NATIVEAOT_REQUIRE_TOOLCHAIN";

    [Fact]
    public void NativeAotInfrastructureSkipDetectionShouldBeDisabledWhenToolchainIsRequired()
    {
        var previousValue = Environment.GetEnvironmentVariable(RequireNativeAotToolchainEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(RequireNativeAotToolchainEnvironmentVariable, "1");

            var result = new CommandResult(
                exitCode: 1,
                standardOutput: string.Empty,
                standardError: "Platform linker was not found");

            TryGetNativeAotInfrastructureSkipReason(result, out _)
               .Should()
               .BeFalse("CI can opt into hard failure when the NativeAOT toolchain is missing");
        }
        finally
        {
            Environment.SetEnvironmentVariable(RequireNativeAotToolchainEnvironmentVariable, previousValue);
        }
    }

    [Fact]
    public void NativeAotPublishShouldRunWithGeneratedDuckTypeRegistryAndWithoutDynamicEmit()
    {
        var runtimeIdentifier = ResolveRuntimeIdentifier();
        var tempDirectory = Path.Combine(Path.GetTempPath(), "dd-trace-ducktype-aot-nativeaot", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var contractsDirectory = Path.Combine(tempDirectory, "SampleDuckContracts");
            var appDirectory = Path.Combine(tempDirectory, "SampleDuckNativeAotApp");
            Directory.CreateDirectory(contractsDirectory);
            Directory.CreateDirectory(appDirectory);

            var contractsProjectPath = Path.Combine(contractsDirectory, "SampleDuckContracts.csproj");
            var contractsSourcePath = Path.Combine(contractsDirectory, "ValueContracts.cs");
            var appProjectPath = Path.Combine(appDirectory, "SampleDuckNativeAotApp.csproj");
            var appSourcePath = Path.Combine(appDirectory, "Program.cs");
            var datadogTraceAssemblyPath = typeof(Datadog.Trace.Tracer).Assembly.Location;
            File.Exists(datadogTraceAssemblyPath).Should().BeTrue("Datadog.Trace assembly must exist for NativeAOT sample references");

            File.WriteAllText(contractsProjectPath, BuildContractsProjectFile(datadogTraceAssemblyPath));
            File.WriteAllText(contractsSourcePath, BuildContractsSourceFile());

            var buildContractsResult = RunProcess(
                fileName: "dotnet",
                workingDirectory: contractsDirectory,
                timeoutMilliseconds: 180_000,
                captureOutput: false,
                arguments:
                [
                    "build",
                    contractsProjectPath,
                    "-c",
                    "Release"
                ]);

            buildContractsResult.ExitCode.Should().Be(0, "building sample contracts should succeed");

            var contractsAssemblyPath = Path.Combine(contractsDirectory, "bin", "Release", "net8.0", "SampleDuckContracts.dll");
            File.Exists(contractsAssemblyPath).Should().BeTrue("contracts assembly must exist before ducktype-aot generation");

            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-nativeaot-map.json");
            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = "SampleDuckContracts.IValueProxy",
                        proxyAssembly = "SampleDuckContracts",
                        targetType = "SampleDuckContracts.ValueTarget",
                        targetAssembly = "SampleDuckContracts"
                    },
                    new
                    {
                        mode = "reverse",
                        proxyType = "SampleDuckContracts.IReverseValueProxy",
                        proxyAssembly = "SampleDuckContracts",
                        targetType = "SampleDuckContracts.ReverseValueDelegation",
                        targetAssembly = "SampleDuckContracts"
                    },
                    new
                    {
                        mode = "forward",
                        proxyType = "SampleDuckContracts.ValueCopyProxy",
                        proxyAssembly = "SampleDuckContracts",
                        targetType = "SampleDuckContracts.ValueCopyTarget",
                        targetAssembly = "SampleDuckContracts"
                    },
                    new
                    {
                        mode = "forward",
                        proxyType = "SampleDuckContracts.IInnerProxy",
                        proxyAssembly = "SampleDuckContracts",
                        targetType = "SampleDuckContracts.InnerTarget",
                        targetAssembly = "SampleDuckContracts"
                    },
                    new
                    {
                        mode = "forward",
                        proxyType = "SampleDuckContracts.IChainProxy",
                        proxyAssembly = "SampleDuckContracts",
                        targetType = "SampleDuckContracts.ChainTarget",
                        targetAssembly = "SampleDuckContracts"
                    },
                    new
                    {
                        mode = "forward",
                        proxyType = "SampleDuckContracts.KitchenSinkProxy",
                        proxyAssembly = "SampleDuckContracts",
                        targetType = "SampleDuckContracts.KitchenSinkTarget",
                        targetAssembly = "SampleDuckContracts"
                    },
                    new
                    {
                        mode = "forward",
                        proxyType = "SampleDuckContracts.IFailureProxy",
                        proxyAssembly = "SampleDuckContracts",
                        targetType = "SampleDuckContracts.FailureTarget",
                        targetAssembly = "SampleDuckContracts"
                    },
                    new
                    {
                        mode = "reverse",
                        proxyType = "SampleDuckContracts.IReverseFailureProxy",
                        proxyAssembly = "SampleDuckContracts",
                        targetType = "SampleDuckContracts.ReverseFailureDelegation",
                        targetAssembly = "SampleDuckContracts"
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var outputAssemblyPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.NativeAotSample.dll");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.NativeAotSample.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.NativeAotSample.props");
            var runnerAssemblyPath = typeof(DuckTypeAotGenerateProcessor).Assembly.Location;

            var generateResult = RunProcess(
                fileName: "dotnet",
                workingDirectory: tempDirectory,
                timeoutMilliseconds: 180_000,
                captureOutput: false,
                arguments:
                [
                    runnerAssemblyPath,
                    "ducktype-aot",
                    "generate",
                    "--proxy-assembly",
                    contractsAssemblyPath,
                    "--target-folder",
                    Path.GetDirectoryName(contractsAssemblyPath) ?? tempDirectory,
                    "--target-filter",
                    "*.dll",
                    "--map-file",
                    mapFilePath,
                    "--output",
                    outputAssemblyPath,
                    "--assembly-name",
                    "Datadog.Trace.DuckType.AotRegistry.NativeAotSample",
                    "--emit-trimmer-descriptor",
                    trimmerDescriptorPath,
                    "--emit-props",
                    propsPath
                ]);

            generateResult.ExitCode.Should().Be(0, "ducktype-aot generation should succeed for the NativeAOT sample");
            File.Exists(outputAssemblyPath).Should().BeTrue();
            File.Exists(trimmerDescriptorPath).Should().BeTrue();
            File.Exists(propsPath).Should().BeTrue();
            File.ReadAllText(propsPath).Should().Contain("<TrimmerRootDescriptor Include=");
            File.ReadAllText(propsPath).Should().Contain(EscapeXml(trimmerDescriptorPath));

            File.WriteAllText(appProjectPath, BuildAppProjectFile(datadogTraceAssemblyPath, contractsProjectPath));
            File.WriteAllText(appSourcePath, BuildAppSourceFile(callBootstrapExplicitly: false));

            var publishOutputDirectory = Path.Combine(tempDirectory, "publish");
            var publishResult = RunProcess(
                fileName: "dotnet",
                workingDirectory: appDirectory,
                timeoutMilliseconds: 600_000,
                captureOutput: false,
                arguments:
                [
                    "publish",
                    appProjectPath,
                    "-c",
                    "Release",
                    "-r",
                    runtimeIdentifier,
                    "--self-contained",
                    "true",
                    "/p:PublishAot=true",
                    "/p:InvariantGlobalization=true",
                    $"/p:DuckTypeAotPropsPath={propsPath}",
                    "-o",
                    publishOutputDirectory
                ]);

            if (publishResult.ExitCode != 0)
            {
                var publishDiagnostics = RunProcess(
                    fileName: "dotnet",
                    workingDirectory: appDirectory,
                    timeoutMilliseconds: 180_000,
                    captureOutput: true,
                    arguments:
                    [
                        "publish",
                        appProjectPath,
                        "-c",
                        "Release",
                        "-r",
                        runtimeIdentifier,
                        "--self-contained",
                        "true",
                        "/p:PublishAot=true",
                        "/p:InvariantGlobalization=true",
                        $"/p:DuckTypeAotPropsPath={propsPath}",
                        "-o",
                        publishOutputDirectory
                    ]);

                if (publishDiagnostics.ExitCode != 0 &&
                    TryGetNativeAotInfrastructureSkipReason(publishDiagnostics, out var skipReason))
                {
                    throw new SkipException(
                        $"NativeAOT publish prerequisites are not available for runtime identifier '{runtimeIdentifier}'. {skipReason}");
                }

                publishDiagnostics.ExitCode.Should().Be(
                    0,
                    $"NativeAOT publish should succeed.{Environment.NewLine}STDOUT:{Environment.NewLine}{publishDiagnostics.StandardOutput}{Environment.NewLine}STDERR:{Environment.NewLine}{publishDiagnostics.StandardError}");
            }

            var executablePath = Path.Combine(
                publishOutputDirectory,
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "SampleDuckNativeAotApp.exe" : "SampleDuckNativeAotApp");
            File.Exists(executablePath).Should().BeTrue($"published NativeAOT executable was expected at '{executablePath}'.");

            var runResult = RunProcess(
                fileName: executablePath,
                workingDirectory: publishOutputDirectory,
                timeoutMilliseconds: 120_000,
                captureOutput: true,
                arguments: []);
            runResult.ExitCode.Should().Be(
                0,
                $"NativeAOT sample execution should succeed.{Environment.NewLine}STDOUT:{Environment.NewLine}{runResult.StandardOutput}{Environment.NewLine}STDERR:{Environment.NewLine}{runResult.StandardError}");

            runResult.StandardOutput.Should().Contain("CAN_CREATE:True");
            runResult.StandardOutput.Should().Contain("BOOTSTRAP:ModuleInitializer");
            runResult.StandardOutput.Should().Contain("VALUE:42");
            runResult.StandardOutput.Should().Contain("CAN_CREATE_REVERSE:True");
            runResult.StandardOutput.Should().Contain("REVERSE_VALUE:42");
            runResult.StandardOutput.Should().Contain("CAN_CREATE_COPY:True");
            runResult.StandardOutput.Should().Contain("COPY_VALUE:42");
            runResult.StandardOutput.Should().Contain("CAN_CREATE_CHAIN:True");
            runResult.StandardOutput.Should().Contain("CHAIN_VALUE:native-aot");
            runResult.StandardOutput.Should().Contain("CHAIN_RECEIVED:True");
            runResult.StandardOutput.Should().Contain("CAN_CREATE_KITCHEN:True");
            runResult.StandardOutput.Should().Contain("KITCHEN_INDEXER:native-index");
            runResult.StandardOutput.Should().Contain("KITCHEN_SECRET:native-secret");
            runResult.StandardOutput.Should().Contain("KITCHEN_GENERIC:123");
            runResult.StandardOutput.Should().Contain("KITCHEN_VALUE_WITH_TYPE:native-secret:native-index:String");
            runResult.StandardOutput.Should().Contain("KITCHEN_STATIC:native-static");
            runResult.StandardOutput.Should().Contain("TYPED_METHOD_HANDLE:ArgumentException");
            runResult.StandardOutput.Should().Contain("FAILURE_REPLAY:DuckTypeTargetMethodNotFoundException");
            runResult.StandardOutput.Should().Contain("FAILURE_NON_GENERIC_REPLAY:TargetInvocationException:DuckTypeTargetMethodNotFoundException");
            runResult.StandardOutput.Should().Contain("REVERSE_FAILURE_NON_GENERIC_REPLAY:TargetInvocationException:DuckTypeReverseProxyMissingMethodImplementationException");
            runResult.StandardOutput.Should().Contain("MISS_REPLAY:TargetInvocationException:DuckTypeAotMissingProxyRegistrationException");
            runResult.StandardOutput.Should().Contain("DYNAMIC_CODE:False");
            runResult.StandardOutput.Should().Contain("DYNAMIC_ASSEMBLIES:0");
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    private static string BuildContractsProjectFile(string datadogTraceAssemblyPath)
    {
        return
            "<Project Sdk=\"Microsoft.NET.Sdk\">" + Environment.NewLine +
            "  <PropertyGroup>" + Environment.NewLine +
            "    <TargetFramework>net8.0</TargetFramework>" + Environment.NewLine +
            "    <Nullable>enable</Nullable>" + Environment.NewLine +
            "  </PropertyGroup>" + Environment.NewLine +
            "  <ItemGroup>" + Environment.NewLine +
            "    <Reference Include=\"Datadog.Trace\">" + Environment.NewLine +
            $"      <HintPath>{EscapeXml(datadogTraceAssemblyPath)}</HintPath>" + Environment.NewLine +
            "      <Private>true</Private>" + Environment.NewLine +
            "    </Reference>" + Environment.NewLine +
            "  </ItemGroup>" + Environment.NewLine +
            "</Project>" + Environment.NewLine;
    }

    private static string BuildContractsSourceFile()
    {
        return string.Join(
                   Environment.NewLine,
                   new[]
                   {
                       "using System;",
                       "using System.Collections.Generic;",
                       "using System.Reflection;",
                       "using Datadog.Trace.DuckTyping;",
                       string.Empty,
                       "namespace Datadog.Trace.DuckTyping",
                       "{",
                       "    [AttributeUsage(AttributeTargets.Struct)]",
                       "    public sealed class DuckCopyAttribute : Attribute",
                       "    {",
                       "    }",
                       string.Empty,
                       "    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]",
                       "    public sealed class DuckFieldAttribute : Attribute",
                       "    {",
                       "        public string? Name { get; set; }",
                       string.Empty,
                       "        public BindingFlags BindingFlags { get; set; } = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy;",
                       "    }",
                       "}",
                       string.Empty,
                       "namespace SampleDuckContracts",
                       "{",
                       "    public interface IValueProxy",
                       "    {",
                       "        int GetValue();",
                       "    }",
                       string.Empty,
                       "    public interface IReverseValueProxy",
                       "    {",
                       "        int DoubleValue(int value);",
                       "    }",
                       string.Empty,
                       "    public interface IInnerProxy",
                       "    {",
                       "        string Name { get; }",
                       "    }",
                       string.Empty,
                       "    public interface IChainProxy",
                       "    {",
                       "        IInnerProxy Roundtrip(IInnerProxy value);",
                       "    }",
                       string.Empty,
                       "    public interface IFailureProxy",
                       "    {",
                       "        string Missing();",
                       "    }",
                       string.Empty,
                       "    public interface IReverseFailureProxy",
                       "    {",
                       "        string Missing();",
                       "    }",
                       string.Empty,
                       "    public interface IMissingProxy",
                       "    {",
                       "        string Value { get; }",
                       "    }",
                       string.Empty,
                       "    public interface IManualHandleProxy",
                       "    {",
                       "        int GetValue();",
                       "    }",
                       string.Empty,
                       "    [DuckCopy]",
                       "    public struct ValueCopyProxy",
                       "    {",
                       "        public int Value;",
                       "    }",
                       string.Empty,
                       "    public abstract class KitchenSinkProxy",
                       "    {",
                       "        public abstract string this[int index] { get; set; }",
                       string.Empty,
                       "        [DuckField(Name = \"_secret\")]",
                       "        public abstract string Secret { get; set; }",
                       string.Empty,
                       "        [DuckField(Name = \"StaticText\", BindingFlags = BindingFlags.Public | BindingFlags.Static)]",
                       "        public abstract string StaticValue { get; set; }",
                       string.Empty,
                       "        public abstract T Echo<T>(T value);",
                       string.Empty,
                       "        public abstract ValueWithType<string> Describe();",
                       "    }",
                       string.Empty,
                       "    public sealed class ValueTarget",
                       "    {",
                       "        private readonly int _value;",
                       string.Empty,
                       "        public ValueTarget(int value)",
                       "        {",
                       "            _value = value;",
                       "        }",
                       string.Empty,
                       "        public int GetValue()",
                       "        {",
                       "            return _value;",
                       "        }",
                       "    }",
                       string.Empty,
                       "    public sealed class ReverseValueDelegation",
                       "    {",
                       "        public int DoubleValue(int value)",
                       "        {",
                       "            return value * 2;",
                       "        }",
                       "    }",
                       string.Empty,
                       "    public sealed class ValueCopyTarget",
                       "    {",
                       "        public ValueCopyTarget(int value)",
                       "        {",
                       "            Value = value;",
                       "        }",
                       string.Empty,
                       "        public int Value { get; set; }",
                       "    }",
                       string.Empty,
                       "    public sealed class InnerTarget",
                       "    {",
                       "        public InnerTarget(string name)",
                       "        {",
                       "            Name = name;",
                       "        }",
                       string.Empty,
                       "        public string Name { get; }",
                       "    }",
                       string.Empty,
                       "    public sealed class ChainTarget",
                       "    {",
                       "        public InnerTarget? LastReceived { get; private set; }",
                       string.Empty,
                       "        public InnerTarget Roundtrip(InnerTarget value)",
                       "        {",
                       "            LastReceived = value;",
                       "            return value;",
                       "        }",
                       "    }",
                       string.Empty,
                       "    public sealed class KitchenSinkTarget",
                       "    {",
                       "        private readonly Dictionary<int, string> _items = new Dictionary<int, string>();",
                       "        private string _secret;",
                       string.Empty,
                       "        public KitchenSinkTarget(string secret, string item)",
                       "        {",
                       "            _secret = secret;",
                       "            _items[7] = item;",
                       "        }",
                       string.Empty,
                       "        public static string StaticText = \"initial-static\";",
                       string.Empty,
                       "        public string this[int index]",
                       "        {",
                       "            get => _items[index];",
                       "            set => _items[index] = value;",
                       "        }",
                       string.Empty,
                       "        public T Echo<T>(T value)",
                       "        {",
                       "            return value;",
                       "        }",
                       string.Empty,
                       "        public string Describe()",
                       "        {",
                       "            return _secret + \":\" + _items[7];",
                       "        }",
                       "    }",
                       string.Empty,
                       "    public sealed class ManualHandleTarget",
                       "    {",
                       "        public ManualHandleTarget(int value)",
                       "        {",
                       "            Value = value;",
                       "        }",
                       string.Empty,
                       "        public int Value { get; }",
                       "    }",
                       string.Empty,
                       "    public sealed class ManualHandleGeneratedProxy : IManualHandleProxy",
                       "    {",
                       "        private readonly ManualHandleTarget _target;",
                       string.Empty,
                       "        public ManualHandleGeneratedProxy(ManualHandleTarget target)",
                       "        {",
                       "            _target = target;",
                       "        }",
                       string.Empty,
                       "        public int GetValue()",
                       "        {",
                       "            return _target.Value;",
                       "        }",
                       string.Empty,
                       "        public static IManualHandleProxy CreateTyped(ManualHandleTarget target)",
                       "        {",
                       "            return new ManualHandleGeneratedProxy(target);",
                       "        }",
                       "    }",
                       string.Empty,
                       "    public sealed class FailureTarget",
                       "    {",
                       "    }",
                       string.Empty,
                       "    public sealed class ReverseFailureDelegation",
                       "    {",
                       "    }",
                       string.Empty,
                       "    public sealed class MissingTarget",
                       "    {",
                       "        public string Value => \"missing\";",
                       "    }",
                       "}"
                   }) + Environment.NewLine;
    }

    private static string BuildAppProjectFile(string datadogTraceAssemblyPath, string contractsProjectPath)
    {
        return
            "<Project Sdk=\"Microsoft.NET.Sdk\">" + Environment.NewLine +
            "  <PropertyGroup>" + Environment.NewLine +
            "    <OutputType>Exe</OutputType>" + Environment.NewLine +
            "    <TargetFramework>net8.0</TargetFramework>" + Environment.NewLine +
            "    <Nullable>enable</Nullable>" + Environment.NewLine +
            "    <ImplicitUsings>enable</ImplicitUsings>" + Environment.NewLine +
            "  </PropertyGroup>" + Environment.NewLine +
            "  <ItemGroup>" + Environment.NewLine +
            "    <Reference Include=\"Datadog.Trace\">" + Environment.NewLine +
            $"      <HintPath>{EscapeXml(datadogTraceAssemblyPath)}</HintPath>" + Environment.NewLine +
            "      <Private>true</Private>" + Environment.NewLine +
            "    </Reference>" + Environment.NewLine +
            $"    <ProjectReference Include=\"{EscapeXml(contractsProjectPath)}\" />" + Environment.NewLine +
            "  </ItemGroup>" + Environment.NewLine +
            "  <Import Project=\"$(DuckTypeAotPropsPath)\" Condition=\"'$(DuckTypeAotPropsPath)' != '' and Exists('$(DuckTypeAotPropsPath)')\" />" + Environment.NewLine +
            "</Project>" + Environment.NewLine;
    }

    private static string BuildAppSourceFile(bool callBootstrapExplicitly)
    {
        var bootstrapLine = callBootstrapExplicitly
            ? "DuckTypeAotRegistryBootstrap.Initialize();"
            : "_ = typeof(DuckTypeAotRegistryBootstrap).Assembly.FullName;";
        var bootstrapMode = callBootstrapExplicitly ? "Explicit" : "ModuleInitializer";
        return
            "using System;" + Environment.NewLine +
            "using System.Runtime.CompilerServices;" + Environment.NewLine +
            "using Datadog.Trace.DuckTyping;" + Environment.NewLine +
            "using Datadog.Trace.DuckTyping.Generated;" + Environment.NewLine +
            "using SampleDuckContracts;" + Environment.NewLine +
            Environment.NewLine +
            "var dynamicAssemblyLoads = 0;" + Environment.NewLine +
            "AppDomain.CurrentDomain.AssemblyLoad += (_, args) =>" + Environment.NewLine +
            "{" + Environment.NewLine +
            "    if (args.LoadedAssembly.IsDynamic)" + Environment.NewLine +
            "    {" + Environment.NewLine +
            "        dynamicAssemblyLoads++;" + Environment.NewLine +
            "    }" + Environment.NewLine +
            "};" + Environment.NewLine +
            Environment.NewLine +
            bootstrapLine + Environment.NewLine +
            $"Console.WriteLine(\"BOOTSTRAP:{bootstrapMode}\");" + Environment.NewLine +
            Environment.NewLine +
            "var createTypeResult = DuckType.GetOrCreateProxyType(typeof(IValueProxy), typeof(ValueTarget));" + Environment.NewLine +
            "if (!createTypeResult.CanCreate())" + Environment.NewLine +
            "{" + Environment.NewLine +
            "    Console.WriteLine(\"CAN_CREATE:False\");" + Environment.NewLine +
            "    Environment.ExitCode = 11;" + Environment.NewLine +
            "    return;" + Environment.NewLine +
            "}" + Environment.NewLine +
            Environment.NewLine +
            "var createReverseTypeResult = DuckType.GetOrCreateReverseProxyType(typeof(IReverseValueProxy), typeof(ReverseValueDelegation));" + Environment.NewLine +
            "if (!createReverseTypeResult.CanCreate())" + Environment.NewLine +
            "{" + Environment.NewLine +
            "    Console.WriteLine(\"CAN_CREATE_REVERSE:False\");" + Environment.NewLine +
            "    Environment.ExitCode = 12;" + Environment.NewLine +
            "    return;" + Environment.NewLine +
            "}" + Environment.NewLine +
            Environment.NewLine +
            "var createCopyTypeResult = DuckType.GetOrCreateProxyType(typeof(ValueCopyProxy), typeof(ValueCopyTarget));" + Environment.NewLine +
            "if (!createCopyTypeResult.CanCreate())" + Environment.NewLine +
            "{" + Environment.NewLine +
            "    Console.WriteLine(\"CAN_CREATE_COPY:False\");" + Environment.NewLine +
            "    Environment.ExitCode = 13;" + Environment.NewLine +
            "    return;" + Environment.NewLine +
            "}" + Environment.NewLine +
            Environment.NewLine +
            "var createChainTypeResult = DuckType.GetOrCreateProxyType(typeof(IChainProxy), typeof(ChainTarget));" + Environment.NewLine +
            "if (!createChainTypeResult.CanCreate())" + Environment.NewLine +
            "{" + Environment.NewLine +
            "    Console.WriteLine(\"CAN_CREATE_CHAIN:False\");" + Environment.NewLine +
            "    Environment.ExitCode = 14;" + Environment.NewLine +
            "    return;" + Environment.NewLine +
            "}" + Environment.NewLine +
            Environment.NewLine +
            "var createKitchenTypeResult = DuckType.GetOrCreateProxyType(typeof(KitchenSinkProxy), typeof(KitchenSinkTarget));" + Environment.NewLine +
            "if (!createKitchenTypeResult.CanCreate())" + Environment.NewLine +
            "{" + Environment.NewLine +
            "    Console.WriteLine(\"CAN_CREATE_KITCHEN:False\");" + Environment.NewLine +
            "    Environment.ExitCode = 15;" + Environment.NewLine +
            "    return;" + Environment.NewLine +
            "}" + Environment.NewLine +
            Environment.NewLine +
            "var proxy = createTypeResult.CreateInstance<IValueProxy>(new ValueTarget(42));" + Environment.NewLine +
            "var reverseProxy = (IReverseValueProxy)DuckType.CreateReverse(typeof(IReverseValueProxy), new ReverseValueDelegation());" + Environment.NewLine +
            "var copyProxy = createCopyTypeResult.CreateInstance<ValueCopyProxy>(new ValueCopyTarget(42));" + Environment.NewLine +
            "var chainTarget = new ChainTarget();" + Environment.NewLine +
            "var chainProxy = createChainTypeResult.CreateInstance<IChainProxy>(chainTarget);" + Environment.NewLine +
            "var innerProxy = DuckType.Create<IInnerProxy>(new InnerTarget(\"native-aot\"));" + Environment.NewLine +
            "var chainResult = chainProxy.Roundtrip(innerProxy!);" + Environment.NewLine +
            "var kitchenTarget = new KitchenSinkTarget(\"initial-secret\", \"initial-index\");" + Environment.NewLine +
            "var kitchenProxy = createKitchenTypeResult.CreateInstance<KitchenSinkProxy>(kitchenTarget);" + Environment.NewLine +
            "kitchenProxy[7] = \"native-index\";" + Environment.NewLine +
            "kitchenProxy.Secret = \"native-secret\";" + Environment.NewLine +
            "KitchenSinkTarget.StaticText = \"initial-static\";" + Environment.NewLine +
            "kitchenProxy.StaticValue = \"native-static\";" + Environment.NewLine +
            "var kitchenValueWithType = kitchenProxy.Describe();" + Environment.NewLine +
            "Console.WriteLine(\"CAN_CREATE:True\");" + Environment.NewLine +
            "Console.WriteLine($\"VALUE:{proxy.GetValue()}\");" + Environment.NewLine +
            "Console.WriteLine(\"CAN_CREATE_REVERSE:True\");" + Environment.NewLine +
            "Console.WriteLine($\"REVERSE_VALUE:{reverseProxy.DoubleValue(21)}\");" + Environment.NewLine +
            "Console.WriteLine(\"CAN_CREATE_COPY:True\");" + Environment.NewLine +
            "Console.WriteLine($\"COPY_VALUE:{copyProxy.Value}\");" + Environment.NewLine +
            "Console.WriteLine(\"CAN_CREATE_CHAIN:True\");" + Environment.NewLine +
            "Console.WriteLine($\"CHAIN_VALUE:{chainResult.Name}\");" + Environment.NewLine +
            "Console.WriteLine($\"CHAIN_RECEIVED:{object.ReferenceEquals(chainTarget.LastReceived, ((IDuckType)innerProxy!).Instance)}\");" + Environment.NewLine +
            "Console.WriteLine(\"CAN_CREATE_KITCHEN:True\");" + Environment.NewLine +
            "Console.WriteLine($\"KITCHEN_INDEXER:{kitchenProxy[7]}\");" + Environment.NewLine +
            "Console.WriteLine($\"KITCHEN_SECRET:{kitchenProxy.Secret}\");" + Environment.NewLine +
            "Console.WriteLine($\"KITCHEN_GENERIC:{kitchenProxy.Echo(123)}\");" + Environment.NewLine +
            "Console.WriteLine($\"KITCHEN_VALUE_WITH_TYPE:{kitchenValueWithType.Value}:{kitchenValueWithType.Type.Name}\");" + Environment.NewLine +
            "Console.WriteLine($\"KITCHEN_STATIC:{KitchenSinkTarget.StaticText}\");" + Environment.NewLine +
            "try" + Environment.NewLine +
            "{" + Environment.NewLine +
            "    var typedHandle = ((Func<ManualHandleTarget, IManualHandleProxy>)ManualHandleGeneratedProxy.CreateTyped).Method.MethodHandle;" + Environment.NewLine +
            "    DuckType.RegisterAotProxy(typeof(IManualHandleProxy), typeof(ManualHandleTarget), typeof(ManualHandleGeneratedProxy), typedHandle);" + Environment.NewLine +
            "    Console.WriteLine(\"TYPED_METHOD_HANDLE:NoException\");" + Environment.NewLine +
            "    Environment.ExitCode = 16;" + Environment.NewLine +
            "    return;" + Environment.NewLine +
            "}" + Environment.NewLine +
            "catch (ArgumentException ex)" + Environment.NewLine +
            "{" + Environment.NewLine +
            "    Console.WriteLine($\"TYPED_METHOD_HANDLE:{ex.GetType().Name}\");" + Environment.NewLine +
            "}" + Environment.NewLine +
            "var failureResult = DuckType.GetOrCreateProxyType(typeof(IFailureProxy), typeof(FailureTarget));" + Environment.NewLine +
            "if (failureResult.CanCreate())" + Environment.NewLine +
            "{" + Environment.NewLine +
            "    Console.WriteLine(\"FAILURE_REPLAY:CanCreate\");" + Environment.NewLine +
            "    Environment.ExitCode = 17;" + Environment.NewLine +
            "    return;" + Environment.NewLine +
            "}" + Environment.NewLine +
            Environment.NewLine +
            "try" + Environment.NewLine +
            "{" + Environment.NewLine +
            "    _ = failureResult.CreateInstance<IFailureProxy>(new FailureTarget());" + Environment.NewLine +
            "    Console.WriteLine(\"FAILURE_REPLAY:NoException\");" + Environment.NewLine +
            "    Environment.ExitCode = 18;" + Environment.NewLine +
            "    return;" + Environment.NewLine +
            "}" + Environment.NewLine +
            "catch (Exception ex)" + Environment.NewLine +
            "{" + Environment.NewLine +
            "    Console.WriteLine($\"FAILURE_REPLAY:{ex.GetType().Name}\");" + Environment.NewLine +
            "}" + Environment.NewLine +
            Environment.NewLine +
            "try" + Environment.NewLine +
            "{" + Environment.NewLine +
            "    _ = DuckType.Create(typeof(IFailureProxy), new FailureTarget());" + Environment.NewLine +
            "    Console.WriteLine(\"FAILURE_NON_GENERIC_REPLAY:NoException\");" + Environment.NewLine +
            "    Environment.ExitCode = 19;" + Environment.NewLine +
            "    return;" + Environment.NewLine +
                "}" + Environment.NewLine +
                "catch (Exception ex)" + Environment.NewLine +
                "{" + Environment.NewLine +
                "    Console.WriteLine($\"FAILURE_NON_GENERIC_REPLAY:{ex.GetType().Name}:{ex.InnerException?.GetType().Name}\");" + Environment.NewLine +
                "}" + Environment.NewLine +
                Environment.NewLine +
                "try" + Environment.NewLine +
                "{" + Environment.NewLine +
                "    _ = DuckType.CreateReverse(typeof(IReverseFailureProxy), new ReverseFailureDelegation());" + Environment.NewLine +
                "    Console.WriteLine(\"REVERSE_FAILURE_NON_GENERIC_REPLAY:NoException\");" + Environment.NewLine +
                "    Environment.ExitCode = 20;" + Environment.NewLine +
                "    return;" + Environment.NewLine +
                "}" + Environment.NewLine +
                "catch (Exception ex)" + Environment.NewLine +
                "{" + Environment.NewLine +
                "    Console.WriteLine($\"REVERSE_FAILURE_NON_GENERIC_REPLAY:{ex.GetType().Name}:{ex.InnerException?.GetType().Name}\");" + Environment.NewLine +
                "}" + Environment.NewLine +
                Environment.NewLine +
                "try" + Environment.NewLine +
                "{" + Environment.NewLine +
                "    _ = DuckType.Create(typeof(IMissingProxy), new MissingTarget());" + Environment.NewLine +
                "    Console.WriteLine(\"MISS_REPLAY:NoException\");" + Environment.NewLine +
                "    Environment.ExitCode = 21;" + Environment.NewLine +
                "    return;" + Environment.NewLine +
                "}" + Environment.NewLine +
            "catch (Exception ex)" + Environment.NewLine +
            "{" + Environment.NewLine +
            "    Console.WriteLine($\"MISS_REPLAY:{ex.GetType().Name}:{ex.InnerException?.GetType().Name}\");" + Environment.NewLine +
            "}" + Environment.NewLine +
            "Console.WriteLine($\"DYNAMIC_CODE:{RuntimeFeature.IsDynamicCodeSupported}\");" + Environment.NewLine +
            "Console.WriteLine($\"DYNAMIC_ASSEMBLIES:{dynamicAssemblyLoads}\");" + Environment.NewLine;
    }

    private static string ResolveRuntimeIdentifier()
    {
        string architectureSuffix;
        switch (RuntimeInformation.ProcessArchitecture)
        {
            case Architecture.X64:
                architectureSuffix = "x64";
                break;
            case Architecture.Arm64:
                architectureSuffix = "arm64";
                break;
            default:
                ThrowNativeAotInfrastructureUnavailable($"NativeAOT integration test is only supported on x64/arm64. Current architecture: {RuntimeInformation.ProcessArchitecture}");
                throw new InvalidOperationException("Unreachable NativeAOT infrastructure branch.");
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return $"win-{architectureSuffix}";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return $"linux-{architectureSuffix}";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return $"osx-{architectureSuffix}";
        }

        ThrowNativeAotInfrastructureUnavailable("NativeAOT integration test is only supported on Windows, Linux, and macOS.");
        throw new InvalidOperationException("Unreachable NativeAOT infrastructure branch.");
    }

    private static bool TryGetNativeAotInfrastructureSkipReason(CommandResult result, out string reason)
    {
        if (IsNativeAotToolchainRequired())
        {
            reason = string.Empty;
            return false;
        }

        var combined = $"{result.StandardOutput}{Environment.NewLine}{result.StandardError}";
        var knownInfrastructureMarkers = new[]
        {
            "NETSDK1183",
            "Native compilation is not supported in this environment",
            "Platform linker was not found",
            "Platform linker ('clang' or 'gcc') was not found",
            "requires Xcode",
            "xcode-select: error",
            "The command \"clang\" exited with code 127"
        };

        foreach (var marker in knownInfrastructureMarkers)
        {
            if (combined.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                reason = $"Detected infrastructure limitation marker '{marker}'.";
                return true;
            }
        }

        reason = string.Empty;
        return false;
    }

    private static void ThrowNativeAotInfrastructureUnavailable(string reason)
    {
        if (IsNativeAotToolchainRequired())
        {
            throw new InvalidOperationException(reason);
        }

        throw new SkipException(reason);
    }

    private static bool IsNativeAotToolchainRequired()
    {
        var value = Environment.GetEnvironmentVariable(RequireNativeAotToolchainEnvironmentVariable);
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static CommandResult RunProcess(string fileName, string workingDirectory, int timeoutMilliseconds, bool captureOutput, string[] arguments)
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
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best-effort kill for timeout handling.
            }

            throw new TimeoutException($"Process '{fileName}' timed out after {timeoutMilliseconds}ms.");
        }

        if (!captureOutput)
        {
            return new CommandResult(process.ExitCode, string.Empty, string.Empty);
        }

        Task.WaitAll(standardOutputTask!, standardErrorTask!);
        return new CommandResult(process.ExitCode, standardOutputTask!.Result, standardErrorTask!.Result);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    private static string EscapeXml(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
    }

    private readonly struct CommandResult
    {
        internal CommandResult(int exitCode, string standardOutput, string standardError)
        {
            ExitCode = exitCode;
            StandardOutput = standardOutput;
            StandardError = standardError;
        }

        internal int ExitCode { get; }

        internal string StandardOutput { get; }

        internal string StandardError { get; }
    }
}
#endif
