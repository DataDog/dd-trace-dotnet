// <copyright file="ExplorationTestChangeDetector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Linq;

internal static class ExplorationTestChangeDetector
{
    // Rules are additive: a single change may require more than one exploration test use case.
    // Keep the tracer paths broad so changes to tests and shared tracer infrastructure are not skipped.
    private static readonly string[] TracerPathPrefixes =
    {
        "tracer/src/",
        "tracer/test/",
        "tracer/samples/",
        "tracer/tools/",
        "tracer/dependabot/",
        "shared/src/msi-installer/Tracer/",
        "docs/Datadog.FeatureFlags.OpenFeature/",
    };

    private static readonly string[] TracerPaths =
    {
        ".gitlab/download-serverless-artifacts.sh",
        "docker-compose.serverless.yml",
        "tracer/build/_build/docker/serverless.lambda.dockerfile",
    };

    // These product-specific paths currently do not trigger tracer exploration tests. Keeping that
    // distinction here avoids broadening CI when ownership moves to a more specific accountable team.
    private static readonly string[] TracerExcludedPathPrefixes =
    {
        // ASM
        "tracer/src/Datadog.Trace/AppSec/",
        "tracer/src/Datadog.Trace/Iast/",
        "tracer/src/Datadog.Tracer.Native/iast/",
        "tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/CryptographyAlgorithm/",
        "tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/HashAlgorithm/",
        "tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/RestSharp/",
        "tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/StackTraceLeak/",
        "tracer/test/Datadog.Trace.Security.IntegrationTests/",
        "tracer/test/Datadog.Trace.Security.Unit.Tests/",
        "tracer/test/benchmarks/Benchmarks.Trace/Asm/",
        "tracer/test/benchmarks/Benchmarks.Trace/Iast/",
        "tracer/test/test-applications/integrations/Samples.InstrumentedTests/",
        "tracer/test/test-applications/security/",
        "tracer/test/snapshots/Iast",
        "tracer/test/snapshots/iast",
        "tracer/test/snapshots/Rasp",
        "tracer/test/snapshots/Security",

        // Debugger
        "tracer/src/Datadog.Trace/Debugger/",
        "tracer/src/Datadog.Trace/PDBs/",
        "tracer/src/Datadog.Trace/FaultTolerant/",
        "tracer/src/Datadog.InstrumentedAssemblyGenerator/",
        "tracer/src/Datadog.InstrumentedAssemblyVerification/",
        "tracer/test/Datadog.Trace.Debugger.IntegrationTests/",
        "tracer/test/Datadog.Trace.Tests/Debugger/",
        "tracer/test/test-applications/debugger/",

        // CI Visibility
        "tracer/src/Datadog.Trace/Ci/",
        "tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Testing/",
        "tracer/src/Datadog.Trace.Tools.Runner/Ci",
        "tracer/src/Datadog.Trace.Tools.Runner/CI",
        "tracer/src/Datadog.Trace.Tools.Runner/Coverage",
        "tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/CI/",
        "tracer/test/Datadog.Trace.Tests/Ci/",
        "tracer/test/test-applications/integrations/Samples.MSTest",
        "tracer/test/test-applications/integrations/Samples.NUnit",
        "tracer/test/test-applications/integrations/Samples.Selenium",
        "tracer/test/test-applications/integrations/Samples.XUnit",
        "tracer/test/snapshots/MsTestV2",
        "tracer/test/snapshots/NUnit",
        "tracer/test/snapshots/Selenium",
        "tracer/test/snapshots/XUnit",

        // Generated source
        "tracer/src/Datadog.Trace/Generated/",
    };

    private static readonly string[] TracerExcludedPaths =
    {
        "tracer/Directory.Build.props",
        "tracer/missing-nullability-files.csv",
        "tracer/src/Datadog.Trace/Configuration/IntegrationId.cs",
        "tracer/src/Datadog.Trace/PDBs/MethodSymbolResolver.cs",
        "tracer/src/Datadog.Trace/Tags.AppSec.cs",
        "tracer/src/Datadog.Trace/Telemetry/Metrics/IntegrationIdExtensions.cs",
        "tracer/src/Datadog.Trace/Telemetry/Metrics/MetricTags.cs",
        "tracer/src/Datadog.Trace/TracerConstants.cs",
        "tracer/src/Datadog.Trace.Tools.Runner/RunCiCommand.cs",
        "tracer/src/Datadog.Tracer.Native/Generated/generated_callsites.g.h",
        "tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/SourceCodeIntegrationGitMetadataTests.cs",
    };

