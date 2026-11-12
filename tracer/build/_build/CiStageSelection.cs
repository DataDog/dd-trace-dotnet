// <copyright file="CiStageSelection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;

/// <summary>
/// Chooses tracer and profiler integration stages from a pull request diff.
/// </summary>
public static class CiStageSelection
{
    // Keep this list in sync with pr.paths.exclude in .azure-pipelines/ultimate-pipeline.yml.
    // tracer/tools/*.ps*1 there is the .ps1, .psm1, and .psd1 scripts in that directory only.
    private const string LibdatadogCmake = "build/cmake/FindLibdatadog.cmake";
    private const string LibdatadogPortPrefix = "build/vcpkg_local_ports/libdatadog/";
    private const string ProfilerPrefix = "profiler/";
    private const string ProfilerManagedTestsPrefix = "tracer/test/Datadog.Trace.Tests/ContinuousProfiler/";
    private const string TracerToolsPrefix = "tracer/tools/";

    private static readonly string[] IgnoredFiles =
    {
        ".azure-pipelines/noop-pipeline.yml",
        ".gitignore",
        ".github/dependabot.yml",
        ".github/labeller.yml",
        ".github/pull_request_template.md",
        "AGENTS.md",
        "CLAUDE.md",
        "LICENSE",
        "NOTICE",
    };

    private static readonly string[] IgnoredPrefixes =
    {
        ".github/actions/",
        ".github/chainguard/",
        ".github/ISSUE_TEMPLATE/",
        ".github/workflows/",
        ".claude/",
        ".pi/",
        ".devcontainer/",
        ".vscode/",
        "docs/",
        "tracer/samples/",
    };

    public static Result Select(IEnumerable<string> changedPaths)
    {
        var libdatadogChanged = false;
        var sawRelevantPath = false;
        var allProfilerOnly = true;

        foreach (var changedPath in changedPaths)
        {
            var path = Normalize(changedPath);
            if (path.Length == 0)
            {
                continue;
            }

            if (IsLibdatadogPin(path))
            {
                libdatadogChanged = true;
            }

            if (IsIgnored(path))
            {
                continue;
            }

            sawRelevantPath = true;
            if (!IsProfilerOnly(path))
            {
                allProfilerOnly = false;
            }
        }

        return new Result(libdatadogChanged, sawRelevantPath && allProfilerOnly);
    }

    private static string Normalize(string changedPath)
    {
        if (changedPath is null)
        {
            return string.Empty;
        }

        return changedPath.Replace('\\', '/');
    }

    private static bool IsLibdatadogPin(string path)
    {
        return string.Equals(path, LibdatadogCmake, StringComparison.Ordinal)
            || path.StartsWith(LibdatadogPortPrefix, StringComparison.Ordinal);
    }

    private static bool IsIgnored(string path)
    {
        for (var i = 0; i < IgnoredFiles.Length; i++)
        {
            if (string.Equals(path, IgnoredFiles[i], StringComparison.Ordinal))
            {
                return true;
            }
        }

        for (var i = 0; i < IgnoredPrefixes.Length; i++)
        {
            if (path.StartsWith(IgnoredPrefixes[i], StringComparison.Ordinal))
            {
                return true;
            }
        }

        if (IsReadme(path) || IsTracerToolsScript(path))
        {
            return true;
        }

        return false;
    }

    private static bool IsReadme(string path)
    {
        var slash = path.LastIndexOf('/');
        var name = slash < 0 ? path : path.Substring(slash + 1);
        return string.Equals(name, "README.md", StringComparison.Ordinal);
    }

    private static bool IsTracerToolsScript(string path)
    {
        if (!path.StartsWith(TracerToolsPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var name = path.Substring(TracerToolsPrefix.Length);
        if (name.IndexOf('/') >= 0)
        {
            return false;
        }

        return name.EndsWith(".ps1", StringComparison.Ordinal)
            || name.EndsWith(".psm1", StringComparison.Ordinal)
            || name.EndsWith(".psd1", StringComparison.Ordinal);
    }

    private static bool IsProfilerOnly(string path)
    {
        return path.StartsWith(ProfilerPrefix, StringComparison.Ordinal)
            || path.StartsWith(ProfilerManagedTestsPrefix, StringComparison.Ordinal)
            || IsLibdatadogPin(path);
    }

    public readonly struct Result
    {
        public Result(bool libdatadogChanged, bool skipTracerOnlyStages)
        {
            LibdatadogChanged = libdatadogChanged;
            SkipTracerOnlyStages = skipTracerOnlyStages;
        }

        public bool LibdatadogChanged { get; }

        public bool SkipTracerOnlyStages { get; }
    }
}
