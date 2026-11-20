// <copyright file="TestingPlatformModuleLocator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Datadog.Trace.Tools.Runner;

/// <summary>
/// Finds the output folders of the test assemblies that <c>dotnet test</c> runs with the Microsoft.Testing.Platform command.
/// </summary>
internal static class TestingPlatformModuleLocator
{
    // Evaluation only: the target writes the path of every Microsoft.Testing.Platform test assembly, one file per project and
    // target framework, and doesn't build anything. It is imported through the hooks MSBuild offers for the single-targeting
    // and the multi-targeting evaluation of a project, so it works for projects, solutions and solution filters.
    private const string TargetsFileContent = """
        <Project>
          <Target Name="DatadogListTestModules">
            <ItemGroup Condition="'$(TargetFramework)' == '' and '$(TargetFrameworks)' != ''">
              <_DatadogTargetFramework Include="$(TargetFrameworks)" />
            </ItemGroup>
            <MSBuild Projects="$(MSBuildProjectFullPath)" Targets="DatadogListTestModules" Properties="TargetFramework=%(_DatadogTargetFramework.Identity)" Condition="'@(_DatadogTargetFramework)' != ''" />
            <WriteLinesToFile Condition="'$(TargetFramework)' != '' and '$(IsTestingPlatformApplication)' == 'true'" File="$(DatadogModulesDirectory)/$([System.Guid]::NewGuid()).txt" Lines="$(TargetPath)" />
          </Target>
        </Project>
        """;

    private static readonly TimeSpan EvaluationTimeout = TimeSpan.FromMinutes(5);

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(TestingPlatformModuleLocator));

    /// <summary>
    /// Gets the folders with the test assemblies that the command runs.
    /// </summary>
    /// <param name="program">The dotnet executable, used to evaluate the projects.</param>
    /// <param name="commandLine">The parsed <c>dotnet test</c> command.</param>
    /// <param name="workingDirectory">The directory the command runs in.</param>
    /// <param name="folders">The distinct folders that contain the test assemblies.</param>
    /// <returns>False when the assemblies couldn't be determined, or when there are none.</returns>
    public static bool TryGetOutputFolders(string program, TestingPlatformCommandLine commandLine, string workingDirectory, out IReadOnlyList<string> folders)
    {
        var assemblyPaths = commandLine.TestModules is { } testModules
                                ? GetMatchedModulePaths(testModules, commandLine.TestModulesRootDirectory ?? workingDirectory, workingDirectory)
                                : EvaluateProjects(program, commandLine, workingDirectory);

        var comparer = FrameworkDescription.Instance.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        folders = assemblyPaths?.Select(Path.GetDirectoryName)
                                .Where(folder => !StringUtil.IsNullOrEmpty(folder))
                                .Select(folder => folder!)
                                .Distinct(comparer)
                                .ToList() ?? [];
        return folders.Count > 0;
    }

    /// <summary>
    /// Expands a <c>--test-modules</c> expression like <c>dotnet test</c> does: patterns separated by ';', with a leading '!' to exclude.
    /// </summary>
    internal static List<string> GetMatchedModulePaths(string testModules, string rootDirectory, string workingDirectory)
    {
        var root = Path.GetFullPath(Path.Combine(workingDirectory, rootDirectory));
        if (!Directory.Exists(root))
        {
            return [];
        }

        var matcher = new Matcher();
        foreach (var rawPattern in testModules.Split([';'], StringSplitOptions.RemoveEmptyEntries))
        {
            var pattern = rawPattern.Trim();
            if (pattern.Length == 0)
            {
                continue;
            }

            if (pattern[0] == '!')
            {
                var exclude = pattern.Substring(1).TrimStart();
                if (exclude.Length != 0)
                {
                    matcher.AddExclude(exclude);
                }
            }
            else
            {
                matcher.AddInclude(pattern);
            }
        }

        return matcher.GetResultsInFullPath(root).ToList();
    }

    private static List<string>? EvaluateProjects(string program, TestingPlatformCommandLine commandLine, string workingDirectory)
    {
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), "dd-trace-mtp-" + Path.GetRandomFileName());
        try
        {
            Directory.CreateDirectory(temporaryDirectory);
            var targetsFile = Path.Combine(temporaryDirectory, "Datadog.ListTestModules.targets");
            var modulesDirectory = Path.Combine(temporaryDirectory, "modules");
            Directory.CreateDirectory(modulesDirectory);
            File.WriteAllText(targetsFile, TargetsFileContent);

            var arguments = new List<string> { "msbuild" };
            if (commandLine.ProjectOrSolution is { } projectOrSolution)
            {
                arguments.Add(projectOrSolution);
            }

            arguments.Add("-nologo");
            arguments.Add("-verbosity:quiet");
            arguments.Add("-nodeReuse:false");
            arguments.Add("-target:DatadogListTestModules");
            arguments.AddRange(commandLine.MsBuildArguments);
            arguments.Add("-p:CustomAfterMicrosoftCommonTargets=" + targetsFile);
            arguments.Add("-p:CustomAfterMicrosoftCommonCrossTargetingTargets=" + targetsFile);
            arguments.Add("-p:DatadogModulesDirectory=" + modulesDirectory);

            var output = ProcessHelpers.RunCommand(new ProcessHelpers.Command(program, Utils.GetArgumentsAsString(arguments), workingDirectory, timeout: EvaluationTimeout));
            if (output is null || output.TimedOut || output.ExitCode != 0)
            {
                Log.Warning("TestingPlatformModuleLocator: Evaluating the test projects failed (exit code {ExitCode}): {Output}", output?.ExitCode, Truncate(output?.Output));
                return null;
            }

            return Directory.EnumerateFiles(modulesDirectory, "*.txt")
                            .SelectMany(File.ReadAllLines)
                            .Select(line => line.Trim())
                            .Where(line => line.Length > 0)
                            .ToList();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "TestingPlatformModuleLocator: Evaluating the test projects failed.");
            return null;
        }
        finally
        {
            try
            {
                Directory.Delete(temporaryDirectory, recursive: true);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "TestingPlatformModuleLocator: Error deleting the temporary folder.");
            }
        }
    }

    private static string Truncate(string? text)
        => text is { Length: > 1000 } ? text.Substring(0, 1000) : text ?? string.Empty;
}
