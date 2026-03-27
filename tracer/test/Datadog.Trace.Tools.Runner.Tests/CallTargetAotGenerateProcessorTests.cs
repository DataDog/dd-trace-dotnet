// <copyright file="CallTargetAotGenerateProcessorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET8_0
#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Datadog.Trace.Tools.Runner.CallTargetAot;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// Verifies that the CallTarget NativeAOT generator records both supported and unsupported bindings and emits
/// structured artifacts that explain the resulting compatibility surface.
/// </summary>
public class CallTargetAotGenerateProcessorTests
{
    /// <summary>
    /// Builds the sample application, runs the generator, and verifies that unsupported target shapes are preserved
    /// as evaluated diagnostics instead of being silently dropped.
    /// </summary>
    [Fact]
    public void GenerateShouldRecordSupportedAndUnsupportedBindings()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "dd-trace-calltarget-aot-generate", Guid.NewGuid().ToString("N"));
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
                captureOutput: true,
                arguments:
                [
                    "build",
                    appProjectPath,
                    "-c",
                    "Release"
                ]);
            buildResult.ExitCode.Should().Be(
                0,
                $"building the sample app for generator tests should succeed.{Environment.NewLine}STDOUT:{Environment.NewLine}{buildResult.StandardOutput}{Environment.NewLine}STDERR:{Environment.NewLine}{buildResult.StandardError}");

            var tracerAssemblyPath = typeof(Datadog.Trace.Tracer).Assembly.Location;
            var appAssemblyDirectory = Path.Combine(appDirectory, "bin", "Release", "net8.0");
            var outputAssemblyPath = Path.Combine(tempDirectory, "Datadog.Trace.CallTarget.AotRegistry.GenerateTests.dll");
            var options = new CallTargetAotGenerateOptions(
                tracerAssemblyPath,
                [appAssemblyDirectory],
                ["SampleCallTargetNativeAotApp.dll", "SampleCallTargetNativeAotLibrary.dll"],
                outputAssemblyPath,
                "Datadog.Trace.CallTarget.AotRegistry.GenerateTests",
                outputAssemblyPath + ".targets",
                outputAssemblyPath + ".props",
                outputAssemblyPath + ".linker.xml",
                outputAssemblyPath + ".manifest.json",
                outputAssemblyPath + ".rewrite-plan.json",
                outputAssemblyPath + ".compat.md",
                outputAssemblyPath + ".compat.json");

            var discoveredDefinitions = CallTargetAotDefinitionDiscovery.Discover(tracerAssemblyPath);
            discoveredDefinitions.Should().Contain(definition => definition.TargetMethodName == "ExecuteStaticUnsupported");
            discoveredDefinitions.Should().Contain(definition => definition.TargetMethodName == "ExecuteGenericUnsupported");
            discoveredDefinitions.Should().Contain(definition => definition.TargetMethodName == "ExecuteByRefUnsupported");
            discoveredDefinitions.Should().Contain(definition => definition.TargetMethodName == "ExecuteByRefReturnUnsupported");

            var evaluatedDefinitions = CallTargetAotMethodMatcher.Match(discoveredDefinitions, options);
            evaluatedDefinitions.Should().Contain(definition => definition.TargetMethodName == "ExecuteReference" && definition.IsSupported);
            evaluatedDefinitions.Should().Contain(definition => definition.TargetMethodName == "ExecuteStaticUnsupported" && !definition.IsSupported && definition.DiagnosticCode == "CTAOT001");
            evaluatedDefinitions.Should().Contain(definition => definition.TargetMethodName == "ExecuteGenericUnsupported" && !definition.IsSupported && definition.DiagnosticCode == "CTAOT002");
            evaluatedDefinitions.Should().Contain(definition => definition.TargetMethodName == "ExecuteByRefUnsupported" && !definition.IsSupported && definition.DiagnosticCode == "CTAOT004");
            evaluatedDefinitions.Should().Contain(definition => definition.TargetMethodName == "ExecuteByRefReturnUnsupported" && !definition.IsSupported && definition.DiagnosticCode == "CTAOT003");

            CallTargetAotGenerateProcessor.Process(options).Should().Be(0);

            var manifest = JsonConvert.DeserializeObject<CallTargetAotManifest>(File.ReadAllText(options.ManifestPath));
            manifest.Should().NotBeNull();
            manifest!.RewritePlan.TargetAssemblyFileNames.Should().Contain("SampleCallTargetNativeAotLibrary.dll");
            manifest.EvaluatedDefinitions.Should().Contain(definition => definition.TargetMethodName == "ExecuteStaticUnsupported" && !definition.IsSupported);
            manifest.EvaluatedDefinitions.Should().Contain(definition => definition.TargetMethodName == "ExecuteGenericUnsupported" && !definition.IsSupported);
            manifest.EvaluatedDefinitions.Should().Contain(definition => definition.TargetMethodName == "ExecuteByRefUnsupported" && !definition.IsSupported);
            manifest.EvaluatedDefinitions.Should().Contain(definition => definition.TargetMethodName == "ExecuteByRefReturnUnsupported" && !definition.IsSupported);
            manifest.MatchedDefinitions.Should().NotContain(definition => definition.TargetMethodName == "ExecuteStaticUnsupported");
            manifest.MatchedDefinitions.Should().NotContain(definition => definition.TargetMethodName == "ExecuteGenericUnsupported");
            manifest.MatchedDefinitions.Should().NotContain(definition => definition.TargetMethodName == "ExecuteByRefUnsupported");
            manifest.MatchedDefinitions.Should().NotContain(definition => definition.TargetMethodName == "ExecuteByRefReturnUnsupported");

            var compatibilityMatrix = JObject.Parse(File.ReadAllText(options.CompatibilityMatrixPath));
            var compatibilityBindings = compatibilityMatrix["bindings"]!.Children<JObject>().ToList();
            compatibilityMatrix["compatibleBindings"]!.Value<int>().Should().BeGreaterThan(0);
            compatibilityMatrix["incompatibleBindings"]!.Value<int>().Should().BeGreaterThanOrEqualTo(4);
            compatibilityBindings.Any(binding =>
                    string.Equals(binding["targetMethod"]?.Value<string>(), "ExecuteStaticUnsupported", StringComparison.Ordinal) &&
                    string.Equals(binding["status"]?.Value<string>(), "incompatible", StringComparison.Ordinal) &&
                    string.Equals(binding["diagnosticCode"]?.Value<string>(), "CTAOT001", StringComparison.Ordinal))
                .Should()
                .BeTrue();
            compatibilityBindings.Any(binding =>
                    string.Equals(binding["targetMethod"]?.Value<string>(), "ExecuteGenericUnsupported", StringComparison.Ordinal) &&
                    string.Equals(binding["diagnosticCode"]?.Value<string>(), "CTAOT002", StringComparison.Ordinal))
                .Should()
                .BeTrue();
            compatibilityBindings.Any(binding =>
                    string.Equals(binding["targetMethod"]?.Value<string>(), "ExecuteByRefUnsupported", StringComparison.Ordinal) &&
                    string.Equals(binding["diagnosticCode"]?.Value<string>(), "CTAOT004", StringComparison.Ordinal))
                .Should()
                .BeTrue();
            compatibilityBindings.Any(binding =>
                    string.Equals(binding["targetMethod"]?.Value<string>(), "ExecuteByRefReturnUnsupported", StringComparison.Ordinal) &&
                    string.Equals(binding["diagnosticCode"]?.Value<string>(), "CTAOT003", StringComparison.Ordinal))
                .Should()
                .BeTrue();

            var compatibilityReport = File.ReadAllText(options.CompatibilityReportPath);
            compatibilityReport.Should().Contain("ExecuteStaticUnsupported");
            compatibilityReport.Should().Contain("CTAOT001");
            compatibilityReport.Should().Contain("ExecuteGenericUnsupported");
            compatibilityReport.Should().Contain("CTAOT002");
            compatibilityReport.Should().Contain("ExecuteByRefUnsupported");
            compatibilityReport.Should().Contain("CTAOT004");
            compatibilityReport.Should().Contain("ExecuteByRefReturnUnsupported");
            compatibilityReport.Should().Contain("CTAOT003");
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    /// <summary>
    /// Builds the sample application project file that imports the generated props file when the publish workflow uses it.
    /// </summary>
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
    /// Builds the referenced library project used by the generator tests.
    /// </summary>
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
    /// Builds the referenced library source used by the generator tests.
    /// </summary>
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
    /// Builds the sample application source that contains both supported and unsupported CallTarget target shapes.
    /// </summary>
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
            "    private int _byRefReturnValue = 41;" + Environment.NewLine +
            Environment.NewLine +
            "    public int DuckValue => 101;" + Environment.NewLine +
            Environment.NewLine +
            "    public void Execute() => Console.WriteLine(\"TARGET:1\");" + Environment.NewLine +
            "    public int ExecuteWithValue(int value) => value + 1;" + Environment.NewLine +
            "    public void ExecuteSlowBegin(int arg1, int arg2, int arg3, int arg4, int arg5, int arg6, int arg7, int arg8, int arg9) { }" + Environment.NewLine +
            "    public async Task ExecuteAsync() => await Task.Yield();" + Environment.NewLine +
            "    public async Task<int> ExecuteAsyncWithValue(int value) { await Task.Yield(); return value + 2; }" + Environment.NewLine +
            "    public async ValueTask ExecuteValueAsync() => await Task.Yield();" + Environment.NewLine +
            "    public async ValueTask<int> ExecuteValueAsyncWithValue(int value) { await Task.Yield(); return value + 2; }" + Environment.NewLine +
            "    public override void ExecuteDerived() { }" + Environment.NewLine +
            "    public void ExecuteInterface() { }" + Environment.NewLine +
            "    public static void ExecuteStaticUnsupported() { }" + Environment.NewLine +
            "    public T ExecuteGenericUnsupported<T>(T value) => value;" + Environment.NewLine +
            "    public void ExecuteByRefUnsupported(ref int value) => value++;" + Environment.NewLine +
            "    public ref int ExecuteByRefReturnUnsupported() => ref _byRefReturnValue;" + Environment.NewLine +
            "    public void ExecuteDuckBegin(DuckPayload payload, int count) { }" + Environment.NewLine +
            "    public DuckPayload ExecuteDuckReturn(int value) => new DuckPayload(value + 3);" + Environment.NewLine +
            "    public async Task<DuckPayload> ExecuteDuckAsync(int value) { await Task.Yield(); return new DuckPayload(value + 4); }" + Environment.NewLine +
            "}" + Environment.NewLine +
            Environment.NewLine +
            "internal static class Program" + Environment.NewLine +
            "{" + Environment.NewLine +
            "    private static void Main()" + Environment.NewLine +
            "    {" + Environment.NewLine +
            "        var target = new InstrumentedTarget();" + Environment.NewLine +
            "        var referencedTarget = new ReferencedTarget();" + Environment.NewLine +
            "        target.Execute();" + Environment.NewLine +
            "        target.ExecuteWithValue(7);" + Environment.NewLine +
            "        target.ExecuteSlowBegin(1, 2, 3, 4, 5, 6, 7, 8, 9);" + Environment.NewLine +
            "        target.ExecuteAsync().GetAwaiter().GetResult();" + Environment.NewLine +
            "        target.ExecuteAsyncWithValue(5).GetAwaiter().GetResult();" + Environment.NewLine +
            "        target.ExecuteValueAsync().GetAwaiter().GetResult();" + Environment.NewLine +
            "        target.ExecuteValueAsyncWithValue(9).GetAwaiter().GetResult();" + Environment.NewLine +
            "        target.ExecuteDerived();" + Environment.NewLine +
            "        target.ExecuteInterface();" + Environment.NewLine +
            "        referencedTarget.ExecuteReference();" + Environment.NewLine +
            "        target.ExecuteDuckBegin(new DuckPayload(3), 7);" + Environment.NewLine +
            "        target.ExecuteDuckReturn(13);" + Environment.NewLine +
            "        target.ExecuteDuckAsync(17).GetAwaiter().GetResult();" + Environment.NewLine +
            "        Console.WriteLine($\"DYNAMIC_CODE:{RuntimeFeature.IsDynamicCodeSupported}\");" + Environment.NewLine +
            "    }" + Environment.NewLine +
            "}" + Environment.NewLine;
    }

    /// <summary>
    /// Executes an external process and returns its exit code and captured output.
    /// </summary>
    private static CommandResult RunProcess(string fileName, string workingDirectory, int timeoutMilliseconds, bool captureOutput, string[] arguments)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = captureOutput,
            RedirectStandardError = captureOutput,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
        {
            processStartInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(processStartInfo) ?? throw new InvalidOperationException($"Failed to start process '{fileName}'.");
        var standardOutputBuilder = new StringBuilder();
        var standardErrorBuilder = new StringBuilder();
        if (captureOutput)
        {
            process.OutputDataReceived += (_, eventArgs) =>
            {
                if (eventArgs.Data is not null)
                {
                    _ = standardOutputBuilder.AppendLine(eventArgs.Data);
                }
            };
            process.ErrorDataReceived += (_, eventArgs) =>
            {
                if (eventArgs.Data is not null)
                {
                    _ = standardErrorBuilder.AppendLine(eventArgs.Data);
                }
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }

        if (!process.WaitForExit(timeoutMilliseconds))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best effort cleanup after timeout.
            }

            throw new TimeoutException($"Process '{fileName}' timed out after {timeoutMilliseconds}ms.");
        }

        return new CommandResult(process.ExitCode, standardOutputBuilder.ToString(), standardErrorBuilder.ToString());
    }

    /// <summary>
    /// Removes a temporary test directory once verification is complete.
    /// </summary>
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
            // Ignore best-effort cleanup failures in test code.
        }
    }

    /// <summary>
    /// Stores the exit code and captured output from a completed external process.
    /// </summary>
    private sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError);
}
#endif
