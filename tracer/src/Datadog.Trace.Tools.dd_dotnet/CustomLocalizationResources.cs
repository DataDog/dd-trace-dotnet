// <copyright file="CustomLocalizationResources.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;

namespace Datadog.Trace.Tools.dd_dotnet;

internal class CustomLocalizationResources : LocalizationResources
{
    /// <summary>
    ///   Interpolates values into a localized string similar to Command &apos;{0}&apos; expects a single argument but {1} were provided.
    /// </summary>
    public override string ExpectsOneArgument(SymbolResult symbolResult) =>
        symbolResult is CommandResult
            ? GetResourceString(Properties.Resources.CommandExpectsOneArgument, Token(symbolResult).Value, symbolResult.Tokens.Count)
            : GetResourceString(Properties.Resources.OptionExpectsOneArgument, Token(symbolResult).Value, symbolResult.Tokens.Count);

    /// <summary>
    ///   Interpolates values into a localized string similar to No argument was provided for Command &apos;{0}&apos;..
    /// </summary>
    public override string NoArgumentProvided(SymbolResult symbolResult) =>
        symbolResult is CommandResult
            ? GetResourceString(Properties.Resources.CommandNoArgumentProvided, Token(symbolResult).Value)
            : GetResourceString(Properties.Resources.OptionNoArgumentProvided, Token(symbolResult).Value);

    /// <summary>
    ///   Interpolates values into a localized string similar to Command &apos;{0}&apos; expects no more than {1} arguments, but {2} were provided.
    /// </summary>
    public override string ExpectsFewerArguments(
        Token token,
        int providedNumberOfValues,
        int maximumNumberOfValues) =>
        token.Type == TokenType.Command
            ? GetResourceString(Properties.Resources.CommandExpectsFewerArguments, token, maximumNumberOfValues, providedNumberOfValues)
            : GetResourceString(Properties.Resources.OptionExpectsFewerArguments, token, maximumNumberOfValues, providedNumberOfValues);

    /// <summary>
    ///   Interpolates values into a localized string similar to Directory does not exist: {0}.
    /// </summary>
    public override string DirectoryDoesNotExist(string path) =>
        GetResourceString(Properties.Resources.DirectoryDoesNotExist, path);

    /// <summary>
    ///   Interpolates values into a localized string similar to File does not exist: {0}.
    /// </summary>
    public override string FileDoesNotExist(string filePath) =>
        GetResourceString(Properties.Resources.FileDoesNotExist, filePath);

    /// <summary>
    ///   Interpolates values into a localized string similar to File or directory does not exist: {0}.
    /// </summary>
    public override string FileOrDirectoryDoesNotExist(string path) =>
        GetResourceString(Properties.Resources.FileOrDirectoryDoesNotExist, path);

    /// <summary>
    ///   Interpolates values into a localized string similar to Character not allowed in a path: {0}.
    /// </summary>
    public override string InvalidCharactersInPath(char invalidChar) =>
        GetResourceString(Properties.Resources.InvalidCharactersInPath, invalidChar);

    /// <summary>
    ///   Interpolates values into a localized string similar to Character not allowed in a file name: {0}.
    /// </summary>
    public override string InvalidCharactersInFileName(char invalidChar) =>
        GetResourceString(Properties.Resources.InvalidCharactersInFileName, invalidChar);

    /// <summary>
    ///   Interpolates values into a localized string similar to Required argument missing for command: {0}.
    /// </summary>
    public override string RequiredArgumentMissing(SymbolResult symbolResult) =>
        symbolResult is CommandResult
            ? GetResourceString(Properties.Resources.CommandRequiredArgumentMissing, Token(symbolResult).Value)
            : GetResourceString(Properties.Resources.OptionRequiredArgumentMissing, Token(symbolResult).Value);

    /// <summary>
    ///   Interpolates values into a localized string similar to Required command was not provided.
    /// </summary>
    public override string RequiredCommandWasNotProvided() =>
        GetResourceString(Properties.Resources.RequiredCommandWasNotProvided);

    /// <summary>
    ///   Interpolates values into a localized string similar to Argument &apos;{0}&apos; not recognized. Must be one of:{1}.
    /// </summary>
    public override string UnrecognizedArgument(string unrecognizedArg, IReadOnlyCollection<string> allowedValues) =>
        GetResourceString(Properties.Resources.UnrecognizedArgument, unrecognizedArg, $"\n\t{string.Join("\n\t", allowedValues.Select(v => $"'{v}'"))}");

    /// <summary>
    ///   Interpolates values into a localized string similar to Unrecognized command or argument &apos;{0}&apos;.
    /// </summary>
    public override string UnrecognizedCommandOrArgument(string arg) =>
        GetResourceString(Properties.Resources.UnrecognizedCommandOrArgument, arg);

    /// <summary>
    ///   Interpolates values into a localized string similar to Response file not found &apos;{0}&apos;.
    /// </summary>
    public override string ResponseFileNotFound(string filePath) =>
        GetResourceString(Properties.Resources.ResponseFileNotFound, filePath);

