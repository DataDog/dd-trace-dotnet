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
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Util;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Datadog.Trace.Tools.Runner;

/// <summary>
/// Finds the output folders of the test assemblies that <c>dotnet test</c> runs with the Microsoft.Testing.Platform command.
/// </summary>
internal static class TestingPlatformModuleLocator
{
    private const string ListTargetName = "DatadogListTestModules";

    private const string ModulesDirectoryProperty = "_DatadogMtpCoverageModulesDirectory";

    // Evaluation only: the target writes the path of every Microsoft.Testing.Platform test assembly, one file per project and
    // target framework, and doesn't build anything. It is imported through the hooks MSBuild offers for the single-targeting
    // and the multi-targeting evaluation of a project, so it works for projects, solutions and solution filters.
#pragma warning disable MA0136 // The line endings of the targets file don't matter to MSBuild
    private const string TargetsFileContent = $$"""
        <Project>
          <Target Name="{{ListTargetName}}">
            <ItemGroup Condition="'$(TargetFramework)' == '' and '$(TargetFrameworks)' != ''">
              <_DatadogMtpCoverageTargetFramework Include="$(TargetFrameworks)" />
            </ItemGroup>
            <MSBuild Projects="$(MSBuildProjectFullPath)" Targets="{{ListTargetName}}" Properties="TargetFramework=%(_DatadogMtpCoverageTargetFramework.Identity)" Condition="'@(_DatadogMtpCoverageTargetFramework)' != ''" />
            <WriteLinesToFile Condition="'$(TargetFramework)' != '' and '$(IsTestingPlatformApplication)' == 'true'" File="$({{ModulesDirectoryProperty}})/$([System.IO.Path]::GetRandomFileName()).txt" Lines="$(TargetPath)" />
          </Target>
        </Project>
        """;
#pragma warning restore MA0136

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
    public static bool TryGetOutputFolders(string program, TestingPlatformCommandLine commandLine, string workingDirectory, out List<string> folders)
    {
        var assemblyPaths = commandLine.TestModules is { } testModules
                                ? GetMatchedModulePaths(testModules, commandLine.TestModulesRootDirectory ?? workingDirectory, workingDirectory)
                                : EvaluateProjects(program, commandLine, workingDirectory);

        if (assemblyPaths is null)
        {
            folders = [];
            return false;
        }

        folders = assemblyPaths.Select(Path.GetDirectoryName)
                               .Where(folder => !StringUtil.IsNullOrEmpty(folder))
                               .Distinct(FilePathComparison.Comparer)
                               .ToList()!;
        return folders.Count > 0;
    }

    /// <summary>
    /// Expands a <c>--test-modules</c> expression like <c>dotnet test</c> does: patterns separated by ';', with a leading '!' to exclude.
    /// </summary>
    [TestingAndPrivateOnly]
    internal static List<string> GetMatchedModulePaths(string testModules, string rootDirectory, string workingDirectory)
    {
        var root = Path.GetFullPath(Path.Combine(workingDirectory, rootDirectory));
        if (!Directory.Exists(root))
        {
            return [];
        }

        var matcher = new Matcher();
        var remaining = testModules.AsSpan();
        while (!remaining.IsEmpty)
        {
            var separator = remaining.IndexOf(';');
            var pattern = (separator < 0 ? remaining : remaining.Slice(0, separator)).Trim();
            remaining = separator < 0 ? default : remaining.Slice(separator + 1);
            if (pattern.IsEmpty)
            {
                continue;
            }

            if (pattern[0] == '!')
            {
                var exclude = pattern.Slice(1).TrimStart();
                if (!exclude.IsEmpty)
                {
                    matcher.AddExclude(exclude.ToString());
                }
            }
            else
            {
                matcher.AddInclude(pattern.ToString());
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

            List<string> arguments =
            [
                "msbuild",
                .. commandLine.ProjectOrSolution is { } projectOrSolution ? [projectOrSolution] : Array.Empty<string>(),
                "-nologo",
                "-verbosity:quiet",
                "-nodeReuse:false",
                "-target:" + ListTargetName,
                .. commandLine.MsBuildArguments,
                "-p:CustomAfterMicrosoftCommonTargets=" + targetsFile,
                "-p:CustomAfterMicrosoftCommonCrossTargetingTargets=" + targetsFile,
                "-p:" + ModulesDirectoryProperty + "=" + modulesDirectory,
            ];

            if (!TimedProcess.TryRun(program, Utils.GetArgumentsAsString(arguments), workingDirectory, EvaluationTimeout, out var output, out var error, out var exitCode))
            {
                Log.Warning<TimeSpan>("TestingPlatformModuleLocator: Evaluating the test projects didn't finish within {Timeout}.", EvaluationTimeout);
                return null;
            }

            if (exitCode != 0)
            {
                // A solution can contain projects that don't import the common targets (docker-compose, database projects...);
                // they can't be test applications, and the SDK skips them too, so only their missing target is tolerated.
                if (!OnlyProjectsWithoutTheTargetFailed(output, error))
                {
                    Log.Warning<int, string>("TestingPlatformModuleLocator: Evaluating the test projects failed (exit code {ExitCode}): {Output}", exitCode, Truncate(output, error));
                    return null;
                }

                Log.Debug("TestingPlatformModuleLocator: Some projects don't import the common targets and were skipped.");
            }

            return Directory.EnumerateFiles(modulesDirectory, "*.txt")
                            .SelectMany(File.ReadAllLines)
                            .Where(line => !StringUtil.IsNullOrWhiteSpace(line))
                            .Select(line => line.Trim())
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

    // MSBuild reports a project without the target as "<project> : error MSB4057: The target "DatadogListTestModules" does not exist...".
    // The error code and the target name aren't localized.
    private static bool OnlyProjectsWithoutTheTargetFailed(string output, string error)
    {
        var errors = 0;
        return OnlyMissingTargetErrors(output, ref errors) && OnlyMissingTargetErrors(error, ref errors) && errors > 0;
    }

    // Counts the missing target errors of the text, and returns false when the text reports another error.
    private static bool OnlyMissingTargetErrors(string text, ref int errors)
    {
        var remaining = text.AsSpan();
        while (!remaining.IsEmpty)
        {
            var lineEnd = remaining.IndexOfAny('\r', '\n');
            var line = lineEnd < 0 ? remaining : remaining.Slice(0, lineEnd);
            remaining = lineEnd < 0 ? default : remaining.Slice(lineEnd + 1);
            if (line.IndexOf(": error ".AsSpan()) < 0)
            {
                continue;
            }

            if (line.IndexOf("MSB4057".AsSpan()) < 0 || line.IndexOf(ListTargetName.AsSpan()) < 0)
            {
                return false;
            }

            errors++;
        }

        return true;
    }

    private static string Truncate(string output, string error)
    {
        const int maxLength = 1000;
        if (output.Length >= maxLength)
        {
            return output.Substring(0, maxLength);
        }

        return error.Length > maxLength - output.Length ? output + error.Substring(0, maxLength - output.Length) : output + error;
    }
}
