// <copyright file="TestImpact.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using Microsoft.Extensions.FileSystemGlobbing;

namespace TestSelection;

internal static class TestImpact
{
    // These rules describe test dependencies, independently of code ownership.
    // Only narrow coverage for well-understood product paths. Shared and unknown paths run all test areas.
    // Skipping a PR entirely is controlled by ultimate-pipeline.yml, not by this classifier.
    private static readonly (TestArea Areas, Matcher Paths)[] Rules =
    {
        (TestArea.Tracer, MatchPaths(
            "tracer/src/Datadog.Trace.Manual/**",
            "tracer/src/Datadog.Trace.OpenTracing/**",
            "tracer/src/Datadog.Trace/OpenTelemetry/**")),
        // Instrumentations are also exercised by security tests. Testing integrations additionally match CI Visibility below.
        (TestArea.Tracer | TestArea.Asm, MatchPaths(
            "tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/**")),
        (TestArea.Asm, MatchPaths(
            "tracer/src/Datadog.Trace/AppSec/**",
            "tracer/src/Datadog.Trace/Iast/**",
            "tracer/src/Datadog.Tracer.Native/iast/**",
            "tracer/test/Datadog.Trace.Security.IntegrationTests/**",
            "tracer/test/Datadog.Trace.Security.Unit.Tests/**",
            "tracer/test/test-applications/security/**")),
        (TestArea.CiVisibility, MatchPaths(
            "tracer/src/Datadog.Trace/Ci/**",
            "tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Testing/**",
            "tracer/src/Datadog.Trace.BenchmarkDotNet/**",
            "tracer/src/Datadog.Trace.Coverage.collector/**",
            "tracer/src/Datadog.Trace.MSBuild/**",
            "tracer/test/Datadog.Trace.Tests/Ci/**",
            "tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/CI/**",
            "tracer/test/Datadog.Trace.BenchmarkDotNet.Tests/**",
            "tracer/test/test-applications/integrations/Samples.XUnit*/**",
            "tracer/test/test-applications/integrations/Samples.NUnit*/**",
            "tracer/test/test-applications/integrations/Samples.MSTest*/**")),
        (TestArea.Debugger, MatchPaths(
            "tracer/src/Datadog.Trace/Debugger/**",
            "tracer/test/Datadog.Trace.Debugger.IntegrationTests/**",
            "tracer/test/test-applications/debugger/**")),
        (TestArea.Profiler, MatchPaths(
            "profiler/src/**",
            "profiler/test/**")),
        (TestArea.Tracer | TestArea.Profiler, MatchPaths(
            "tracer/src/Datadog.Trace/ContinuousProfiler/**")),
        // Build inputs may affect consumers outside the directory containing them.
        (TestArea.All, MatchPaths(
            "**/*.*proj",
            "**/*.props",
            "**/*.targets",
            "**/*.sln*",
            "**/*.cmake",
            "**/CMakeLists.txt")),
    };

    public static TestArea GetAffectedAreas(string changedFile)
    {
        var path = changedFile.Replace('\\', '/');
        var affectedAreas = TestArea.None;

        // Combine matches so that a narrower rule cannot remove another area's coverage.
        foreach (var (areas, _) in Rules.Where(rule => rule.Paths.Match(path).HasMatches))
        {
            affectedAreas |= areas;
        }

        if (affectedAreas == TestArea.None)
        {
            return TestArea.All;
        }

        // Preserve tracer exploration coverage when changing test code or sample applications.
        if (path.StartsWith("tracer/test/", StringComparison.Ordinal))
        {
            affectedAreas |= TestArea.Tracer;
        }

        return affectedAreas;
    }

    private static Matcher MatchPaths(params string[] paths)
    {
        var matcher = new Matcher(StringComparison.Ordinal);
        matcher.AddIncludePatterns(paths);
        return matcher;
    }
}
