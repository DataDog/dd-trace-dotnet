// <copyright file="DuckTypeAotFullSuiteParityIntegrationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET8_0
#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using Datadog.Trace.Tools.Runner.DuckTypeAot;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

public class DuckTypeAotFullSuiteParityIntegrationTests
{
    private const string EnableParityRunEnvironmentVariable = "DD_RUN_DUCKTYPE_AOT_FULL_SUITE_PARITY";
    private const string EnableParityMatrixRunEnvironmentVariable = "DD_RUN_DUCKTYPE_AOT_FULL_SUITE_PARITY_MATRIX";
    private const string MatrixFrameworksEnvironmentVariable = "DD_DUCKTYPE_AOT_FULL_SUITE_PARITY_FRAMEWORKS";
    private const string ParitySeedEnvironmentVariable = "DD_DUCKTYPE_AOT_FULL_SUITE_PARITY_SEED";
    private const string KeepArtifactsEnvironmentVariable = "DD_DUCKTYPE_AOT_FULL_SUITE_PARITY_KEEP_ARTIFACTS";
    private const string RandomSeedEnvironmentVariable = "RANDOM_SEED";
    private const string DotNetX64Path = "/usr/local/share/dotnet/x64/dotnet";
    private const string DotNetX64Root = "/usr/local/share/dotnet/x64";
    private const string DefaultParitySeed = "20260301";

    private static readonly string[] DefaultMatrixFrameworks =
    [
        "net10.0",
        "net9.0",
        "net8.0",
        "net7.0",
        "net6.0",
        "net5.0",
        "netcoreapp3.1",
        "netcoreapp3.0",
        "netcoreapp2.1"
    ];

    private static readonly object RuntimeInventoryLock = new();
    private static readonly Dictionary<string, DotNetRuntimeInventory> RuntimeInventories = new(StringComparer.Ordinal);

