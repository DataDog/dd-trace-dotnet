// <copyright file="AsmTestChangeDetector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;

internal static class AsmTestChangeDetector
{
    private static readonly string[] AsmPathPrefixes =
    {
        "tracer/src/Datadog.Trace/AppSec/",
        "tracer/src/Datadog.Trace/Iast/",
        "tracer/src/Datadog.Tracer.Native/iast/",
        "tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/AspNet/",
        "tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/AspNetCore/",
        "tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/CryptographyAlgorithm/",
        "tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/HashAlgorithm/",
        "tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Process/",
        "tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/RestSharp/",
        "tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/StackTraceLeak/",
        "tracer/test/Datadog.Trace.Security.IntegrationTests/",
        "tracer/test/Datadog.Trace.Security.Unit.Tests/",
        "tracer/test/Datadog.Trace.Tests/AppSec/",
        "tracer/test/Datadog.Trace.Tests/Headers/Ip/",
        "tracer/test/Datadog.Trace.Tools.Analyzers.Tests/AspectAnalyzers/",
        "tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/AspNet/",
        "tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/AspNetCore/",
        "tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/ProcessStartTests/",
        "tracer/test/benchmarks/Benchmarks.Trace/Asm/",
        "tracer/test/benchmarks/Benchmarks.Trace/Iast/",
        "tracer/test/test-applications/integrations/Samples.GrpcDotNet/",
        "tracer/test/test-applications/integrations/Samples.InstrumentedTests/",
        "tracer/test/test-applications/integrations/Samples.ProcessStart/",
        "tracer/test/test-applications/integrations/Samples.WeakCipher/",
        "tracer/test/test-applications/regression/Sandbox.LegacySecurityPolicy/",
        "tracer/test/test-applications/security/",
        "tracer/test/snapshots/Iast",
        "tracer/test/snapshots/iast",
        "tracer/test/snapshots/ProcessStart",
        "tracer/test/snapshots/Rasp",
        "tracer/test/snapshots/Security",
        "tracer/test/snapshots/WeakCipherTests",
    };

    private static readonly string[] AsmPaths =
    {
        "tracer/build/_build/AsmTestChangeDetector.cs",
        "tracer/build/_build/Build.VariableGenerations.cs",
        "tracer/missing-nullability-files.csv",
        "tracer/src/Datadog.Trace/Tags.AppSec.cs",
        "tracer/src/Datadog.Tracer.Native/Generated/generated_callsites.g.h",
        "tracer/test/Datadog.Trace.Tests/SpanContextPropagatorTests_AddSecurityTestingHeadersAsTags.cs",
        "tracer/test/Datadog.Tracer.Native.Tests/dataflow_test.cpp",
        "tracer/test/Datadog.Tracer.Native.Tests/iast_util_test.cpp",
    };

    private static readonly string[] NonCommonTracerPathPrefixes =
    {
        "tracer/test/",
        "tracer/src/Datadog.Trace.",
        "tracer/src/Datadog.Trace/Agent/",
        "tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/",
        "tracer/src/Datadog.Trace/ContinuousProfiler/",
        "tracer/src/Datadog.Trace/DogStatsd/",
        "tracer/src/Datadog.Trace/FaultTolerant/",
        "tracer/src/Datadog.Trace/Generated/",
        "tracer/src/Datadog.Trace/LibDatadog/",
        "tracer/src/Datadog.Trace/Logging/",
        "tracer/src/Datadog.Trace/OpenTelemetry/",
        "tracer/src/Datadog.Trace/PDBs/",
    };

    public static bool IsMatch(string path)
    {
        var normalizedPath = path.Replace('\\', '/').TrimStart('/');

        if (AsmPaths.Contains(normalizedPath, StringComparer.OrdinalIgnoreCase) || StartsWithAny(normalizedPath, AsmPathPrefixes))
        {
            return true;
        }

        var isCommonTestHelper = normalizedPath.StartsWith("tracer/test/Datadog.Trace.TestHelpers/", StringComparison.OrdinalIgnoreCase);
        return ExplorationTestChangeDetector.IsMatch(ExplorationTestUseCase.Tracer, normalizedPath) &&
               (isCommonTestHelper || !StartsWithAny(normalizedPath, NonCommonTracerPathPrefixes));
    }

    private static bool StartsWithAny(string path, string[] prefixes)
    {
        return prefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}
