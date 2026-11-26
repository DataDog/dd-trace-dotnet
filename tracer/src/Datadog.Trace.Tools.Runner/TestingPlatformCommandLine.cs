// <copyright file="TestingPlatformCommandLine.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;

namespace Datadog.Trace.Tools.Runner;

/// <summary>
/// The parts of a <c>dotnet test</c> command line, for the Microsoft.Testing.Platform command, that decide which test assemblies run.
/// </summary>
internal sealed class TestingPlatformCommandLine
{
    // Options of the Microsoft.Testing.Platform `dotnet test` command that take a value, so the value is not taken for a project path.
    private static readonly string[] OptionsWithValue =
    [
        "--project", "--solution", "--test-modules", "--root-directory", "--results-directory", "--results-directory-layout",
        "--config-file", "--diagnostic-output-directory", "--max-parallel-test-modules", "--minimum-expected-tests",
        "--maximum-failed-tests", "--timeout", "-e", "--environment", "-v", "--verbosity", "--output",
        "-c", "--configuration", "-f", "--framework", "-r", "--runtime", "--artifacts-path", "-p", "--property"
    ];

    // Options that change the output folders in ways this class doesn't reproduce.
    private static readonly string[] UnsupportedOptions = ["-a", "--arch", "--os", "--use-current-runtime", "--ucr", "--device"];

    // Options of the command that don't take a value.
    private static readonly string[] FlagOptions =
    [
        "--no-build", "--no-restore", "--no-dependencies", "--no-ansi", "--no-progress", "--no-launch-profile", "--no-launch-profile-arguments",
        "--list-tests", "--no-logo", "--nologo", "--no-banner", "-?", "-h", "--help"
    ];

    private static readonly string[] MsBuildPropertyPrefixes = ["-p:", "/p:", "-property:", "/property:", "--property:"];

    private static readonly string[] ProjectFileExtensions = [".sln", ".slnx", ".slnf"];

    private TestingPlatformCommandLine(bool noBuild, string? testModules, string? testModulesRootDirectory, string? projectOrSolution, List<string> msBuildArguments, string? unsupportedOption)
    {
        NoBuild = noBuild;
        TestModules = testModules;
        TestModulesRootDirectory = testModulesRootDirectory;
        ProjectOrSolution = projectOrSolution;
        MsBuildArguments = msBuildArguments;
        UnsupportedOption = unsupportedOption;
    }

    /// <summary>
    /// Gets a value indicating whether the command doesn't build the projects.
    /// </summary>
    public bool NoBuild { get; }

    /// <summary>
    /// Gets the <c>--test-modules</c> expression, or null when the projects are selected instead.
    /// </summary>
    public string? TestModules { get; }

    /// <summary>
    /// Gets the <c>--root-directory</c> value, or null when it wasn't set.
    /// </summary>
    public string? TestModulesRootDirectory { get; }

    /// <summary>
    /// Gets the project, solution or folder to test, or null when the command uses the current directory.
    /// </summary>
    public string? ProjectOrSolution { get; }

    /// <summary>
    /// Gets the MSBuild arguments equivalent to the options that select the configuration, framework, runtime and properties.
    /// </summary>
    public IReadOnlyList<string> MsBuildArguments { get; }

    /// <summary>
    /// Gets the first option that can't be reproduced, or null.
    /// </summary>
    public string? UnsupportedOption { get; }

    /// <summary>
    /// Gets a value indicating whether the command provably runs the assemblies already in the output folders, without building them.
    /// </summary>
    public bool RunsPrebuiltAssemblies => NoBuild || TestModules is not null;

