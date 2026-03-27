// <copyright file="CallTargetAotNativeAotPublishIntegrationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET8_0
#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.CallTargetNativeAot;
using Datadog.Trace.Tools.Runner.CallTargetAot;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// Verifies the first CallTarget NativeAOT milestone: generated props and targets can rewrite the compiled app assembly before publish,
/// and the final native executable runs the injected module initializer bootstrap.
/// </summary>
public class CallTargetAotNativeAotPublishIntegrationTests
{
    /// <summary>
    /// Builds a minimal app, generates CallTarget AOT milestone artifacts, publishes with NativeAOT, and verifies the injected bootstrap executes.
    /// </summary>
    [Fact]
    public void NativeAotPublishShouldConsumeRewrittenIntermediateAssemblyAndExecuteInjectedBootstrap()
    {
        var runtimeIdentifier = ResolveRuntimeIdentifier();
        var tempDirectory = Path.Combine(Path.GetTempPath(), "dd-trace-calltarget-aot-nativeaot", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var libraryDirectory = Path.Combine(tempDirectory, "SampleCallTargetNativeAotLibrary");
            Directory.CreateDirectory(libraryDirectory);
            var libraryProjectPath = Path.Combine(libraryDirectory, "SampleCallTargetNativeAotLibrary.csproj");
            var librarySourcePath = Path.Combine(libraryDirectory, "ReferencedTarget.cs");
            File.WriteAllText(libraryProjectPath, BuildLibraryProjectFile());
            File.WriteAllText(librarySourcePath, BuildLibrarySourceFile());

            var appDirectory = Path.Combine(tempDirectory, "SampleCallTargetNativeAotApp");
            Directory.CreateDirectory(appDirectory);

            var appProjectPath = Path.Combine(appDirectory, "SampleCallTargetNativeAotApp.csproj");
            var appSourcePath = Path.Combine(appDirectory, "Program.cs");
            File.WriteAllText(appProjectPath, BuildAppProjectFile(libraryProjectPath));
            File.WriteAllText(appSourcePath, BuildAppSourceFile());

            var buildResult = RunProcess(
                fileName: "dotnet",
                workingDirectory: appDirectory,
                timeoutMilliseconds: 180_000,
                captureOutput: false,
                arguments:
                [
                    "build",
                    appProjectPath,
                    "-c",
                    "Release"
                ]);
            buildResult.ExitCode.Should().Be(0, "building the milestone sample app should succeed");

            var appAssemblyPath = Path.Combine(appDirectory, "bin", "Release", "net8.0", "SampleCallTargetNativeAotApp.dll");
            File.Exists(appAssemblyPath).Should().BeTrue("the sample app assembly must exist before calltarget-aot generation");

            var runnerAssemblyPath = typeof(CallTargetAotGenerateProcessor).Assembly.Location;
            var tracerAssemblyPath = typeof(Datadog.Trace.Tracer).Assembly.Location;
            var outputAssemblyPath = Path.Combine(tempDirectory, "Datadog.Trace.CallTarget.AotRegistry.NativeAotSample.dll");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "Datadog.Trace.CallTarget.AotRegistry.NativeAotSample.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "Datadog.Trace.CallTarget.AotRegistry.NativeAotSample.props");
            var targetsPath = Path.Combine(tempDirectory, "Datadog.Trace.CallTarget.AotRegistry.NativeAotSample.targets");
            var manifestPath = Path.Combine(tempDirectory, "Datadog.Trace.CallTarget.AotRegistry.NativeAotSample.manifest.json");
            var rewritePlanPath = Path.Combine(tempDirectory, "Datadog.Trace.CallTarget.AotRegistry.NativeAotSample.rewrite-plan.json");
            var compatibilityReportPath = Path.Combine(tempDirectory, "Datadog.Trace.CallTarget.AotRegistry.NativeAotSample.compat.md");
            var compatibilityMatrixPath = Path.Combine(tempDirectory, "Datadog.Trace.CallTarget.AotRegistry.NativeAotSample.compat.json");

            var generateResult = RunProcess(
                fileName: "dotnet",
                workingDirectory: tempDirectory,
                timeoutMilliseconds: 180_000,
                captureOutput: false,
                arguments:
                [
                    "exec",
                    "--roll-forward",
                    "Major",
                    runnerAssemblyPath,
                    "calltarget-aot",
                    "generate",
                    "--tracer-assembly",
                    tracerAssemblyPath,
                    "--target-folder",
                    Path.GetDirectoryName(appAssemblyPath) ?? tempDirectory,
                    "--target-filter",
                    Path.GetFileName(appAssemblyPath),
                    "--target-filter",
                    "SampleCallTargetNativeAotLibrary.dll",
                    "--output",
                    outputAssemblyPath,
                    "--assembly-name",
                    "Datadog.Trace.CallTarget.AotRegistry.NativeAotSample",
                    "--emit-trimmer-descriptor",
                    trimmerDescriptorPath,
                    "--emit-props",
                    propsPath,
                    "--emit-targets",
                    targetsPath,
                    "--emit-manifest",
                    manifestPath,
                    "--emit-rewrite-plan",
                    rewritePlanPath,
                    "--emit-compat-report",
                    compatibilityReportPath,
                    "--emit-compat-matrix",
                    compatibilityMatrixPath
                ]);
            generateResult.ExitCode.Should().Be(0, "calltarget-aot generation should succeed for the NativeAOT milestone sample");
            File.Exists(outputAssemblyPath).Should().BeTrue();
            File.Exists(propsPath).Should().BeTrue();
            File.Exists(targetsPath).Should().BeTrue();
            File.Exists(manifestPath).Should().BeTrue();
            File.Exists(rewritePlanPath).Should().BeTrue();
            File.Exists(compatibilityReportPath).Should().BeTrue();
            File.Exists(compatibilityMatrixPath).Should().BeTrue();
            File.ReadAllText(rewritePlanPath).Should().Contain("SampleCallTargetNativeAotLibrary.dll");
            File.ReadAllText(compatibilityReportPath).Should().Contain("ExecuteReference");
            File.ReadAllText(compatibilityMatrixPath).Should().Contain("\"status\": \"compatible\"");

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
                    $"/p:CallTargetAotPropsPath={propsPath}",
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
                        $"/p:CallTargetAotPropsPath={propsPath}",
                        "-o",
                        publishOutputDirectory
                    ]);