    [Fact]
    public void FullDuckTypingSuiteShouldHaveMatchingOutcomesBetweenDynamicAndAotModes()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(EnableParityRunEnvironmentVariable), "1", StringComparison.Ordinal))
        {
            return;
        }

        RunFullSuiteParityForFramework("net8.0", ResolveDotNetExecutableOrThrow("net8.0"));
    }

    [Fact]
    public void FullDuckTypingSuiteMatrixShouldHaveMatchingOutcomesBetweenDynamicAndAotModes()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(EnableParityMatrixRunEnvironmentVariable), "1", StringComparison.Ordinal))
        {
            return;
        }

        var unresolvedFrameworks = new List<string>();
        var frameworkSelections = new List<(string Framework, string DotNetExecutable)>();
        foreach (var framework in ResolveRequestedMatrixFrameworks())
        {
            var dotNetExecutable = SelectDotNetExecutableForFramework(framework);
            if (string.IsNullOrWhiteSpace(dotNetExecutable))
            {
                unresolvedFrameworks.Add(framework);
                continue;
            }

            frameworkSelections.Add((framework, dotNetExecutable));
        }

        unresolvedFrameworks.Should().BeEmpty(
            "the matrix run requires a compatible dotnet runtime for each requested framework." +
            Environment.NewLine +
            BuildRuntimeResolutionDiagnostics(unresolvedFrameworks));

        var failures = new List<string>();
        foreach (var (framework, dotNetExecutable) in frameworkSelections)
        {
            try
            {
                RunFullSuiteParityForFramework(framework, dotNetExecutable);
            }
            catch (Exception ex)
            {
                failures.Add($"[{framework}] dotnet='{dotNetExecutable}' => {ex}");
            }
        }

        failures.Should().BeEmpty(
            "all requested full-suite parity matrix lanes should pass." +
            Environment.NewLine +
            string.Join(Environment.NewLine, failures));
    }

    private static void RunFullSuiteParityForFramework(string framework, string dotNetExecutable)
    {
        var repositoryRoot = FindRepositoryRoot();
        var duckTypingTestsProjectPath = Path.Combine(
            repositoryRoot,
            "tracer",
            "test",
            "Datadog.Trace.DuckTyping.Tests",
            "Datadog.Trace.DuckTyping.Tests.csproj");
        var runnerProjectPath = Path.Combine(
            repositoryRoot,
            "tracer",
            "src",
            "Datadog.Trace.Tools.Runner",
            "Datadog.Trace.Tools.Runner.csproj");
        var duckTypingTestsAssemblyPath = Path.Combine(
            repositoryRoot,
            "tracer",
            "test",
            "Datadog.Trace.DuckTyping.Tests",
            "bin",
            "Release",
            framework,
            "Datadog.Trace.DuckTyping.Tests.dll");
        var runnerAssemblyPath = Path.Combine(
            repositoryRoot,
            "tracer",
            "src",
            "Datadog.Trace.Tools.Runner",
            "bin",
            "Release",
            "Tool",
            framework,
            "Datadog.Trace.Tools.Runner.dll");

        File.Exists(duckTypingTestsProjectPath).Should().BeTrue("the full-suite parity harness requires the duck typing tests project");
        File.Exists(runnerProjectPath).Should().BeTrue("the full-suite parity harness requires the runner project");

        var paritySeed = ResolveParitySeed();
        var keepArtifacts = ShouldKeepArtifacts();
        var retainArtifacts = keepArtifacts;

        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "dd-trace-ducktype-aot-full-suite-parity",
            NormalizeFrameworkToken(framework),
            Guid.NewGuid().ToString("N"));
        var dynamicResultsDirectory = Path.Combine(tempDirectory, "dynamic-results");
        var aotResultsDirectory = Path.Combine(tempDirectory, "aot-results");
        var dynamicTrxPath = Path.Combine(dynamicResultsDirectory, "dynamic.trx");
        var aotTrxPath = Path.Combine(aotResultsDirectory, "aot.trx");
        var discoveredMapPath = Path.Combine(tempDirectory, "ducktype-aot-discovered-map.json");
        var sanitizedDiscoveredMapPath = Path.Combine(tempDirectory, "ducktype-aot-discovered-map-sanitized.json");
        var registryAssemblyName = $"Datadog.Trace.DuckType.AotRegistry.FullSuiteParity.{NormalizeFrameworkToken(framework)}";
        var generatedRegistryPath = Path.Combine(tempDirectory, $"{registryAssemblyName}.dll");

        Directory.CreateDirectory(tempDirectory);
        Directory.CreateDirectory(dynamicResultsDirectory);
        Directory.CreateDirectory(aotResultsDirectory);

        try
        {
            if (!File.Exists(runnerAssemblyPath))
            {
                var buildRunnerResult = RunProcess(
                    fileName: dotNetExecutable,
                    workingDirectory: repositoryRoot,
                    timeoutMilliseconds: 600_000,
                    captureOutput: true,
                    environmentVariables: BuildDotNetProcessEnvironment(dotNetExecutable, additionalEnvironmentVariables: null),
                    arguments:
                    [
                        "build",
                        runnerProjectPath,
                        "-c",
                        "Release",
                        "--framework",
                        framework
                    ]);
                var buildRunnerFailureMessage =
                    "runner build must succeed before full-suite parity execution." +
                    Environment.NewLine +
                    $"framework: {framework}" +
                    Environment.NewLine +
                    $"dotnet: {dotNetExecutable}" +
                    Environment.NewLine +
                    "STDOUT:" +
                    Environment.NewLine +
                    buildRunnerResult.StandardOutput +
                    Environment.NewLine +
                    "STDERR:" +
                    Environment.NewLine +
                    buildRunnerResult.StandardError;
                buildRunnerResult.ExitCode.Should().Be(0, buildRunnerFailureMessage);
            }

            File.Exists(runnerAssemblyPath).Should().BeTrue("the runner assembly should be available before registry generation");

            var dynamicResult = RunProcess(
                fileName: dotNetExecutable,
                workingDirectory: repositoryRoot,
                timeoutMilliseconds: 600_000,
                captureOutput: true,
                environmentVariables: BuildDotNetProcessEnvironment(
                    dotNetExecutable,
                    new Dictionary<string, string?>
                    {
                        ["DD_DUCKTYPE_TEST_MODE"] = "dynamic",
                        ["DD_DUCKTYPE_DISCOVERY_OUTPUT_PATH"] = discoveredMapPath,
                        ["DD_DUCKTYPE_AOT_REGISTRY_PATH"] = null,
                        [RandomSeedEnvironmentVariable] = paritySeed
                    }),
                arguments:
                [
                    "test",
                    duckTypingTestsProjectPath,
                    "-c",
                    "Release",
                    "--framework",
                    framework,
                    "--logger",
                    "trx;LogFileName=dynamic.trx",
                    "--results-directory",
                    dynamicResultsDirectory
                ]);

            var dynamicExitCodeMessage =
                "the dynamic full-suite run must succeed before parity is evaluated." +
                Environment.NewLine +
                BuildParityDiagnostics(
                    framework,
                    dotNetExecutable,
                    paritySeed,
                    tempDirectory,
                    dynamicTrxPath,
                    aotTrxPath,
                    discoveredMapPath,
                    sanitizedDiscoveredMapPath,
                    generatedRegistryPath,
                    excludedMappings: Array.Empty<string>()) +
                Environment.NewLine +
                "STDOUT:" +
                Environment.NewLine +
                dynamicResult.StandardOutput +
                Environment.NewLine +
                "STDERR:" +
                Environment.NewLine +
                dynamicResult.StandardError;
            dynamicResult.ExitCode.Should().Be(
                0,
                dynamicExitCodeMessage);

            File.Exists(duckTypingTestsAssemblyPath).Should().BeTrue("the test assembly should be produced by the dynamic run");
            File.Exists(dynamicTrxPath).Should().BeTrue("dynamic run should produce a trx report");
            File.Exists(discoveredMapPath).Should().BeTrue("dynamic discovery should produce a ducktype-aot map file");
            var includeCurrentProcessAssemblies =
                string.Equals(framework, "net8.0", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(dotNetExecutable, DotNetX64Path, StringComparison.Ordinal);
            var generateInput = PrepareGenerateInput(
                discoveredMapPath,
                sanitizedDiscoveredMapPath,
                duckTypingTestsAssemblyPath,
                runnerAssemblyPath,
                repositoryRoot,
                framework,
                dotNetExecutable,
                includeCurrentProcessAssemblies);

            var generateArguments = new List<string>
            {
                runnerAssemblyPath,
                "ducktype-aot",
                "generate"
            };

            foreach (var proxyAssemblyPath in generateInput.ProxyAssemblyPaths)
            {
                generateArguments.Add("--proxy-assembly");
                generateArguments.Add(proxyAssemblyPath);
            }

            foreach (var targetFolder in generateInput.TargetFolders)
            {
                generateArguments.Add("--target-folder");
                generateArguments.Add(targetFolder);
            }

            generateArguments.Add("--target-filter");
            generateArguments.Add("*.dll");

            generateArguments.Add("--map-file");
            generateArguments.Add(generateInput.SanitizedMapPath);
            generateArguments.Add("--output");
            generateArguments.Add(generatedRegistryPath);
            generateArguments.Add("--assembly-name");
            generateArguments.Add(registryAssemblyName);

            var generateResult = RunProcess(
                fileName: dotNetExecutable,
                workingDirectory: tempDirectory,
                timeoutMilliseconds: 300_000,
                captureOutput: true,
                environmentVariables: BuildDotNetProcessEnvironment(dotNetExecutable, additionalEnvironmentVariables: null),
                arguments: generateArguments.ToArray());

            var generateFailureMessage =
                "AOT registry generation from discovered mappings should succeed." +
                Environment.NewLine +
                BuildParityDiagnostics(
                    framework,
                    dotNetExecutable,
                    paritySeed,
                    tempDirectory,
                    dynamicTrxPath,
                    aotTrxPath,
                    discoveredMapPath,
                    sanitizedDiscoveredMapPath,
                    generatedRegistryPath,
                    generateInput.ExcludedMappings) +
                Environment.NewLine +
                $"Excluded mappings before generation: {generateInput.ExcludedMappings.Count}" +
                (generateInput.ExcludedMappings.Count > 0
                     ? Environment.NewLine + string.Join(Environment.NewLine, generateInput.ExcludedMappings.Take(25))
                     : string.Empty) +
                Environment.NewLine +
                "STDOUT:" +
                Environment.NewLine +
                generateResult.StandardOutput +
                Environment.NewLine +
                "STDERR:" +
                Environment.NewLine +
                generateResult.StandardError;
            generateResult.ExitCode.Should().Be(0, generateFailureMessage);
            File.Exists(generatedRegistryPath).Should().BeTrue("registry generation should emit an assembly for the AOT-mode run");

            var aotResult = RunProcess(
                fileName: dotNetExecutable,
                workingDirectory: repositoryRoot,
                timeoutMilliseconds: 600_000,
                captureOutput: true,
                environmentVariables: BuildDotNetProcessEnvironment(
                    dotNetExecutable,
                    new Dictionary<string, string?>
                    {
                        ["DD_DUCKTYPE_TEST_MODE"] = "aot",
                        ["DD_DUCKTYPE_AOT_REGISTRY_PATH"] = generatedRegistryPath,
                        ["DD_DUCKTYPE_DISCOVERY_OUTPUT_PATH"] = null,
                        [RandomSeedEnvironmentVariable] = paritySeed
                    }),
                arguments:
                [
                    "test",
                    duckTypingTestsProjectPath,
                    "-c",
                    "Release",
                    "--framework",
                    framework,
                    "--no-build",
                    "--logger",
                    "trx;LogFileName=aot.trx",
                    "--results-directory",
                    aotResultsDirectory
                ]);

            var aotExitCodeMessage =
                "the AOT full-suite run must succeed before parity is evaluated." +
                Environment.NewLine +
                BuildParityDiagnostics(
                    framework,
                    dotNetExecutable,
                    paritySeed,
                    tempDirectory,
                    dynamicTrxPath,
                    aotTrxPath,
                    discoveredMapPath,
                    sanitizedDiscoveredMapPath,
                    generatedRegistryPath,
                    generateInput.ExcludedMappings) +
                Environment.NewLine +
                "STDOUT:" +
                Environment.NewLine +
                aotResult.StandardOutput +
                Environment.NewLine +
                "STDERR:" +
                Environment.NewLine +
                aotResult.StandardError;

            aotResult.ExitCode.Should().Be(
                0,
                aotExitCodeMessage);
            File.Exists(aotTrxPath).Should().BeTrue("AOT run should produce a trx report");

            var dynamicOutcomes = ReadResults(dynamicTrxPath);
            var aotOutcomes = ReadResults(aotTrxPath);

            var dynamicFailedOrError = dynamicOutcomes
                                      .Where(entry => ShouldCompareAssertionMessage(entry.Value.Outcome))
                                      .Select(entry => $"{entry.Key} => {entry.Value.Outcome}: {FormatForDisplay(entry.Value.ErrorMessage)}")
                                      .ToList();
            var aotFailedOrError = aotOutcomes
                                  .Where(entry => ShouldCompareAssertionMessage(entry.Value.Outcome))
                                  .Select(entry => $"{entry.Key} => {entry.Value.Outcome}: {FormatForDisplay(entry.Value.ErrorMessage)}")
                                  .ToList();

            dynamicFailedOrError.Should().BeEmpty(
                "the dynamic full-suite baseline must be green (no Failed/Error outcomes)." +
                Environment.NewLine +
                string.Join(Environment.NewLine, dynamicFailedOrError.Take(50)) +
                (dynamicFailedOrError.Count > 50 ? $"{Environment.NewLine}... ({dynamicFailedOrError.Count - 50} additional failures)" : string.Empty));

            aotFailedOrError.Should().BeEmpty(
                "the AOT full-suite run must be green (no Failed/Error outcomes)." +
                Environment.NewLine +
                string.Join(Environment.NewLine, aotFailedOrError.Take(50)) +
                (aotFailedOrError.Count > 50 ? $"{Environment.NewLine}... ({aotFailedOrError.Count - 50} additional failures)" : string.Empty));

            var missingInAot = dynamicOutcomes.Keys.Except(aotOutcomes.Keys, StringComparer.Ordinal).ToList();
            var missingInDynamic = aotOutcomes.Keys.Except(dynamicOutcomes.Keys, StringComparer.Ordinal).ToList();
            var missingKeysMessage =
                "the same test cases should execute in both dynamic and AOT mode runs." +
                Environment.NewLine +
                "Missing in AOT:" +
                Environment.NewLine +
                string.Join(Environment.NewLine, missingInAot.Take(25)) +
                (missingInAot.Count > 25 ? $"{Environment.NewLine}... ({missingInAot.Count - 25} additional entries)" : string.Empty) +
                Environment.NewLine +
                "Missing in Dynamic:" +
                Environment.NewLine +
                string.Join(Environment.NewLine, missingInDynamic.Take(25)) +
                (missingInDynamic.Count > 25 ? $"{Environment.NewLine}... ({missingInDynamic.Count - 25} additional entries)" : string.Empty);
            (missingInAot.Count + missingInDynamic.Count).Should().Be(0, missingKeysMessage);

            var mismatches = new List<string>();
            foreach (var (testName, dynamicOutcome) in dynamicOutcomes.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                if (!aotOutcomes.TryGetValue(testName, out var aotOutcome))
                {
                    mismatches.Add($"{testName} => dynamic={dynamicOutcome.Outcome}, aot=<missing>");
                    continue;
                }

                if (!string.Equals(dynamicOutcome.Outcome, aotOutcome.Outcome, StringComparison.Ordinal))
                {
                    mismatches.Add($"{testName} => outcome(dynamic={dynamicOutcome.Outcome}, aot={aotOutcome.Outcome})");
                    continue;
                }

                if (!ShouldCompareAssertionMessage(dynamicOutcome.Outcome))
                {
                    continue;
                }

                if (!string.Equals(dynamicOutcome.ErrorMessage, aotOutcome.ErrorMessage, StringComparison.Ordinal))
                {
                    mismatches.Add(
                        $"{testName} => assertion-message(dynamic='{FormatForDisplay(dynamicOutcome.ErrorMessage)}', aot='{FormatForDisplay(aotOutcome.ErrorMessage)}')");
                }
            }

            mismatches.Should().BeEmpty(
                "all full-suite duck typing test outcomes should match between dynamic and AOT modes." +
                Environment.NewLine +
                BuildParityDiagnostics(
                    framework,
                    dotNetExecutable,
                    paritySeed,
                    tempDirectory,
                    dynamicTrxPath,
                    aotTrxPath,
                    discoveredMapPath,
                    sanitizedDiscoveredMapPath,
                    generatedRegistryPath,
                    generateInput.ExcludedMappings) +
                Environment.NewLine +
                string.Join(Environment.NewLine, mismatches.Take(50)) +
                (mismatches.Count > 50 ? $"{Environment.NewLine}... ({mismatches.Count - 50} additional mismatches)" : string.Empty));
        }
        catch
        {
            retainArtifacts = true;
            throw;
        }
        finally
        {
            // Branch: take this path when (retainArtifacts) evaluates to true.
            if (retainArtifacts)
            {
                Console.WriteLine($"DuckType AOT full-suite parity artifacts retained at: {tempDirectory}");
            }
            else
            {
                TryDeleteDirectory(tempDirectory);
            }
        }
    }

    private static string BuildParityDiagnostics(
        string framework,
        string dotNetExecutable,
        string paritySeed,
        string tempDirectory,
        string dynamicTrxPath,
        string aotTrxPath,
        string discoveredMapPath,
        string sanitizedDiscoveredMapPath,
        string generatedRegistryPath,
        IReadOnlyList<string> excludedMappings)
    {
        var excludedPreview = excludedMappings.Count == 0
                                  ? "<none>"
                                  : string.Join(Environment.NewLine, excludedMappings.Take(10));
        return
            $"Framework: {framework}" + Environment.NewLine +
            $"Dotnet executable: {dotNetExecutable}" + Environment.NewLine +
            $"Parity seed: {paritySeed}" + Environment.NewLine +
            $"Temp directory: {tempDirectory}" + Environment.NewLine +
            $"Dynamic TRX: {dynamicTrxPath}" + Environment.NewLine +
            $"AOT TRX: {aotTrxPath}" + Environment.NewLine +
            $"Discovered map: {discoveredMapPath}" + Environment.NewLine +
            $"Sanitized map: {sanitizedDiscoveredMapPath}" + Environment.NewLine +
            $"Generated registry: {generatedRegistryPath}" + Environment.NewLine +
            $"Excluded mappings: {excludedMappings.Count}" + Environment.NewLine +
            excludedPreview;
    }

    private static string ResolveParitySeed()
    {
        var configuredSeed = Environment.GetEnvironmentVariable(ParitySeedEnvironmentVariable);
        return string.IsNullOrWhiteSpace(configuredSeed) ? DefaultParitySeed : configuredSeed.Trim();
    }

    private static bool ShouldKeepArtifacts()
    {
        var value = Environment.GetEnvironmentVariable(KeepArtifactsEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveDotNetExecutableOrThrow(string framework)
    {
        var dotNetExecutable = SelectDotNetExecutableForFramework(framework);
        dotNetExecutable.Should().NotBeNullOrWhiteSpace(
            "a compatible dotnet runtime is required for the selected framework." +
            Environment.NewLine +
            BuildRuntimeResolutionDiagnostics([framework]));
        return dotNetExecutable!;
    }

    private static IReadOnlyList<string> ResolveRequestedMatrixFrameworks()
    {
        var configured = Environment.GetEnvironmentVariable(MatrixFrameworksEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(configured))
        {
            return DefaultMatrixFrameworks;
        }

        var frameworks = configured.Split([',', ';', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                                   .Select(item => item.Trim())
                                   .Where(item => !string.IsNullOrWhiteSpace(item))
                                   .Distinct(StringComparer.OrdinalIgnoreCase)
                                   .ToList();
        frameworks.Should().NotBeEmpty(
            $"environment variable '{MatrixFrameworksEnvironmentVariable}' must contain at least one framework token.");
        return frameworks;
    }

    private static string NormalizeFrameworkToken(string framework)
    {
        return framework.Replace('.', '_')
                        .Replace('-', '_')
                        .Replace('+', '_');
    }

    private static string? SelectDotNetExecutableForFramework(string framework)
    {
        var requiredRuntimePrefix = GetFrameworkRuntimePrefix(framework);
        foreach (var candidate in GetDotNetExecutableCandidates())
        {
            var inventory = GetRuntimeInventory(candidate);
            if (inventory.NetCoreRuntimeVersionPrefixes.Contains(requiredRuntimePrefix, StringComparer.Ordinal))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string BuildRuntimeResolutionDiagnostics(IReadOnlyCollection<string> unresolvedFrameworks)
    {
        var lines = new List<string>
        {
            $"Unresolved frameworks: {string.Join(", ", unresolvedFrameworks.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))}"
        };

        foreach (var candidate in GetDotNetExecutableCandidates())
        {
            var inventory = GetRuntimeInventory(candidate);
            lines.Add($"dotnet candidate: {candidate}");
            lines.Add($"  exit code: {inventory.ExitCode}");
            lines.Add(
                "  Microsoft.NETCore.App prefixes: " +
                (inventory.NetCoreRuntimeVersionPrefixes.Count == 0
                     ? "<none>"
                     : string.Join(
                         ", ",
                         inventory.NetCoreRuntimeVersionPrefixes.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))));
            if (inventory.ExitCode != 0 && !string.IsNullOrWhiteSpace(inventory.StandardError))
            {
                lines.Add($"  stderr: {inventory.StandardError.Trim()}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static IReadOnlyList<string> GetDotNetExecutableCandidates()
    {
        var candidates = new List<string> { "dotnet" };
        if (File.Exists(DotNetX64Path))
        {
            candidates.Add(DotNetX64Path);
        }

        return candidates.Distinct(StringComparer.Ordinal).ToList();
    }

    private static string GetFrameworkRuntimePrefix(string framework)
    {
        if (framework.StartsWith("netcoreapp", StringComparison.OrdinalIgnoreCase))
        {
            return framework.Substring("netcoreapp".Length);
        }

        if (framework.StartsWith("net", StringComparison.OrdinalIgnoreCase))
        {
            return framework.Substring("net".Length);
        }

        throw new ArgumentOutOfRangeException(nameof(framework), framework, "Unsupported framework moniker format.");
    }

    private static IReadOnlyDictionary<string, string?>? BuildDotNetProcessEnvironment(
        string dotNetExecutable,
        IReadOnlyDictionary<string, string?>? additionalEnvironmentVariables)
    {
        Dictionary<string, string?>? environmentVariables = new(StringComparer.Ordinal)
        {
            // Ensure nested dotnet invocations keep using the selected host.
            ["DOTNET_HOST_PATH"] = dotNetExecutable,
            // Prevent stale outer-process overrides from forcing a mismatched msbuild host.
            ["MSBUILD_EXE_PATH"] = null,
            ["MSBuildSDKsPath"] = null,
            ["MSBuildExtensionsPath"] = null,
            ["MSBuildExtensionsPath32"] = null,
            ["MSBuildExtensionsPath64"] = null
        };

        if (string.Equals(dotNetExecutable, DotNetX64Path, StringComparison.Ordinal))
        {
            environmentVariables["DOTNET_ROOT"] = DotNetX64Root;
            environmentVariables["DOTNET_ROOT_X64"] = DotNetX64Root;
            environmentVariables["DOTNET_ROOT_ARM64"] = null;
            environmentVariables["DOTNET_MULTILEVEL_LOOKUP"] = "0";
        }

        if (additionalEnvironmentVariables is null || additionalEnvironmentVariables.Count == 0)
        {
            return environmentVariables.Count == 0 ? null : environmentVariables;
        }

        foreach (var (key, value) in additionalEnvironmentVariables)
        {
            environmentVariables[key] = value;
        }

        return environmentVariables.Count == 0 ? null : environmentVariables;
    }

    private static DotNetRuntimeInventory GetRuntimeInventory(string dotNetExecutable)
    {
        lock (RuntimeInventoryLock)
        {
            if (RuntimeInventories.TryGetValue(dotNetExecutable, out var cached))
            {
                return cached;
            }
        }

        DotNetRuntimeInventory inventory;
        try
        {
            var result = RunProcess(
                fileName: dotNetExecutable,
                workingDirectory: Path.GetTempPath(),
                timeoutMilliseconds: 60_000,
                captureOutput: true,
                environmentVariables: BuildDotNetProcessEnvironment(dotNetExecutable, additionalEnvironmentVariables: null),
                arguments: ["--list-runtimes"]);

            var runtimePrefixes = new HashSet<string>(StringComparer.Ordinal);
            var runtimeDirectoriesByPrefix = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var line in result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("Microsoft.NETCore.App ", StringComparison.Ordinal))
                {
                    continue;
                }

                var tokens = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < 2)
                {
                    continue;
                }

                if (TryExtractMajorMinorVersionPrefix(tokens[1], out var prefix))
                {
                    runtimePrefixes.Add(prefix);

                    var runtimeDirectoryStart = trimmed.LastIndexOf('[');
                    var runtimeDirectoryEnd = trimmed.LastIndexOf(']');
                    if (runtimeDirectoryStart >= 0 && runtimeDirectoryEnd > runtimeDirectoryStart)
                    {
                        var runtimeDirectory = trimmed.Substring(runtimeDirectoryStart + 1, runtimeDirectoryEnd - runtimeDirectoryStart - 1);
                        if (!string.IsNullOrWhiteSpace(runtimeDirectory))
                        {
                            var normalizedRuntimeDirectory = runtimeDirectory;
                            var versionedRuntimeDirectory = Path.Combine(runtimeDirectory, tokens[1]);
                            if (Directory.Exists(versionedRuntimeDirectory))
                            {
                                normalizedRuntimeDirectory = versionedRuntimeDirectory;
                            }

                            if (Directory.Exists(normalizedRuntimeDirectory))
                            {
                                runtimeDirectoriesByPrefix[prefix] = normalizedRuntimeDirectory;
                            }
                        }
                    }
                }
            }

            inventory = new DotNetRuntimeInventory(result.ExitCode, result.StandardError, runtimePrefixes, runtimeDirectoriesByPrefix);
        }
        catch (Exception ex)
        {
            inventory = new DotNetRuntimeInventory(
                exitCode: -1,
                standardError: ex.Message,
                netCoreRuntimeVersionPrefixes: [],
                runtimeDirectoriesByPrefix: new Dictionary<string, string>(StringComparer.Ordinal));
        }

        lock (RuntimeInventoryLock)
        {
            RuntimeInventories[dotNetExecutable] = inventory;
            return inventory;
        }
    }

    private static bool TryExtractMajorMinorVersionPrefix(string version, out string prefix)
    {
        prefix = string.Empty;
        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        var segments = version.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
        {
            return false;
        }

        prefix = $"{segments[0]}.{segments[1]}";
        return true;
    }

    private static Dictionary<string, TestResultSnapshot> ReadResults(string trxPath)
    {
        var document = XDocument.Load(trxPath);
        var ns = document.Root?.Name.Namespace ?? XNamespace.None;

        var outcomes = new Dictionary<string, TestResultSnapshot>(StringComparer.Ordinal);
        foreach (var result in document.Descendants(ns + "UnitTestResult"))
        {
            var testName = result.Attribute("testName")?.Value;
            var outcome = result.Attribute("outcome")?.Value;
            if (string.IsNullOrWhiteSpace(testName) || string.IsNullOrWhiteSpace(outcome))
            {
                continue;
            }

            var errorMessage = result.Element(ns + "Output")
                                     ?.Element(ns + "ErrorInfo")
                                     ?.Element(ns + "Message")
                                     ?.Value;
            outcomes[testName] = new TestResultSnapshot(outcome, NormalizeMessage(errorMessage));
        }

        return outcomes;
    }

    private static bool ShouldCompareAssertionMessage(string outcome)
    {
        return string.Equals(outcome, "Failed", StringComparison.Ordinal) ||
               string.Equals(outcome, "Error", StringComparison.Ordinal);
    }

    private static string NormalizeMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        return message.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
    }

    private static string FormatForDisplay(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "<empty>";
        }

        const int maxLength = 240;
        if (message.Length <= maxLength)
        {
            return message;
        }

        return message.Substring(0, maxLength) + "...";
    }

    private static GenerateInput PrepareGenerateInput(
        string discoveredMapPath,
        string sanitizedMapPath,
        string duckTypingTestsAssemblyPath,
        string runnerAssemblyPath,
        string repositoryRoot,
        string framework,
        string dotNetExecutable,
        bool includeCurrentProcessAssemblies)
    {
        var parseResult = DuckTypeAotMapFileParser.Parse(discoveredMapPath);
        parseResult.Errors.Should().BeEmpty("dynamic discovery output should always be parseable by ducktype-aot map parser");
        parseResult.Mappings.Should().NotBeEmpty("full-suite dynamic discovery should produce at least one mapping");

        var assemblyPathIndex = BuildAssemblyPathIndex(
        [
            Path.GetDirectoryName(duckTypingTestsAssemblyPath) ?? string.Empty,
            Path.GetDirectoryName(runnerAssemblyPath) ?? string.Empty
        ],
        includeCurrentProcessAssemblies);
        var isolatedRuntimeAssemblyDirectory = Path.Combine(
            Path.GetDirectoryName(sanitizedMapPath) ?? Path.GetTempPath(),
            "resolved-runtime-assemblies");

        bool TryEnsureAssemblyResolved(string assemblyName)
        {
            if (assemblyPathIndex.ContainsKey(assemblyName))
            {
                return true;
            }

            if (ShouldAttemptRepositoryAssemblyResolution(assemblyName))
            {
                var resolvedRepositoryPath = TryResolveAssemblyPathFromRepository(repositoryRoot, framework, assemblyName);
                if (!string.IsNullOrWhiteSpace(resolvedRepositoryPath))
                {
                    TryAddAssemblyPath(assemblyPathIndex, resolvedRepositoryPath);
                    if (!assemblyPathIndex.ContainsKey(assemblyName))
                    {
                        var normalizedAssemblyName = DuckTypeAotNameHelpers.NormalizeAssemblyName(assemblyName);
                        if (!string.IsNullOrWhiteSpace(normalizedAssemblyName))
                        {
                            assemblyPathIndex[normalizedAssemblyName] = resolvedRepositoryPath;
                        }
                    }

                    if (assemblyPathIndex.ContainsKey(assemblyName))
                    {
                        return true;
                    }
                }
            }

            var resolvedRuntimePath = TryResolveAssemblyPathFromDotNetRuntime(dotNetExecutable, framework, assemblyName);
            if (!string.IsNullOrWhiteSpace(resolvedRuntimePath))
            {
                Directory.CreateDirectory(isolatedRuntimeAssemblyDirectory);
                var isolatedAssemblyPath = Path.Combine(isolatedRuntimeAssemblyDirectory, $"{assemblyName}.dll");
                File.Copy(resolvedRuntimePath, isolatedAssemblyPath, overwrite: true);
                TryAddAssemblyPath(assemblyPathIndex, isolatedAssemblyPath);
                if (!assemblyPathIndex.ContainsKey(assemblyName))
                {
                    // System.Private.CoreLib (especially from older runtimes) can fail AssemblyName metadata reads.
                    // Keep parity-generation orchestration resilient by indexing this known-resolved path by map name.
                    var normalizedAssemblyName = DuckTypeAotNameHelpers.NormalizeAssemblyName(assemblyName);
                    if (!string.IsNullOrWhiteSpace(normalizedAssemblyName))
                    {
                        assemblyPathIndex[normalizedAssemblyName] = isolatedAssemblyPath;
                    }
                }
            }

            return assemblyPathIndex.ContainsKey(assemblyName);
        }

        var filteredMappings = new List<DuckTypeAotMapping>();
        var excludedMappings = new List<string>();

        foreach (var mapping in parseResult.Mappings)
        {
            if (IsRuntimeGeneratedAssemblyName(mapping.ProxyAssemblyName) || IsRuntimeGeneratedAssemblyName(mapping.TargetAssemblyName))
            {
                excludedMappings.Add($"runtime-generated-assembly: {mapping.Key}");
                continue;
            }

            if (DuckTypeAotNameHelpers.IsOpenGenericTypeName(mapping.ProxyTypeName) ||
                DuckTypeAotNameHelpers.IsOpenGenericTypeName(mapping.TargetTypeName))
            {
                excludedMappings.Add($"open-generic: {mapping.Key}");
                continue;
            }

            if (!TryEnsureAssemblyResolved(mapping.ProxyAssemblyName))
            {
                excludedMappings.Add($"proxy-assembly-unresolved: {mapping.Key}");
                continue;
            }

            if (!TryEnsureAssemblyResolved(mapping.TargetAssemblyName))
            {
                excludedMappings.Add($"target-assembly-unresolved: {mapping.Key}");
                continue;
            }

            filteredMappings.Add(mapping);
        }

        filteredMappings.Should().NotBeEmpty("at least one discovered mapping should be eligible for generation");

        var unexpectedExcludedMappings = excludedMappings
                                        .Where(reason =>
                                            !reason.StartsWith("runtime-generated-assembly: ", StringComparison.Ordinal))
                                        .ToList();
        unexpectedExcludedMappings.Should().BeEmpty(
            "all discovered mappings should be resolvable and closed for parity generation." +
            Environment.NewLine +
            string.Join(Environment.NewLine, unexpectedExcludedMappings.Take(50)) +
            (unexpectedExcludedMappings.Count > 50
                 ? $"{Environment.NewLine}... ({unexpectedExcludedMappings.Count - 50} additional exclusions)"
                 : string.Empty));

        var proxyAssemblyPaths = filteredMappings
                                .Select(mapping => assemblyPathIndex[mapping.ProxyAssemblyName])
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .ToList();

        if (!proxyAssemblyPaths.Contains(duckTypingTestsAssemblyPath, StringComparer.OrdinalIgnoreCase))
        {
            proxyAssemblyPaths.Insert(0, duckTypingTestsAssemblyPath);
        }

        var attributeDiscoveryResult = DuckTypeAotAttributeDiscovery.Discover(proxyAssemblyPaths);
        attributeDiscoveryResult.Errors.Should().BeEmpty("proxy assemblies used for parity generation should be readable for attribute discovery");

        var requiredAttributeTargetAssemblies = attributeDiscoveryResult.Mappings
                                                                      .Select(mapping => mapping.TargetAssemblyName)
                                                                      .Where(name => !string.IsNullOrWhiteSpace(name))
                                                                      .Distinct(StringComparer.OrdinalIgnoreCase)
                                                                      .ToList();
        foreach (var targetAssemblyName in requiredAttributeTargetAssemblies)
        {
            if (TryEnsureAssemblyResolved(targetAssemblyName))
            {
                continue;
            }
        }

        var unresolvedAttributeTargetAssemblies = requiredAttributeTargetAssemblies
                                                 .Where(name => !assemblyPathIndex.ContainsKey(name))
                                                 .ToList();
        unresolvedAttributeTargetAssemblies.Should().BeEmpty(
            "attribute-discovered mappings should have their target assemblies resolved for parity generation." +
            Environment.NewLine +
            string.Join(Environment.NewLine, unresolvedAttributeTargetAssemblies));

        var targetAssemblyPaths = filteredMappings
                                 .Select(mapping => mapping.TargetAssemblyName)
                                 .Concat(requiredAttributeTargetAssemblies)
                                 .Distinct(StringComparer.OrdinalIgnoreCase)
                                 .Select(name => assemblyPathIndex[name])
                                 .Distinct(StringComparer.OrdinalIgnoreCase)
                                 .ToList();

        if (!targetAssemblyPaths.Contains(duckTypingTestsAssemblyPath, StringComparer.OrdinalIgnoreCase))
        {
            targetAssemblyPaths.Insert(0, duckTypingTestsAssemblyPath);
        }

        WriteSanitizedMapFile(filteredMappings, sanitizedMapPath);
        File.Exists(sanitizedMapPath).Should().BeTrue("sanitized map file should be written before generation");

        var targetFolders = targetAssemblyPaths
                           .Select(Path.GetDirectoryName)
                           .Where(path => !string.IsNullOrWhiteSpace(path))
                           .Distinct(StringComparer.OrdinalIgnoreCase)
                           .Cast<string>()
                           .ToList();

        var duckTypingTestsDirectory = Path.GetDirectoryName(duckTypingTestsAssemblyPath);
        if (!string.IsNullOrWhiteSpace(duckTypingTestsDirectory) &&
            !targetFolders.Contains(duckTypingTestsDirectory, StringComparer.OrdinalIgnoreCase))
        {
            targetFolders.Insert(0, duckTypingTestsDirectory);
        }

        return new GenerateInput(sanitizedMapPath, proxyAssemblyPaths, targetFolders, excludedMappings);
    }

    private static Dictionary<string, string> BuildAssemblyPathIndex(IEnumerable<string> searchDirectories, bool includeCurrentProcessAssemblies)
    {
        var assemblyPathsByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var directory in searchDirectories.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (var candidatePath in Directory.EnumerateFiles(directory, "*.dll", SearchOption.TopDirectoryOnly))
            {
                TryAddAssemblyPath(assemblyPathsByName, candidatePath);
            }
        }

        if (!includeCurrentProcessAssemblies)
        {
            return assemblyPathsByName;
        }

        var trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (!string.IsNullOrWhiteSpace(trustedPlatformAssemblies))
        {
            foreach (var candidatePath in trustedPlatformAssemblies.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                TryAddAssemblyPath(assemblyPathsByName, candidatePath);
            }
        }

        foreach (var loadedAssembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (loadedAssembly.IsDynamic)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(loadedAssembly.Location))
            {
                continue;
            }

            TryAddAssemblyPath(assemblyPathsByName, loadedAssembly.Location);
        }

        return assemblyPathsByName;
    }

    private static void TryAddAssemblyPath(IDictionary<string, string> assemblyPathsByName, string candidatePath)
    {
        if (string.IsNullOrWhiteSpace(candidatePath) || !File.Exists(candidatePath))
        {
            return;
        }

        try
        {
            var assemblyName = AssemblyName.GetAssemblyName(candidatePath).Name;
            var normalizedName = DuckTypeAotNameHelpers.NormalizeAssemblyName(assemblyName ?? string.Empty);
            if (string.IsNullOrWhiteSpace(normalizedName) || assemblyPathsByName.ContainsKey(normalizedName))
            {
                return;
            }

            assemblyPathsByName[normalizedName] = candidatePath;
        }
        catch
        {
            // Fallback for runtime assemblies where metadata name reads can fail (for example, older System.Private.CoreLib).
            var fileName = Path.GetFileNameWithoutExtension(candidatePath);
            var normalizedName = DuckTypeAotNameHelpers.NormalizeAssemblyName(fileName ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(normalizedName) && !assemblyPathsByName.ContainsKey(normalizedName))
            {
                assemblyPathsByName[normalizedName] = candidatePath;
            }
        }
    }

    private static string? TryResolveAssemblyPathFromRepository(string repositoryRoot, string framework, string assemblyName)
    {
        if (string.IsNullOrWhiteSpace(repositoryRoot) || string.IsNullOrWhiteSpace(framework) || string.IsNullOrWhiteSpace(assemblyName))
        {
            return null;
        }

        var candidateFileName = $"{assemblyName}.dll";
        var frameworkMarker = $"{Path.DirectorySeparatorChar}{framework}{Path.DirectorySeparatorChar}";
        var rootSearchDirectories = new[]
        {
            Path.Combine(repositoryRoot, "tracer", "src"),
            Path.Combine(repositoryRoot, "tracer", "test")
        };

        foreach (var rootSearchDirectory in rootSearchDirectories)
        {
            if (!Directory.Exists(rootSearchDirectory))
            {
                continue;
            }

            var preferredPath = Directory.EnumerateFiles(rootSearchDirectory, candidateFileName, SearchOption.AllDirectories)
                                         .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}Console{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                                         .OrderByDescending(path => path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                                         .ThenByDescending(path => path.Contains(frameworkMarker, StringComparison.OrdinalIgnoreCase))
                                         .ThenByDescending(path => path.Contains($"{Path.DirectorySeparatorChar}netstandard2.0{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                                         .ThenByDescending(path => path.Contains($"{Path.DirectorySeparatorChar}net8.0{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                                         .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(preferredPath))
            {
                return preferredPath;
            }
        }

        return null;
    }

    private static bool ShouldAttemptRepositoryAssemblyResolution(string assemblyName)
    {
        if (string.IsNullOrWhiteSpace(assemblyName))
        {
            return false;
        }

        return assemblyName.StartsWith("Datadog.", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryResolveAssemblyPathFromDotNetRuntime(string dotNetExecutable, string framework, string assemblyName)
    {
        if (string.IsNullOrWhiteSpace(dotNetExecutable) ||
            string.IsNullOrWhiteSpace(framework) ||
            string.IsNullOrWhiteSpace(assemblyName))
        {
            return null;
        }

        var runtimePrefix = GetFrameworkRuntimePrefix(framework);
        var runtimeInventory = GetRuntimeInventory(dotNetExecutable);
        if (!runtimeInventory.RuntimeDirectoriesByPrefix.TryGetValue(runtimePrefix, out var runtimeDirectory))
        {
            return null;
        }

        var candidatePath = Path.Combine(runtimeDirectory, $"{assemblyName}.dll");
        if (File.Exists(candidatePath))
        {
            return candidatePath;
        }

        var normalizedAssemblyName = DuckTypeAotNameHelpers.NormalizeAssemblyName(assemblyName);
        foreach (var runtimeAssemblyPath in Directory.EnumerateFiles(runtimeDirectory, "*.dll", SearchOption.TopDirectoryOnly))
        {
            var runtimeAssemblyName = Path.GetFileNameWithoutExtension(runtimeAssemblyPath);
            if (string.IsNullOrWhiteSpace(runtimeAssemblyName))
            {
                continue;
            }

            var normalizedRuntimeAssemblyName = DuckTypeAotNameHelpers.NormalizeAssemblyName(runtimeAssemblyName);
            if (string.Equals(normalizedRuntimeAssemblyName, normalizedAssemblyName, StringComparison.OrdinalIgnoreCase))
            {
                return runtimeAssemblyPath;
            }
        }

        return null;
    }

    private static bool IsRuntimeGeneratedAssemblyName(string assemblyName)
    {
        if (string.IsNullOrWhiteSpace(assemblyName))
        {
            return false;
        }

        return assemblyName.StartsWith("Datadog.DuckTypeAssembly.", StringComparison.Ordinal) ||
               assemblyName.StartsWith("Datadog.DuckTypeNotVisibleAssembly.", StringComparison.Ordinal);
    }

    private static void WriteSanitizedMapFile(IEnumerable<DuckTypeAotMapping> mappings, string path)
    {
        var document = new MapDocument
        {
            Mappings = mappings
                      .OrderBy(mapping => mapping.Key, StringComparer.Ordinal)
                      .Select(mapping => new MapEntry
                      {
                          Mode = mapping.Mode == DuckTypeAotMappingMode.Reverse ? "reverse" : "forward",
                          ProxyType = mapping.ProxyTypeName,
                          ProxyAssembly = mapping.ProxyAssemblyName,
                          TargetType = mapping.TargetTypeName,
                          TargetAssembly = mapping.TargetAssemblyName
                      })
                      .ToList()
        };

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(
            document,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });
        File.WriteAllText(path, json);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "tracer", "src", "Datadog.Trace", "Datadog.Trace.csproj");
            if (File.Exists(candidate))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root from the current test execution directory.");
    }

    private static CommandResult RunProcess(
        string fileName,
        string workingDirectory,
        int timeoutMilliseconds,
        bool captureOutput,
        IReadOnlyDictionary<string, string?>? environmentVariables,
        string[] arguments)
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

        if (environmentVariables is not null)
        {
            foreach (var (key, value) in environmentVariables)
            {
                if (string.IsNullOrEmpty(value))
                {
                    _ = startInfo.Environment.Remove(key);
                }
                else
                {
                    startInfo.Environment[key] = value;
                }
            }
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
                // Best effort cleanup after timeout.
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

    private readonly struct DotNetRuntimeInventory
    {
        internal DotNetRuntimeInventory(
            int exitCode,
            string standardError,
            IReadOnlyCollection<string> netCoreRuntimeVersionPrefixes,
            IReadOnlyDictionary<string, string> runtimeDirectoriesByPrefix)
        {
            ExitCode = exitCode;
            StandardError = standardError;
            NetCoreRuntimeVersionPrefixes = netCoreRuntimeVersionPrefixes;
            RuntimeDirectoriesByPrefix = runtimeDirectoriesByPrefix;
        }

        internal int ExitCode { get; }

        internal string StandardError { get; }

        internal IReadOnlyCollection<string> NetCoreRuntimeVersionPrefixes { get; }

        internal IReadOnlyDictionary<string, string> RuntimeDirectoriesByPrefix { get; }
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

    private readonly struct TestResultSnapshot
    {
        internal TestResultSnapshot(string outcome, string errorMessage)
        {
            Outcome = outcome;
            ErrorMessage = errorMessage;
        }

        internal string Outcome { get; }

        internal string ErrorMessage { get; }
    }

    private readonly struct GenerateInput
    {
        internal GenerateInput(
            string sanitizedMapPath,
            IReadOnlyList<string> proxyAssemblyPaths,
            IReadOnlyList<string> targetFolders,
            IReadOnlyList<string> excludedMappings)
        {
            SanitizedMapPath = sanitizedMapPath;
            ProxyAssemblyPaths = proxyAssemblyPaths;
            TargetFolders = targetFolders;
            ExcludedMappings = excludedMappings;
        }

        internal string SanitizedMapPath { get; }

        internal IReadOnlyList<string> ProxyAssemblyPaths { get; }

        internal IReadOnlyList<string> TargetFolders { get; }

        internal IReadOnlyList<string> ExcludedMappings { get; }
    }

    private sealed class MapDocument
    {
        public List<MapEntry> Mappings { get; set; } = [];
    }

    private sealed class MapEntry
    {
        public string Mode { get; set; } = string.Empty;

        public string ProxyType { get; set; } = string.Empty;

        public string ProxyAssembly { get; set; } = string.Empty;

        public string TargetType { get; set; } = string.Empty;

        public string TargetAssembly { get; set; } = string.Empty;
    }
}
#endif