    /// <summary>
    /// Parses the arguments of a <c>dotnet test</c> command.
    /// </summary>
    /// <param name="arguments">The arguments, starting at the <c>test</c> verb or before it.</param>
    /// <param name="workingDirectory">The directory the command runs in.</param>
    /// <returns>The parsed command line.</returns>
    public static TestingPlatformCommandLine Parse(IReadOnlyList<string> arguments, string workingDirectory)
    {
        var noBuild = false;
        string? testModules = null;
        string? rootDirectory = null;
        string? project = null;
        string? solution = null;
        string? positional = null;
        string? unsupportedOption = null;
        var msBuildArguments = new List<string>();
        var previousIsUnknownOption = false;

        var index = 0;
        while (index < arguments.Count && !string.Equals(Unquote(arguments[index]), "test", StringComparison.OrdinalIgnoreCase))
        {
            index++;
        }

        for (index++; index < arguments.Count; index++)
        {
            var argument = Unquote(arguments[index]);
            var afterUnknownOption = previousIsUnknownOption;
            previousIsUnknownOption = false;
            if (argument == "--")
            {
                // The rest is for the test applications.
                break;
            }

            if (argument == "--no-build")
            {
                noBuild = true;
            }
            else if (TryGetValue(arguments, ref index, argument, ["--test-modules"], out var value))
            {
                testModules = value;
            }
            else if (TryGetValue(arguments, ref index, argument, ["--root-directory"], out value))
            {
                rootDirectory = value;
            }
            else if (TryGetValue(arguments, ref index, argument, ["--project"], out value))
            {
                project = value;
            }
            else if (TryGetValue(arguments, ref index, argument, ["--solution"], out value))
            {
                solution = value;
            }
            else if (TryGetValue(arguments, ref index, argument, ["-c", "--configuration"], out value))
            {
                msBuildArguments.Add("-p:Configuration=" + value);
            }
            else if (TryGetValue(arguments, ref index, argument, ["-f", "--framework"], out value))
            {
                msBuildArguments.Add("-p:TargetFramework=" + value);
            }
            else if (TryGetValue(arguments, ref index, argument, ["-r", "--runtime"], out value))
            {
                msBuildArguments.Add("-p:RuntimeIdentifier=" + value);
                msBuildArguments.Add("-p:_CommandLineDefinedRuntimeIdentifier=true");
            }
            else if (TryGetValue(arguments, ref index, argument, ["--artifacts-path"], out value))
            {
                msBuildArguments.Add("-p:ArtifactsPath=" + Path.GetFullPath(Path.Combine(workingDirectory, value)));
            }
            else if (IsMsBuildProperty(argument, out var property))
            {
                msBuildArguments.Add("-p:" + property);
            }
            else if (TryGetValue(arguments, ref index, argument, ["-p", "--property"], out value))
            {
                msBuildArguments.Add("-p:" + value);
            }
            else if (IsOption(argument, UnsupportedOptions))
            {
                unsupportedOption ??= GetOptionName(argument);
            }
            else if (IsOption(argument, OptionsWithValue))
            {
                // An option this class doesn't use: skip its value, unless it was attached with ':' or '='.
                if (argument.IndexOf(':') < 0 && argument.IndexOf('=') < 0)
                {
                    index++;
                }
            }
            else if (IsOptionToken(argument))
            {
                // An option that isn't listed, such as one of the test application: its value is not a project.
                // Short options with the value attached, like -cRelease, are not understood either.
                if (IsAttachedShortOption(argument))
                {
                    unsupportedOption ??= argument;
                }
                else if (Array.IndexOf(FlagOptions, GetOptionName(argument)) < 0 && argument.IndexOf(':') < 0 && argument.IndexOf('=') < 0)
                {
                    previousIsUnknownOption = true;
                }
            }
            else if (positional is null && !afterUnknownOption && IsProjectOrSolutionPath(argument, workingDirectory))
            {
                positional = argument;
            }
        }

        return new TestingPlatformCommandLine(noBuild, testModules, rootDirectory, solution ?? project ?? positional, msBuildArguments, unsupportedOption);
    }

    private static string Unquote(string argument) => argument.Trim('"');

    private static string GetOptionName(string argument)
    {
        var separator = argument.IndexOfAny([':', '=']);
        return separator < 0 ? argument : argument.Substring(0, separator);
    }

    private static bool IsOption(string argument, string[] names) => Array.IndexOf(names, GetOptionName(argument)) >= 0;

    private static bool TryGetValue(IReadOnlyList<string> arguments, ref int index, string argument, string[] names, out string value)
    {
        foreach (var name in names)
        {
            if (argument == name)
            {
                value = index + 1 < arguments.Count ? Unquote(arguments[index + 1]) : string.Empty;
                index++;
                return true;
            }

            if (argument.Length > name.Length && argument.StartsWith(name, StringComparison.Ordinal) && (argument[name.Length] == ':' || argument[name.Length] == '='))
            {
                value = argument.Substring(name.Length + 1);
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    private static bool IsMsBuildProperty(string argument, out string property)
    {
        foreach (var prefix in MsBuildPropertyPrefixes)
        {
            if (argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                property = argument.Substring(prefix.Length);
                return true;
            }
        }

        property = string.Empty;
        return false;
    }

    private static bool IsOptionToken(string argument) => argument.Length > 1 && argument[0] == '-';

    private static bool IsAttachedShortOption(string argument)
        => argument.Length > 2 && argument[0] == '-' && argument[1] != '-' && "cfa".IndexOf(argument[1]) >= 0 && argument[2] != ':' && argument[2] != '=';

    private static bool IsProjectOrSolutionPath(string argument, string workingDirectory)
    {
        if (argument.Length == 0 || argument[0] == '-' || (argument.StartsWith("/p", StringComparison.OrdinalIgnoreCase) && argument.IndexOf(':') > 0))
        {
            return false;
        }

        var extension = Path.GetExtension(argument);
        if (extension.EndsWith("proj", StringComparison.OrdinalIgnoreCase) || Array.IndexOf(ProjectFileExtensions, extension.ToLowerInvariant()) >= 0)
        {
            return true;
        }

        return Directory.Exists(Path.Combine(workingDirectory, argument));
    }
}
