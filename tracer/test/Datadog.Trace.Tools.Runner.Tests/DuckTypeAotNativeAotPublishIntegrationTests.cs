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
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Tools.Runner.DuckTypeAot;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

public class DuckTypeAotNativeAotPublishIntegrationTests
{
    [Fact]
    public void NativeAotPublishShouldRunWithGeneratedDuckTypeRegistryAndWithoutDynamicEmit()
    {
        var runtimeIdentifier = ResolveRuntimeIdentifier();
        var solutionDirectory = EnvironmentTools.GetSolutionDirectory();
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

            File.WriteAllText(contractsProjectPath, BuildContractsProjectFile());
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

            var datadogTraceAssemblyPath = typeof(Datadog.Trace.Tracer).Assembly.Location;
            File.Exists(datadogTraceAssemblyPath).Should().BeTrue("Datadog.Trace assembly must exist for NativeAOT sample app reference");

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
                    "--target-assembly",
                    contractsAssemblyPath,
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
            File.Exists(propsPath).Should().BeTrue();

            File.WriteAllText(appProjectPath, BuildAppProjectFile(datadogTraceAssemblyPath, contractsProjectPath));
            File.WriteAllText(appSourcePath, BuildAppSourceFile());

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
            runResult.StandardOutput.Should().Contain("VALUE:42");
            runResult.StandardOutput.Should().Contain("DYNAMIC_CODE:False");
            runResult.StandardOutput.Should().Contain("DYNAMIC_ASSEMBLIES:0");
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    private static string BuildContractsProjectFile()
    {
        return
            "<Project Sdk=\"Microsoft.NET.Sdk\">" + Environment.NewLine +
            "  <PropertyGroup>" + Environment.NewLine +
            "    <TargetFramework>net8.0</TargetFramework>" + Environment.NewLine +
            "    <Nullable>enable</Nullable>" + Environment.NewLine +
            "  </PropertyGroup>" + Environment.NewLine +
            "</Project>" + Environment.NewLine;
    }

    private static string BuildContractsSourceFile()
    {
        return
            "namespace SampleDuckContracts;" + Environment.NewLine +
            Environment.NewLine +
            "public interface IValueProxy" + Environment.NewLine +
            "{" + Environment.NewLine +
            "    int GetValue();" + Environment.NewLine +
            "}" + Environment.NewLine +
            Environment.NewLine +
            "public sealed class ValueTarget" + Environment.NewLine +
            "{" + Environment.NewLine +
            "    private readonly int _value;" + Environment.NewLine +
            Environment.NewLine +
            "    public ValueTarget(int value)" + Environment.NewLine +
            "    {" + Environment.NewLine +
            "        _value = value;" + Environment.NewLine +
            "    }" + Environment.NewLine +
            Environment.NewLine +
            "    public int GetValue()" + Environment.NewLine +
            "    {" + Environment.NewLine +
            "        return _value;" + Environment.NewLine +
            "    }" + Environment.NewLine +
            "}" + Environment.NewLine;
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

    private static string BuildAppSourceFile()
    {
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
            "DuckTypeAotRegistryBootstrap.Initialize();" + Environment.NewLine +
            Environment.NewLine +
            "var createTypeResult = DuckType.GetOrCreateProxyType(typeof(IValueProxy), typeof(ValueTarget));" + Environment.NewLine +
            "if (!createTypeResult.CanCreate())" + Environment.NewLine +
            "{" + Environment.NewLine +
            "    Console.WriteLine(\"CAN_CREATE:False\");" + Environment.NewLine +
            "    Environment.ExitCode = 11;" + Environment.NewLine +
            "    return;" + Environment.NewLine +
            "}" + Environment.NewLine +
            Environment.NewLine +
            "var proxy = createTypeResult.CreateInstance<IValueProxy>(new ValueTarget(42));" + Environment.NewLine +
            "Console.WriteLine(\"CAN_CREATE:True\");" + Environment.NewLine +
            "Console.WriteLine($\"VALUE:{proxy.GetValue()}\");" + Environment.NewLine +
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
                throw new SkipException($"NativeAOT integration test is only supported on x64/arm64. Current architecture: {RuntimeInformation.ProcessArchitecture}");
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

        throw new SkipException("NativeAOT integration test is only supported on Windows, Linux, and macOS.");
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
