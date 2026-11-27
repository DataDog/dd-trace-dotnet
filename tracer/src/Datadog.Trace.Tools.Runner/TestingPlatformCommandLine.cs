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
/// <remarks>
/// <see cref="Datadog.Trace.Ci.Coverage.Backfill.CoverageBackfillCommandLine"/> parses the VSTest <c>dotnet test</c> command for coverage
/// backfill: it looks for options anywhere in the command (including after <c>--</c> and in <c>dotnet-coverage</c> child commands) and
/// takes positional arguments as the project. The Microsoft.Testing.Platform command has a different grammar (<c>--project</c>,
/// <c>--solution</c>, <c>--test-modules</c>, <c>--root-directory</c>, and everything after <c>--</c> belongs to the test
/// applications), so it is parsed here instead of extending the backfill parser and the option lists it relies on.
/// </remarks>
internal sealed class TestingPlatformCommandLine
{
    // Other options of the Microsoft.Testing.Platform `dotnet test` command that take a value, so the value is not taken for a
    // project path. The options this class reads are handled before these lists are checked.
    private static readonly string[] OptionsWithValue =
    [
        "--results-directory", "--results-directory-layout", "--config-file", "--diagnostic-output-directory", "--max-parallel-test-modules",
        "--minimum-expected-tests", "--maximum-failed-tests", "--timeout", "-e", "--environment", "-v", "-verbosity", "--verbosity", "--output"
    ];

    // Options that change the output folders in ways this class doesn't reproduce.
    private static readonly string[] UnsupportedOptions = ["-a", "--arch", "--os", "--use-current-runtime", "--ucr", "--device"];

    // Options of the command that don't take a value.
    private static readonly string[] FlagOptions =
    [
        "--no-restore", "--no-dependencies", "--no-ansi", "--no-progress", "--no-artifact-post-processing", "--no-launch-profile",
        "--no-launch-profile-arguments", "--list-tests", "--list-devices", "--no-logo", "-nologo", "--nologo", "--no-banner", "-?", "-h", "--help"
    ];

    // The forms of the MSBuild property option, followed by ':' and the property, or by the property in the next argument.
    private static readonly string[] MsBuildPropertyOptions = ["-p", "/p", "--p", "-property", "/property", "--property"];

    private static readonly string[] ProjectFileExtensions = [".sln", ".slnx", ".slnf"];

    private static readonly char[] QuoteCharacters = ['"', '\''];

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
    public List<string> MsBuildArguments { get; }

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

        // The SDK gives -c, -f, -r and --artifacts-path precedence over the same properties set with -p, whatever their order,
        // so they are added after the properties (the last value of a global property wins).
        var optionMsBuildArguments = new List<string>();
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
                // The SDK also accepts an explicit value, as in --no-build false.
                noBuild = true;
                if (index + 1 < arguments.Count && bool.TryParse(Unquote(arguments[index + 1]), out var nextValue))
                {
                    noBuild = nextValue;
                    index++;
                }
            }
            else if (argument.StartsWith("--no-build:", StringComparison.Ordinal) || argument.StartsWith("--no-build=", StringComparison.Ordinal))
            {
                noBuild = bool.TryParse(argument.Substring("--no-build:".Length), out var attachedValue) && attachedValue;
            }
            else if (TryGetValue(arguments, ref index, argument, "--test-modules", out var value))
            {
                testModules = value;
            }
            else if (TryGetValue(arguments, ref index, argument, "--root-directory", out value))
            {
                rootDirectory = value;
            }
            else if (TryGetValue(arguments, ref index, argument, "--project", out value))
            {
                project = value;
            }
            else if (TryGetValue(arguments, ref index, argument, "--solution", out value))
            {
                solution = value;
            }
            else if (TryGetValue(arguments, ref index, argument, "-c", "--configuration", out value))
            {
                optionMsBuildArguments.Add("-p:Configuration=" + value);
            }
            else if (TryGetValue(arguments, ref index, argument, "-f", "--framework", out value))
            {
                optionMsBuildArguments.Add("-p:TargetFramework=" + value);
            }
            else if (TryGetValue(arguments, ref index, argument, "-r", "--runtime", out value))
            {
                optionMsBuildArguments.Add("-p:RuntimeIdentifier=" + value);
                optionMsBuildArguments.Add("-p:_CommandLineDefinedRuntimeIdentifier=true");
            }
            else if (TryGetValue(arguments, ref index, argument, "--artifacts-path", out value))
            {
                optionMsBuildArguments.Add("-p:ArtifactsPath=" + Path.GetFullPath(Path.Combine(workingDirectory, value)));
            }
            else if (IsMsBuildProperty(argument, out var property))
            {
                msBuildArguments.Add("-p:" + property);
            }
            else if (TryGetValue(arguments, ref index, argument, MsBuildPropertyOptions, out value))
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

        msBuildArguments.AddRange(optionMsBuildArguments);
        return new TestingPlatformCommandLine(noBuild, testModules, rootDirectory, solution ?? project ?? positional, msBuildArguments, unsupportedOption);
    }

    private static string Unquote(string argument) => argument.Trim(QuoteCharacters);

    private static string GetOptionName(string argument)
    {
        var separator = argument.IndexOfAny([':', '=']);
        return separator < 0 ? argument : argument.Substring(0, separator);
    }

    private static bool IsOption(string argument, string[] names) => Array.IndexOf(names, GetOptionName(argument)) >= 0;

    // Overloads for one and two names, so the option names don't allocate an array for every argument.
    private static bool TryGetValue(IReadOnlyList<string> arguments, ref int index, string argument, string name, string alias, out string value)
        => TryGetValue(arguments, ref index, argument, name, out value) || TryGetValue(arguments, ref index, argument, alias, out value);

    private static bool TryGetValue(IReadOnlyList<string> arguments, ref int index, string argument, string[] names, out string value)
    {
        foreach (var name in names)
        {
            if (TryGetValue(arguments, ref index, argument, name, out value))
            {
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    private static bool TryGetValue(IReadOnlyList<string> arguments, ref int index, string argument, string name, out string value)
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

        value = string.Empty;
        return false;
    }

    private static bool IsMsBuildProperty(string argument, out string property)
    {
        foreach (var option in MsBuildPropertyOptions)
        {
            if (argument.Length > option.Length && argument[option.Length] == ':' && argument.StartsWith(option, StringComparison.OrdinalIgnoreCase))
            {
                property = argument.Substring(option.Length + 1);
                return true;
            }
        }

        property = string.Empty;
        return false;
    }

    private static bool IsOptionToken(string argument) => argument.Length > 1 && argument[0] == '-';

    // Short options with the value attached, like -cRelease or -rlinux-x64 (-property is a long form of -p).
    private static bool IsAttachedShortOption(string argument)
        => argument.Length > 2 && argument[0] == '-' && argument[1] != '-' && "cfarp".IndexOf(argument[1]) >= 0 && argument[2] != ':' && argument[2] != '=' &&
           !argument.StartsWith("-property", StringComparison.OrdinalIgnoreCase);

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