                if (publishDiagnostics.ExitCode != 0 &&
                    TryGetNativeAotInfrastructureSkipReason(publishDiagnostics, out var skipReason))
                {
                    throw new SkipException($"NativeAOT publish prerequisites are not available for runtime identifier '{runtimeIdentifier}'. {skipReason}");
                }

                publishDiagnostics.ExitCode.Should().Be(
                    0,
                    $"NativeAOT publish should succeed.{Environment.NewLine}STDOUT:{Environment.NewLine}{publishDiagnostics.StandardOutput}{Environment.NewLine}STDERR:{Environment.NewLine}{publishDiagnostics.StandardError}");
            }

            var executablePath = Path.Combine(
                publishOutputDirectory,
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "SampleCallTargetNativeAotApp.exe" : "SampleCallTargetNativeAotApp");
            File.Exists(executablePath).Should().BeTrue($"published NativeAOT executable was expected at '{executablePath}'.");

            var runResult = RunProcess(
                fileName: executablePath,
                workingDirectory: publishOutputDirectory,
                timeoutMilliseconds: 120_000,
                captureOutput: true,
                arguments: []);
            runResult.ExitCode.Should().Be(
                0,
                $"NativeAOT milestone sample execution should succeed.{Environment.NewLine}STDOUT:{Environment.NewLine}{runResult.StandardOutput}{Environment.NewLine}STDERR:{Environment.NewLine}{runResult.StandardError}");

            runResult.StandardOutput.Should().Contain(CallTargetAotRegistryAssemblyEmitter.BootstrapMarker);
            runResult.StandardOutput.Should().Contain(SampleCallTargetNativeAotIntegration.BeginMarker);
            runResult.StandardOutput.Should().Contain("TARGET:1");
            runResult.StandardOutput.Should().Contain(SampleCallTargetNativeAotIntegration.EndMarker);
            runResult.StandardOutput.Should().Contain(SampleCallTargetNativeAotIntegration.BeginWithValueMarker);
            runResult.StandardOutput.Should().Contain("TARGET_VALUE:7");
            runResult.StandardOutput.Should().Contain(SampleCallTargetNativeAotIntegration.EndWithValueMarker);
            runResult.StandardOutput.Should().Contain("RETURN_VALUE:8");
            runResult.StandardOutput.Should().Contain($"{SampleCallTargetNativeAotIntegration.SlowBeginMarker}:1:9");
            runResult.StandardOutput.Should().Contain("TARGET_SLOW_BEGIN:1:2:3:4:5:6:7:8:9");
            runResult.StandardOutput.Should().Contain("TARGET_ASYNC:1");
            runResult.StandardOutput.Should().Contain(SampleCallTargetNativeAotIntegration.AsyncEndMarker);
            runResult.StandardOutput.Should().Contain(SampleCallTargetNativeAotIntegration.AsyncEndWithValueMarker);
            runResult.StandardOutput.Should().Contain("TARGET_ASYNC_VALUE:5");
            runResult.StandardOutput.Should().Contain("ASYNC_RETURN_VALUE:7");
            runResult.StandardOutput.Should().Contain("TARGET_VALUE_ASYNC:1");
            runResult.StandardOutput.Should().Contain("TARGET_VALUE_ASYNC_VALUE:9");
            runResult.StandardOutput.Should().Contain("VALUE_ASYNC_RETURN_VALUE:11");
            runResult.StandardOutput.Should().Contain("TARGET_DERIVED:1");
            runResult.StandardOutput.Should().Contain("TARGET_INTERFACE:1");
            runResult.StandardOutput.Should().Contain(SampleCallTargetNativeAotReferenceIntegration.BeginMarker);
            runResult.StandardOutput.Should().Contain(SampleCallTargetNativeAotReferenceIntegration.EndMarker);
            runResult.StandardOutput.Should().Contain("TARGET_REFERENCE:1");
            runResult.StandardOutput.Should().Contain($"{SampleCallTargetNativeAotDuckTypeIntegration.DuckBeginMarker}:101:3:7");
            runResult.StandardOutput.Should().Contain($"{SampleCallTargetNativeAotDuckTypeIntegration.DuckEndMarker}:101");
            runResult.StandardOutput.Should().Contain($"{SampleCallTargetNativeAotDuckTypeIntegration.DuckReturnMarker}:101:16");
            runResult.StandardOutput.Should().Contain($"{SampleCallTargetNativeAotDuckTypeAsyncIntegration.DuckAsyncMarker}:101:21");
            runResult.StandardOutput.Should().Contain("TARGET_DUCK_BEGIN:3:7:101");
            runResult.StandardOutput.Should().Contain("TARGET_DUCK_RETURN:13");
            runResult.StandardOutput.Should().Contain("TARGET_DUCK_ASYNC:17");
            runResult.StandardOutput.Should().Contain("DUCK_RETURN_VALUE:16");
            runResult.StandardOutput.Should().Contain("DUCK_ASYNC_RETURN_VALUE:21");
            runResult.StandardOutput.Should().Contain("APP_MAIN:1");
            runResult.StandardOutput.Should().Contain("DYNAMIC_CODE:False");
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    /// <summary>
    /// Builds the minimal sample project file that imports generated CallTarget AOT props when provided.
    /// </summary>
    /// <returns>The sample project file content.</returns>
    private static string BuildAppProjectFile(string libraryProjectPath)
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
            $"    <ProjectReference Include=\"{libraryProjectPath}\" />" + Environment.NewLine +
            "  </ItemGroup>" + Environment.NewLine +
            "  <Import Project=\"$(CallTargetAotPropsPath)\" Condition=\"'$(CallTargetAotPropsPath)' != '' and Exists('$(CallTargetAotPropsPath)')\" />" + Environment.NewLine +
            "</Project>" + Environment.NewLine;
    }

    /// <summary>
    /// Builds the minimal referenced library project used to verify that rewritten references are swapped into publish inputs.
    /// </summary>
    /// <returns>The sample library project file content.</returns>
    private static string BuildLibraryProjectFile()
    {
        return
            "<Project Sdk=\"Microsoft.NET.Sdk\">" + Environment.NewLine +
            "  <PropertyGroup>" + Environment.NewLine +
            "    <TargetFramework>net8.0</TargetFramework>" + Environment.NewLine +
            "    <Nullable>enable</Nullable>" + Environment.NewLine +
            "    <ImplicitUsings>enable</ImplicitUsings>" + Environment.NewLine +
            "  </PropertyGroup>" + Environment.NewLine +
            "</Project>" + Environment.NewLine;
    }

    /// <summary>
    /// Builds the minimal referenced library source used to prove that a rewritten reference assembly is consumed during publish.
    /// </summary>
    /// <returns>The sample library source.</returns>
    private static string BuildLibrarySourceFile()
    {
        return
            "using System;" + Environment.NewLine +
            Environment.NewLine +
            "namespace SampleCallTargetNativeAotLibrary;" + Environment.NewLine +
            Environment.NewLine +
            "public sealed class ReferencedTarget" + Environment.NewLine +
            "{" + Environment.NewLine +
            "    public void ExecuteReference()" + Environment.NewLine +
            "    {" + Environment.NewLine +
            "        Console.WriteLine(\"TARGET_REFERENCE:1\");" + Environment.NewLine +
            "    }" + Environment.NewLine +
            "}" + Environment.NewLine;
    }

    /// <summary>
    /// Builds the minimal sample program used to prove that the injected bootstrap ran before main.
    /// </summary>
    /// <returns>The sample program source.</returns>
    private static string BuildAppSourceFile()
    {
        return
            "using System;" + Environment.NewLine +
            "using System.Runtime.CompilerServices;" + Environment.NewLine +
            "using System.Threading.Tasks;" + Environment.NewLine +
            "using SampleCallTargetNativeAotLibrary;" + Environment.NewLine +
            Environment.NewLine +
            "namespace SampleCallTargetNativeAotApp;" + Environment.NewLine +
            Environment.NewLine +
            "internal abstract class InstrumentedBase" + Environment.NewLine +
            "{" + Environment.NewLine +
            "    public abstract void ExecuteDerived();" + Environment.NewLine +
            "}" + Environment.NewLine +
            Environment.NewLine +
            "internal interface IInstrumentedContract" + Environment.NewLine +
            "{" + Environment.NewLine +
            "    void ExecuteInterface();" + Environment.NewLine +
            "}" + Environment.NewLine +
            Environment.NewLine +
            "internal sealed class DuckPayload" + Environment.NewLine +
            "{" + Environment.NewLine +
            "    public DuckPayload(int value)" + Environment.NewLine +
            "    {" + Environment.NewLine +
            "        Value = value;" + Environment.NewLine +
            "    }" + Environment.NewLine +
            Environment.NewLine +
            "    public int Value { get; }" + Environment.NewLine +
            "}" + Environment.NewLine +
            Environment.NewLine +
            "internal sealed class InstrumentedTarget" + Environment.NewLine +
            "    : InstrumentedBase, IInstrumentedContract" + Environment.NewLine +
            "{" + Environment.NewLine +
            "    public int DuckValue => 101;" + Environment.NewLine +
            Environment.NewLine +
            "    public void Execute()" + Environment.NewLine +
            "    {" + Environment.NewLine +
            "        Console.WriteLine(\"TARGET:1\");" + Environment.NewLine +
            "    }" + Environment.NewLine +
            Environment.NewLine +
            "    public int ExecuteWithValue(int value)" + Environment.NewLine +
            "    {" + Environment.NewLine +
            "        Console.WriteLine($\"TARGET_VALUE:{value}\");" + Environment.NewLine +
            "        return value + 1;" + Environment.NewLine +
            "    }" + Environment.NewLine +
            Environment.NewLine +
            "    public void ExecuteSlowBegin(int arg1, int arg2, int arg3, int arg4, int arg5, int arg6, int arg7, int arg8, int arg9)" + Environment.NewLine +
            "    {" + Environment.NewLine +
            "        Console.WriteLine($\"TARGET_SLOW_BEGIN:{arg1}:{arg2}:{arg3}:{arg4}:{arg5}:{arg6}:{arg7}:{arg8}:{arg9}\");" + Environment.NewLine +
            "    }" + Environment.NewLine +
            Environment.NewLine +
            "    public async Task ExecuteAsync()" + Environment.NewLine +
            "    {" + Environment.NewLine +
            "        Console.WriteLine(\"TARGET_ASYNC:1\");" + Environment.NewLine +
            "        await Task.Yield();" + Environment.NewLine +
            "    }" + Environment.NewLine +
            Environment.NewLine +
            "    public async Task<int> ExecuteAsyncWithValue(int value)" + Environment.NewLine +
            "    {" + Environment.NewLine +
            "        Console.WriteLine($\"TARGET_ASYNC_VALUE:{value}\");" + Environment.NewLine +
            "        await Task.Yield();" + Environment.NewLine +
            "        return value + 2;" + Environment.NewLine +
            "    }" + Environment.NewLine +
            Environment.NewLine +
            "    public async ValueTask ExecuteValueAsync()" + Environment.NewLine +
            "    {" + Environment.NewLine +
            "        Console.WriteLine(\"TARGET_VALUE_ASYNC:1\");" + Environment.NewLine +
            "        await Task.Yield();" + Environment.NewLine +
            "    }" + Environment.NewLine +
            Environment.NewLine +
            "    public async ValueTask<int> ExecuteValueAsyncWithValue(int value)" + Environment.NewLine +
            "    {" + Environment.NewLine +
            "        Console.WriteLine($\"TARGET_VALUE_ASYNC_VALUE:{value}\");" + Environment.NewLine +
            "        await Task.Yield();" + Environment.NewLine +
            "        return value + 2;" + Environment.NewLine +
            "    }" + Environment.NewLine +
            Environment.NewLine +
            "    public override void ExecuteDerived()" + Environment.NewLine +
            "    {" + Environment.NewLine +
            "        Console.WriteLine(\"TARGET_DERIVED:1\");" + Environment.NewLine +
            "    }" + Environment.NewLine +
            Environment.NewLine +
            "    public void ExecuteInterface()" + Environment.NewLine +
            "    {" + Environment.NewLine +
            "        Console.WriteLine(\"TARGET_INTERFACE:1\");" + Environment.NewLine +
            "    }" + Environment.NewLine +
            Environment.NewLine +
            "    public static void ExecuteStaticUnsupported()" + Environment.NewLine +
            "    {" + Environment.NewLine +
            "        Console.WriteLine(\"TARGET_STATIC_UNSUPPORTED:1\");" + Environment.NewLine +
            "    }" + Environment.NewLine +
            Environment.NewLine +
            "    public T ExecuteGenericUnsupported<T>(T value)" + Environment.NewLine +
            "    {" + Environment.NewLine +
            "        Console.WriteLine($\"TARGET_GENERIC_UNSUPPORTED:{value}\");" + Environment.NewLine +
            "        return value;" + Environment.NewLine +
            "    }" + Environment.NewLine +
            Environment.NewLine +
            "    public void ExecuteByRefUnsupported(ref int value)" + Environment.NewLine +
            "    {" + Environment.NewLine +
            "        value++;" + Environment.NewLine +
            "        Console.WriteLine($\"TARGET_BYREF_UNSUPPORTED:{value}\");" + Environment.NewLine +
            "    }" + Environment.NewLine +
            Environment.NewLine +
            "    private int _byRefReturnValue = 41;" + Environment.NewLine +
            Environment.NewLine +
            "    public ref int ExecuteByRefReturnUnsupported()" + Environment.NewLine +
            "    {" + Environment.NewLine +
            "        return ref _byRefReturnValue;" + Environment.NewLine +
            "    }" + Environment.NewLine +
            Environment.NewLine +
            "    public void ExecuteDuckBegin(DuckPayload payload, int count)" + Environment.NewLine +
            "    {" + Environment.NewLine +
            "        Console.WriteLine($\"TARGET_DUCK_BEGIN:{payload.Value}:{count}:{DuckValue}\");" + Environment.NewLine +
            "    }" + Environment.NewLine +
            Environment.NewLine +
            "    public DuckPayload ExecuteDuckReturn(int value)" + Environment.NewLine +
            "    {" + Environment.NewLine +
            "        Console.WriteLine($\"TARGET_DUCK_RETURN:{value}\");" + Environment.NewLine +
            "        return new DuckPayload(value + 3);" + Environment.NewLine +
            "    }" + Environment.NewLine +
            Environment.NewLine +
            "    public async Task<DuckPayload> ExecuteDuckAsync(int value)" + Environment.NewLine +
            "    {" + Environment.NewLine +
            "        Console.WriteLine($\"TARGET_DUCK_ASYNC:{value}\");" + Environment.NewLine +
            "        await Task.Yield();" + Environment.NewLine +
            "        return new DuckPayload(value + 4);" + Environment.NewLine +
            "    }" + Environment.NewLine +
            "}" + Environment.NewLine +
            Environment.NewLine +
            "internal static class Program" + Environment.NewLine +
            "{" + Environment.NewLine +
            "    private static void Main()" + Environment.NewLine +
            "    {" + Environment.NewLine +
            "        var target = new InstrumentedTarget();" + Environment.NewLine +
            "        var referencedTarget = new ReferencedTarget();" + Environment.NewLine +
            "        target.Execute();" + Environment.NewLine +
            "        Console.WriteLine($\"RETURN_VALUE:{target.ExecuteWithValue(7)}\");" + Environment.NewLine +
            "        target.ExecuteSlowBegin(1, 2, 3, 4, 5, 6, 7, 8, 9);" + Environment.NewLine +
            "        target.ExecuteAsync().GetAwaiter().GetResult();" + Environment.NewLine +
            "        Console.WriteLine($\"ASYNC_RETURN_VALUE:{target.ExecuteAsyncWithValue(5).GetAwaiter().GetResult()}\");" + Environment.NewLine +
            "        target.ExecuteValueAsync().GetAwaiter().GetResult();" + Environment.NewLine +
            "        Console.WriteLine($\"VALUE_ASYNC_RETURN_VALUE:{target.ExecuteValueAsyncWithValue(9).GetAwaiter().GetResult()}\");" + Environment.NewLine +
            "        target.ExecuteDerived();" + Environment.NewLine +
            "        target.ExecuteInterface();" + Environment.NewLine +
            "        referencedTarget.ExecuteReference();" + Environment.NewLine +
            "        target.ExecuteDuckBegin(new DuckPayload(3), 7);" + Environment.NewLine +
            "        Console.WriteLine($\"DUCK_RETURN_VALUE:{target.ExecuteDuckReturn(13).Value}\");" + Environment.NewLine +
            "        Console.WriteLine($\"DUCK_ASYNC_RETURN_VALUE:{target.ExecuteDuckAsync(17).GetAwaiter().GetResult().Value}\");" + Environment.NewLine +
            "        Console.WriteLine(\"APP_MAIN:1\");" + Environment.NewLine +
            "        Console.WriteLine($\"DYNAMIC_CODE:{RuntimeFeature.IsDynamicCodeSupported}\");" + Environment.NewLine +
            "    }" + Environment.NewLine +
            "}" + Environment.NewLine;
    }

    /// <summary>
    /// Resolves the runtime identifier used for NativeAOT publish based on the current test host OS and architecture.
    /// </summary>
    /// <returns>The runtime identifier to pass to <c>dotnet publish</c>.</returns>
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

    /// <summary>
    /// Detects common environment failures that mean NativeAOT infrastructure is not installed rather than the milestone workflow being incorrect.
    /// </summary>
    /// <param name="result">The process result produced by the failed publish command.</param>
    /// <param name="reason">The detected infrastructure reason when one is found.</param>
    /// <returns><see langword="true"/> when the publish failure should be treated as an infrastructure skip.</returns>
    private static bool TryGetNativeAotInfrastructureSkipReason(CommandResult result, out string reason)
    {
        var combined = $"{result.StandardOutput}{Environment.NewLine}{result.StandardError}";
        var knownInfrastructureMarkers = new[]
        {
            "Platform linker not found",
            "runtime pack was not downloaded",
            "Microsoft.DotNet.ILCompiler",
            "clang",
            "linker command failed",
            "NativeAOT",
            "ilc",
            "No available native toolchain",
            "Failed to locate managed application"
        };

        foreach (var marker in knownInfrastructureMarkers)
        {
            if (combined.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                reason = combined;
                return true;
            }
        }

        reason = string.Empty;
        return false;
    }

    /// <summary>
    /// Executes an external process and returns its exit code and optional captured output.
    /// </summary>
    /// <param name="fileName">The executable file name.</param>
    /// <param name="workingDirectory">The working directory used to launch the process.</param>
    /// <param name="timeoutMilliseconds">The maximum time to wait for the process to finish.</param>
    /// <param name="captureOutput">Whether stdout and stderr should be captured.</param>
    /// <param name="arguments">The command-line arguments to pass to the process.</param>
    /// <returns>The resulting process outcome.</returns>
    private static CommandResult RunProcess(string fileName, string workingDirectory, int timeoutMilliseconds, bool captureOutput, string[] arguments)
    {
        using var process = new Process();
        process.StartInfo.FileName = fileName;
        process.StartInfo.WorkingDirectory = workingDirectory;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.RedirectStandardOutput = captureOutput;
        process.StartInfo.RedirectStandardError = captureOutput;

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();

        string standardOutput = string.Empty;
        string standardError = string.Empty;
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
                process.WaitForExit();
            }
            catch
            {
                // Ignore cleanup failures because the timeout is the meaningful test failure.
            }

            if (captureOutput && standardOutputTask is not null && standardErrorTask is not null)
            {
                Task.WaitAll(standardOutputTask, standardErrorTask);
                standardOutput = standardOutputTask.Result;
                standardError = standardErrorTask.Result;
            }

            var timeoutMessage = new StringBuilder();
            timeoutMessage.Append($"Process '{fileName}' exceeded timeout of {timeoutMilliseconds} ms.");
            if (captureOutput)
            {
                timeoutMessage.Append($"{Environment.NewLine}STDOUT:{Environment.NewLine}{standardOutput}");
                timeoutMessage.Append($"{Environment.NewLine}STDERR:{Environment.NewLine}{standardError}");
            }

            throw new TimeoutException(timeoutMessage.ToString());
        }

        if (captureOutput && standardOutputTask is not null && standardErrorTask is not null)
        {
            Task.WaitAll(standardOutputTask, standardErrorTask);
            standardOutput = standardOutputTask.Result;
            standardError = standardErrorTask.Result;
        }

        return new CommandResult(process.ExitCode, standardOutput, standardError);
    }

    /// <summary>
    /// Deletes a temporary directory tree without failing the test if cleanup is blocked by transient file locks.
    /// </summary>
    /// <param name="path">The directory tree to delete.</param>
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
            // Ignore cleanup failures because they do not affect the result of the verification.
        }
    }

    /// <summary>
    /// Represents the result of a completed external command.
    /// </summary>
    private readonly struct CommandResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CommandResult"/> struct.
        /// </summary>
        /// <param name="exitCode">The process exit code.</param>
        /// <param name="standardOutput">The captured standard output.</param>
        /// <param name="standardError">The captured standard error.</param>
        public CommandResult(int exitCode, string standardOutput, string standardError)
        {
            ExitCode = exitCode;
            StandardOutput = standardOutput;
            StandardError = standardError;
        }

        /// <summary>
        /// Gets the process exit code.
        /// </summary>
        public int ExitCode { get; }

        /// <summary>
        /// Gets the captured standard output.
        /// </summary>
        public string StandardOutput { get; }

        /// <summary>
        /// Gets the captured standard error.
        /// </summary>
        public string StandardError { get; }
    }
}
#endif