    /// <summary>
    ///   Interpolates values into a localized string similar to Error reading response file &apos;{0}&apos;: {1}.
    /// </summary>
    public override string ErrorReadingResponseFile(string filePath, IOException e) =>
        GetResourceString(Properties.Resources.ErrorReadingResponseFile, filePath, e.Message);

    /// <summary>
    ///   Interpolates values into a localized string similar to Show help and usage information.
    /// </summary>
    public override string HelpOptionDescription() =>
        GetResourceString(Properties.Resources.HelpOptionDescription);

    /// <summary>
    ///   Interpolates values into a localized string similar to Usage:.
    /// </summary>
    public override string HelpUsageTitle() =>
        GetResourceString(Properties.Resources.HelpUsageTitle);

    /// <summary>
    ///   Interpolates values into a localized string similar to Description:.
    /// </summary>
    public override string HelpDescriptionTitle() =>
        GetResourceString(Properties.Resources.HelpDescriptionTitle);

    /// <summary>
    ///   Interpolates values into a localized string similar to [options].
    /// </summary>
    public override string HelpUsageOptions() =>
        GetResourceString(Properties.Resources.HelpUsageOptions);

    /// <summary>
    ///   Interpolates values into a localized string similar to [command].
    /// </summary>
    public override string HelpUsageCommand() =>
        GetResourceString(Properties.Resources.HelpUsageCommand);

    /// <summary>
    ///   Interpolates values into a localized string similar to [[--] &lt;additional arguments&gt;...]].
    /// </summary>
    public override string HelpUsageAdditionalArguments() =>
        GetResourceString(Properties.Resources.HelpUsageAdditionalArguments);

    /// <summary>
    ///   Interpolates values into a localized string similar to Arguments:.
    /// </summary>
    public override string HelpArgumentsTitle() =>
        GetResourceString(Properties.Resources.HelpArgumentsTitle);

    /// <summary>
    ///   Interpolates values into a localized string similar to Options:.
    /// </summary>
    public override string HelpOptionsTitle() =>
        GetResourceString(Properties.Resources.HelpOptionsTitle);

    /// <summary>
    ///   Interpolates values into a localized string similar to (REQUIRED).
    /// </summary>
    public override string HelpOptionsRequiredLabel() =>
        GetResourceString(Properties.Resources.HelpOptionsRequiredLabel);

    /// <summary>
    ///   Interpolates values into a localized string similar to default.
    /// </summary>
    public override string HelpArgumentDefaultValueLabel() =>
        GetResourceString(Properties.Resources.HelpArgumentDefaultValueLabel);

    /// <summary>
    ///   Interpolates values into a localized string similar to Commands:.
    /// </summary>
    public override string HelpCommandsTitle() =>
        GetResourceString(Properties.Resources.HelpCommandsTitle);

    /// <summary>
    ///   Interpolates values into a localized string similar to Additional Arguments:.
    /// </summary>
    public override string HelpAdditionalArgumentsTitle() =>
        GetResourceString(Properties.Resources.HelpAdditionalArgumentsTitle);

    /// <summary>
    ///   Interpolates values into a localized string similar to Arguments passed to the application that is being run..
    /// </summary>
    public override string HelpAdditionalArgumentsDescription() =>
        GetResourceString(Properties.Resources.HelpAdditionalArgumentsDescription);

    /// <summary>
    ///   Interpolates values into a localized string similar to &apos;{0}&apos; was not matched. Did you mean one of the following?.
    /// </summary>
    public override string SuggestionsTokenNotMatched(string token)
        => GetResourceString(Properties.Resources.SuggestionsTokenNotMatched, token);

    /// <summary>
    ///   Interpolates values into a localized string similar to Show version information.
    /// </summary>
    public override string VersionOptionDescription()
        => GetResourceString(Properties.Resources.VersionOptionDescription);

    /// <summary>
    ///   Interpolates values into a localized string similar to {0} option cannot be combined with other arguments..
    /// </summary>
    public override string VersionOptionCannotBeCombinedWithOtherArguments(string optionAlias)
        => GetResourceString(Properties.Resources.VersionOptionCannotBeCombinedWithOtherArguments, optionAlias);

    /// <summary>
    ///   Interpolates values into a localized string similar to Unhandled exception: .
    /// </summary>
    public override string ExceptionHandlerHeader()
        => GetResourceString(Properties.Resources.ExceptionHandlerHeader);

    /// <summary>
    ///   Interpolates values into a localized string similar to Cannot parse argument &apos;{0}&apos; as expected type {1}..
    /// </summary>
    public override string ArgumentConversionCannotParse(string value, Type expectedType)
        => GetResourceString(Properties.Resources.ArgumentConversionCannotParse, value, expectedType);

    /// <summary>
    ///   Interpolates values into a localized string similar to Cannot parse argument &apos;{0}&apos; for command &apos;{1}&apos; as expected type {2}..
    /// </summary>
    public override string ArgumentConversionCannotParseForCommand(string value, string commandAlias, Type expectedType)
        => GetResourceString(Properties.Resources.ArgumentConversionCannotParseForCommand, value, commandAlias, expectedType);

