// <copyright file="CoverageBackfillCommandLine.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using Datadog.Trace.Configuration;
using Datadog.Trace.Util;
using Datadog.Trace.Util.Json;

namespace Datadog.Trace.Ci.Coverage.Backfill;

/// <summary>
/// Parses the propagated test command into coverage-relevant options without treating arbitrary text as coverage activation.
/// </summary>
internal readonly struct CoverageBackfillCommandLine
{
    private const string CoverletOutputPropertyName = "coverletoutput";
    private const string CoverletOutputFormatPropertyName = "coverletoutputformat";
    private const string CollectCoveragePropertyName = "collectcoverage";
    private const string ConfigurationPropertyName = "Configuration";
    private const string DefaultConfigurationPropertyValue = "Debug";
    private const string DefaultPlatformPropertyValue = "AnyCPU";
    private const string CoverletMsBuildPackageName = "coverlet.msbuild";
    private const string DirectoryBuildPropsFileName = "Directory.Build.props";
    private const string DirectoryBuildTargetsFileName = "Directory.Build.targets";
    private const string DirectoryBuildPropsPathPropertyName = "DirectoryBuildPropsPath";
    private const string DirectoryBuildTargetsPathPropertyName = "DirectoryBuildTargetsPath";
    private const string ImportDirectoryBuildPropsPropertyName = "ImportDirectoryBuildProps";
    private const string ImportDirectoryBuildTargetsPropertyName = "ImportDirectoryBuildTargets";
    private const string PlatformPropertyName = "Platform";
    private const string RunSettingsTestCaseFilterName = "runconfiguration.testcasefilter";
    private const string RunSettingsTargetFrameworkVersionName = "runconfiguration.targetframeworkversion";
    private const string RunSettingsResultsDirectoryName = "runconfiguration.resultsdirectory";
    private const string RunSettingsFilePathPropertyName = "runsettingsfilepath";
    private const string RunSettingsRunConfigurationPrefix = "runconfiguration.";
    private const string TargetFrameworkPropertyName = "TargetFramework";
    private const string TargetFrameworksPropertyName = "TargetFrameworks";
    private const string TestingPlatformCommandLineArgumentsPropertyName = "testingplatformcommandlinearguments";
    private const string VstestSettingPropertyName = "vstestsetting";
    private const string VstestCollectPropertyName = "vstestcollect";
    private const string VstestCliRunSettingsPropertyName = "vstestclirunsettings";
    private const string VstestResultsDirectoryPropertyName = "vstestresultsdirectory";
    private const int MaxResponseFileExpansionDepth = 8;
    private const char QuotedResponseFileLiteralPrefix = '\x1F';
    private const string SlnxProjectPathAttributeName = "Path";
    private const string MsBuildProjectDirectoryPropertyName = "MSBuildProjectDirectory";
    private const string MsBuildProjectFullPathPropertyName = "MSBuildProjectFullPath";
    private const string MsBuildThisFileDirectoryPropertyName = "MSBuildThisFileDirectory";
    private const string MsBuildThisFileFullPathPropertyName = "MSBuildThisFileFullPath";
    private const string MsBuildThisFileNamePropertyName = "MSBuildThisFileName";
    private const string MsBuildThisFileExtensionPropertyName = "MSBuildThisFileExtension";

    private static readonly string[] CollectOptions = ["--collect", "/collect"];
    private static readonly string[] CoverletTestingPlatformCoverageOptions = ["--coverlet"];
    private static readonly string[] CoverletTestingPlatformCoverageOutputFormatOptions = ["--coverlet-output-format"];
    private static readonly string[] DotnetCoverageShortFormatValues = ["coverage", "xml", "cobertura"];
    private static readonly string[] DotnetCoverageOutputFormatOptions = ["--output-format", "-f"];
    private static readonly string[] DotnetCoverageCollectValueOptions =
    [
        "--output", "-o", "/output",
        "--output-format", "-f",
        "--settings", "-s", "/settings",
        "--include-files", "-if",
        "--log-file", "-l",
        "--log-level", "-ll",
        "--session-id", "-id",
        "--timeout", "-t"
    ];

    private static readonly string[] DotnetCoverageCollectFlagOptions =
    [
        "--server-mode", "-sv",
        "--background", "-b",
        "--disable-console-output", "-dco",
        "--nologo"
    ];

    private static readonly string[] DotnetExecValueOptions = ["--depsfile", "--runtimeconfig", "--additionalprobingpath", "--fx-version", "--roll-forward"];
    private static readonly string[] DotnetSdkGlobalFlagOptions = ["--diagnostics", "-d", "--info", "--version"];
    private static readonly string[] DotnetRunValueOptions =
    [
        "--project", "-p", "--file",
        "--configuration", "-c",
        "--framework", "-f",
        "--runtime", "-r",
        "--launch-profile", "-lp",
        "--verbosity", "-v", "-verbosity",
        "--arch", "-a",
        "--os",
        "--artifacts-path",
        "--environment", "-e"
    ];

    private static readonly string[] DotnetTestValueOptions =
    [
        "--settings", "-s", "/settings",
        "--logger", "-l", "/logger",
        "--results-directory", "--ResultsDirectory", "/ResultsDirectory",
        "--filter",
        "--diag", "-d",
        "--collect", "/collect",
        "--test-adapter-path",
        "--framework", "-f",
        "--configuration", "-c",
        "--runtime", "-r",
        "--arch", "-a",
        "--os",
        "--environment", "-e",
        "--artifacts-path",
        "--blame-hang-timeout",
        "--blame-hang-dump-type",
        "--blame-crash-dump-type"
    ];

    private static readonly string[] DotnetTestConfigurationOptions = ["--configuration", "-c"];
    private static readonly string[] DotnetTestFrameworkOptions = ["--framework", "-f"];

    private static readonly string[] DotnetRunFlagOptions =
    [
        "--no-launch-profile",
        "--no-build",
        "--interactive",
        "--force",
        "--no-restore",
        "--no-dependencies",
        "--no-cache",
        "--self-contained", "--sc",
        "--no-self-contained",
        "--disable-build-servers"
    ];

    private static readonly string[] MsBuildPropertyInlinePrefixes = ["/p:", "-p:", "/p=", "-p=", "/property:", "-property:", "--property:", "/property=", "-property=", "--property="];
    private static readonly string[] MsBuildPropertySeparateOptions = ["/p", "-p", "/property", "-property", "--property"];
    private static readonly string[] MsBuildTargetInlinePrefixes = ["/t:", "-t:", "--target:", "/target:", "-target:", "/t=", "-t=", "--target=", "/target=", "-target="];
    private static readonly string[] MsBuildTargetSeparateOptions = ["/t", "-t", "--target", "/target", "-target"];
    private static readonly string[] MicrosoftTestingPlatformCoverageOptions = ["--coverage"];
    private static readonly string[] MicrosoftTestingPlatformCoverageOutputOptions = ["--coverage-output"];
    private static readonly string[] MicrosoftTestingPlatformCoverageOutputFormatOptions = ["--coverage-output-format"];
    private static readonly string[] MicrosoftCodeCoverageEnableOptions = ["/enablecodecoverage"];
    private static readonly string[] OutputPathOptions = ["--output", "-o", "/output"];
    private static readonly string[] RunSettingsOptions = ["--settings", "-s", "/settings"];
    private static readonly string[] StandaloneTestRunnerExecutableNames =
    [
        "nunit-console",
        "nunit3-console",
        "xunit.console",
        "xunit.console.x86",
        "xunit.console.x64"
    ];

    private static readonly StringComparison PathComparison = FrameworkDescription.Instance.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    private static readonly StringComparer PathComparer = FrameworkDescription.Instance.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private readonly List<string> _arguments;
    private readonly string? _responseFileBaseDirectory;

    private CoverageBackfillCommandLine(string? commandLine, string? responseFileBaseDirectory)
    {
        _responseFileBaseDirectory = responseFileBaseDirectory;
        _arguments = ExpandResponseFileArguments(SplitCommandLine(commandLine, preserveQuotedResponseFileLiterals: false), responseFileBaseDirectory, depth: 0, visitedFiles: null);
    }

    private enum MsBuildConditionState
    {
        False,
        True,
        Unknown
    }

    private static List<string> SplitCommandLine(string? commandLine, bool preserveQuotedResponseFileLiterals)
    {
        var arguments = new List<string>();
        if (commandLine is null || StringUtil.IsNullOrWhiteSpace(commandLine))
        {
            return arguments;
        }

        var currentArgument = new StringBuilder(commandLine.Length);
        var inQuotes = false;
        var preserveQuotedSegment = false;
        var argumentStartedWithQuote = false;
        var quotedResponseFileLiteral = false;
        var backslashCount = 0;

        for (var i = 0; i < commandLine.Length; i++)
        {
            var currentChar = commandLine[i];
            if (currentChar == '\\')
            {
                backslashCount++;
                continue;
            }

            if (currentChar == '"')
            {
                currentArgument.Append('\\', backslashCount / 2);
                if ((backslashCount & 1) == 0)
                {
                    if (!inQuotes)
                    {
                        argumentStartedWithQuote = currentArgument.Length == 0;
                        preserveQuotedSegment = currentArgument.Length > 0 &&
                                                currentArgument[currentArgument.Length - 1] is '=' or ':';
                    }
                    else if (preserveQuotedResponseFileLiterals &&
                             argumentStartedWithQuote &&
                             currentArgument.Length > 0 &&
                             currentArgument[0] == '@')
                    {
                        quotedResponseFileLiteral = true;
                    }

                    if (preserveQuotedSegment)
                    {
                        currentArgument.Append(currentChar);
                    }

                    inQuotes = !inQuotes;
                    if (!inQuotes)
                    {
                        preserveQuotedSegment = false;
                    }
                }
                else
                {
                    currentArgument.Append(currentChar);
                }

                backslashCount = 0;
                continue;
            }

            if (backslashCount > 0)
            {
                currentArgument.Append('\\', backslashCount);
                backslashCount = 0;
            }

            if (!inQuotes && char.IsWhiteSpace(currentChar))
            {
                if (currentArgument.Length > 0)
                {
                    AddCommandLineArgument(arguments, currentArgument.ToString(), quotedResponseFileLiteral);
                    currentArgument.Clear();
                }

                argumentStartedWithQuote = false;
                quotedResponseFileLiteral = false;
                continue;
            }

            currentArgument.Append(currentChar);
        }

        if (backslashCount > 0)
        {
            currentArgument.Append('\\', backslashCount);
        }

        if (currentArgument.Length > 0)
        {
            AddCommandLineArgument(arguments, currentArgument.ToString(), quotedResponseFileLiteral);
        }

        return arguments;
    }

    private static void AddCommandLineArgument(List<string> arguments, string argument, bool quotedResponseFileLiteral)
    {
        arguments.Add(quotedResponseFileLiteral ? QuotedResponseFileLiteralPrefix + argument : argument);
    }

    private static List<string> ExpandResponseFileArguments(List<string> arguments, string? baseDirectory, int depth, HashSet<string>? visitedFiles)
    {
        if (arguments.Count == 0 || depth >= MaxResponseFileExpansionDepth)
        {
            return arguments;
        }

        List<string>? expandedArguments = null;
        for (var i = 0; i < arguments.Count; i++)
        {
            var argument = arguments[i];
            var responseFilePath = TryResolveResponseFilePath(argument, baseDirectory);
            if (responseFilePath is null)
            {
                expandedArguments?.Add(argument);
                continue;
            }

            visitedFiles ??= new HashSet<string>(PathComparer);
            if (!visitedFiles.Add(responseFilePath))
            {
                expandedArguments?.Add(argument);
                continue;
            }

            try
            {
                var responseFileArguments = ExpandResponseFileArguments(
                    SplitResponseFileCommandLine(File.ReadAllText(responseFilePath)),
                    baseDirectory,
                    depth + 1,
                    visitedFiles);

                expandedArguments ??= arguments.GetRange(0, i);
                expandedArguments.AddRange(responseFileArguments);
            }
            catch (Exception)
            {
                expandedArguments?.Add(argument);
            }
            finally
            {
                visitedFiles.Remove(responseFilePath);
            }
        }

        return expandedArguments ?? arguments;
    }

    private static List<string> SplitResponseFileCommandLine(string commandLine)
    {
        var arguments = new List<string>();
        using var reader = new StringReader(commandLine);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var argument = line.Trim();
            if (StringUtil.IsNullOrEmpty(argument) ||
                argument.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var strippedArgument = StripOuterResponseFileQuotes(argument);
            var quotedResponseFileLiteral = !ReferenceEquals(strippedArgument, argument) &&
                                            strippedArgument.Length > 0 &&
                                            strippedArgument[0] == '@';
            AddCommandLineArgument(arguments, strippedArgument, quotedResponseFileLiteral);
        }

        return arguments;
    }

    private static string? TryResolveResponseFilePath(string argument, string? baseDirectory)
    {
        if (!TryGetResponseFileReference(argument, out var responseFilePath))
        {
            return null;
        }

        if (StringUtil.IsNullOrWhiteSpace(responseFilePath))
        {
            return null;
        }

        try
        {
            if (!Path.IsPathRooted(responseFilePath) && !StringUtil.IsNullOrWhiteSpace(baseDirectory))
            {
                responseFilePath = Path.Combine(baseDirectory, responseFilePath);
            }

            responseFilePath = Path.GetFullPath(responseFilePath);
            return File.Exists(responseFilePath) ? responseFilePath : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool TryGetResponseFileReference(string argument, out string responseFilePath)
    {
        responseFilePath = string.Empty;
        var responseFileReference = argument.Trim();
        if (IsQuotedResponseFileLiteral(responseFileReference))
        {
            return false;
        }

        responseFileReference = StripOuterResponseFileQuotes(responseFileReference);
        if (responseFileReference.Length <= 1 || responseFileReference[0] != '@')
        {
            return false;
        }

        responseFilePath = responseFileReference.Substring(1);
        return true;
    }

    private static string StripOuterResponseFileQuotes(string value)
    {
        if (value.Length >= 2 &&
            value[0] == '"' &&
            value[value.Length - 1] == '"')
        {
            return value.Substring(1, value.Length - 2);
        }

        return value;
    }

    private static bool IsQuotedResponseFileLiteral(string value)
        => value.Length > 0 && value[0] == QuotedResponseFileLiteralPrefix;

    private static string StripQuotedResponseFileLiteralPrefix(string value)
        => IsQuotedResponseFileLiteral(value) ? value.Substring(1) : value;

    /// <summary>
    /// Parses a command line into a reusable coverage command context.
    /// </summary>
    /// <param name="commandLine">Command line to parse.</param>
    /// <param name="responseFileBaseDirectory">Optional base directory for resolving relative response file paths.</param>
    /// <returns>Parsed coverage command context.</returns>
    public static CoverageBackfillCommandLine Parse(string? commandLine, string? responseFileBaseDirectory = null)
    {
        return new CoverageBackfillCommandLine(commandLine, responseFileBaseDirectory);
    }

    /// <summary>
    /// Gets whether the command enables Coverlet collector or Coverlet MSBuild coverage.
    /// </summary>
    /// <param name="runSettingsBaseDirectory">Optional base directory for resolving relative runsettings paths.</param>
    public bool UsesCoverletCoverage(string? runSettingsBaseDirectory = null)
    {
        return UsesCoverletCollectorCoverage(runSettingsBaseDirectory) ||
               UsesCoverletMsBuildCoverage(runSettingsBaseDirectory);
    }

    /// <summary>
    /// Gets whether the command enables Coverlet MSBuild coverage.
    /// </summary>
    public bool UsesCoverletMsBuildCoverage(string? baseDirectory = null)
    {
        if (TryGetMsBuildPropertyValue(CollectCoveragePropertyName, out var collectCoverage))
        {
            return IsTrueValue(collectCoverage);
        }

        return UsesCoverletMsBuildCoverageFromProjectFiles(baseDirectory);
    }

    /// <summary>
    /// Gets whether the command enables the VSTest Coverlet collector.
    /// </summary>
    /// <param name="runSettingsBaseDirectory">Optional base directory for resolving relative runsettings paths.</param>
    public bool UsesCoverletCollectorCoverage(string? runSettingsBaseDirectory = null)
    {
        return IsVstestCoverageCommand() &&
               (HasCollectValue("xplat code coverage") ||
                HasCollectValue("coverlet.collector") ||
                HasRunSettingsDataCollector("xplat code coverage", runSettingsBaseDirectory));
    }

    /// <summary>
    /// Gets project file paths passed positionally to <c>dotnet test</c>, including quoted <c>dotnet-coverage collect</c> child commands.
    /// </summary>
    /// <returns>Raw project file paths from the command line.</returns>
    public IEnumerable<string> GetDotnetTestProjectFilePaths()
    {
        foreach (var projectFilePath in GetOwnDotnetTestFilePaths(IsProjectFilePath))
        {
            yield return projectFilePath;
        }

        foreach (var childCommand in GetDotnetCoverageCollectNestedChildCommands())
        {
            foreach (var projectFilePath in childCommand.GetDotnetTestProjectFilePaths())
            {
                yield return projectFilePath;
            }
        }
    }

    /// <summary>
    /// Gets solution file paths passed positionally to <c>dotnet test</c>, including quoted <c>dotnet-coverage collect</c> child commands.
    /// </summary>
    /// <returns>Raw solution file paths from the command line.</returns>
    public IEnumerable<string> GetDotnetTestSolutionFilePaths()
    {
        foreach (var solutionFilePath in GetOwnDotnetTestFilePaths(IsSolutionFilePath))
        {
            yield return solutionFilePath;
        }

        foreach (var childCommand in GetDotnetCoverageCollectNestedChildCommands())
        {
            foreach (var solutionFilePath in childCommand.GetDotnetTestSolutionFilePaths())
            {
                yield return solutionFilePath;
            }
        }
    }

    /// <summary>
    /// Gets existing directory paths passed positionally to <c>dotnet test</c>, including quoted <c>dotnet-coverage collect</c> child commands.
    /// </summary>
    /// <param name="baseDirectory">Optional base directory for resolving relative target directories.</param>
    /// <returns>Resolved directory paths from the command line.</returns>
    public IEnumerable<string> GetDotnetTestDirectoryPaths(string? baseDirectory)
    {
        foreach (var directoryPath in GetOwnDotnetTestDirectoryPaths(baseDirectory))
        {
            yield return directoryPath;
        }

        foreach (var childCommand in GetDotnetCoverageCollectNestedChildCommands())
        {
            foreach (var directoryPath in childCommand.GetDotnetTestDirectoryPaths(baseDirectory))
            {
                yield return directoryPath;
            }
        }
    }

    /// <summary>
    /// Gets whether <c>dotnet test</c> was invoked without an explicit project or solution path.
    /// </summary>
    /// <returns>True when the SDK will discover the project or solution from the working directory.</returns>
    public bool UsesImplicitDotnetTestTarget()
    {
        if (!IsDotnetTestCommand())
        {
            return false;
        }

        foreach (var ignored in GetOwnDotnetTestTargetArguments())
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Gets whether the command enables Coverlet through Microsoft Testing Platform.
    /// </summary>
    public bool UsesCoverletTestingPlatformCoverage()
    {
        return HasMicrosoftTestingPlatformOption(CoverletTestingPlatformCoverageOptions) ||
               HasTestingPlatformCommandLineArgumentOption(CoverletTestingPlatformCoverageOptions);
    }

    /// <summary>
    /// Gets whether the command enables Microsoft CodeCoverage collection.
    /// </summary>
    /// <param name="runSettingsBaseDirectory">Optional base directory for resolving relative runsettings paths.</param>
    public bool UsesMicrosoftCodeCoverage(string? runSettingsBaseDirectory = null)
    {
        return IsVstestCoverageCommand() &&
               (HasCollectValue("code coverage") ||
                HasOption(MicrosoftCodeCoverageEnableOptions) ||
                HasRunSettingsDataCollector("code coverage", runSettingsBaseDirectory));
    }

    /// <summary>
    /// Gets whether the command already enables Datadog's VSTest coverage collector.
    /// </summary>
    public bool UsesDatadogCoverageCollector()
    {
        return HasCollectValue("DatadogCoverage");
    }

    /// <summary>
    /// Gets whether Microsoft CodeCoverage is configured through command-line collect format or runsettings to emit XML.
    /// </summary>
    /// <param name="runSettingsBaseDirectory">Optional base directory for resolving relative runsettings paths.</param>
    /// <returns>True when a referenced Code Coverage collector declares XML output.</returns>
    public bool UsesMicrosoftCodeCoverageXmlRunSettings(string? runSettingsBaseDirectory = null)
    {
        if (!IsVstestCoverageCommand())
        {
            return false;
        }

        if (TryGetMicrosoftCodeCoverageCollectFormat(out var collectFormat))
        {
            return ContainsSeparatedValue(collectFormat, "xml");
        }

        return HasRunSettingsDataCollectorFormat("code coverage", "xml", runSettingsBaseDirectory);
    }

    /// <summary>
    /// Gets the effective explicit format configured on the Microsoft CodeCoverage collect option or VSTestCollect property.
    /// </summary>
    /// <param name="format">Configured format value when present on the effective CodeCoverage collect selection.</param>
    /// <returns>True when the effective CodeCoverage collect selection declares a format.</returns>
    public bool TryGetMicrosoftCodeCoverageCollectFormat(out string format)
    {
        format = string.Empty;
        var foundFormat = false;
        foreach (var collectValue in GetCollectValues())
        {
            if (!ContainsSeparatedValue(collectValue, "code coverage"))
            {
                continue;
            }

            foundFormat = false;
            format = string.Empty;
            foreach (var part in SplitSeparatedValues(collectValue))
            {
                if (!TrySplitProperty(part, out var key, out var value))
                {
                    key = StripSurroundingQuotes(part.Trim());
                    value = string.Empty;
                }

                if (!key.Equals("format", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                format = value;
                foundFormat = true;
            }
        }

        return foundFormat;
    }

    /// <summary>
    /// Gets whether the command enables Microsoft CodeCoverage through Microsoft Testing Platform.
    /// </summary>
    public bool UsesMicrosoftTestingPlatformCoverage()
    {
        return HasMicrosoftTestingPlatformOption(MicrosoftTestingPlatformCoverageOptions) ||
               HasTestingPlatformCommandLineArgumentOption(MicrosoftTestingPlatformCoverageOptions);
    }

    /// <summary>
    /// Gets whether any of the specified command-line options are present.
    /// </summary>
    /// <param name="optionNames">Option names to search for.</param>
    /// <returns>True when at least one option is present.</returns>
    public bool HasOption(string[] optionNames)
    {
        foreach (var argument in _arguments)
        {
            if (HasOption(argument, optionNames))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets whether the command contains the dotnet short target-framework option, including inline <c>-fnet8.0</c>.
    /// </summary>
    /// <returns>True when a short framework selector is present.</returns>
    public bool ContainsShortFrameworkOption()
    {
        var isDotnetCoverageCollect = TryGetDotnetCoverageCollectOptionRange(out var dotnetCoverageOptionStartIndex, out var dotnetCoverageOptionEndIndex);
        for (var i = 0; i < _arguments.Count; i++)
        {
            var argument = StripSurroundingQuotes(_arguments[i]);
            if (!IsShortFrameworkOptionArgument(argument))
            {
                continue;
            }

            if (isDotnetCoverageCollect &&
                i >= dotnetCoverageOptionStartIndex &&
                i < dotnetCoverageOptionEndIndex &&
                TryGetShortOptionValue(i, out var shortOptionValue, out var consumesNextArgument) &&
                ContainsAnySeparatedValue(shortOptionValue, DotnetCoverageShortFormatValues))
            {
                if (consumesNextArgument)
                {
                    i++;
                }

                continue;
            }

            return true;
        }

        return false;
    }

    private bool IsShortFrameworkOptionArgument(string argument)
    {
        argument = StripSurroundingQuotes(argument);
        if (argument.Length < 2 ||
            argument[0] != '-' ||
            (argument[1] != 'f' && argument[1] != 'F'))
        {
            return false;
        }

        // Avoid matching long options such as "--filter"; a real short option starts at the first dash.
        if (argument.Length > 2 && argument[2] == '-')
        {
            return false;
        }

        if (argument.Length == 2 ||
            argument[2] is ':' or '=')
        {
            return true;
        }

        return StripSurroundingQuotes(argument.Substring(2)).StartsWith("net", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets whether the command or a quoted <c>dotnet-coverage collect</c> child command contains one of the supplied options.
    /// </summary>
    /// <param name="optionNames">Option names to search for.</param>
    /// <returns>True when an option is present in the parsed command or nested child command.</returns>
    public bool HasOptionIncludingDotnetCoverageChildCommand(string[] optionNames)
    {
        if (HasOption(optionNames))
        {
            return true;
        }

        foreach (var childCommand in GetDotnetCoverageCollectNestedChildCommands())
        {
            if (childCommand.HasOption(optionNames))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets whether the command or a quoted <c>dotnet-coverage collect</c> child command contains a target-framework selector.
    /// </summary>
    /// <returns>True when a target-framework selector is present in the parsed command or nested child command.</returns>
    public bool ContainsShortFrameworkOptionIncludingDotnetCoverageChildCommand()
    {
        if (ContainsShortFrameworkOption())
        {
            return true;
        }

        foreach (var childCommand in GetDotnetCoverageCollectNestedChildCommands())
        {
            if (childCommand.ContainsShortFrameworkOption())
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets whether the command or a quoted <c>dotnet-coverage collect</c> child command references a runsettings testcase filter.
    /// </summary>
    /// <returns>True when a runsettings testcase filter is configured.</returns>
    public bool HasRunSettingsTestCaseFilterIncludingDotnetCoverageChildCommand()
    {
        if (HasRunSettingsRunConfigurationValue(RunSettingsTestCaseFilterName))
        {
            return true;
        }

        foreach (var childCommand in GetDotnetCoverageCollectNestedChildCommands())
        {
            if (childCommand.HasRunSettingsRunConfigurationValue(RunSettingsTestCaseFilterName))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets whether the command or a quoted <c>dotnet-coverage collect</c> child command references a runsettings target-framework selector.
    /// </summary>
    /// <returns>True when a runsettings target framework is configured.</returns>
    public bool HasRunSettingsTargetFrameworkIncludingDotnetCoverageChildCommand()
    {
        if (HasRunSettingsRunConfigurationValue(RunSettingsTargetFrameworkVersionName))
        {
            return true;
        }

        foreach (var childCommand in GetDotnetCoverageCollectNestedChildCommands())
        {
            if (childCommand.HasRunSettingsRunConfigurationValue(RunSettingsTargetFrameworkVersionName))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets whether the command or a quoted <c>dotnet-coverage collect</c> child command contains a non-empty MSBuild property.
    /// </summary>
    /// <param name="propertyNames">MSBuild property names to detect.</param>
    /// <returns>True when any supplied property has a non-empty effective value.</returns>
    public bool HasAnyNonEmptyMsBuildPropertyIncludingDotnetCoverageChildCommand(string[] propertyNames)
    {
        if (HasAnyNonEmptyMsBuildProperty(propertyNames))
        {
            return true;
        }

        foreach (var childCommand in GetDotnetCoverageCollectNestedChildCommands())
        {
            if (childCommand.HasAnyNonEmptyMsBuildProperty(propertyNames))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets whether the command or a quoted <c>dotnet-coverage collect</c> child command has a non-empty effective MSBuild property.
    /// </summary>
    /// <param name="propertyNames">MSBuild property names to detect.</param>
    /// <param name="requireActiveCoverletMsBuildProject">True when project-file properties should only be read from an active coverlet.msbuild project.</param>
    /// <returns>True when any supplied property has a non-empty effective value.</returns>
    public bool HasAnyNonEmptyEffectiveMsBuildPropertyIncludingDotnetCoverageChildCommand(string[] propertyNames, bool requireActiveCoverletMsBuildProject = true)
    {
        if (HasAnyNonEmptyEffectiveMsBuildProperty(propertyNames, requireActiveCoverletMsBuildProject))
        {
            return true;
        }

        foreach (var childCommand in GetDotnetCoverageCollectNestedChildCommands())
        {
            if (childCommand.HasAnyNonEmptyEffectiveMsBuildProperty(propertyNames, requireActiveCoverletMsBuildProject))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets whether the command or a quoted <c>dotnet-coverage collect</c> child command passes a matching option through
    /// the Microsoft Testing Platform <c>TestingPlatformCommandLineArguments</c> MSBuild property.
    /// </summary>
    /// <param name="optionNames">MTP application argument option names to detect.</param>
    /// <returns>True when the property value contains one of the supplied options.</returns>
    public bool HasTestingPlatformCommandLineArgumentOptionIncludingDotnetCoverageChildCommand(string[] optionNames)
    {
        if (HasTestingPlatformCommandLineArgumentOption(optionNames))
        {
            return true;
        }

        foreach (var childCommand in GetDotnetCoverageCollectNestedChildCommands())
        {
            if (childCommand.HasTestingPlatformCommandLineArgumentOption(optionNames))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets whether the command or a quoted <c>dotnet-coverage collect</c> child command passes an unexpanded response file
    /// through the Microsoft Testing Platform <c>TestingPlatformCommandLineArguments</c> MSBuild property.
    /// </summary>
    /// <returns>True when the property contains a response file that could not be expanded locally.</returns>
    public bool HasUnexpandedTestingPlatformCommandLineArgumentResponseFileReferenceIncludingDotnetCoverageChildCommand()
    {
        if (HasUnexpandedTestingPlatformCommandLineArgumentResponseFileReference())
        {
            return true;
        }

        foreach (var childCommand in GetDotnetCoverageCollectNestedChildCommands())
        {
            if (childCommand.HasUnexpandedTestingPlatformCommandLineArgumentResponseFileReference())
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets whether the command or a quoted <c>dotnet-coverage collect</c> child command contains an unsupported external coverage threshold.
    /// </summary>
    /// <param name="thresholdOptionNames">Command-line threshold option names to detect.</param>
    /// <param name="thresholdPropertyNames">MSBuild threshold property names to detect.</param>
    /// <param name="thresholdTypePropertyName">MSBuild property name that selects threshold dimensions.</param>
    /// <param name="allowedThresholdTypes">Threshold dimensions that can be reconciled by line coverage backfill.</param>
    /// <param name="externalCoveragePath">Configured external coverage report path.</param>
    /// <returns>True when the command or a nested child command contains a threshold that cannot be safely reconciled.</returns>
    public bool HasUnsupportedExternalThresholdIncludingDotnetCoverageChildCommand(
        string[] thresholdOptionNames,
        string[] thresholdPropertyNames,
        string thresholdTypePropertyName,
        string[] allowedThresholdTypes,
        string externalCoveragePath)
    {
        if (HasUnsupportedExternalThreshold(thresholdOptionNames, thresholdPropertyNames, thresholdTypePropertyName, allowedThresholdTypes, externalCoveragePath))
        {
            return true;
        }

        foreach (var childCommand in GetDotnetCoverageCollectNestedChildCommands())
        {
            if (childCommand.HasUnsupportedExternalThreshold(thresholdOptionNames, thresholdPropertyNames, thresholdTypePropertyName, allowedThresholdTypes, externalCoveragePath))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets whether the command or a quoted <c>dotnet-coverage collect</c> child command contains an unsupported Coverlet MSBuild threshold.
    /// </summary>
    /// <param name="thresholdPropertyNames">MSBuild threshold property names to detect.</param>
    /// <param name="thresholdTypePropertyName">MSBuild property name that selects threshold dimensions.</param>
    /// <param name="allowedThresholdTypes">Threshold dimensions that can be reconciled by line coverage backfill.</param>
    /// <returns>True when Coverlet coverage is active with a non-line threshold dimension.</returns>
    public bool HasUnsupportedCoverletThresholdIncludingDotnetCoverageChildCommand(
        string[] thresholdPropertyNames,
        string thresholdTypePropertyName,
        string[] allowedThresholdTypes)
    {
        if (HasUnsupportedCoverletThreshold(thresholdPropertyNames, thresholdTypePropertyName, allowedThresholdTypes))
        {
            return true;
        }

        foreach (var childCommand in GetDotnetCoverageCollectNestedChildCommands())
        {
            if (childCommand.HasUnsupportedCoverletThreshold(thresholdPropertyNames, thresholdTypePropertyName, allowedThresholdTypes))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets whether a dotnet-coverage <c>collect -f</c> option contains any expected output format value.
    /// </summary>
    /// <param name="expectedValues">Expected output format values.</param>
    /// <returns>True when the command is dotnet-coverage collect and the short format option has an expected value.</returns>
    public bool DotnetCoverageShortFormatOptionValueContainsAny(string[] expectedValues)
    {
        return TryGetDotnetCoverageCollectOptionValue(["-f"], out var value) &&
               ContainsAnySeparatedValue(value, expectedValues);
    }

    /// <summary>
    /// Gets whether the effective dotnet-coverage collect output-format option contains any expected value.
    /// </summary>
    /// <param name="expectedValues">Expected output format values.</param>
    /// <returns>True when the command is dotnet-coverage collect and the effective format option has an expected value.</returns>
    public bool DotnetCoverageOutputFormatOptionValueContainsAny(string[] expectedValues)
    {
        return TryGetDotnetCoverageCollectOptionValue(DotnetCoverageOutputFormatOptions, out var value) &&
               ContainsAnySeparatedValue(value, expectedValues);
    }

    /// <summary>
    /// Gets whether the command invokes <c>dotnet-coverage collect</c>.
    /// </summary>
    /// <returns>True when dotnet-coverage collection is active.</returns>
    public bool UsesDotnetCoverage()
    {
        return IsDotnetCoverageCollectCommand();
    }

    /// <summary>
    /// Gets whether an unexpanded response file reference remains in the parsed command.
    /// </summary>
    /// <returns>True when a response file was missing, unreadable, recursive, or too deeply nested.</returns>
    public bool HasUnexpandedResponseFileReference()
    {
        foreach (var argument in _arguments)
        {
            if (TryGetResponseFileReference(argument, out _))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets whether the command or a quoted <c>dotnet-coverage collect</c> child command contains an unexpanded response file reference.
    /// </summary>
    /// <returns>True when a response file was missing, unreadable, recursive, or too deeply nested.</returns>
    public bool HasUnexpandedResponseFileReferenceIncludingDotnetCoverageChildCommand()
    {
        if (HasUnexpandedResponseFileReference())
        {
            return true;
        }

        foreach (var childCommand in GetDotnetCoverageCollectNestedChildCommands())
        {
            if (childCommand.HasUnexpandedResponseFileReference())
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets quoted coverage-relevant child commands passed to <c>dotnet-coverage collect</c>.
    /// </summary>
    /// <returns>Parsed child commands that can carry coverage options for the test process.</returns>
    public IEnumerable<CoverageBackfillCommandLine> GetDotnetCoverageCollectChildCommands()
        => GetDotnetCoverageCollectNestedChildCommands();

    /// <summary>
    /// Gets whether an argument value, including values in quoted <c>dotnet-coverage collect</c> child commands, exactly references a path.
    /// </summary>
    /// <param name="path">Path to find.</param>
    /// <returns>True when a full argument or inline option value equals the supplied path.</returns>
    public bool ReferencesPath(string path)
    {
        foreach (var argument in _arguments)
        {
            var normalizedArgument = StripSurroundingQuotes(argument);
            if (PathsEqual(normalizedArgument, path))
            {
                return true;
            }

            var valueSeparatorIndex = normalizedArgument.IndexOf('=');
            if (valueSeparatorIndex < 0 && IsOptionLikeArgument(normalizedArgument))
            {
                valueSeparatorIndex = normalizedArgument.IndexOf(':');
            }

            if (valueSeparatorIndex >= 0 &&
                valueSeparatorIndex + 1 < normalizedArgument.Length &&
                PathsEqual(normalizedArgument.Substring(valueSeparatorIndex + 1), path))
            {
                return true;
            }
        }

        foreach (var childCommand in GetDotnetCoverageCollectNestedChildCommands())
        {
            if (childCommand.ReferencesPath(path))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets whether a recognized coverage-report output option or property resolves to the configured external report path.
    /// </summary>
    /// <param name="path">Configured external coverage report path.</param>
    /// <returns>True when a known output option/property writes the supplied path.</returns>
    public bool WritesCoverageReportPath(string path)
    {
        return WritesDotnetCoverageReportPath(path) ||
               WritesCoverletMsBuildCoverageReportPath(path) ||
               WritesMicrosoftTestingPlatformCoverageReportPath(path) ||
               WritesCoverletTestingPlatformCoverageReportPath(path);
    }

    /// <summary>
    /// Gets whether a recognized coverage-report output option or property writes a report somewhere other than the configured external path.
    /// </summary>
    /// <param name="path">Configured external coverage report path.</param>
    /// <returns>True when the command explicitly writes another coverage report path.</returns>
    public bool WritesCoverageReportPathOtherThan(string path)
    {
        return WritesDotnetCoverageReportPathOtherThan(path) ||
               WritesCoverletMsBuildCoverageReportPathOtherThan(path) ||
               WritesMicrosoftTestingPlatformCoverageReportPathOtherThan(path);
    }

    /// <summary>
    /// Gets whether a recognized coverage writer is active but cannot prove its exact output file path from the command line.
    /// </summary>
    /// <returns>True when an active writer can produce an additional report with an unverifiable path.</returns>
    public bool HasCoverageReportPathWriterWithUnverifiableOutputPath()
    {
        return UsesCoverletTestingPlatformCoverage();
    }

    /// <summary>
    /// Gets whether dotnet-coverage collect writes the configured external report path.
    /// </summary>
    /// <param name="path">Configured external coverage report path.</param>
    /// <returns>True when dotnet-coverage writes the supplied path.</returns>
    public bool WritesDotnetCoverageReportPath(string path)
    {
        if (StringUtil.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return IsDotnetCoverageCollectCommand() &&
               TryGetDotnetCoverageCollectOptionValue(OutputPathOptions, out var outputPath) &&
               PathsEqual(outputPath, path);
    }

    private bool WritesDotnetCoverageReportPathOtherThan(string path)
    {
        return !StringUtil.IsNullOrWhiteSpace(path) &&
               IsDotnetCoverageCollectCommand() &&
               TryGetDotnetCoverageCollectOptionValue(OutputPathOptions, out var outputPath) &&
               !PathsEqual(outputPath, path);
    }

    /// <summary>
    /// Gets whether Coverlet MSBuild writes the configured external report path.
    /// </summary>
    /// <param name="path">Configured external coverage report path.</param>
    /// <returns>True when Coverlet MSBuild writes the supplied path.</returns>
    public bool WritesCoverletMsBuildCoverageReportPath(string path)
    {
        if (StringUtil.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (!UsesCoverletMsBuildCoverage())
        {
            return false;
        }

        foreach (var outputPath in GetCoverletOutputPathsOrDefault())
        {
            if (CoverletOutputReferencesReportPath(outputPath, path))
            {
                return true;
            }
        }

        return false;
    }

    private bool WritesCoverletMsBuildCoverageReportPathOtherThan(string path)
    {
        if (StringUtil.IsNullOrWhiteSpace(path) ||
            !UsesCoverletMsBuildCoverage())
        {
            return false;
        }

        foreach (var outputPath in GetCoverletOutputPathsOrDefault())
        {
            if (!CoverletOutputReferencesReportPath(outputPath, path))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets whether Coverlet MSBuild writes a line-capable XML report to the configured external report path.
    /// </summary>
    /// <param name="path">Configured external coverage report path.</param>
    /// <returns>True when Coverlet MSBuild writes a supported mutable XML report to the supplied path.</returns>
    public bool WritesLineCapableCoverletMsBuildCoverageReportPath(string path)
    {
        if (StringUtil.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (!UsesCoverletMsBuildCoverage())
        {
            return false;
        }

        foreach (var outputPath in GetCoverletOutputPathsOrDefault())
        {
            if (CoverletOutputExactFileWritesLineCapableReportPath(outputPath, path) ||
                CoverletOutputDirectoryWritesLineCapableReportPath(outputPath, path) ||
                CoverletOutputFileWritesLineCapableReportPath(outputPath, path))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets whether Microsoft Testing Platform CodeCoverage writes the configured external report path.
    /// </summary>
    /// <param name="path">Configured external coverage report path.</param>
    /// <returns>True when Microsoft Testing Platform writes the supplied path.</returns>
    public bool WritesMicrosoftTestingPlatformCoverageReportPath(string path)
    {
        return !StringUtil.IsNullOrWhiteSpace(path) &&
               UsesMicrosoftTestingPlatformCoverage() &&
               TryGetEffectiveMicrosoftTestingPlatformOptionValue(MicrosoftTestingPlatformCoverageOutputOptions, out var outputPath) &&
               PathsEqual(outputPath, path);
    }

    private bool WritesMicrosoftTestingPlatformCoverageReportPathOtherThan(string path)
    {
        return !StringUtil.IsNullOrWhiteSpace(path) &&
               UsesMicrosoftTestingPlatformCoverage() &&
               TryGetEffectiveMicrosoftTestingPlatformOptionValue(MicrosoftTestingPlatformCoverageOutputOptions, out var outputPath) &&
               !PathsEqual(outputPath, path);
    }

    /// <summary>
    /// Gets whether Coverlet Microsoft Testing Platform writes the configured external report path.
    /// </summary>
    /// <param name="path">Configured external coverage report path.</param>
    /// <returns>True when Coverlet Microsoft Testing Platform writes the supplied path.</returns>
    public bool WritesCoverletTestingPlatformCoverageReportPath(string path)
    {
        if (StringUtil.IsNullOrWhiteSpace(path) ||
            !UsesCoverletTestingPlatformCoverage())
        {
            return false;
        }

        // coverlet.MTP does not accept an exact output path option. It writes timestamped
        // files into the platform result directory, so a configured external path cannot be
        // proven safe from command-line options alone.
        return false;
    }

    /// <summary>
    /// Gets whether the effective Microsoft Testing Platform CodeCoverage output format contains any expected value.
    /// </summary>
    /// <param name="expectedValues">Expected output format values.</param>
    /// <returns>True when Microsoft Testing Platform CodeCoverage is active and the effective format option has an expected value.</returns>
    public bool MicrosoftTestingPlatformCoverageOutputFormatContainsAny(string[] expectedValues)
    {
        return UsesMicrosoftTestingPlatformCoverage() &&
               TryGetEffectiveMicrosoftTestingPlatformOptionValue(MicrosoftTestingPlatformCoverageOutputFormatOptions, out var value) &&
               ContainsAnySeparatedValue(value, expectedValues);
    }

    /// <summary>
    /// Gets whether the effective Coverlet Microsoft Testing Platform output format contains any expected value.
    /// </summary>
    /// <param name="expectedValues">Expected output format values.</param>
    /// <returns>True when Coverlet Microsoft Testing Platform is active and the effective format option has an expected value.</returns>
    public bool CoverletTestingPlatformCoverageOutputFormatContainsAny(string[] expectedValues)
    {
        return UsesCoverletTestingPlatformCoverage() &&
               MicrosoftTestingPlatformOptionValueContainsAny(CoverletTestingPlatformCoverageOutputFormatOptions, expectedValues);
    }

    /// <summary>
    /// Gets an option value from either a separate value token or an inline <c>:</c>/<c>=</c> value.
    /// </summary>
    /// <param name="optionNames">Supported option names.</param>
    /// <param name="value">Option value when found.</param>
    /// <returns>True when the option was present and had a value.</returns>
    public bool TryGetOptionValue(string[] optionNames, out string value)
    {
        value = string.Empty;
        var foundValue = false;
        for (var i = 0; i < _arguments.Count; i++)
        {
            var argument = StripSurroundingQuotes(_arguments[i]);
            if (TryGetInlineOptionValueIncludingEmpty(argument, optionNames, out var inlineValue))
            {
                value = inlineValue;
                foundValue = !StringUtil.IsNullOrEmpty(value);
                continue;
            }

            if (IsOptionName(argument, optionNames))
            {
                if (i + 1 < _arguments.Count)
                {
                    var nextArgument = StripSurroundingQuotes(_arguments[i + 1]);
                    if (!StringUtil.IsNullOrEmpty(nextArgument) &&
                        !nextArgument.Equals("--", StringComparison.Ordinal) &&
                        !nextArgument.StartsWith("-", StringComparison.Ordinal))
                    {
                        value = nextArgument;
                        foundValue = true;
                        i++;
                        continue;
                    }
                }

                value = string.Empty;
                foundValue = false;
            }
        }

        return foundValue;
    }

    /// <summary>
    /// Gets the effective VSTest results directory from the MSBuild <c>VSTestResultsDirectory</c> property.
    /// </summary>
    /// <param name="resultsDirectory">Configured results directory when found.</param>
    /// <returns>True when the command line contains a non-empty <c>VSTestResultsDirectory</c> property.</returns>
    public bool TryGetVstestResultsDirectory(out string resultsDirectory)
    {
        return TryGetMsBuildPropertyValue(VstestResultsDirectoryPropertyName, out resultsDirectory);
    }

    /// <summary>
    /// Gets whether an option value contains any of the expected comma- or semicolon-separated values.
    /// </summary>
    /// <param name="optionNames">Supported option names.</param>
    /// <param name="expectedValues">Expected option values.</param>
    /// <returns>True when an option value contains any expected value.</returns>
    public bool OptionValueContainsAny(string[] optionNames, string[] expectedValues)
    {
        for (var i = 0; i < _arguments.Count; i++)
        {
            var argument = StripSurroundingQuotes(_arguments[i]);
            if (!TryGetInlineOptionValue(argument, optionNames, out var value))
            {
                if (!IsOptionName(argument, optionNames) ||
                    i + 1 >= _arguments.Count)
                {
                    continue;
                }

                value = StripSurroundingQuotes(_arguments[i + 1]);
            }

            if (ContainsAnySeparatedValue(value, expectedValues))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets whether an MSBuild property exists with a specific value.
    /// </summary>
    /// <param name="propertyName">MSBuild property name.</param>
    /// <param name="expectedValue">Expected property value.</param>
    /// <returns>True when a matching property assignment exists.</returns>
    public bool HasMsBuildPropertyValue(string propertyName, string expectedValue)
    {
        return TryGetMsBuildPropertyValue(propertyName, out var value) &&
               value.Equals(expectedValue, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets whether any MSBuild property with one of the supplied names is present.
    /// </summary>
    /// <param name="propertyNames">MSBuild property names to detect.</param>
    /// <returns>True when any property is present.</returns>
    public bool HasAnyMsBuildProperty(string[] propertyNames)
    {
        foreach (var propertyPayload in GetMsBuildPropertyPayloads())
        {
            foreach (var property in SplitMsBuildProperties(propertyPayload))
            {
                var key = TrySplitProperty(property, out var propertyName, out _) ? propertyName : property;
                foreach (var expectedPropertyName in propertyNames)
                {
                    if (key.Equals(expectedPropertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Gets whether any MSBuild property with one of the supplied names has a non-empty effective value.
    /// </summary>
    /// <param name="propertyNames">MSBuild property names to detect.</param>
    /// <returns>True when any property's last value is non-empty.</returns>
    public bool HasAnyNonEmptyMsBuildProperty(string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (TryGetMsBuildPropertyValue(propertyName, out _))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasAnyNonEmptyEffectiveMsBuildProperty(string[] propertyNames, bool requireActiveCoverletMsBuildProject)
    {
        foreach (var propertyName in propertyNames)
        {
            foreach (var propertyValue in GetEffectiveMsBuildPropertyValues(propertyName, requireActiveCoverletMsBuildProject))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets whether an MSBuild property exists with one of the supplied comma- or semicolon-separated values.
    /// </summary>
    /// <param name="propertyName">MSBuild property name.</param>
    /// <param name="expectedValues">Expected property values.</param>
    /// <returns>True when a matching property assignment contains any expected value.</returns>
    public bool MsBuildPropertyValueContainsAny(string propertyName, string[] expectedValues)
    {
        return TryGetMsBuildPropertyValue(propertyName, out var value) &&
               ContainsAnySeparatedValue(value, expectedValues);
    }

    /// <summary>
    /// Gets whether an effective MSBuild property value contains only supplied comma- or semicolon-separated values.
    /// </summary>
    /// <param name="propertyName">MSBuild property name.</param>
    /// <param name="allowedValues">Allowed property values.</param>
    /// <returns>True when the property exists and every non-empty separated value is allowed.</returns>
    public bool MsBuildPropertyValueContainsOnly(string propertyName, string[] allowedValues)
    {
        return TryGetMsBuildPropertyValue(propertyName, out var value) &&
               ContainsOnlySeparatedValues(value, allowedValues);
    }

    private bool HasAnyNonEmptyEffectiveMsBuildProperty(string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (TryGetEffectiveMsBuildPropertyValue(propertyName, out _))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetEffectiveMsBuildPropertyValue(string propertyName, out string value)
    {
        foreach (var candidateValue in GetEffectiveMsBuildPropertyValues(propertyName))
        {
            value = candidateValue;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private bool EffectiveMsBuildPropertyValueContainsOnly(string propertyName, string[] allowedValues)
    {
        var found = false;
        foreach (var value in GetEffectiveMsBuildPropertyValues(propertyName))
        {
            found = true;
            if (!ContainsOnlySeparatedValues(value, allowedValues))
            {
                return false;
            }
        }

        return found;
    }

    private IEnumerable<string> GetEffectiveMsBuildPropertyValues(string propertyName, bool requireActiveCoverletMsBuildProject = true)
    {
        if (TryGetMsBuildPropertyValue(propertyName, requireNonEmpty: false, out var commandLineValue))
        {
            if (!StringUtil.IsNullOrEmpty(commandLineValue))
            {
                yield return commandLineValue;
            }

            yield break;
        }

        foreach (var projectFilePath in GetResolvedMsBuildProjectFilePaths(_responseFileBaseDirectory))
        {
            if (requireActiveCoverletMsBuildProject)
            {
                foreach (var configuration in GetActiveCoverletMsBuildProjectConfigurations(projectFilePath))
                {
                    if (configuration.Properties.TryGetValue(propertyName, out var value) &&
                        !StringUtil.IsNullOrEmpty(value))
                    {
                        yield return value;
                    }
                }

                continue;
            }

            if (TryReadCoverletMsBuildProjectProperties(projectFilePath, out var properties, out _) &&
                properties.TryGetValue(propertyName, out var projectValue) &&
                !StringUtil.IsNullOrEmpty(projectValue))
            {
                yield return projectValue;
            }
        }
    }

    /// <summary>
    /// Gets whether a dotnet-coverage collect option value contains any expected value.
    /// </summary>
    /// <param name="optionNames">Supported dotnet-coverage option names.</param>
    /// <param name="expectedValues">Expected option values.</param>
    /// <returns>True when the command is dotnet-coverage collect and the option value contains any expected value.</returns>
    public bool DotnetCoverageOptionValueContainsAny(string[] optionNames, string[] expectedValues)
    {
        return TryGetDotnetCoverageCollectOptionValue(optionNames, out var value) &&
               ContainsAnySeparatedValue(value, expectedValues);
    }

    private bool HasUnsupportedExternalThreshold(
        string[] thresholdOptionNames,
        string[] thresholdPropertyNames,
        string thresholdTypePropertyName,
        string[] allowedThresholdTypes,
        string externalCoveragePath)
    {
        if (HasOption(thresholdOptionNames))
        {
            return true;
        }

        if (!HasAnyNonEmptyEffectiveMsBuildProperty(thresholdPropertyNames))
        {
            return false;
        }

        if (!UsesCoverletMsBuildCoverage())
        {
            return false;
        }

        return !WritesLineCapableCoverletMsBuildCoverageReportPath(externalCoveragePath) ||
               !EffectiveMsBuildPropertyValueContainsOnly(thresholdTypePropertyName, allowedThresholdTypes);
    }

    private bool HasUnsupportedCoverletThreshold(
        string[] thresholdPropertyNames,
        string thresholdTypePropertyName,
        string[] allowedThresholdTypes)
    {
        return UsesCoverletMsBuildCoverage() &&
               HasAnyNonEmptyEffectiveMsBuildProperty(thresholdPropertyNames) &&
               !EffectiveMsBuildPropertyValueContainsOnly(thresholdTypePropertyName, allowedThresholdTypes);
    }

    private bool HasTestingPlatformCommandLineArgumentOption(string[] optionNames)
    {
        foreach (var testingPlatformArguments in GetTestingPlatformCommandLineArguments())
        {
            if (testingPlatformArguments.HasOption(optionNames))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasUnexpandedTestingPlatformCommandLineArgumentResponseFileReference()
    {
        foreach (var testingPlatformArguments in GetTestingPlatformCommandLineArguments())
        {
            if (testingPlatformArguments.HasUnexpandedResponseFileReference())
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetMsBuildPropertyValue(string propertyName, out string value)
        => TryGetMsBuildPropertyValue(propertyName, requireNonEmpty: true, out value);

    private bool TryGetMsBuildPropertyValue(string propertyName, bool requireNonEmpty, out string value)
    {
        value = string.Empty;
        var found = false;
        foreach (var propertyPayload in GetMsBuildPropertyPayloads())
        {
            foreach (var property in SplitMsBuildProperties(propertyPayload))
            {
                if (!TrySplitProperty(property, out var key, out var propertyValue))
                {
                    key = StripSurroundingQuotes(property.Trim());
                    propertyValue = string.Empty;
                }

                if (!IsRunConfigurationAssignment(key) &&
                    !StringUtil.IsNullOrEmpty(key) &&
                    key.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = propertyValue;
                    found = true;
                }
            }
        }

        return found && (!requireNonEmpty || !StringUtil.IsNullOrEmpty(value));
    }

    private bool UsesCoverletMsBuildCoverageFromProjectFiles(string? baseDirectory)
    {
        if (!IsMsBuildPropertyCommand())
        {
            return false;
        }

        foreach (var projectFilePath in GetResolvedMsBuildProjectFilePaths(baseDirectory ?? _responseFileBaseDirectory))
        {
            foreach (var ignored in GetActiveCoverletMsBuildProjectConfigurations(projectFilePath))
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerable<CoverletMsBuildProjectConfiguration> GetActiveCoverletMsBuildProjectConfigurations(string projectFilePath)
    {
        if (!TryReadCoverletMsBuildProjectProperties(projectFilePath, out var properties, out var hasCoverletMsBuildReference))
        {
            yield break;
        }

        if (IsActiveCoverletMsBuildProject(properties, hasCoverletMsBuildReference))
        {
            yield return new CoverletMsBuildProjectConfiguration(projectFilePath, properties);
            yield break;
        }

        if (HasExplicitTargetFrameworkProperty() ||
            !properties.TryGetValue(TargetFrameworksPropertyName, out var targetFrameworks) ||
            StringUtil.IsNullOrWhiteSpace(targetFrameworks))
        {
            yield break;
        }

        foreach (var targetFramework in SplitSeparatedValues(targetFrameworks!))
        {
            var normalizedTargetFramework = StripSurroundingQuotes(targetFramework);
            if (StringUtil.IsNullOrWhiteSpace(normalizedTargetFramework))
            {
                continue;
            }

            if (TryReadCoverletMsBuildProjectProperties(projectFilePath, out var targetFrameworkProperties, out var targetFrameworkHasCoverletMsBuildReference, normalizedTargetFramework) &&
                IsActiveCoverletMsBuildProject(targetFrameworkProperties, targetFrameworkHasCoverletMsBuildReference))
            {
                yield return new CoverletMsBuildProjectConfiguration(projectFilePath, targetFrameworkProperties);
            }
        }
    }

    private bool HasExplicitTargetFrameworkProperty()
    {
        if (TryGetMsBuildPropertyValue(TargetFrameworkPropertyName, requireNonEmpty: false, out _))
        {
            return true;
        }

        for (var i = 0; i < _arguments.Count; i++)
        {
            if (TryGetDotnetTestGlobalPropertyAt(i, out var propertyName, out _, out var consumesNextArgument) &&
                propertyName.Equals(TargetFrameworkPropertyName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (consumesNextArgument)
            {
                i++;
            }
        }

        return false;
    }

    private bool IsActiveCoverletMsBuildProject(Dictionary<string, string> properties, bool hasCoverletMsBuildReference)
    {
        return hasCoverletMsBuildReference &&
               properties.TryGetValue(CollectCoveragePropertyName, out var collectCoverageValue) &&
               IsTrueValue(collectCoverageValue);
    }

    private bool TryReadCoverletMsBuildProjectProperties(string projectFilePath, out Dictionary<string, string> properties, out bool hasCoverletMsBuildReference, string? innerBuildTargetFramework = null)
    {
        hasCoverletMsBuildReference = false;
        properties = CreateMsBuildProjectEvaluationProperties(out var globalProperties);
        AddMsBuildReservedProjectProperties(projectFilePath, properties);
        if (!StringUtil.IsNullOrWhiteSpace(innerBuildTargetFramework) &&
            !globalProperties.Contains(TargetFrameworkPropertyName))
        {
            AddMsBuildGlobalProperty(properties, globalProperties, TargetFrameworkPropertyName, innerBuildTargetFramework!);
        }

        var visitedFiles = new HashSet<string>(PathComparer);
        var projectDirectory = Path.GetDirectoryName(projectFilePath);
        if (!StringUtil.IsNullOrEmpty(projectDirectory) &&
            TryGetDirectoryBuildFilePath(
                properties,
                projectDirectory!,
                ImportDirectoryBuildPropsPropertyName,
                DirectoryBuildPropsPathPropertyName,
                DirectoryBuildPropsFileName,
                out var propsFilePath) &&
            !TryReadCoverletMsBuildEvaluationFile(propsFilePath, properties, globalProperties, visitedFiles, ref hasCoverletMsBuildReference))
        {
            return false;
        }

        AddMsBuildDefaultProperties(properties, globalProperties);
        if (!TryReadCoverletMsBuildEvaluationFile(projectFilePath, properties, globalProperties, visitedFiles, ref hasCoverletMsBuildReference))
        {
            return false;
        }

        if (!StringUtil.IsNullOrEmpty(projectDirectory) &&
            TryGetDirectoryBuildFilePath(
                properties,
                projectDirectory!,
                ImportDirectoryBuildTargetsPropertyName,
                DirectoryBuildTargetsPathPropertyName,
                DirectoryBuildTargetsFileName,
                out var targetsFilePath) &&
            !TryReadCoverletMsBuildEvaluationFile(targetsFilePath, properties, globalProperties, visitedFiles, ref hasCoverletMsBuildReference))
        {
            return false;
        }

        return true;
    }

    private Dictionary<string, string> CreateMsBuildProjectEvaluationProperties(out HashSet<string> globalProperties)
    {
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        globalProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddEffectiveMsBuildCommandLineProperties(properties, globalProperties);
        return properties;
    }

    private void AddMsBuildReservedProjectProperties(string projectFilePath, Dictionary<string, string> properties)
    {
        var fullProjectPath = Path.GetFullPath(projectFilePath);
        var projectDirectory = Path.GetDirectoryName(fullProjectPath);
        if (!StringUtil.IsNullOrEmpty(projectDirectory))
        {
            properties[MsBuildProjectDirectoryPropertyName] = projectDirectory!;
        }

        properties[MsBuildProjectFullPathPropertyName] = fullProjectPath;
    }

    private void AddMsBuildDefaultProperties(Dictionary<string, string> properties, HashSet<string> globalProperties)
    {
        AddMsBuildDefaultProperty(properties, globalProperties, ConfigurationPropertyName, DefaultConfigurationPropertyValue);
        AddMsBuildDefaultProperty(properties, globalProperties, PlatformPropertyName, DefaultPlatformPropertyValue);
    }

    private void AddMsBuildDefaultProperty(Dictionary<string, string> properties, HashSet<string> globalProperties, string key, string value)
    {
        if (globalProperties.Contains(key))
        {
            return;
        }

        if (properties.TryGetValue(key, out var existingValue) &&
            !StringUtil.IsNullOrEmpty(existingValue))
        {
            return;
        }

        properties[key] = value;
    }

    private void AddEffectiveMsBuildCommandLineProperties(Dictionary<string, string> properties, HashSet<string> globalProperties)
    {
        var dotnetTestProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < _arguments.Count; i++)
        {
            var argument = StripSurroundingQuotes(_arguments[i]);
            if (TryGetDotnetTestGlobalPropertyAt(i, out var propertyName, out var propertyValue, out var consumesNextArgument))
            {
                dotnetTestProperties[propertyName] = propertyValue;
                if (consumesNextArgument)
                {
                    i++;
                }

                continue;
            }

            if (TryGetMsBuildPropertyPayload(argument, out var propertyPayload))
            {
                AddMsBuildGlobalProperties(properties, globalProperties, propertyPayload);
                continue;
            }

            if (IsOptionName(argument, MsBuildPropertySeparateOptions) &&
                i + 1 < _arguments.Count &&
                !StringUtil.IsNullOrEmpty(_arguments[i + 1]) &&
                !IsMsBuildPropertyOption(_arguments[i + 1]))
            {
                AddMsBuildGlobalProperties(properties, globalProperties, _arguments[i + 1]);
                i++;
            }
        }

        foreach (var property in dotnetTestProperties)
        {
            if (!globalProperties.Contains(property.Key))
            {
                AddMsBuildGlobalProperty(properties, globalProperties, property.Key, property.Value);
            }
        }
    }

    private void AddMsBuildGlobalProperties(Dictionary<string, string> properties, HashSet<string> globalProperties, string propertyPayload)
    {
        foreach (var property in SplitMsBuildProperties(propertyPayload))
        {
            string key;
            string propertyValue;
            if (!TrySplitProperty(property, out key, out propertyValue))
            {
                key = StripSurroundingQuotes(property.Trim());
                propertyValue = string.Empty;
            }

            AddMsBuildGlobalProperty(properties, globalProperties, key, propertyValue);
        }
    }

    private void AddMsBuildGlobalProperty(Dictionary<string, string> properties, HashSet<string> globalProperties, string key, string value)
    {
        if (IsRunConfigurationAssignment(key) ||
            StringUtil.IsNullOrEmpty(key))
        {
            return;
        }

        properties[key] = value;
        globalProperties.Add(key);
    }

    private bool TryGetDotnetTestGlobalPropertyAt(int argumentIndex, out string propertyName, out string propertyValue, out bool consumesNextArgument)
    {
        propertyName = string.Empty;
        propertyValue = string.Empty;
        consumesNextArgument = false;
        if (!TryGetDotnetTestArgumentRange(argumentIndex, out _, out var endIndex))
        {
            return false;
        }

        var argument = StripSurroundingQuotes(_arguments[argumentIndex]);
        if (TryGetDotnetTestOptionProperty(argument, DotnetTestConfigurationOptions, ConfigurationPropertyName, out propertyName, out propertyValue) ||
            TryGetDotnetTestOptionProperty(argument, DotnetTestFrameworkOptions, TargetFrameworkPropertyName, out propertyName, out propertyValue))
        {
            return true;
        }

        if (IsOptionName(argument, DotnetTestConfigurationOptions))
        {
            propertyName = ConfigurationPropertyName;
        }
        else if (IsOptionName(argument, DotnetTestFrameworkOptions))
        {
            propertyName = TargetFrameworkPropertyName;
        }
        else
        {
            return false;
        }

        if (argumentIndex + 1 >= endIndex ||
            StringUtil.IsNullOrEmpty(_arguments[argumentIndex + 1]))
        {
            return true;
        }

        propertyValue = StripSurroundingQuotes(_arguments[argumentIndex + 1]);
        consumesNextArgument = true;
        return true;
    }

    private bool TryGetDotnetTestOptionProperty(string argument, string[] optionNames, string expectedPropertyName, out string propertyName, out string propertyValue)
    {
        propertyName = string.Empty;
        propertyValue = string.Empty;
        if (TryGetInlineOptionValueIncludingEmpty(argument, optionNames, out var inlineValue) ||
            TryGetCompactShortOptionValue(argument, optionNames, out inlineValue))
        {
            propertyName = expectedPropertyName;
            propertyValue = inlineValue;
            return true;
        }

        return false;
    }

    private bool TryGetDotnetTestArgumentRange(int argumentIndex, out int startIndex, out int endIndex)
    {
        startIndex = -1;
        endIndex = -1;
        for (var i = 0; i < _arguments.Count - 1; i++)
        {
            if (!IsExecutableName(_arguments[i], "dotnet") ||
                !TryGetDotnetCommandIndex(i, out var dotnetCommandIndex) ||
                !StripSurroundingQuotes(_arguments[dotnetCommandIndex]).Equals("test", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            startIndex = dotnetCommandIndex + 1;
            endIndex = GetNextDoubleDashIndex(startIndex, _arguments.Count);
            if (argumentIndex >= startIndex && argumentIndex < endIndex)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryReadCoverletMsBuildEvaluationFile(
        string filePath,
        Dictionary<string, string> properties,
        HashSet<string> globalProperties,
        HashSet<string> visitedFiles,
        ref bool hasCoverletMsBuildReference)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return true;
            }

            filePath = Path.GetFullPath(filePath);
            if (!visitedFiles.Add(filePath))
            {
                return true;
            }

            var readerSettings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };
            var xmlDocument = new XmlDocument
            {
                XmlResolver = null
            };
            using var thisFileProperties = ApplyMsBuildThisFileProperties(filePath, properties);
            using (var reader = XmlReader.Create(filePath, readerSettings))
            {
                xmlDocument.Load(reader);
            }

            if (xmlDocument.DocumentElement is null ||
                GetMsBuildConditionState(xmlDocument.DocumentElement, properties) == MsBuildConditionState.False)
            {
                return true;
            }

            foreach (XmlNode? projectChild in xmlDocument.DocumentElement.ChildNodes)
            {
                if (projectChild is not { NodeType: XmlNodeType.Element })
                {
                    continue;
                }

                var projectChildConditionState = GetMsBuildConditionState(projectChild, properties);
                if (projectChildConditionState == MsBuildConditionState.False)
                {
                    continue;
                }

                if (string.Equals(projectChild.LocalName, "PropertyGroup", StringComparison.OrdinalIgnoreCase))
                {
                    ReadMsBuildPropertyGroup(projectChild, properties, globalProperties, projectChildConditionState);
                    continue;
                }

                if (string.Equals(projectChild.LocalName, "ItemGroup", StringComparison.OrdinalIgnoreCase))
                {
                    hasCoverletMsBuildReference |= HasCoverletMsBuildPackageReference(projectChild, properties, projectChildConditionState);
                    continue;
                }

                if (string.Equals(projectChild.LocalName, "Import", StringComparison.OrdinalIgnoreCase) &&
                    HasCoverletMsBuildImport(projectChild))
                {
                    hasCoverletMsBuildReference = true;
                }

                if (string.Equals(projectChild.LocalName, "Import", StringComparison.OrdinalIgnoreCase) &&
                    TryResolveMsBuildImportPath(projectChild, filePath, properties, out var importPath) &&
                    !TryReadCoverletMsBuildEvaluationFile(importPath, properties, globalProperties, visitedFiles, ref hasCoverletMsBuildReference))
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private RestoreMsBuildProperties ApplyMsBuildThisFileProperties(string filePath, Dictionary<string, string> properties)
    {
        var previousValues = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            [MsBuildThisFileDirectoryPropertyName] = properties.TryGetValue(MsBuildThisFileDirectoryPropertyName, out var directory) ? directory : null,
            [MsBuildThisFileFullPathPropertyName] = properties.TryGetValue(MsBuildThisFileFullPathPropertyName, out var fullPath) ? fullPath : null,
            [MsBuildThisFileNamePropertyName] = properties.TryGetValue(MsBuildThisFileNamePropertyName, out var name) ? name : null,
            [MsBuildThisFileExtensionPropertyName] = properties.TryGetValue(MsBuildThisFileExtensionPropertyName, out var extension) ? extension : null,
        };

        var fileDirectory = Path.GetDirectoryName(filePath);
        if (!StringUtil.IsNullOrEmpty(fileDirectory))
        {
            properties[MsBuildThisFileDirectoryPropertyName] = EnsureTrailingDirectorySeparator(fileDirectory!);
        }

        properties[MsBuildThisFileFullPathPropertyName] = filePath;
        properties[MsBuildThisFileNamePropertyName] = Path.GetFileNameWithoutExtension(filePath);
        properties[MsBuildThisFileExtensionPropertyName] = Path.GetExtension(filePath);
        return new RestoreMsBuildProperties(properties, previousValues);
    }

    private bool TryResolveMsBuildImportPath(XmlNode importNode, string importingFilePath, Dictionary<string, string> properties, out string importPath)
    {
        importPath = string.Empty;
        var project = GetXmlAttributeValue(importNode, "Project");
        if (StringUtil.IsNullOrWhiteSpace(project))
        {
            return false;
        }

        var expandedProject = StripSurroundingQuotes(ExpandMsBuildProperties(project!.Trim(), properties));
        if (StringUtil.IsNullOrWhiteSpace(expandedProject) ||
            expandedProject.IndexOf('*') >= 0 ||
            expandedProject.IndexOf('?') >= 0)
        {
            return false;
        }

        var candidatePath = Path.IsPathRooted(expandedProject) ?
                                expandedProject :
                                Path.Combine(Path.GetDirectoryName(importingFilePath) ?? _responseFileBaseDirectory ?? Environment.CurrentDirectory, expandedProject);
        candidatePath = Path.GetFullPath(candidatePath);
        if (!File.Exists(candidatePath))
        {
            return false;
        }

        importPath = candidatePath;
        return true;
    }

    private void ReadMsBuildPropertyGroup(XmlNode propertyGroupNode, Dictionary<string, string> properties, HashSet<string> globalProperties, MsBuildConditionState propertyGroupConditionState)
    {
        foreach (XmlNode? propertyNode in propertyGroupNode.ChildNodes)
        {
            if (propertyNode is not { NodeType: XmlNodeType.Element })
            {
                continue;
            }

            if (globalProperties.Contains(propertyNode.LocalName))
            {
                continue;
            }

            var propertyConditionState = CombineMsBuildConditionStates(propertyGroupConditionState, GetMsBuildConditionState(propertyNode, properties));
            if (propertyConditionState == MsBuildConditionState.False)
            {
                continue;
            }

            var propertyValue = propertyNode.InnerText.Trim();
            if (propertyConditionState == MsBuildConditionState.Unknown &&
                properties.TryGetValue(propertyNode.LocalName, out var existingValue) &&
                IsTrueValue(existingValue) &&
                !IsTrueValue(propertyValue))
            {
                continue;
            }

            properties[propertyNode.LocalName] = propertyValue;
        }
    }

    private bool HasCoverletMsBuildPackageReference(XmlNode itemGroupNode, Dictionary<string, string> properties, MsBuildConditionState itemGroupConditionState)
    {
        foreach (XmlNode? itemNode in itemGroupNode.ChildNodes)
        {
            if (itemNode is not { NodeType: XmlNodeType.Element } ||
                !string.Equals(itemNode.LocalName, "PackageReference", StringComparison.OrdinalIgnoreCase) ||
                CombineMsBuildConditionStates(itemGroupConditionState, GetMsBuildConditionState(itemNode, properties)) == MsBuildConditionState.False)
            {
                continue;
            }

            var include = GetXmlAttributeValue(itemNode, "Include") ?? GetXmlAttributeValue(itemNode, "Update");
            if (!StringUtil.IsNullOrWhiteSpace(include) &&
                include!.Equals(CoverletMsBuildPackageName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasCoverletMsBuildImport(XmlNode importNode)
    {
        var project = GetXmlAttributeValue(importNode, "Project");
        return !StringUtil.IsNullOrWhiteSpace(project) &&
               project!.IndexOf(CoverletMsBuildPackageName, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private MsBuildConditionState GetMsBuildConditionState(XmlNode node, Dictionary<string, string> properties)
    {
        var condition = GetXmlAttributeValue(node, "Condition");
        if (StringUtil.IsNullOrWhiteSpace(condition))
        {
            return MsBuildConditionState.True;
        }

        return EvaluateSimpleMsBuildCondition(condition!, properties);
    }

    private MsBuildConditionState EvaluateSimpleMsBuildCondition(string condition, Dictionary<string, string> properties)
    {
        var expandedCondition = ExpandMsBuildProperties(condition.Trim(), properties).Trim();
        expandedCondition = StripBalancedSurroundingParentheses(expandedCondition);
        if (StringUtil.IsNullOrEmpty(expandedCondition))
        {
            return MsBuildConditionState.True;
        }

        if (TrySplitMsBuildConditionExpression(expandedCondition, "Or", out var orParts))
        {
            var hasUnknownPart = false;
            foreach (var part in orParts)
            {
                var partResult = EvaluateSimpleMsBuildCondition(part, properties);
                if (partResult == MsBuildConditionState.True)
                {
                    return MsBuildConditionState.True;
                }

                if (partResult == MsBuildConditionState.Unknown)
                {
                    hasUnknownPart = true;
                }
            }

            return hasUnknownPart ? MsBuildConditionState.Unknown : MsBuildConditionState.False;
        }

        if (TrySplitMsBuildConditionExpression(expandedCondition, "And", out var andParts))
        {
            var hasUnknownPart = false;
            foreach (var part in andParts)
            {
                var partResult = EvaluateSimpleMsBuildCondition(part, properties);
                if (partResult == MsBuildConditionState.False)
                {
                    return MsBuildConditionState.False;
                }

                if (partResult == MsBuildConditionState.Unknown)
                {
                    hasUnknownPart = true;
                }
            }

            return hasUnknownPart ? MsBuildConditionState.Unknown : MsBuildConditionState.True;
        }

        if (expandedCondition.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            return MsBuildConditionState.True;
        }

        if (expandedCondition.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            return MsBuildConditionState.False;
        }

        if (TrySplitMsBuildConditionComparison(expandedCondition, "==", out var left, out var right))
        {
            return StripSurroundingQuotes(left.Trim()).Equals(StripSurroundingQuotes(right.Trim()), StringComparison.OrdinalIgnoreCase) ?
                       MsBuildConditionState.True :
                       MsBuildConditionState.False;
        }

        if (TrySplitMsBuildConditionComparison(expandedCondition, "!=", out left, out right))
        {
            return !StripSurroundingQuotes(left.Trim()).Equals(StripSurroundingQuotes(right.Trim()), StringComparison.OrdinalIgnoreCase) ?
                       MsBuildConditionState.True :
                       MsBuildConditionState.False;
        }

        return MsBuildConditionState.Unknown;
    }

    private MsBuildConditionState CombineMsBuildConditionStates(MsBuildConditionState left, MsBuildConditionState right)
    {
        if (left == MsBuildConditionState.False || right == MsBuildConditionState.False)
        {
            return MsBuildConditionState.False;
        }

        return left == MsBuildConditionState.Unknown || right == MsBuildConditionState.Unknown ? MsBuildConditionState.Unknown : MsBuildConditionState.True;
    }

    private string StripBalancedSurroundingParentheses(string condition)
    {
        condition = condition.Trim();
        while (condition.Length >= 2 &&
               condition[0] == '(' &&
               condition[condition.Length - 1] == ')' &&
               HasBalancedOuterParentheses(condition))
        {
            condition = condition.Substring(1, condition.Length - 2).Trim();
        }

        return condition;
    }

    private bool HasBalancedOuterParentheses(string condition)
    {
        var inSingleQuotes = false;
        var inDoubleQuotes = false;
        var depth = 0;
        for (var i = 0; i < condition.Length; i++)
        {
            var currentChar = condition[i];
            if (currentChar == '\'' && !inDoubleQuotes)
            {
                inSingleQuotes = !inSingleQuotes;
                continue;
            }

            if (currentChar == '"' && !inSingleQuotes)
            {
                inDoubleQuotes = !inDoubleQuotes;
                continue;
            }

            if (inSingleQuotes || inDoubleQuotes)
            {
                continue;
            }

            if (currentChar == '(')
            {
                depth++;
                continue;
            }

            if (currentChar != ')')
            {
                continue;
            }

            depth--;
            if (depth == 0 && i < condition.Length - 1)
            {
                return false;
            }

            if (depth < 0)
            {
                return false;
            }
        }

        return depth == 0;
    }

    private bool TrySplitMsBuildConditionExpression(string condition, string conditionOperator, out List<string> parts)
    {
        parts = [];
        var startIndex = 0;
        var inSingleQuotes = false;
        var inDoubleQuotes = false;
        var parenthesesDepth = 0;
        for (var i = 0; i <= condition.Length - conditionOperator.Length; i++)
        {
            var currentChar = condition[i];
            if (currentChar == '\'' && !inDoubleQuotes)
            {
                inSingleQuotes = !inSingleQuotes;
                continue;
            }

            if (currentChar == '"' && !inSingleQuotes)
            {
                inDoubleQuotes = !inDoubleQuotes;
                continue;
            }

            if (inSingleQuotes || inDoubleQuotes)
            {
                continue;
            }

            if (currentChar == '(')
            {
                parenthesesDepth++;
                continue;
            }

            if (currentChar == ')')
            {
                parenthesesDepth--;
                continue;
            }

            if (parenthesesDepth != 0 ||
                string.Compare(condition, i, conditionOperator, 0, conditionOperator.Length, StringComparison.OrdinalIgnoreCase) != 0 ||
                !IsMsBuildConditionOperatorBoundary(condition, i - 1) ||
                !IsMsBuildConditionOperatorBoundary(condition, i + conditionOperator.Length))
            {
                continue;
            }

            var part = condition.Substring(startIndex, i - startIndex).Trim();
            if (StringUtil.IsNullOrEmpty(part))
            {
                parts.Clear();
                return false;
            }

            parts.Add(part);
            startIndex = i + conditionOperator.Length;
            i = startIndex - 1;
        }

        if (parts.Count == 0)
        {
            return false;
        }

        var finalPart = condition.Substring(startIndex).Trim();
        if (StringUtil.IsNullOrEmpty(finalPart))
        {
            parts.Clear();
            return false;
        }

        parts.Add(finalPart);
        return true;
    }

    private bool IsMsBuildConditionOperatorBoundary(string value, int index)
    {
        if (index < 0 || index >= value.Length)
        {
            return true;
        }

        return char.IsWhiteSpace(value[index]) || value[index] is '(' or ')';
    }

    private string ExpandMsBuildProperties(string value, Dictionary<string, string> properties)
    {
        var propertyStart = value.IndexOf("$(", StringComparison.Ordinal);
        if (propertyStart < 0)
        {
            return value;
        }

        var expandedValue = new StringBuilder(value.Length);
        var position = 0;
        while (propertyStart >= 0)
        {
            expandedValue.Append(value, position, propertyStart - position);
            var propertyEnd = value.IndexOf(')', propertyStart + 2);
            if (propertyEnd < 0)
            {
                expandedValue.Append(value, propertyStart, value.Length - propertyStart);
                return expandedValue.ToString();
            }

            var propertyName = value.Substring(propertyStart + 2, propertyEnd - propertyStart - 2);
            if (properties.TryGetValue(propertyName, out var propertyValue))
            {
                expandedValue.Append(propertyValue);
            }

            position = propertyEnd + 1;
            propertyStart = value.IndexOf("$(", position, StringComparison.Ordinal);
        }

        expandedValue.Append(value, position, value.Length - position);
        return expandedValue.ToString();
    }

    private bool TrySplitMsBuildConditionComparison(string condition, string comparisonOperator, out string left, out string right)
    {
        left = string.Empty;
        right = string.Empty;
        var inQuotes = false;
        for (var i = 0; i <= condition.Length - comparisonOperator.Length; i++)
        {
            if (condition[i] == '\'' || condition[i] == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (!inQuotes &&
                string.Compare(condition, i, comparisonOperator, 0, comparisonOperator.Length, StringComparison.Ordinal) == 0)
            {
                left = condition.Substring(0, i);
                right = condition.Substring(i + comparisonOperator.Length);
                return true;
            }
        }

        return false;
    }

    private bool IsTrueValue(string value)
    {
        return StripSurroundingQuotes(value).Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsFalseValue(string value)
    {
        return StripSurroundingQuotes(value).Equals("false", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsRunConfigurationAssignment(string propertyName)
    {
        return propertyName.StartsWith(RunSettingsRunConfigurationPrefix, StringComparison.OrdinalIgnoreCase);
    }

    private bool TryGetEffectiveOptionValue(string[] optionNames, out string value)
    {
        value = string.Empty;
        var foundValue = false;
        var foundOptionWithoutValue = false;
        for (var i = 0; i < _arguments.Count; i++)
        {
            var argument = StripSurroundingQuotes(_arguments[i]);
            if (TryGetInlineOptionValueIncludingEmpty(argument, optionNames, out var candidateValue))
            {
                if (StringUtil.IsNullOrEmpty(candidateValue))
                {
                    foundValue = false;
                    foundOptionWithoutValue = true;
                }
                else
                {
                    value = candidateValue;
                    foundValue = true;
                    foundOptionWithoutValue = false;
                }

                continue;
            }

            if (!IsOptionName(argument, optionNames))
            {
                continue;
            }

            if (i + 1 < _arguments.Count && !StringUtil.IsNullOrEmpty(_arguments[i + 1]))
            {
                var nextArgument = StripSurroundingQuotes(_arguments[i + 1]);
                if (!nextArgument.Equals("--", StringComparison.Ordinal) &&
                    !nextArgument.StartsWith("-", StringComparison.Ordinal))
                {
                    value = nextArgument;
                    foundValue = true;
                    foundOptionWithoutValue = false;
                    i++;
                    continue;
                }
            }

            value = string.Empty;
            foundValue = false;
            foundOptionWithoutValue = true;
        }

        return foundValue && !foundOptionWithoutValue;
    }

    private bool TryGetEffectiveMicrosoftTestingPlatformOptionValue(string[] optionNames, out string value)
    {
        value = string.Empty;
        var foundValue = false;
        var foundOptionWithoutValue = false;
        foreach (var range in GetMicrosoftTestingPlatformOptionRanges())
        {
            for (var i = range.StartIndex; i < range.EndIndex; i++)
            {
                if (TryGetOptionValueAt(i, range.EndIndex, optionNames, out var candidateValue, out var consumesNextArgument))
                {
                    if (StringUtil.IsNullOrEmpty(candidateValue))
                    {
                        foundValue = false;
                        foundOptionWithoutValue = true;
                    }
                    else
                    {
                        value = candidateValue;
                        foundValue = true;
                        foundOptionWithoutValue = false;
                    }
                }

                if (consumesNextArgument)
                {
                    i++;
                }
            }
        }

        foreach (var testingPlatformArguments in GetTestingPlatformCommandLineArguments())
        {
            if (testingPlatformArguments.TryGetEffectiveOptionValue(optionNames, out var propertyValue))
            {
                value = propertyValue;
                foundValue = true;
                foundOptionWithoutValue = false;
            }
            else if (testingPlatformArguments.HasOption(optionNames))
            {
                value = string.Empty;
                foundValue = false;
                foundOptionWithoutValue = true;
            }
        }

        return foundValue && !foundOptionWithoutValue;
    }

    private bool MicrosoftTestingPlatformOptionValueContainsAny(string[] optionNames, string[] expectedValues)
    {
        foreach (var range in GetMicrosoftTestingPlatformOptionRanges())
        {
            for (var i = range.StartIndex; i < range.EndIndex; i++)
            {
                if (!TryGetOptionValueAt(i, range.EndIndex, optionNames, out var value, out var consumesNextArgument))
                {
                    continue;
                }

                if (ContainsAnySeparatedValue(value, expectedValues))
                {
                    return true;
                }

                if (consumesNextArgument)
                {
                    i++;
                }
            }
        }

        foreach (var testingPlatformArguments in GetTestingPlatformCommandLineArguments())
        {
            if (testingPlatformArguments.OptionValueContainsAny(optionNames, expectedValues))
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerable<CoverageBackfillCommandLine> GetTestingPlatformCommandLineArguments()
    {
        foreach (var commandLineArguments in GetEffectiveMsBuildPropertyValues(TestingPlatformCommandLineArgumentsPropertyName, requireActiveCoverletMsBuildProject: false))
        {
            yield return Parse(commandLineArguments, _responseFileBaseDirectory);
        }
    }

    /// <summary>
    /// Reads the inline runsettings results directory referenced by the command line.
    /// </summary>
    /// <param name="resultsDirectory">Configured results directory when found.</param>
    /// <returns>True when inline runsettings declare a results directory.</returns>
    public bool TryGetInlineRunSettingsResultsDirectory(out string resultsDirectory)
    {
        resultsDirectory = string.Empty;
        var isRunSettingsArgument = false;
        var foundResultsDirectory = false;
        foreach (var argument in _arguments)
        {
            var normalizedArgument = StripSurroundingQuotes(argument);
            if (normalizedArgument.Equals("--", StringComparison.Ordinal))
            {
                isRunSettingsArgument = true;
                continue;
            }

            if (isRunSettingsArgument &&
                TryGetRunSettingsAssignmentValueIncludingEmpty(normalizedArgument, RunSettingsResultsDirectoryName, out var inlineResultsDirectory))
            {
                resultsDirectory = inlineResultsDirectory;
                foundResultsDirectory = true;
                continue;
            }

            if (TrySplitProperty(normalizedArgument, out var key, out var value) &&
                key.Equals(VstestCliRunSettingsPropertyName, StringComparison.OrdinalIgnoreCase) &&
                TryGetRunSettingsAssignmentValueIncludingEmpty(value, RunSettingsResultsDirectoryName, out var propertyResultsDirectory))
            {
                resultsDirectory = propertyResultsDirectory;
                foundResultsDirectory = true;
            }
        }

        if (TryGetMsBuildPropertyValue(VstestCliRunSettingsPropertyName, out var runSettings) &&
            TryGetRunSettingsAssignmentValueIncludingEmpty(runSettings, RunSettingsResultsDirectoryName, out var msBuildResultsDirectory))
        {
            resultsDirectory = msBuildResultsDirectory;
            foundResultsDirectory = true;
        }

        return foundResultsDirectory && !StringUtil.IsNullOrWhiteSpace(resultsDirectory);
    }

    /// <summary>
    /// Reads the first file-based runsettings results directory referenced by the command line.
    /// </summary>
    /// <param name="baseDirectory">Base directory for relative runsettings file paths.</param>
    /// <param name="resultsDirectory">Resolved configured results directory when found.</param>
    /// <returns>True when a runsettings file declares a results directory.</returns>
    public bool TryGetRunSettingsFileResultsDirectory(string baseDirectory, out string resultsDirectory)
    {
        resultsDirectory = string.Empty;
        foreach (var runSettingsPath in GetRunSettingsPaths(baseDirectory))
        {
            if (TryReadRunSettingsResultsDirectory(runSettingsPath, out var configuredResultsDirectory))
            {
                resultsDirectory = ResolveRunSettingsResultsDirectory(runSettingsPath, configuredResultsDirectory);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets whether any referenced runsettings file narrows the run with a non-empty TestCaseFilter.
    /// </summary>
    /// <returns>True when a runsettings testcase filter is configured.</returns>
    public bool HasRunSettingsTestCaseFilter()
    {
        return HasRunSettingsRunConfigurationValue(RunSettingsTestCaseFilterName);
    }

    private bool HasRunSettingsRunConfigurationValue(string expectedName)
    {
        if (TryGetInlineRunSettingsRunConfigurationValue(expectedName, out var inlineValue))
        {
            return !StringUtil.IsNullOrWhiteSpace(inlineValue);
        }

        var elementLocalName = GetRunSettingsRunConfigurationLocalName(expectedName);
        foreach (var runSettingsPath in GetRunSettingsPaths())
        {
            if (RunSettingsHasRunConfigurationValue(runSettingsPath, elementLocalName))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetInlineRunSettingsRunConfigurationValue(string expectedName, out string value)
    {
        value = string.Empty;
        var foundValue = false;
        var isRunSettingsArgument = false;
        foreach (var argument in _arguments)
        {
            var normalizedArgument = StripSurroundingQuotes(argument);
            if (normalizedArgument.Equals("--", StringComparison.Ordinal))
            {
                isRunSettingsArgument = true;
                continue;
            }

            if (isRunSettingsArgument &&
                TryGetRunSettingsAssignmentValueIncludingEmpty(normalizedArgument, expectedName, out var inlineValue))
            {
                value = inlineValue;
                foundValue = true;
                continue;
            }

            if (TrySplitProperty(normalizedArgument, out var key, out var propertyPayload) &&
                key.Equals(VstestCliRunSettingsPropertyName, StringComparison.OrdinalIgnoreCase) &&
                TryGetRunSettingsAssignmentValueIncludingEmpty(propertyPayload, expectedName, out var propertyValue))
            {
                value = propertyValue;
                foundValue = true;
            }
        }

        foreach (var runSettings in GetEffectiveMsBuildPropertyValues(VstestCliRunSettingsPropertyName, requireActiveCoverletMsBuildProject: false))
        {
            if (TryGetRunSettingsAssignmentValueIncludingEmpty(runSettings, expectedName, out var msBuildValue))
            {
                value = msBuildValue;
                foundValue = true;
            }
        }

        return foundValue;
    }

    private string GetRunSettingsRunConfigurationLocalName(string expectedName)
    {
        const string Prefix = "runconfiguration.";
        return expectedName.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) ? expectedName.Substring(Prefix.Length) : expectedName;
    }

    private bool TryGetRunSettingsAssignmentValueIncludingEmpty(string runSettings, string expectedName, out string value)
    {
        value = string.Empty;
        var foundValue = false;
        foreach (var property in SplitMsBuildProperties(runSettings))
        {
            if (TrySplitProperty(property, out var key, out var propertyValue) &&
                key.Equals(expectedName, StringComparison.OrdinalIgnoreCase))
            {
                value = propertyValue;
                foundValue = true;
            }
        }

        return foundValue;
    }

    private bool HasCollectValue(string expectedValue)
    {
        foreach (var collectValue in GetCollectValues())
        {
            if (ContainsSeparatedValue(collectValue, expectedValue))
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerable<string> GetCollectValues()
    {
        for (var i = 0; i < _arguments.Count; i++)
        {
            var argument = StripSurroundingQuotes(_arguments[i]);
            if (!TryGetInlineOptionValue(argument, CollectOptions, out var value))
            {
                if (!IsOptionName(argument, CollectOptions) ||
                    i + 1 >= _arguments.Count)
                {
                    continue;
                }

                value = StripSurroundingQuotes(_arguments[i + 1]);
            }

            yield return value;
        }

        foreach (var vstestCollect in GetEffectiveMsBuildPropertyValues(VstestCollectPropertyName, requireActiveCoverletMsBuildProject: false))
        {
            yield return vstestCollect;
        }
    }

    private bool HasMicrosoftTestingPlatformOption(string[] optionNames)
    {
        foreach (var range in GetMicrosoftTestingPlatformOptionRanges())
        {
            for (var i = range.StartIndex; i < range.EndIndex; i++)
            {
                if (HasOption(_arguments[i], optionNames))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private IEnumerable<CommandLineOptionRange> GetMicrosoftTestingPlatformOptionRanges()
    {
        for (var i = 0; i < _arguments.Count - 1; i++)
        {
            if (!IsExecutableName(_arguments[i], "dotnet"))
            {
                continue;
            }

            if (!TryGetDotnetCommandIndex(i, out var dotnetCommandIndex))
            {
                continue;
            }

            var dotnetCommand = StripSurroundingQuotes(_arguments[dotnetCommandIndex]);
            if (dotnetCommand.Equals("test", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var range in GetDotnetTestMicrosoftTestingPlatformOptionRanges(dotnetCommandIndex + 1))
                {
                    yield return range;
                }

                continue;
            }

            if (IsAssemblyPath(dotnetCommand))
            {
                yield return new CommandLineOptionRange(dotnetCommandIndex + 1, GetOptionRangeEnd(dotnetCommandIndex + 1));
                continue;
            }

            if (dotnetCommand.Equals("exec", StringComparison.OrdinalIgnoreCase) &&
                TryGetDotnetExecApplicationArgumentStart(dotnetCommandIndex + 1, out var applicationArgumentStart))
            {
                yield return new CommandLineOptionRange(applicationArgumentStart, GetOptionRangeEnd(applicationArgumentStart));
                continue;
            }

            if (dotnetCommand.Equals("run", StringComparison.OrdinalIgnoreCase) &&
                TryGetDotnetRunApplicationArgumentStart(dotnetCommandIndex + 1, out applicationArgumentStart))
            {
                yield return new CommandLineOptionRange(applicationArgumentStart, GetOptionRangeEnd(applicationArgumentStart));
            }
        }

        if (TryGetDirectApplicationArgumentStart(out var directApplicationArgumentStart))
        {
            yield return new CommandLineOptionRange(directApplicationArgumentStart, GetOptionRangeEnd(directApplicationArgumentStart));
        }
    }

    private IEnumerable<CommandLineOptionRange> GetDotnetTestMicrosoftTestingPlatformOptionRanges(int startIndex)
    {
        var doubleDashIndex = GetNextDoubleDashIndex(startIndex, _arguments.Count);
        if (startIndex < doubleDashIndex)
        {
            yield return new CommandLineOptionRange(startIndex, doubleDashIndex);
        }

        if (doubleDashIndex < _arguments.Count - 1)
        {
            yield return new CommandLineOptionRange(doubleDashIndex + 1, _arguments.Count);
        }
    }

    private int GetOptionRangeEnd(int startIndex)
    {
        return GetNextDoubleDashIndex(startIndex, _arguments.Count);
    }

    private int GetNextDoubleDashIndex(int startIndex, int endIndex)
    {
        for (var i = startIndex; i < _arguments.Count; i++)
        {
            if (i >= endIndex)
            {
                break;
            }

            if (StripSurroundingQuotes(_arguments[i]).Equals("--", StringComparison.Ordinal))
            {
                return i;
            }
        }

        return endIndex;
    }

    private bool TryGetDotnetExecApplicationArgumentStart(int startIndex, out int applicationArgumentStart)
    {
        applicationArgumentStart = -1;
        for (var i = startIndex; i < _arguments.Count; i++)
        {
            var argument = StripSurroundingQuotes(_arguments[i]);
            if (argument.Equals("--", StringComparison.Ordinal))
            {
                return false;
            }

            if (TryGetInlineOptionValueIncludingEmpty(argument, DotnetExecValueOptions, out _) ||
                TryGetCompactShortOptionValue(argument, DotnetExecValueOptions, out _))
            {
                continue;
            }

            if (IsOptionName(argument, DotnetExecValueOptions))
            {
                i++;
                continue;
            }

            if (IsAssemblyPath(argument))
            {
                applicationArgumentStart = i + 1;
                return true;
            }

            if (IsOptionLikeArgument(argument))
            {
                continue;
            }

            applicationArgumentStart = i + 1;
            return true;
        }

        return false;
    }

    private bool TryGetDotnetRunApplicationArgumentStart(int startIndex, out int applicationArgumentStart)
    {
        applicationArgumentStart = -1;
        for (var i = startIndex; i < _arguments.Count; i++)
        {
            var argument = StripSurroundingQuotes(_arguments[i]);
            if (argument.Equals("--", StringComparison.Ordinal))
            {
                if (i < _arguments.Count - 1)
                {
                    applicationArgumentStart = i + 1;
                    return true;
                }

                return false;
            }

            if (HasOption(argument, DotnetRunFlagOptions))
            {
                continue;
            }

            if (TryGetInlineOptionValueIncludingEmpty(argument, DotnetRunValueOptions, out _) ||
                TryGetCompactShortOptionValue(argument, DotnetRunValueOptions, out _))
            {
                continue;
            }

            if (IsOptionName(argument, DotnetRunValueOptions))
            {
                i++;
                continue;
            }

            applicationArgumentStart = i;
            return true;
        }

        return false;
    }

    private bool TryGetDirectApplicationArgumentStart(out int applicationArgumentStart)
    {
        applicationArgumentStart = -1;
        for (var i = 0; i < _arguments.Count; i++)
        {
            var argument = StripSurroundingQuotes(_arguments[i]);
            if (argument.Equals("--", StringComparison.Ordinal))
            {
                return false;
            }

            if (IsDirectApplicationPath(argument))
            {
                applicationArgumentStart = i + 1;
                return true;
            }

            if (IsOptionLikeArgument(argument))
            {
                continue;
            }

            return false;
        }

        return false;
    }

    private bool IsAssemblyPath(string argument)
        => argument.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);

    private bool IsDirectApplicationPath(string argument)
    {
        return !StringUtil.IsNullOrWhiteSpace(argument) &&
               !IsExecutableName(argument, "dotnet") &&
               !IsExecutableName(argument, "dotnet-coverage") &&
               !IsExecutableName(argument, "vstest") &&
               !IsExecutableName(argument, "vstest.console") &&
               !IsExecutableName(argument, "vstest.console.arm64") &&
               !IsExecutableName(argument, "msbuild") &&
               !argument.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) &&
               (IsAssemblyPath(argument) ||
                argument.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                LooksLikeRootedOrRelativePath(argument));
    }

    private bool LooksLikeRootedOrRelativePath(string argument)
    {
        if (argument.StartsWith("./", StringComparison.Ordinal) ||
            argument.StartsWith("../", StringComparison.Ordinal) ||
            argument.StartsWith(".\\", StringComparison.Ordinal) ||
            argument.StartsWith("..\\", StringComparison.Ordinal))
        {
            return true;
        }

        if (argument.StartsWith("/", StringComparison.Ordinal))
        {
            return argument.IndexOf('/', 1) > 0;
        }

        return Path.IsPathRooted(argument);
    }

    private bool TryGetOptionValueAt(int optionIndex, int endIndex, string[] optionNames, out string value, out bool consumesNextArgument)
    {
        value = string.Empty;
        consumesNextArgument = false;
        if (optionIndex < 0 || optionIndex >= endIndex || optionIndex >= _arguments.Count)
        {
            return false;
        }

        var argument = StripSurroundingQuotes(_arguments[optionIndex]);
        if (TryGetInlineOptionValueIncludingEmpty(argument, optionNames, out value))
        {
            return true;
        }

        if (!IsOptionName(argument, optionNames))
        {
            return false;
        }

        if (optionIndex + 1 >= endIndex || optionIndex + 1 >= _arguments.Count || StringUtil.IsNullOrEmpty(_arguments[optionIndex + 1]))
        {
            return true;
        }

        var nextArgument = StripSurroundingQuotes(_arguments[optionIndex + 1]);
        if (nextArgument.Equals("--", StringComparison.Ordinal) ||
            nextArgument.StartsWith("-", StringComparison.Ordinal))
        {
            return true;
        }

        value = nextArgument;
        consumesNextArgument = true;
        return true;
    }

    private bool HasRunSettingsDataCollector(string friendlyName, string? baseDirectory)
    {
        foreach (var runSettingsPath in GetRunSettingsPaths(baseDirectory))
        {
            if (RunSettingsHasDataCollector(runSettingsPath, friendlyName))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasRunSettingsDataCollectorFormat(string friendlyName, string expectedFormat, string? baseDirectory)
    {
        foreach (var runSettingsPath in GetRunSettingsPaths(baseDirectory))
        {
            if (RunSettingsHasDataCollectorFormat(runSettingsPath, friendlyName, expectedFormat))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsDotnetCoverageCollectCommand()
        => TryGetDotnetCoverageCollectIndex(out _);

    private bool TryGetDotnetCoverageCollectIndex(out int collectIndex)
    {
        for (var i = 0; i < _arguments.Count - 1; i++)
        {
            if (!IsDotnetCoverageExecutable(_arguments[i]))
            {
                continue;
            }

            var collectCandidateIndex = i + 1;
            if (IsDotnetToolRunDotnetCoverageExecutableIndex(i))
            {
                while (collectCandidateIndex < _arguments.Count &&
                       IsDotnetToolRunOptionBeforeToolArguments(_arguments[collectCandidateIndex]))
                {
                    collectCandidateIndex++;
                }
            }

            if (collectCandidateIndex < _arguments.Count &&
                StripSurroundingQuotes(_arguments[collectCandidateIndex]).Equals("collect", StringComparison.OrdinalIgnoreCase))
            {
                collectIndex = collectCandidateIndex;
                return true;
            }
        }

        collectIndex = -1;
        return false;
    }

    private bool IsDotnetToolRunDotnetCoverageExecutableIndex(int executableIndex)
    {
        if (executableIndex < 3)
        {
            return false;
        }

        var runIndex = executableIndex - 1;
        while (runIndex >= 0 && IsDotnetToolRunOptionBeforeToolArguments(_arguments[runIndex]))
        {
            runIndex--;
        }

        return runIndex >= 2 &&
               IsExecutableName(_arguments[runIndex - 2], "dotnet") &&
               StripSurroundingQuotes(_arguments[runIndex - 1]).Equals("tool", StringComparison.OrdinalIgnoreCase) &&
               StripSurroundingQuotes(_arguments[runIndex]).Equals("run", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsDotnetToolRunOptionBeforeToolArguments(string argument)
    {
        var normalizedArgument = StripSurroundingQuotes(argument);
        return normalizedArgument.Equals("--", StringComparison.Ordinal) ||
               normalizedArgument.Equals("--allow-roll-forward", StringComparison.OrdinalIgnoreCase) ||
               normalizedArgument.StartsWith("--allow-roll-forward=", StringComparison.OrdinalIgnoreCase) ||
               normalizedArgument.StartsWith("--allow-roll-forward:", StringComparison.OrdinalIgnoreCase);
    }

    private bool TryGetDotnetCoverageCollectOptionRange(out int startIndex, out int endIndex)
    {
        startIndex = 0;
        endIndex = 0;
        if (!TryGetDotnetCoverageCollectIndex(out var collectIndex))
        {
            return false;
        }

        startIndex = collectIndex + 1;
        endIndex = _arguments.Count;
        for (var i = startIndex; i < _arguments.Count; i++)
        {
            var argument = StripSurroundingQuotes(_arguments[i]);
            if (argument.Equals("--", StringComparison.Ordinal))
            {
                endIndex = i;
                return true;
            }
        }

        return true;
    }

    private IEnumerable<CoverageBackfillCommandLine> GetDotnetCoverageCollectNestedChildCommands()
    {
        if (!TryGetDotnetCoverageCollectIndex(out var collectIndex))
        {
            yield break;
        }

        for (var i = collectIndex + 1; i < _arguments.Count; i++)
        {
            var argument = StripSurroundingQuotes(_arguments[i]);
            if (!ContainsWhitespace(argument))
            {
                continue;
            }

            var childCommand = new CoverageBackfillCommandLine(argument, _responseFileBaseDirectory);
            if (childCommand.IsCoverageRelevantTestCommand())
            {
                yield return childCommand;
            }
        }
    }

    private bool ContainsWhitespace(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (char.IsWhiteSpace(value[i]))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsDotnetCoverageCollectOptionBeforeChildCommand(int argumentIndex)
    {
        if (!TryGetDotnetCoverageCollectIndex(out var collectIndex) ||
            argumentIndex <= collectIndex)
        {
            return false;
        }

        var childCommandStartIndex = GetDotnetCoverageCollectChildCommandStartIndex(collectIndex);
        return argumentIndex < childCommandStartIndex;
    }

    private int GetDotnetCoverageCollectChildCommandStartIndex(int collectIndex)
    {
        for (var i = collectIndex + 1; i < _arguments.Count; i++)
        {
            var argument = StripSurroundingQuotes(_arguments[i]);
            if (argument.Equals("--", StringComparison.Ordinal))
            {
                return i + 1;
            }

            if (TryGetDotnetCoverageCollectOptionValueAt(i, DotnetCoverageCollectValueOptions, out _, out var consumesNextArgument))
            {
                if (consumesNextArgument)
                {
                    i++;
                }

                continue;
            }

            if (HasOption(argument, DotnetCoverageCollectFlagOptions))
            {
                continue;
            }

            return i;
        }

        return _arguments.Count;
    }

    private bool TryGetDotnetCoverageCollectOptionValue(string[] optionNames, out string value)
    {
        value = string.Empty;
        if (!TryGetDotnetCoverageCollectOptionRange(out var startIndex, out var endIndex))
        {
            return false;
        }

        var foundValue = false;
        var foundOptionWithoutValue = false;
        for (var i = startIndex; i < endIndex; i++)
        {
            if (TryGetDotnetCoverageCollectOptionValueAt(i, optionNames, out var candidateValue, out var consumesNextArgument))
            {
                if (StringUtil.IsNullOrEmpty(candidateValue))
                {
                    foundValue = false;
                    foundOptionWithoutValue = true;
                }
                else
                {
                    value = candidateValue;
                    foundValue = true;
                    foundOptionWithoutValue = false;
                }
            }

            if (consumesNextArgument)
            {
                i++;
            }
        }

        return foundValue && !foundOptionWithoutValue;
    }

    private bool TryGetDotnetCoverageCollectOptionValueAt(int optionIndex, string[] optionNames, out string value, out bool consumesNextArgument)
    {
        value = string.Empty;
        consumesNextArgument = false;
        if (optionIndex < 0 || optionIndex >= _arguments.Count)
        {
            return false;
        }

        var argument = StripSurroundingQuotes(_arguments[optionIndex]);
        if (TryGetInlineOptionValueIncludingEmpty(argument, optionNames, out value) ||
            TryGetCompactShortOptionValue(argument, optionNames, out value))
        {
            return true;
        }

        if (!IsOptionName(argument, optionNames))
        {
            return false;
        }

        if (optionIndex + 1 >= _arguments.Count || StringUtil.IsNullOrEmpty(_arguments[optionIndex + 1]))
        {
            return true;
        }

        var nextArgument = StripSurroundingQuotes(_arguments[optionIndex + 1]);
        if (nextArgument.Equals("--", StringComparison.Ordinal) ||
            IsDotnetCoverageCollectOptionToken(nextArgument))
        {
            return true;
        }

        value = nextArgument;
        consumesNextArgument = true;
        return true;
    }

    private bool IsDotnetCoverageCollectOptionToken(string argument)
    {
        if (StringUtil.IsNullOrEmpty(argument) ||
            argument[0] is not '-' and not '/')
        {
            return false;
        }

        return HasOption(argument, DotnetCoverageCollectValueOptions) ||
               TryGetCompactShortOptionValue(argument, DotnetCoverageCollectValueOptions, out _) ||
               HasOption(argument, DotnetCoverageCollectFlagOptions);
    }

    private bool TryGetShortOptionValue(int optionIndex, out string value, out bool consumesNextArgument)
    {
        value = string.Empty;
        consumesNextArgument = false;
        if (optionIndex < 0 || optionIndex >= _arguments.Count)
        {
            return false;
        }

        var argument = StripSurroundingQuotes(_arguments[optionIndex]);
        if (argument.Length < 2 ||
            argument[0] != '-' ||
            (argument[1] != 'f' && argument[1] != 'F') ||
            (argument.Length > 2 && argument[2] == '-'))
        {
            return false;
        }

        if (argument.Length > 2)
        {
            value = StripSurroundingQuotes(argument[2] is ':' or '=' ? argument.Substring(3) : argument.Substring(2));
            return !StringUtil.IsNullOrEmpty(value);
        }

        if (optionIndex + 1 >= _arguments.Count || StringUtil.IsNullOrEmpty(_arguments[optionIndex + 1]))
        {
            return false;
        }

        value = StripSurroundingQuotes(_arguments[optionIndex + 1]);
        consumesNextArgument = true;
        return true;
    }

    private bool TryGetCompactShortOptionValue(string argument, string[] optionNames, out string value)
    {
        argument = StripSurroundingQuotes(argument);
        if (IsOptionName(argument, optionNames))
        {
            value = string.Empty;
            return false;
        }

        foreach (var optionName in optionNames)
        {
            if (optionName.Length != 2 ||
                optionName[0] != '-' ||
                !argument.StartsWith(optionName, StringComparison.OrdinalIgnoreCase) ||
                argument.Length <= optionName.Length ||
                argument[optionName.Length] is '-' or ':' or '=')
            {
                continue;
            }

            value = StripSurroundingQuotes(argument.Substring(optionName.Length));
            return !StringUtil.IsNullOrEmpty(value);
        }

        value = string.Empty;
        return false;
    }

    private bool IsDotnetCoverageExecutable(string argument)
        => IsExecutableName(argument, "dotnet-coverage");

    private bool IsMsBuildPropertyCommand()
    {
        for (var i = 0; i < _arguments.Count; i++)
        {
            if (IsExecutableName(_arguments[i], "msbuild"))
            {
                return true;
            }

            if (!IsExecutableName(_arguments[i], "dotnet") ||
                !TryGetDotnetCommandIndex(i, out var dotnetCommandIndex))
            {
                continue;
            }

            var dotnetCommand = StripSurroundingQuotes(_arguments[dotnetCommandIndex]);
            if (dotnetCommand.Equals("test", StringComparison.OrdinalIgnoreCase) ||
                dotnetCommand.Equals("msbuild", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsDotnetTestCommand()
    {
        for (var i = 0; i < _arguments.Count - 1; i++)
        {
            if (IsExecutableName(_arguments[i], "dotnet") &&
                TryGetDotnetCommandIndex(i, out var dotnetCommandIndex) &&
                StripSurroundingQuotes(_arguments[dotnetCommandIndex]).Equals("test", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsProjectFilePath(string argument)
    {
        return argument.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
               argument.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase) ||
               argument.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsSolutionFilePath(string argument)
    {
        return argument.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
               argument.EndsWith(".slnf", StringComparison.OrdinalIgnoreCase) ||
               argument.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsProjectOrSolutionFilePath(string argument)
    {
        return IsProjectFilePath(argument) ||
               IsSolutionFilePath(argument);
    }

    /// <summary>
    /// Gets project file paths passed positionally to <c>msbuild</c> or <c>dotnet msbuild</c>, including quoted <c>dotnet-coverage collect</c> child commands.
    /// </summary>
    /// <returns>Raw project file paths from the command line.</returns>
    public IEnumerable<string> GetMsBuildProjectFilePaths()
    {
        foreach (var projectFilePath in GetOwnMsBuildFilePaths(IsProjectFilePath))
        {
            yield return projectFilePath;
        }

        foreach (var childCommand in GetDotnetCoverageCollectNestedChildCommands())
        {
            foreach (var projectFilePath in childCommand.GetMsBuildProjectFilePaths())
            {
                yield return projectFilePath;
            }
        }
    }

    /// <summary>
    /// Gets solution file paths passed positionally to <c>msbuild</c> or <c>dotnet msbuild</c>, including quoted <c>dotnet-coverage collect</c> child commands.
    /// </summary>
    /// <returns>Raw solution file paths from the command line.</returns>
    public IEnumerable<string> GetMsBuildSolutionFilePaths()
    {
        foreach (var solutionFilePath in GetOwnMsBuildFilePaths(IsSolutionFilePath))
        {
            yield return solutionFilePath;
        }

        foreach (var childCommand in GetDotnetCoverageCollectNestedChildCommands())
        {
            foreach (var solutionFilePath in childCommand.GetMsBuildSolutionFilePaths())
            {
                yield return solutionFilePath;
            }
        }
    }

    private IEnumerable<string> GetOwnDotnetTestFilePaths(Func<string, bool> filePathPredicate)
    {
        foreach (var argument in GetOwnDotnetTestTargetArguments())
        {
            if (filePathPredicate(argument))
            {
                yield return argument;
            }
        }
    }

    private IEnumerable<string> GetOwnDotnetTestDirectoryPaths(string? baseDirectory)
    {
        foreach (var argument in GetOwnDotnetTestTargetArguments())
        {
            if (IsProjectOrSolutionFilePath(argument) ||
                !TryResolvePath(argument, baseDirectory, out var resolvedDirectoryPath) ||
                !Directory.Exists(resolvedDirectoryPath))
            {
                continue;
            }

            yield return resolvedDirectoryPath;
        }
    }

    private IEnumerable<string> GetOwnDotnetTestTargetArguments()
    {
        for (var i = 0; i < _arguments.Count - 1; i++)
        {
            if (!IsExecutableName(_arguments[i], "dotnet") ||
                !TryGetDotnetCommandIndex(i, out var dotnetCommandIndex) ||
                !StripSurroundingQuotes(_arguments[dotnetCommandIndex]).Equals("test", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var endIndex = GetNextDoubleDashIndex(dotnetCommandIndex + 1, _arguments.Count);
            for (var argumentIndex = dotnetCommandIndex + 1; argumentIndex < endIndex; argumentIndex++)
            {
                var argument = StripSurroundingQuotes(_arguments[argumentIndex]);
                if (TryGetInlineOptionValueIncludingEmpty(argument, DotnetTestValueOptions, out _) ||
                    TryGetCompactShortOptionValue(argument, DotnetTestValueOptions, out _) ||
                    TryGetMsBuildPropertyPayload(argument, out _) ||
                    TryGetMsBuildTargetPayload(argument, out _))
                {
                    continue;
                }

                if (IsOptionName(argument, DotnetTestValueOptions) ||
                    IsOptionName(argument, MsBuildPropertySeparateOptions) ||
                    IsOptionName(argument, MsBuildTargetSeparateOptions))
                {
                    argumentIndex++;
                    continue;
                }

                if (IsOptionLikeArgument(argument) &&
                    !LooksLikeRootedOrRelativePath(argument))
                {
                    continue;
                }

                yield return argument;
                break;
            }
        }
    }

    private IEnumerable<string> GetOwnMsBuildFilePaths(Func<string, bool> filePathPredicate)
    {
        for (var i = 0; i < _arguments.Count; i++)
        {
            var startIndex = -1;
            if (IsExecutableName(_arguments[i], "msbuild"))
            {
                startIndex = i + 1;
            }
            else if (IsExecutableName(_arguments[i], "dotnet") &&
                     TryGetDotnetCommandIndex(i, out var dotnetCommandIndex) &&
                     StripSurroundingQuotes(_arguments[dotnetCommandIndex]).Equals("msbuild", StringComparison.OrdinalIgnoreCase))
            {
                startIndex = dotnetCommandIndex + 1;
            }

            if (startIndex < 0)
            {
                continue;
            }

            var endIndex = GetNextDoubleDashIndex(startIndex, _arguments.Count);
            for (var argumentIndex = startIndex; argumentIndex < endIndex; argumentIndex++)
            {
                var argument = StripSurroundingQuotes(_arguments[argumentIndex]);
                if (filePathPredicate(argument) && !IsMsBuildPropertyOption(argument))
                {
                    yield return argument;
                }
            }
        }
    }

    private bool IsVstestCoverageCommand()
    {
        for (var i = 0; i < _arguments.Count; i++)
        {
            if (IsExecutableName(_arguments[i], "vstest") ||
                IsExecutableName(_arguments[i], "vstest.console") ||
                IsExecutableName(_arguments[i], "vstest.console.arm64"))
            {
                return true;
            }

            if (!IsExecutableName(_arguments[i], "dotnet") ||
                !TryGetDotnetCommandIndex(i, out var dotnetCommandIndex))
            {
                continue;
            }

            var dotnetCommand = StripSurroundingQuotes(_arguments[dotnetCommandIndex]);
            if (dotnetCommand.Equals("test", StringComparison.OrdinalIgnoreCase) ||
                dotnetCommand.Equals("vstest", StringComparison.OrdinalIgnoreCase) ||
                IsExecutableName(dotnetCommand, "vstest.console") ||
                IsExecutableName(dotnetCommand, "vstest.console.arm64"))
            {
                return true;
            }
        }

        if (IsMsBuildVstestTargetCommand())
        {
            return true;
        }

        return false;
    }

    private bool IsCoverageRelevantTestCommand()
        => IsVstestCoverageCommand() ||
           HasStandaloneTestRunnerCommand() ||
           HasMicrosoftTestingPlatformOptionRange();

    private bool HasStandaloneTestRunnerCommand()
    {
        foreach (var argument in _arguments)
        {
            foreach (var executableName in StandaloneTestRunnerExecutableNames)
            {
                if (IsExecutableName(argument, executableName))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // A dotnet-coverage child can be an MTP test command even when coverage is enabled only by the outer tool.
    private bool HasMicrosoftTestingPlatformOptionRange()
    {
        foreach (var range in GetMicrosoftTestingPlatformOptionRanges())
        {
            return true;
        }

        return false;
    }

    private bool IsMsBuildVstestTargetCommand()
    {
        for (var i = 0; i < _arguments.Count; i++)
        {
            if (IsExecutableName(_arguments[i], "msbuild"))
            {
                return MsBuildTargetValueContainsAny(["vstest"]);
            }

            if (!IsExecutableName(_arguments[i], "dotnet") ||
                !TryGetDotnetCommandIndex(i, out var dotnetCommandIndex))
            {
                continue;
            }

            if (StripSurroundingQuotes(_arguments[dotnetCommandIndex]).Equals("msbuild", StringComparison.OrdinalIgnoreCase))
            {
                return MsBuildTargetValueContainsAny(["vstest"]);
            }
        }

        return false;
    }

    private bool TryGetDotnetCommandIndex(int dotnetExecutableIndex, out int commandIndex)
    {
        commandIndex = -1;
        for (var i = dotnetExecutableIndex + 1; i < _arguments.Count; i++)
        {
            var argument = StripSurroundingQuotes(_arguments[i]);
            if (argument.Equals("--", StringComparison.Ordinal))
            {
                return false;
            }

            if (HasOption(argument, DotnetSdkGlobalFlagOptions))
            {
                continue;
            }

            if (IsAssemblyPath(argument))
            {
                commandIndex = i;
                return true;
            }

            if (IsOptionLikeArgument(argument))
            {
                return false;
            }

            commandIndex = i;
            return true;
        }

        return false;
    }

    private bool IsExecutableName(string argument, string expectedName)
    {
        argument = StripSurroundingQuotes(argument);
        var slashIndex = argument.LastIndexOf('/');
        var backslashIndex = argument.LastIndexOf('\\');
        var nameStart = Math.Max(slashIndex, backslashIndex) + 1;
        var name = nameStart > 0 && nameStart < argument.Length ? argument.Substring(nameStart) : argument;
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            name = name.Substring(0, name.Length - 4);
        }
        else if (name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            name = name.Substring(0, name.Length - 4);
        }

        return name.Equals(expectedName, StringComparison.OrdinalIgnoreCase);
    }

    private IEnumerable<string> GetRunSettingsPaths(string? baseDirectory = null)
    {
        if (TryGetMsBuildPropertyValue(VstestSettingPropertyName, out var vstestSetting))
        {
            foreach (var runSettingsPath in ResolveMsBuildRunSettingsPropertyPaths(StripSurroundingQuotes(vstestSetting), baseDirectory))
            {
                yield return runSettingsPath;
            }

            yield break;
        }

        for (var i = 0; i < _arguments.Count; i++)
        {
            if (IsDotnetCoverageCollectOptionBeforeChildCommand(i))
            {
                continue;
            }

            var argument = StripSurroundingQuotes(_arguments[i]);
            string? value = null;
            if (TryGetInlineOptionValue(argument, RunSettingsOptions, out var inlineValue))
            {
                value = inlineValue;
            }
            else if (IsOptionName(argument, RunSettingsOptions) &&
                     i + 1 < _arguments.Count &&
                     !StringUtil.IsNullOrEmpty(_arguments[i + 1]))
            {
                value = _arguments[i + 1];
            }

            if (value is not { Length: > 0 } runSettingsPath)
            {
                continue;
            }

            yield return ResolveRunSettingsPath(StripSurroundingQuotes(runSettingsPath), baseDirectory);
        }

        if (TryGetMsBuildPropertyValue(RunSettingsFilePathPropertyName, out var runSettingsFilePath))
        {
            foreach (var resolvedRunSettingsFilePath in ResolveMsBuildRunSettingsPropertyPaths(StripSurroundingQuotes(runSettingsFilePath), baseDirectory))
            {
                yield return resolvedRunSettingsFilePath;
            }
        }
    }

    private bool HasOption(string argument, string[] optionNames)
    {
        argument = StripSurroundingQuotes(argument);
        foreach (var optionName in optionNames)
        {
            if (optionName.EndsWith(":", StringComparison.Ordinal))
            {
                if (argument.StartsWith(optionName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                continue;
            }

            if (argument.Equals(optionName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (argument.Length > optionName.Length &&
                argument.StartsWith(optionName, StringComparison.OrdinalIgnoreCase) &&
                argument[optionName.Length] is '=' or ':')
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetInlineOptionValue(string argument, string[] optionNames, out string value)
    {
        return TryGetInlineOptionValueIncludingEmpty(argument, optionNames, out value) &&
               !StringUtil.IsNullOrEmpty(value);
    }

    private bool TryGetInlineOptionValueIncludingEmpty(string argument, string[] optionNames, out string value)
    {
        argument = StripSurroundingQuotes(argument);
        value = string.Empty;
        foreach (var optionName in optionNames)
        {
            if (!argument.StartsWith(optionName, StringComparison.OrdinalIgnoreCase) ||
                argument.Length <= optionName.Length)
            {
                continue;
            }

            var separator = argument[optionName.Length];
            if (separator is not '=' and not ':')
            {
                continue;
            }

            value = StripSurroundingQuotes(argument.Substring(optionName.Length + 1));
            return true;
        }

        return false;
    }

    private bool IsOptionName(string argument, string[] optionNames)
    {
        argument = StripSurroundingQuotes(argument);
        foreach (var optionName in optionNames)
        {
            if (argument.Equals(optionName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsOptionLikeArgument(string argument)
    {
        return argument.Length > 1 &&
               argument[0] is '-' or '/';
    }

    private bool ContainsAnySeparatedValue(string value, string[] expectedValues)
    {
        foreach (var part in SplitSeparatedValues(value))
        {
            var trimmed = StripSurroundingQuotes(part.Trim());
            foreach (var expectedValue in expectedValues)
            {
                if (trimmed.Equals(expectedValue, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool ContainsOnlySeparatedValues(string value, string[] allowedValues)
    {
        var hasValue = false;
        foreach (var part in SplitSeparatedValues(value))
        {
            var trimmed = StripSurroundingQuotes(part.Trim());
            if (StringUtil.IsNullOrEmpty(trimmed))
            {
                continue;
            }

            var allowed = false;
            foreach (var allowedValue in allowedValues)
            {
                if (trimmed.Equals(allowedValue, StringComparison.OrdinalIgnoreCase))
                {
                    allowed = true;
                    break;
                }
            }

            if (!allowed)
            {
                return false;
            }

            hasValue = true;
        }

        return hasValue;
    }

    private bool ContainsSeparatedValue(string value, string expectedValue)
    {
        foreach (var part in SplitSeparatedValues(value))
        {
            if (StripSurroundingQuotes(part.Trim()).Equals(expectedValue, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerable<string> GetMsBuildPropertyPayloads()
    {
        if (!IsMsBuildPropertyCommand())
        {
            yield break;
        }

        for (var i = 0; i < _arguments.Count; i++)
        {
            var argument = StripSurroundingQuotes(_arguments[i]);
            if (TryGetMsBuildPropertyPayload(argument, out var propertyPayload))
            {
                yield return propertyPayload;
                continue;
            }

            if (IsOptionName(argument, MsBuildPropertySeparateOptions) &&
                i + 1 < _arguments.Count &&
                !StringUtil.IsNullOrEmpty(_arguments[i + 1]) &&
                !IsMsBuildPropertyOption(_arguments[i + 1]))
            {
                yield return _arguments[i + 1];
                i++;
            }
        }
    }

    private bool MsBuildTargetValueContainsAny(string[] expectedValues)
    {
        foreach (var targetPayload in GetMsBuildTargetPayloads())
        {
            if (ContainsAnySeparatedValue(targetPayload, expectedValues))
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerable<string> GetMsBuildTargetPayloads()
    {
        for (var i = 0; i < _arguments.Count; i++)
        {
            var argument = StripSurroundingQuotes(_arguments[i]);
            if (TryGetMsBuildTargetPayload(argument, out var targetPayload))
            {
                yield return targetPayload;
                continue;
            }

            if (IsOptionName(argument, MsBuildTargetSeparateOptions) &&
                i + 1 < _arguments.Count &&
                !StringUtil.IsNullOrEmpty(_arguments[i + 1]))
            {
                yield return _arguments[i + 1];
                i++;
            }
        }
    }

    private bool TryGetMsBuildTargetPayload(string argument, out string targetPayload)
    {
        argument = StripSurroundingQuotes(argument);
        targetPayload = string.Empty;
        foreach (var prefix in MsBuildTargetInlinePrefixes)
        {
            if (argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                targetPayload = argument.Substring(prefix.Length);
                return !StringUtil.IsNullOrEmpty(targetPayload);
            }
        }

        return false;
    }

    private bool TryGetMsBuildPropertyPayload(string argument, out string propertyPayload)
    {
        argument = StripSurroundingQuotes(argument);
        propertyPayload = string.Empty;
        foreach (var prefix in MsBuildPropertyInlinePrefixes)
        {
            if (argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                propertyPayload = argument.Substring(prefix.Length);
                return !StringUtil.IsNullOrEmpty(propertyPayload);
            }
        }

        return false;
    }

    private bool IsMsBuildPropertyOption(string argument)
    {
        return IsOptionName(argument, MsBuildPropertySeparateOptions) ||
               TryGetMsBuildPropertyPayload(argument, out _);
    }

    private IEnumerable<string> SplitMsBuildProperties(string propertyPayload)
    {
        propertyPayload = StripSurroundingQuotes(propertyPayload);
        var currentValue = new StringBuilder(propertyPayload.Length);
        var inQuotes = false;
        for (var i = 0; i < propertyPayload.Length; i++)
        {
            var currentChar = propertyPayload[i];
            if (currentChar == '"')
            {
                inQuotes = !inQuotes;
                currentValue.Append(currentChar);
                continue;
            }

            if (!inQuotes &&
                currentChar is ';' or ',' &&
                HasPropertyAssignmentAhead(propertyPayload, i + 1))
            {
                var property = StripSurroundingQuotes(currentValue.ToString().Trim());
                if (!StringUtil.IsNullOrEmpty(property))
                {
                    yield return property;
                }

                currentValue.Clear();
                continue;
            }

            currentValue.Append(currentChar);
        }

        var finalProperty = StripSurroundingQuotes(currentValue.ToString().Trim());
        if (!StringUtil.IsNullOrEmpty(finalProperty))
        {
            yield return finalProperty;
        }
    }

    private bool TrySplitProperty(string property, out string key, out string value)
    {
        var separator = property.IndexOf('=');
        if (separator < 0)
        {
            key = StripSurroundingQuotes(property.Trim());
            value = string.Empty;
            return false;
        }

        key = StripSurroundingQuotes(property.Substring(0, separator).Trim());
        value = UnescapeMsBuildPropertyValue(StripSurroundingQuotes(property.Substring(separator + 1).Trim()));
        return !StringUtil.IsNullOrEmpty(key);
    }

    private bool HasPropertyAssignmentAhead(string value, int startIndex)
    {
        var inQuotes = false;
        for (var i = startIndex; i < value.Length; i++)
        {
            var currentChar = value[i];
            if (currentChar == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (!inQuotes && currentChar == '=')
            {
                return true;
            }

            if (!inQuotes && currentChar is ';' or ',')
            {
                return false;
            }
        }

        return false;
    }

    private IEnumerable<string> SplitSeparatedValues(string value)
    {
        var currentValue = new StringBuilder(value.Length);
        var inQuotes = false;
        for (var i = 0; i < value.Length; i++)
        {
            var currentChar = value[i];
            if (currentChar == '"')
            {
                inQuotes = !inQuotes;
                currentValue.Append(currentChar);
                continue;
            }

            if (!inQuotes && currentChar is ';' or ',')
            {
                yield return currentValue.ToString();
                currentValue.Clear();
                continue;
            }

            currentValue.Append(currentChar);
        }

        yield return currentValue.ToString();
    }

    private string StripSurroundingQuotes(string value)
    {
        value = StripQuotedResponseFileLiteralPrefix(value);
        value = StripOuterQuotes(value.Trim());
        value = UnescapeQuotedArgument(value);
        value = StripOuterQuotes(value.Trim());
        return value;
    }

    private string StripOuterQuotes(string value)
    {
        if (value.Length >= 2 &&
            value[0] == '"' &&
            value[value.Length - 1] == '"')
        {
            return value.Substring(1, value.Length - 2);
        }

        return value;
    }

    private string UnescapeQuotedArgument(string value)
    {
        var quoteIndex = value.IndexOf("\\\"", StringComparison.Ordinal);
        if (quoteIndex < 0)
        {
            return value;
        }

        var unescapedValue = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' &&
                i + 1 < value.Length &&
                value[i + 1] == '"')
            {
                unescapedValue.Append('"');
                i++;
                continue;
            }

            unescapedValue.Append(value[i]);
        }

        return unescapedValue.ToString();
    }

    private string UnescapeMsBuildPropertyValue(string value)
    {
        var percentIndex = value.IndexOf('%');
        if (percentIndex < 0)
        {
            return value;
        }

        var unescapedValue = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '%' &&
                i + 2 < value.Length &&
                TryParseHexDigit(value[i + 1], out var high) &&
                TryParseHexDigit(value[i + 2], out var low))
            {
                unescapedValue.Append((char)((high << 4) + low));
                i += 2;
                continue;
            }

            unescapedValue.Append(value[i]);
        }

        return unescapedValue.ToString();
    }

    private bool TryParseHexDigit(char value, out int digit)
    {
        if (value >= '0' && value <= '9')
        {
            digit = value - '0';
            return true;
        }

        if (value >= 'a' && value <= 'f')
        {
            digit = value - 'a' + 10;
            return true;
        }

        if (value >= 'A' && value <= 'F')
        {
            digit = value - 'A' + 10;
            return true;
        }

        digit = 0;
        return false;
    }

    private bool RunSettingsHasDataCollector(string runSettingsPath, string friendlyName)
    {
        try
        {
            if (!File.Exists(runSettingsPath))
            {
                return false;
            }

            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };
            var xmlDocument = new XmlDocument
            {
                XmlResolver = null
            };
            using var reader = XmlReader.Create(runSettingsPath, settings);
            xmlDocument.Load(reader);
            var dataCollectors = xmlDocument.SelectNodes("//*[translate(local-name(), 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz')='datacollector']");
            if (dataCollectors is null)
            {
                return false;
            }

            foreach (XmlNode? dataCollector in dataCollectors)
            {
                if (dataCollector is null)
                {
                    continue;
                }

                var candidateFriendlyName = dataCollector.Attributes?["friendlyName"]?.Value;
                if (string.Equals(candidateFriendlyName, friendlyName, StringComparison.OrdinalIgnoreCase) &&
                    IsRunSettingsDataCollectorEnabled(dataCollector))
                {
                    return true;
                }
            }
        }
        catch (Exception)
        {
            return false;
        }

        return false;
    }

    private bool RunSettingsHasDataCollectorFormat(string runSettingsPath, string friendlyName, string expectedFormat)
    {
        try
        {
            if (!File.Exists(runSettingsPath))
            {
                return false;
            }

            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };
            var xmlDocument = new XmlDocument
            {
                XmlResolver = null
            };
            using var reader = XmlReader.Create(runSettingsPath, settings);
            xmlDocument.Load(reader);
            var dataCollectors = xmlDocument.SelectNodes("//*[translate(local-name(), 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz')='datacollector']");
            if (dataCollectors is null)
            {
                return false;
            }

            foreach (XmlNode? dataCollector in dataCollectors)
            {
                if (dataCollector is null)
                {
                    continue;
                }

                var candidateFriendlyName = dataCollector.Attributes?["friendlyName"]?.Value;
                if (!string.Equals(candidateFriendlyName, friendlyName, StringComparison.OrdinalIgnoreCase) ||
                    !IsRunSettingsDataCollectorEnabled(dataCollector))
                {
                    continue;
                }

                var formatNode = dataCollector.SelectSingleNode(".//*[translate(local-name(), 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz')='format']");
                if (formatNode is not null &&
                    string.Equals(formatNode.InnerText?.Trim(), expectedFormat, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch (Exception)
        {
            return false;
        }

        return false;
    }

    private bool IsRunSettingsDataCollectorEnabled(XmlNode dataCollector)
    {
        var enabledValue = dataCollector.Attributes?["enabled"]?.Value;
        if (StringUtil.IsNullOrWhiteSpace(enabledValue))
        {
            return true;
        }

        return !bool.TryParse(enabledValue!.Trim(), out var enabled) || enabled;
    }

    private bool TryReadRunSettingsResultsDirectory(string runSettingsPath, out string resultsDirectory)
    {
        resultsDirectory = string.Empty;
        if (!File.Exists(runSettingsPath))
        {
            return false;
        }

        try
        {
            var readerSettings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };
            var xmlDocument = new XmlDocument
            {
                XmlResolver = null
            };
            using var reader = XmlReader.Create(runSettingsPath, readerSettings);
            xmlDocument.Load(reader);
            var node = xmlDocument.SelectSingleNode("//*[translate(local-name(), 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz')='runconfiguration']/*[translate(local-name(), 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz')='resultsdirectory']");
            var value = node?.InnerText?.Trim() ?? string.Empty;
            if (StringUtil.IsNullOrEmpty(value))
            {
                return false;
            }

            resultsDirectory = value;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private bool RunSettingsHasRunConfigurationValue(string runSettingsPath, string elementLocalName)
    {
        try
        {
            if (!File.Exists(runSettingsPath))
            {
                return false;
            }

            var readerSettings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };
            var xmlDocument = new XmlDocument
            {
                XmlResolver = null
            };
            using var reader = XmlReader.Create(runSettingsPath, readerSettings);
            xmlDocument.Load(reader);
            var node = xmlDocument.SelectSingleNode($"//*[translate(local-name(), 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz')='runconfiguration']/*[translate(local-name(), 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz')='{elementLocalName}']");
            return !StringUtil.IsNullOrWhiteSpace(node?.InnerText);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private bool PathsEqual(string left, string right)
    {
        left = StripSurroundingQuotes(left);
        right = StripSurroundingQuotes(right);
        if (left.Equals(right, PathComparison))
        {
            return true;
        }

        return TryResolvePath(left, out var resolvedLeft) &&
               TryResolvePath(right, out var resolvedRight) &&
               resolvedLeft.Equals(resolvedRight, PathComparison);
    }

    private bool CoverletOutputPathsEqual(string left, string right)
    {
        if (PathsEqual(left, right))
        {
            return true;
        }

        foreach (var baseDirectory in GetMsBuildProjectBaseDirectories(null))
        {
            if (TryResolvePath(left, baseDirectory, out var resolvedLeft) &&
                TryResolvePath(right, out var resolvedRight) &&
                resolvedLeft.Equals(resolvedRight, PathComparison))
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerable<string> GetCoverletOutputPathsOrDefault()
    {
        if (TryGetMsBuildPropertyValue(CoverletOutputPropertyName, requireNonEmpty: false, out var commandLineOutputPath))
        {
            if (!StringUtil.IsNullOrEmpty(commandLineOutputPath))
            {
                foreach (var outputPath in GetCommandLineCoverletOutputPaths(commandLineOutputPath))
                {
                    yield return outputPath;
                }
            }
            else
            {
                foreach (var defaultOutputDirectory in GetCoverletMsBuildDefaultOutputDirectories())
                {
                    yield return EnsureTrailingDirectorySeparator(defaultOutputDirectory);
                }
            }

            yield break;
        }

        var hasActiveProject = false;
        foreach (var projectFilePath in GetResolvedMsBuildProjectFilePaths(_responseFileBaseDirectory))
        {
            foreach (var configuration in GetActiveCoverletMsBuildProjectConfigurations(projectFilePath))
            {
                hasActiveProject = true;
                if (configuration.Properties.TryGetValue(CoverletOutputPropertyName, out var projectOutputPath) &&
                    !StringUtil.IsNullOrEmpty(projectOutputPath))
                {
                    var projectDirectory = Path.GetDirectoryName(configuration.ProjectFilePath);
                    yield return StringUtil.IsNullOrEmpty(projectDirectory) ?
                                     StripSurroundingQuotes(projectOutputPath) :
                                     ResolveCoverletOutputPath(projectOutputPath, projectDirectory!);
                    continue;
                }

                var defaultProjectDirectory = Path.GetDirectoryName(configuration.ProjectFilePath);
                if (!StringUtil.IsNullOrEmpty(defaultProjectDirectory))
                {
                    yield return EnsureTrailingDirectorySeparator(defaultProjectDirectory!);
                }
            }
        }

        if (hasActiveProject)
        {
            yield break;
        }

        foreach (var defaultOutputDirectory in GetCoverletMsBuildDefaultOutputDirectories())
        {
            yield return EnsureTrailingDirectorySeparator(defaultOutputDirectory);
        }
    }

    private IEnumerable<string> GetCommandLineCoverletOutputPaths(string commandLineOutputPath)
    {
        commandLineOutputPath = StripSurroundingQuotes(commandLineOutputPath);
        if (Path.IsPathRooted(commandLineOutputPath))
        {
            yield return commandLineOutputPath;
            yield break;
        }

        var projectBaseDirectories = GetMsBuildProjectBaseDirectories(null);
        if (projectBaseDirectories.Count == 0)
        {
            yield return commandLineOutputPath;
            yield break;
        }

        var outputIsDirectoryLike = IsDirectoryLikeCoverletOutput(commandLineOutputPath);
        foreach (var projectBaseDirectory in projectBaseDirectories)
        {
            yield return ResolveCoverletOutputPath(commandLineOutputPath, projectBaseDirectory, outputIsDirectoryLike);
        }
    }

    private string ResolveCoverletOutputPath(string outputPath, string baseDirectory)
    {
        outputPath = StripSurroundingQuotes(outputPath);
        if (Path.IsPathRooted(outputPath))
        {
            return outputPath;
        }

        return ResolveCoverletOutputPath(outputPath, baseDirectory, IsDirectoryLikeCoverletOutput(outputPath));
    }

    private string ResolveCoverletOutputPath(string outputPath, string baseDirectory, bool outputIsDirectoryLike)
    {
        var resolvedOutputPath = Path.GetFullPath(Path.Combine(baseDirectory, outputPath));
        return outputIsDirectoryLike ? EnsureTrailingDirectorySeparator(resolvedOutputPath) : resolvedOutputPath;
    }

    private IEnumerable<string> GetCoverletMsBuildDefaultOutputDirectories()
    {
        var directories = GetActiveCoverletMsBuildProjectBaseDirectories();
        if (directories.Count > 0)
        {
            foreach (var directory in directories)
            {
                yield return directory;
            }

            yield break;
        }

        directories = GetMsBuildProjectBaseDirectories(null);
        foreach (var directory in directories)
        {
            yield return directory;
        }

        if (directories.Count == 0 &&
            UsesImplicitDotnetTestTarget() &&
            TryGetImplicitDotnetTestProjectDirectory(out var implicitProjectDirectory))
        {
            yield return implicitProjectDirectory;
        }
    }

    private List<string> GetActiveCoverletMsBuildProjectBaseDirectories()
    {
        var directories = new List<string>();
        foreach (var projectFilePath in GetResolvedMsBuildProjectFilePaths(_responseFileBaseDirectory))
        {
            foreach (var ignored in GetActiveCoverletMsBuildProjectConfigurations(projectFilePath))
            {
                TryAddProjectBaseDirectory(directories, projectFilePath, null);
                break;
            }
        }

        return directories;
    }

    private bool CoverletOutputReferencesReportPath(string outputPath, string reportPath)
    {
        if (IsDirectoryLikeCoverletOutput(outputPath))
        {
            return CoverletOutputDirectoryReferencesReportPath(outputPath, reportPath);
        }

        outputPath = StripSurroundingQuotes(outputPath);
        if (CoverletOutputPathsEqual(outputPath, reportPath) ||
            CoverletOutputExactFileWithTargetFrameworkReferencesReportPath(outputPath, reportPath))
        {
            return true;
        }

        return CoverletOutputFileReferencesReportPath(outputPath, reportPath);
    }

    private bool CoverletOutputExactFileWritesLineCapableReportPath(string outputPath, string reportPath)
    {
        outputPath = StripSurroundingQuotes(outputPath);
        if (IsDirectoryLikeCoverletOutput(outputPath) ||
            (!CoverletOutputPathsEqual(outputPath, reportPath) && !CoverletOutputExactFileWithTargetFrameworkReferencesReportPath(outputPath, reportPath)) ||
            !TryGetEffectiveMsBuildPropertyValue(CoverletOutputFormatPropertyName, out var outputFormats))
        {
            return false;
        }

        var hasFileReportFormat = false;
        var lastFileReportFormat = string.Empty;
        foreach (var outputFormat in SplitSeparatedValues(outputFormats))
        {
            var normalizedFormat = StripSurroundingQuotes(outputFormat.Trim());
            if (!IsCoverletFileReportFormat(normalizedFormat))
            {
                continue;
            }

            hasFileReportFormat = true;
            lastFileReportFormat = normalizedFormat;
        }

        return hasFileReportFormat && IsLineCapableCoverletXmlFormat(lastFileReportFormat);
    }

    private bool CoverletOutputDirectoryReferencesReportPath(string outputPath, string reportPath)
    {
        if (!TryGetEffectiveMsBuildPropertyValue(CoverletOutputFormatPropertyName, out var outputFormats))
        {
            return false;
        }

        foreach (var outputFormat in SplitSeparatedValues(outputFormats))
        {
            var normalizedFormat = StripSurroundingQuotes(outputFormat.Trim());
            if (!TryGetCoverletReportFileName(normalizedFormat, out var fileName))
            {
                continue;
            }

            if (CoverletOutputPathsEqual(Path.Combine(outputPath, fileName), reportPath) ||
                CoverletOutputDirectoryWithTargetFrameworkReferencesReportPath(outputPath, fileName, reportPath))
            {
                return true;
            }
        }

        return false;
    }

    private bool CoverletOutputDirectoryWritesLineCapableReportPath(string outputPath, string reportPath)
    {
        if (!IsDirectoryLikeCoverletOutput(outputPath) ||
            !TryGetEffectiveMsBuildPropertyValue(CoverletOutputFormatPropertyName, out var outputFormats))
        {
            return false;
        }

        foreach (var outputFormat in SplitSeparatedValues(outputFormats))
        {
            var normalizedFormat = StripSurroundingQuotes(outputFormat.Trim());
            if (!IsLineCapableCoverletXmlFormat(normalizedFormat))
            {
                continue;
            }

            var fileName = $"coverage.{normalizedFormat.ToLowerInvariant()}.xml";
            if (CoverletOutputPathsEqual(Path.Combine(outputPath, fileName), reportPath) ||
                CoverletOutputDirectoryWithTargetFrameworkReferencesReportPath(outputPath, fileName, reportPath))
            {
                return true;
            }
        }

        return false;
    }

    private bool CoverletOutputFileReferencesReportPath(string outputPath, string reportPath)
    {
        if (!TryGetEffectiveMsBuildPropertyValue(CoverletOutputFormatPropertyName, out var outputFormats))
        {
            return false;
        }

        outputPath = StripSurroundingQuotes(outputPath);
        foreach (var outputFormat in SplitSeparatedValues(outputFormats))
        {
            var normalizedFormat = StripSurroundingQuotes(outputFormat.Trim());
            if (!TryGetCoverletReportFileName(normalizedFormat, out var fileName))
            {
                continue;
            }

            var extensionIndex = fileName.IndexOf('.');
            if (extensionIndex >= 0 &&
                (CoverletOutputPathsEqual($"{outputPath}{fileName.Substring(extensionIndex)}", reportPath) ||
                 CoverletOutputFileWithTargetFrameworkReferencesReportPath(outputPath, fileName.Substring(extensionIndex), reportPath)))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsCoverletFileReportFormat(string format)
    {
        return !StringUtil.IsNullOrEmpty(format) &&
               (format.Equals("json", StringComparison.OrdinalIgnoreCase) ||
                format.Equals("lcov", StringComparison.OrdinalIgnoreCase) ||
                format.Equals("cobertura", StringComparison.OrdinalIgnoreCase) ||
                format.Equals("opencover", StringComparison.OrdinalIgnoreCase));
    }

    private bool IsLineCapableCoverletXmlFormat(string format)
    {
        return !StringUtil.IsNullOrEmpty(format) &&
               (format.Equals("cobertura", StringComparison.OrdinalIgnoreCase) ||
                format.Equals("opencover", StringComparison.OrdinalIgnoreCase));
    }

    private bool TryGetCoverletReportFileName(string format, out string fileName)
    {
        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            fileName = "coverage.json";
            return true;
        }

        if (format.Equals("lcov", StringComparison.OrdinalIgnoreCase))
        {
            fileName = "coverage.info";
            return true;
        }

        if (format.Equals("cobertura", StringComparison.OrdinalIgnoreCase))
        {
            fileName = "coverage.cobertura.xml";
            return true;
        }

        if (format.Equals("opencover", StringComparison.OrdinalIgnoreCase))
        {
            fileName = "coverage.opencover.xml";
            return true;
        }

        fileName = string.Empty;
        return false;
    }

    private bool CoverletOutputFileWritesLineCapableReportPath(string outputPath, string reportPath)
    {
        if (IsDirectoryLikeCoverletOutput(outputPath) ||
            !TryGetEffectiveMsBuildPropertyValue(CoverletOutputFormatPropertyName, out var outputFormats))
        {
            return false;
        }

        outputPath = StripSurroundingQuotes(outputPath);
        foreach (var outputFormat in SplitSeparatedValues(outputFormats))
        {
            var normalizedFormat = StripSurroundingQuotes(outputFormat.Trim());
            if (!IsLineCapableCoverletXmlFormat(normalizedFormat))
            {
                continue;
            }

            var extension = $".{normalizedFormat.ToLowerInvariant()}.xml";
            if (CoverletOutputPathsEqual($"{outputPath}{extension}", reportPath) ||
                CoverletOutputFileWithTargetFrameworkReferencesReportPath(outputPath, extension, reportPath))
            {
                return true;
            }
        }

        return false;
    }

    private bool CoverletOutputExactFileWithTargetFrameworkReferencesReportPath(string outputPath, string reportPath)
    {
        var outputExtension = Path.GetExtension(outputPath);
        if (StringUtil.IsNullOrEmpty(outputExtension))
        {
            return false;
        }

        var outputFileName = Path.GetFileName(outputPath);
        var expectedPrefix = Path.GetFileNameWithoutExtension(outputFileName) + ".";
        return CoverletOutputWithTargetFrameworkReferencesReportPath(GetDirectoryOrCurrent(outputPath), expectedPrefix, outputExtension, reportPath);
    }

    private bool CoverletOutputDirectoryWithTargetFrameworkReferencesReportPath(string outputPath, string fileName, string reportPath)
    {
        var extensionIndex = fileName.IndexOf('.');
        if (extensionIndex < 0)
        {
            return false;
        }

        var expectedPrefix = fileName.Substring(0, extensionIndex + 1);
        var expectedSuffix = fileName.Substring(extensionIndex);
        return CoverletOutputWithTargetFrameworkReferencesReportPath(outputPath, expectedPrefix, expectedSuffix, reportPath);
    }

    private bool CoverletOutputFileWithTargetFrameworkReferencesReportPath(string outputPath, string extension, string reportPath)
    {
        var expectedPrefix = Path.GetFileName(outputPath) + ".";
        return CoverletOutputWithTargetFrameworkReferencesReportPath(GetDirectoryOrCurrent(outputPath), expectedPrefix, extension, reportPath);
    }

    private bool CoverletOutputWithTargetFrameworkReferencesReportPath(string outputDirectory, string expectedPrefix, string expectedSuffix, string reportPath)
    {
        if (StringUtil.IsNullOrEmpty(expectedPrefix) ||
            StringUtil.IsNullOrEmpty(expectedSuffix) ||
            !CoverletOutputDirectoryPathsEqual(outputDirectory, GetDirectoryOrCurrent(reportPath)))
        {
            return false;
        }

        var reportFileName = Path.GetFileName(reportPath);
        if (!reportFileName.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase) ||
            !reportFileName.EndsWith(expectedSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var targetFramework = reportFileName.Substring(expectedPrefix.Length, reportFileName.Length - expectedPrefix.Length - expectedSuffix.Length);
        return IsCoverletTargetFrameworkSegment(targetFramework);
    }

    private bool IsCoverletTargetFrameworkSegment(string targetFramework)
    {
        if (StringUtil.IsNullOrWhiteSpace(targetFramework))
        {
            return false;
        }

        if (!targetFramework.StartsWith("net", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var hasDigit = false;
        foreach (var character in targetFramework)
        {
            if (char.IsDigit(character))
            {
                hasDigit = true;
                continue;
            }

            if (char.IsLetter(character))
            {
                continue;
            }

            if (character != '.' &&
                character != '-' &&
                character != '_')
            {
                return false;
            }
        }

        return hasDigit;
    }

    private string GetDirectoryOrCurrent(string path)
        => Path.GetDirectoryName(path) ?? ".";

    private bool DirectoryPathsEqual(string left, string right)
        => PathsEqual(TrimTrailingDirectorySeparators(left), TrimTrailingDirectorySeparators(right));

    private bool CoverletOutputDirectoryPathsEqual(string left, string right)
        => CoverletOutputPathsEqual(TrimTrailingDirectorySeparators(left), TrimTrailingDirectorySeparators(right));

    private string TrimTrailingDirectorySeparators(string path)
    {
        if (StringUtil.IsNullOrEmpty(path))
        {
            return path;
        }

        var root = Path.GetPathRoot(path);
        var minimumLength = StringUtil.IsNullOrEmpty(root) ? 0 : root.Length;
        while (path.Length > minimumLength && IsDirectorySeparator(path[path.Length - 1]))
        {
            path = path.Substring(0, path.Length - 1);
        }

        return path;
    }

    private bool IsDirectorySeparator(char value)
        => value == Path.DirectorySeparatorChar ||
           value == Path.AltDirectorySeparatorChar ||
           value == '/' ||
           value == '\\';

    private bool IsDirectoryLikeCoverletOutput(string outputPath)
    {
        if (StringUtil.IsNullOrWhiteSpace(outputPath))
        {
            return false;
        }

        outputPath = StripSurroundingQuotes(outputPath);
        return outputPath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ||
               outputPath.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal) ||
               outputPath.EndsWith("\\", StringComparison.Ordinal);
    }

    private string EnsureTrailingDirectorySeparator(string path)
    {
        if (StringUtil.IsNullOrEmpty(path) ||
            IsDirectoryLikeCoverletOutput(path))
        {
            return path;
        }

        return path + Path.DirectorySeparatorChar;
    }

    private bool TryGetImplicitDotnetTestProjectDirectory(out string projectDirectory)
    {
        projectDirectory = string.Empty;
        if (!TryResolvePath(".", out var baseDirectory))
        {
            return false;
        }

        try
        {
            string? discoveredProjectPath = null;
            foreach (var filePath in Directory.EnumerateFiles(baseDirectory, "*.*proj", SearchOption.TopDirectoryOnly))
            {
                if (!IsProjectFilePath(filePath))
                {
                    continue;
                }

                if (discoveredProjectPath is not null)
                {
                    return false;
                }

                discoveredProjectPath = filePath;
            }

            if (discoveredProjectPath is null)
            {
                return false;
            }

            projectDirectory = Path.GetDirectoryName(discoveredProjectPath) ?? string.Empty;
            return !StringUtil.IsNullOrEmpty(projectDirectory);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private bool TryResolvePath(string path, out string resolvedPath)
        => TryResolvePath(path, preferredBaseDirectory: null, out resolvedPath);

    private bool TryResolvePath(string path, string? preferredBaseDirectory, out string resolvedPath)
    {
        resolvedPath = string.Empty;
        if (StringUtil.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            if (Path.IsPathRooted(path))
            {
                resolvedPath = Path.GetFullPath(path);
                return true;
            }

            var sessionWorkingDirectory = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory);
            var fallbackBaseDirectory = preferredBaseDirectory is { Length: > 0 } configuredPreferredBaseDirectory && Path.IsPathRooted(configuredPreferredBaseDirectory) ?
                                            configuredPreferredBaseDirectory :
                                            _responseFileBaseDirectory is { Length: > 0 } configuredResponseFileBaseDirectory && Path.IsPathRooted(configuredResponseFileBaseDirectory) ?
                                            configuredResponseFileBaseDirectory :
                                            sessionWorkingDirectory is { Length: > 0 } configuredWorkingDirectory && Path.IsPathRooted(configuredWorkingDirectory) ?
                                            configuredWorkingDirectory :
                                            Environment.CurrentDirectory;
            resolvedPath = Path.GetFullPath(Path.Combine(fallbackBaseDirectory, path));
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private IEnumerable<string> ResolveMsBuildRunSettingsPropertyPaths(string runSettingsPath, string? baseDirectory)
    {
        if (Path.IsPathRooted(runSettingsPath))
        {
            yield return Path.GetFullPath(runSettingsPath);
            yield break;
        }

        var hasProjectBaseDirectory = false;
        foreach (var projectBaseDirectory in GetMsBuildProjectBaseDirectories(baseDirectory))
        {
            hasProjectBaseDirectory = true;
            yield return Path.GetFullPath(Path.Combine(projectBaseDirectory, runSettingsPath));
        }

        if (!hasProjectBaseDirectory)
        {
            yield return ResolveRunSettingsPath(runSettingsPath, baseDirectory);
        }
    }

    private List<string> GetMsBuildProjectBaseDirectories(string? baseDirectory)
    {
        var directories = new List<string>();
        foreach (var projectFilePath in GetDotnetTestProjectFilePaths())
        {
            TryAddProjectBaseDirectory(directories, projectFilePath, baseDirectory);
        }

        foreach (var projectFilePath in GetMsBuildProjectFilePaths())
        {
            TryAddProjectBaseDirectory(directories, projectFilePath, baseDirectory);
        }

        foreach (var solutionFilePath in GetDotnetTestSolutionFilePaths())
        {
            TryAddSolutionProjectBaseDirectories(directories, solutionFilePath, baseDirectory);
        }

        foreach (var targetDirectoryPath in GetDotnetTestDirectoryPaths(baseDirectory))
        {
            TryAddImplicitMsBuildProjectBaseDirectoriesFromDirectory(directories, targetDirectoryPath);
        }

        foreach (var solutionFilePath in GetMsBuildSolutionFilePaths())
        {
            TryAddSolutionProjectBaseDirectories(directories, solutionFilePath, baseDirectory);
        }

        TryAddImplicitMsBuildProjectBaseDirectories(directories, baseDirectory);
        foreach (var childCommand in GetDotnetCoverageCollectNestedChildCommands())
        {
            childCommand.TryAddImplicitMsBuildProjectBaseDirectories(directories, baseDirectory);
        }

        return directories;
    }

    private List<string> GetResolvedMsBuildProjectFilePaths(string? baseDirectory)
    {
        var projectFilePaths = new List<string>();
        foreach (var projectFilePath in GetDotnetTestProjectFilePaths())
        {
            TryAddResolvedProjectFilePath(projectFilePaths, projectFilePath, baseDirectory);
        }

        foreach (var projectFilePath in GetMsBuildProjectFilePaths())
        {
            TryAddResolvedProjectFilePath(projectFilePaths, projectFilePath, baseDirectory);
        }

        foreach (var solutionFilePath in GetDotnetTestSolutionFilePaths())
        {
            TryAddSolutionProjectFilePaths(projectFilePaths, solutionFilePath, baseDirectory);
        }

        foreach (var targetDirectoryPath in GetDotnetTestDirectoryPaths(baseDirectory))
        {
            TryAddImplicitProjectFilePathFromDirectory(projectFilePaths, targetDirectoryPath);
        }

        foreach (var solutionFilePath in GetMsBuildSolutionFilePaths())
        {
            TryAddSolutionProjectFilePaths(projectFilePaths, solutionFilePath, baseDirectory);
        }

        TryAddImplicitMsBuildProjectFilePaths(projectFilePaths, baseDirectory);
        foreach (var childCommand in GetDotnetCoverageCollectNestedChildCommands())
        {
            childCommand.TryAddImplicitMsBuildProjectFilePaths(projectFilePaths, baseDirectory);
        }

        return projectFilePaths;
    }

    private void TryAddImplicitMsBuildProjectBaseDirectories(List<string> directories, string? baseDirectory)
    {
        if (!UsesImplicitDotnetTestTarget() &&
            !UsesImplicitMsBuildVstestTarget())
        {
            return;
        }

        TryAddImplicitMsBuildProjectBaseDirectoriesFromDirectory(directories, baseDirectory);
    }

    private void TryAddImplicitMsBuildProjectFilePaths(List<string> projectFilePaths, string? baseDirectory)
    {
        if (!UsesImplicitDotnetTestTarget() &&
            !UsesImplicitMsBuildVstestTarget())
        {
            return;
        }

        TryAddImplicitProjectFilePathFromDirectory(projectFilePaths, baseDirectory);
    }

    private void TryAddImplicitMsBuildProjectBaseDirectoriesFromDirectory(List<string> directories, string? baseDirectory)
    {
        if (!TryResolvePath(".", baseDirectory, out var resolvedBaseDirectory) ||
            !Directory.Exists(resolvedBaseDirectory))
        {
            return;
        }

        if (TryGetImplicitSolutionFilePath(resolvedBaseDirectory, out var solutionFilePath))
        {
            TryAddSolutionProjectBaseDirectories(directories, solutionFilePath, resolvedBaseDirectory);
            return;
        }

        if (TryGetImplicitProjectFilePath(resolvedBaseDirectory, out var projectFilePath))
        {
            TryAddProjectBaseDirectory(directories, projectFilePath, resolvedBaseDirectory);
        }
    }

    private void TryAddImplicitProjectFilePathFromDirectory(List<string> projectFilePaths, string? baseDirectory)
    {
        if (!TryResolvePath(".", baseDirectory, out var resolvedBaseDirectory) ||
            !Directory.Exists(resolvedBaseDirectory))
        {
            return;
        }

        if (TryGetImplicitSolutionFilePath(resolvedBaseDirectory, out var solutionFilePath))
        {
            TryAddSolutionProjectFilePaths(projectFilePaths, solutionFilePath, resolvedBaseDirectory);
            return;
        }

        if (TryGetImplicitProjectFilePath(resolvedBaseDirectory, out var projectFilePath))
        {
            TryAddResolvedProjectFilePath(projectFilePaths, projectFilePath, resolvedBaseDirectory);
        }
    }

    private void TryAddSolutionProjectBaseDirectories(List<string> directories, string solutionFilePath, string? baseDirectory)
    {
        if (!TryResolvePath(solutionFilePath, baseDirectory, out var resolvedSolutionFilePath) ||
            !File.Exists(resolvedSolutionFilePath))
        {
            return;
        }

        var solutionDirectory = Path.GetDirectoryName(resolvedSolutionFilePath);
        if (StringUtil.IsNullOrEmpty(solutionDirectory))
        {
            return;
        }

        foreach (var projectFilePath in GetSolutionProjectFilePaths(resolvedSolutionFilePath))
        {
            TryAddProjectBaseDirectory(directories, NormalizeSolutionProjectPath(projectFilePath), solutionDirectory);
        }
    }

    private void TryAddSolutionProjectFilePaths(List<string> projectFilePaths, string solutionFilePath, string? baseDirectory)
    {
        if (!TryResolvePath(solutionFilePath, baseDirectory, out var resolvedSolutionFilePath) ||
            !File.Exists(resolvedSolutionFilePath))
        {
            return;
        }

        var solutionDirectory = Path.GetDirectoryName(resolvedSolutionFilePath);
        if (StringUtil.IsNullOrEmpty(solutionDirectory))
        {
            return;
        }

        foreach (var projectFilePath in GetSolutionProjectFilePaths(resolvedSolutionFilePath))
        {
            TryAddResolvedProjectFilePath(projectFilePaths, NormalizeSolutionProjectPath(projectFilePath), solutionDirectory);
        }
    }

    private void TryAddProjectBaseDirectory(List<string> directories, string projectFilePath, string? baseDirectory)
    {
        if (!TryResolvePath(projectFilePath, baseDirectory, out var resolvedProjectFilePath))
        {
            return;
        }

        var projectDirectory = Path.GetDirectoryName(resolvedProjectFilePath);
        if (StringUtil.IsNullOrEmpty(projectDirectory))
        {
            return;
        }

        foreach (var directory in directories)
        {
            if (directory.Equals(projectDirectory, PathComparison))
            {
                return;
            }
        }

        directories.Add(projectDirectory!);
    }

    private void TryAddResolvedProjectFilePath(List<string> projectFilePaths, string projectFilePath, string? baseDirectory)
    {
        if (!TryResolvePath(projectFilePath, baseDirectory, out var resolvedProjectFilePath) ||
            !File.Exists(resolvedProjectFilePath) ||
            !IsProjectFilePath(resolvedProjectFilePath))
        {
            return;
        }

        foreach (var existingProjectFilePath in projectFilePaths)
        {
            if (existingProjectFilePath.Equals(resolvedProjectFilePath, PathComparison))
            {
                return;
            }
        }

        projectFilePaths.Add(resolvedProjectFilePath);
    }

    private bool TryGetDirectoryBuildFilePath(
        Dictionary<string, string> properties,
        string projectDirectory,
        string importPropertyName,
        string pathPropertyName,
        string defaultFileName,
        out string filePath)
    {
        filePath = string.Empty;
        if (properties.TryGetValue(importPropertyName, out var importValue) &&
            IsFalseValue(importValue))
        {
            return false;
        }

        if (properties.TryGetValue(pathPropertyName, out var configuredPath) &&
            !StringUtil.IsNullOrWhiteSpace(configuredPath))
        {
            var normalizedPath = StripSurroundingQuotes(configuredPath.Trim());
            if (Path.IsPathRooted(normalizedPath) &&
                File.Exists(normalizedPath))
            {
                filePath = Path.GetFullPath(normalizedPath);
                return true;
            }

            return false;
        }

        var discoveredPath = FindDirectoryBuildFile(projectDirectory, defaultFileName);
        if (discoveredPath is null)
        {
            return false;
        }

        filePath = discoveredPath;
        return true;
    }

    private string? FindDirectoryBuildFile(string directory, string fileName)
    {
        try
        {
            var currentDirectory = directory;
            while (!StringUtil.IsNullOrEmpty(currentDirectory))
            {
                var candidate = Path.Combine(currentDirectory!, fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                var parentDirectory = Path.GetDirectoryName(currentDirectory!);
                if (StringUtil.IsNullOrEmpty(parentDirectory) ||
                    parentDirectory!.Equals(currentDirectory, PathComparison))
                {
                    return null;
                }

                currentDirectory = parentDirectory;
            }
        }
        catch (Exception)
        {
            return null;
        }

        return null;
    }

    /// <summary>
    /// Gets whether <c>msbuild</c> or <c>dotnet msbuild</c> invokes the VSTest target without an explicit project or solution path.
    /// </summary>
    /// <returns>True when MSBuild will discover the project or solution from the working directory.</returns>
    public bool UsesImplicitMsBuildVstestTarget()
    {
        if (!IsMsBuildVstestTargetCommand())
        {
            return false;
        }

        foreach (var ignored in GetOwnMsBuildFilePaths(IsProjectOrSolutionFilePath))
        {
            return false;
        }

        return true;
    }

    private bool TryGetImplicitSolutionFilePath(string baseDirectory, out string solutionFilePath)
    {
        solutionFilePath = string.Empty;
        try
        {
            string? discoveredSolutionPath = null;
            foreach (var filePath in Directory.EnumerateFiles(baseDirectory, "*", SearchOption.TopDirectoryOnly))
            {
                if (!IsSolutionFilePath(filePath))
                {
                    continue;
                }

                if (discoveredSolutionPath is not null)
                {
                    return false;
                }

                discoveredSolutionPath = filePath;
            }

            if (discoveredSolutionPath is null)
            {
                return false;
            }

            solutionFilePath = discoveredSolutionPath;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private bool TryGetImplicitProjectFilePath(string baseDirectory, out string projectFilePath)
    {
        projectFilePath = string.Empty;
        try
        {
            string? discoveredProjectPath = null;
            foreach (var filePath in Directory.EnumerateFiles(baseDirectory, "*", SearchOption.TopDirectoryOnly))
            {
                if (!IsProjectFilePath(filePath))
                {
                    continue;
                }

                if (discoveredProjectPath is not null)
                {
                    return false;
                }

                discoveredProjectPath = filePath;
            }

            if (discoveredProjectPath is null)
            {
                return false;
            }

            projectFilePath = discoveredProjectPath;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private List<string> GetSolutionProjectFilePaths(string solutionFilePath)
    {
        try
        {
            if (solutionFilePath.EndsWith(".slnf", StringComparison.OrdinalIgnoreCase))
            {
                return GetSlnfProjectFilePaths(solutionFilePath);
            }

            if (solutionFilePath.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
            {
                return GetSlnxProjectFilePaths(solutionFilePath);
            }

            return GetSlnProjectFilePaths(solutionFilePath);
        }
        catch (Exception)
        {
            return [];
        }
    }

    private List<string> GetSlnxProjectFilePaths(string solutionFilePath)
    {
        var projectFilePaths = new List<string>();
        var readerSettings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };
        var xmlDocument = new XmlDocument
        {
            XmlResolver = null
        };
        using var reader = XmlReader.Create(solutionFilePath, readerSettings);
        xmlDocument.Load(reader);
        var projectNodes = xmlDocument.SelectNodes("//*[translate(local-name(), 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz')='project']");
        if (projectNodes is null)
        {
            return projectFilePaths;
        }

        foreach (XmlNode? projectNode in projectNodes)
        {
            if (projectNode is null)
            {
                continue;
            }

            var projectFilePath = GetXmlAttributeValue(projectNode, SlnxProjectPathAttributeName);
            if (!StringUtil.IsNullOrWhiteSpace(projectFilePath) &&
                IsProjectFilePath(projectFilePath!))
            {
                projectFilePaths.Add(projectFilePath!);
            }
        }

        return projectFilePaths;
    }

    private List<string> GetSlnfProjectFilePaths(string solutionFilterFilePath)
    {
        var projectFilePaths = new List<string>();
        var solutionFilter = JsonHelper.DeserializeObject<SolutionFilterFile>(File.ReadAllText(solutionFilterFilePath));
        if (solutionFilter?.Solution?.Projects is not { } projects)
        {
            return projectFilePaths;
        }

        var projectBaseDirectory = GetSlnfProjectBaseDirectory(solutionFilterFilePath, solutionFilter.Solution.Path);
        foreach (var projectFilePath in projects)
        {
            if (StringUtil.IsNullOrWhiteSpace(projectFilePath) ||
                !IsProjectFilePath(projectFilePath!))
            {
                continue;
            }

            projectFilePaths.Add(Path.IsPathRooted(projectFilePath!) ? projectFilePath! : Path.Combine(projectBaseDirectory, NormalizeSolutionProjectPath(projectFilePath!)));
        }

        return projectFilePaths;
    }

    private string GetSlnfProjectBaseDirectory(string solutionFilterFilePath, string? solutionPath)
    {
        var solutionFilterDirectory = Path.GetDirectoryName(solutionFilterFilePath) ?? ".";
        if (StringUtil.IsNullOrWhiteSpace(solutionPath))
        {
            return solutionFilterDirectory;
        }

        var normalizedSolutionPath = NormalizeSolutionProjectPath(solutionPath!);
        var resolvedSolutionPath = Path.IsPathRooted(normalizedSolutionPath) ? normalizedSolutionPath : Path.Combine(solutionFilterDirectory, normalizedSolutionPath);
        return Path.GetDirectoryName(resolvedSolutionPath) ?? solutionFilterDirectory;
    }

    private List<string> GetSlnProjectFilePaths(string solutionFilePath)
    {
        var projectFilePaths = new List<string>();
        foreach (var line in File.ReadLines(solutionFilePath))
        {
            if (!line.TrimStart().StartsWith("Project(", StringComparison.Ordinal) ||
                !TryGetSecondQuotedValueAfterEquals(line, out var projectFilePath) ||
                !IsProjectFilePath(projectFilePath))
            {
                continue;
            }

            projectFilePaths.Add(projectFilePath);
        }

        return projectFilePaths;
    }

    private string? GetXmlAttributeValue(XmlNode node, string attributeName)
    {
        if (node.Attributes is null)
        {
            return null;
        }

        foreach (XmlAttribute? attribute in node.Attributes)
        {
            if (attribute is null)
            {
                continue;
            }

            if (attribute.Name.Equals(attributeName, StringComparison.OrdinalIgnoreCase))
            {
                return attribute.Value;
            }
        }

        return null;
    }

    private bool TryGetSecondQuotedValueAfterEquals(string line, out string value)
    {
        value = string.Empty;
        var equalsIndex = line.IndexOf('=');
        if (equalsIndex < 0)
        {
            return false;
        }

        var quotedValueIndex = 0;
        for (var i = equalsIndex + 1; i < line.Length; i++)
        {
            if (line[i] != '"')
            {
                continue;
            }

            var endIndex = line.IndexOf('"', i + 1);
            if (endIndex < 0)
            {
                return false;
            }

            quotedValueIndex++;
            if (quotedValueIndex == 2)
            {
                value = line.Substring(i + 1, endIndex - i - 1);
                return true;
            }

            i = endIndex;
        }

        return false;
    }

    private string NormalizeSolutionProjectPath(string projectFilePath)
    {
        return Path.DirectorySeparatorChar == '/' ?
                   projectFilePath.Replace('\\', '/') :
                   projectFilePath.Replace('/', '\\');
    }

    private string ResolveRunSettingsPath(string runSettingsPath, string? baseDirectory)
    {
        if (Path.IsPathRooted(runSettingsPath))
        {
            return Path.GetFullPath(runSettingsPath);
        }

        if (baseDirectory is { Length: > 0 } configuredBaseDirectory && Path.IsPathRooted(configuredBaseDirectory))
        {
            return Path.GetFullPath(Path.Combine(configuredBaseDirectory, runSettingsPath));
        }

        var sessionWorkingDirectory = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory);
        var fallbackBaseDirectory = _responseFileBaseDirectory is { Length: > 0 } configuredResponseFileBaseDirectory && Path.IsPathRooted(configuredResponseFileBaseDirectory) ?
                                        configuredResponseFileBaseDirectory :
                                        sessionWorkingDirectory is { Length: > 0 } configuredWorkingDirectory && Path.IsPathRooted(configuredWorkingDirectory) ?
                                        configuredWorkingDirectory :
                                        Environment.CurrentDirectory;
        return Path.GetFullPath(Path.Combine(fallbackBaseDirectory, runSettingsPath));
    }

    private string ResolveRunSettingsResultsDirectory(string runSettingsPath, string resultsDirectory)
    {
        if (Path.IsPathRooted(resultsDirectory))
        {
            return Path.GetFullPath(resultsDirectory);
        }

        var runSettingsDirectory = Path.GetDirectoryName(runSettingsPath);
        return Path.GetFullPath(Path.Combine(StringUtil.IsNullOrEmpty(runSettingsDirectory) ? Environment.CurrentDirectory : runSettingsDirectory!, resultsDirectory));
    }

    private readonly struct CommandLineOptionRange
    {
        public CommandLineOptionRange(int startIndex, int endIndex)
        {
            StartIndex = startIndex;
            EndIndex = endIndex;
        }

        public int StartIndex { get; }

        public int EndIndex { get; }
    }

    private readonly struct CoverletMsBuildProjectConfiguration
    {
        public CoverletMsBuildProjectConfiguration(string projectFilePath, Dictionary<string, string> properties)
        {
            ProjectFilePath = projectFilePath;
            Properties = properties;
        }

        public string ProjectFilePath { get; }

        public Dictionary<string, string> Properties { get; }
    }

    private sealed class RestoreMsBuildProperties : IDisposable
    {
        private readonly Dictionary<string, string> _properties;
        private readonly Dictionary<string, string?> _previousValues;

        public RestoreMsBuildProperties(Dictionary<string, string> properties, Dictionary<string, string?> previousValues)
        {
            _properties = properties;
            _previousValues = previousValues;
        }

        public void Dispose()
        {
            foreach (var item in _previousValues)
            {
                if (item.Value is null)
                {
                    _properties.Remove(item.Key);
                }
                else
                {
                    _properties[item.Key] = item.Value;
                }
            }
        }
    }

    /// <summary>
    /// Represents the root object in a Visual Studio solution filter file.
    /// </summary>
    private sealed class SolutionFilterFile
    {
        /// <summary>
        /// Gets or sets the filtered solution metadata.
        /// </summary>
        public SolutionFilterSolution? Solution { get; set; }
    }

    /// <summary>
    /// Represents the solution and project list from a Visual Studio solution filter file.
    /// </summary>
    private sealed class SolutionFilterSolution
    {
        /// <summary>
        /// Gets or sets the referenced solution path.
        /// </summary>
        public string? Path { get; set; }

        /// <summary>
        /// Gets or sets the project paths included by the solution filter.
        /// </summary>
        public string[]? Projects { get; set; }
    }
}