    private static readonly string[] DebuggerPathPrefixes =
    {
        "tracer/src/Datadog.Trace/Debugger/",
        "tracer/src/Datadog.Trace/PDBs/",
        "tracer/src/Datadog.Trace/FaultTolerant/",
        "tracer/src/Datadog.InstrumentedAssemblyGenerator/",
        "tracer/src/Datadog.InstrumentedAssemblyVerification/",
        "tracer/test/Datadog.Trace.Debugger.IntegrationTests/",
        "tracer/test/Datadog.Trace.Tests/Debugger/",
        "tracer/test/Datadog.Trace.Tests/Pdb/",
        "tracer/test/snapshots/ProbeTests",
        "tracer/test/snapshots/SymbolUploadApiTests",
        "tracer/test/test-applications/debugger/",
    };

    private static readonly string[] DebuggerPaths =
    {
        "Datadog.Trace.Debugger.slnf",
        "tracer/build/_build/Build.Steps.Debugger.cs",
        "tracer/src/Datadog.Trace/ClrProfiler/Instrumentation.cs",
        "tracer/test/Datadog.Trace.ClrProfiler.Managed.Tests/PdbReaderTests.cs",
        "tracer/test/Datadog.Trace.ClrProfiler.Managed.Tests/SourceLinkUriParserTests.cs",
        "tracer/test/Datadog.Trace.TestHelpers/MockProbeSnapshot.cs",
        "tracer/missing-nullability-files.csv",
    };

    private static readonly string[] ProfilerPathPrefixes =
    {
        "profiler/",
        "shared/src/Datadog.Trace.ClrProfiler.Native/",
        "tracer/src/Datadog.Trace/ContinuousProfiler/",
        "tracer/test/Datadog.Trace.Tests/ContinuousProfiler/",
        "tracer/test/test-applications/throughput/Samples.AspNetCoreSimpleController/",
    };

    private static readonly string[] ProfilerPaths =
    {
        "tracer/build/_build/Build.Profiler.Steps.cs",
        "tracer/missing-nullability-files.csv",
    };

    private static readonly string[] ExplorationTestInfrastructurePaths =
    {
        "tracer/build/_build/Build.ExplorationTests.cs",
        "tracer/build/_build/Build.VariableGenerations.cs",
        "tracer/build/_build/ExplorationTests/ExplorationTestChangeDetector.cs",
    };

    public static bool IsMatch(ExplorationTestUseCase useCase, string path)
    {
        var normalizedPath = path.Replace('\\', '/').TrimStart('/');

        if (ExplorationTestInfrastructurePaths.Contains(normalizedPath, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        return useCase switch
        {
            ExplorationTestUseCase.Tracer => IsTracerMatch(normalizedPath),
            ExplorationTestUseCase.Debugger => StartsWithAny(normalizedPath, DebuggerPathPrefixes) ||
                                               DebuggerPaths.Contains(normalizedPath, StringComparer.OrdinalIgnoreCase) ||
                                               IsNativeDebuggerFile(normalizedPath),
            ExplorationTestUseCase.ContinuousProfiler => StartsWithAny(normalizedPath, ProfilerPathPrefixes) || ProfilerPaths.Contains(normalizedPath, StringComparer.OrdinalIgnoreCase),
            _ => throw new ArgumentOutOfRangeException(nameof(useCase), useCase, null),
        };
    }

    private static bool StartsWithAny(string path, string[] prefixes)
    {
        return prefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsTracerMatch(string path)
    {
        var isIncluded = StartsWithAny(path, TracerPathPrefixes) ||
                         TracerPaths.Contains(path, StringComparer.OrdinalIgnoreCase) ||
                         ProfilerPaths.Contains(path, StringComparer.OrdinalIgnoreCase) ||
                         IsTracerRootFile(path) ||
                         IsServerlessDocumentation(path);

        if (!isIncluded)
        {
            return false;
        }

        return !StartsWithAny(path, TracerExcludedPathPrefixes) &&
               !TracerExcludedPaths.Contains(path, StringComparer.OrdinalIgnoreCase) &&
               !IsNativeDebuggerFile(path) &&
               !path.EndsWith("/Datadog.Trace.Trimming.xml", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNativeDebuggerFile(string path)
    {
        var fileName = Path.GetFileName(path);
        return (fileName.StartsWith("debugger_", StringComparison.OrdinalIgnoreCase) ||
                fileName.StartsWith("fault_tolerant_", StringComparison.OrdinalIgnoreCase)) &&
               (fileName.EndsWith(".cpp", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".h", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsTracerRootFile(string path)
    {
        const string tracerDirectory = "tracer/";
        return path.StartsWith(tracerDirectory, StringComparison.OrdinalIgnoreCase) && path.IndexOf('/', tracerDirectory.Length) < 0;
    }

    private static bool IsServerlessDocumentation(string path)
    {
        if (!path.StartsWith("docs/development/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var fileName = Path.GetFileName(path);
        return fileName.Contains("AzureFunctions", StringComparison.OrdinalIgnoreCase) ||
               fileName.Contains("Lambda", StringComparison.OrdinalIgnoreCase);
    }
}