    /// <summary>
    ///   Interpolates values into a localized string similar to Cannot parse argument &apos;{0}&apos; for option &apos;{1}&apos; as expected type {2}..
    /// </summary>
    public override string ArgumentConversionCannotParseForOption(string value, string optionAlias, Type expectedType)
        => GetResourceString(Properties.Resources.ArgumentConversionCannotParseForOption, value, optionAlias, expectedType);

    internal static Token Token(SymbolResult symbolResult)
    {
        return symbolResult switch
        {
            CommandResult commandResult => commandResult.Token,
            OptionResult optionResult => optionResult.Token ?? CreateImplicitToken(optionResult.Option),
            _ => throw new ArgumentOutOfRangeException(nameof(symbolResult))
        };

        Token CreateImplicitToken(Option option)
        {
            var optionName = option.Name;

            var defaultAlias = option.Aliases.First(alias => RemovePrefix(alias) == optionName);

            return new Token(defaultAlias, TokenType.Option, option);
        }
    }

    private static string RemovePrefix(string alias)
    {
        int prefixLength = GetPrefixLength(alias);
        return prefixLength > 0
                   ? alias.Substring(prefixLength)
                   : alias;
    }

    private static int GetPrefixLength(string alias)
    {
        if (alias[0] == '-')
        {
            return alias.Length > 1 && alias[1] == '-'
                       ? 2
                       : 1;
        }

        if (alias[0] == '/')
        {
            return 1;
        }

        return 0;
    }

    private class Properties
    {
        internal class Resources
        {
            public static string CommandExpectsFewerArguments => "Command '{0}' expects no more than {1} arguments, but {2} were provided.";

            public static string CommandExpectsOneArgument => "Command '{0}' expects a single argument but {1} were provided.";

            public static string CommandNoArgumentProvided => "No argument was provided for Command '{0}'.";

            public static string DirectoryDoesNotExist => "Directory does not exist: '{0}'.";

            public static string OptionExpectsFewerArguments => "Option '{0}' expects no more than {1} arguments, but {2} were provided.";

            public static string OptionExpectsOneArgument => "Option '{0}' expects a single argument but {1} were provided.";

            public static string OptionNoArgumentProvided => "No argument was provided for Option '{0}'.";

            public static string FileDoesNotExist => "File does not exist: '{0}'.";

            public static string FileOrDirectoryDoesNotExist => "File or directory does not exist: '{0}'.";

            public static string InvalidCharactersInPath => "Character not allowed in a path: '{0}'.";

            public static string CommandRequiredArgumentMissing => "Required argument missing for command: '{0}'.";

            public static string OptionRequiredArgumentMissing => "Required argument missing for option: '{0}'.";

            public static string RequiredCommandWasNotProvided => "Required command was not provided.";

            public static string UnrecognizedArgument => "Argument '{0}' not recognized. Must be one of:{1}";

            public static string UnrecognizedCommandOrArgument => "Unrecognized command or argument '{0}'.";

            public static string ResponseFileNotFound => "Response file not found '{0}'.";

            public static string ErrorReadingResponseFile => "Error reading response file '{0}': {1}";

            public static string HelpOptionDescription => "Show help and usage information";

            public static string InvalidCharactersInFileName => "Character not allowed in a file name: '{0}'.";

            public static string HelpUsageAdditionalArguments => "[[--] <additional arguments>...]]";

            public static string HelpUsageCommand => "[command]";

            public static string HelpUsageOptions => "[options]";

            public static string HelpUsageTitle => "Usage:";

            public static string HelpDescriptionTitle => "Description:";

            public static string HelpArgumentDefaultValueLabel => "default";

            public static string HelpArgumentsTitle => "Arguments:";

            public static string HelpOptionsRequiredLabel => "(REQUIRED)";

            public static string HelpOptionsTitle => "Options:";

            public static string HelpAdditionalArgumentsDescription => "Arguments passed to the application that is being run.";

            public static string HelpAdditionalArgumentsTitle => "Additional Arguments:";

            public static string HelpCommandsTitle => "Commands:";

            public static string ExceptionHandlerHeader => "Unhandled exception: ";

            public static string SuggestionsTokenNotMatched => "'{0}' was not matched. Did you mean one of the following?";

            public static string VersionOptionCannotBeCombinedWithOtherArguments => "{0} option cannot be combined with other arguments.";

            public static string VersionOptionDescription => "Show version information";

            public static string ArgumentConversionCannotParse => "Cannot parse argument '{0}' as expected type '{1}'.";

            public static string ArgumentConversionCannotParseForCommand => "Cannot parse argument '{0}' for command '{1}' as expected type '{2}'.";

            public static string ArgumentConversionCannotParseForOption => "Cannot parse argument '{0}' for option '{1}' as expected type '{2}'.";

            public static string RequiredOptionWasNotProvided => "Option '{0}' is required.";

            public static string ArgumentConversionCannotParseForCommand_Completions => "Cannot parse argument '{0}' for command '{1}' as expected type '{2}'. Did you mean one of the following?{3}";

            public static string ArgumentConversionCannotParseForOption_Completions => "Cannot parse argument '{0}' for option '{1}' as expected type '{2}'. Did you mean one of the following?{3}";
        }
    }
}
