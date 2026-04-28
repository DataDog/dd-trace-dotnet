// <copyright file="GenerateCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Text.Json;
using Datadog.AutoInstrumentation.Generator.Cli.Output;
using Datadog.AutoInstrumentation.Generator.Core;

namespace Datadog.AutoInstrumentation.Generator.Cli.Commands;

internal class GenerateCommand : Command
{
    private static readonly JsonSerializerOptions ConfigDeserializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    // Arguments (validated in handler to allow --list-keys without them)
    private readonly Argument<FileInfo?> _assemblyPathArg = new("assembly-path") { Arity = ArgumentArity.ZeroOrOne, Description = "Path to the .NET assembly (.dll) file" };

    // Options (validated in handler to allow --list-keys without them)
    private readonly Option<string?> _typeOption = new("--type") { Description = "Fully qualified type name (e.g., MyLib.MyClass)" };
    private readonly Option<string?> _methodOption = new("--method") { Description = "Method name to instrument" };

    // Method resolution
    private readonly Option<string[]?> _parameterTypesOption = new("--parameter-types") { Description = "Parameter type full names for overload disambiguation (space-separated, e.g., --parameter-types System.String System.Int32)", AllowMultipleArgumentsPerToken = true };
    private readonly Option<int?> _overloadIndexOption = new("--overload-index") { Description = "0-based overload index for disambiguation" };

    // Shortcut flags (most common toggles); async detection is automatic via auto-detect.
    private readonly Option<bool> _noMethodBeginOption = new("--no-method-begin") { Description = "Skip OnMethodBegin handler generation" };
    private readonly Option<bool> _noMethodEndOption = new("--no-method-end") { Description = "Skip OnMethodEnd handler generation" };

    // Configuration mechanisms
    private readonly Option<string?> _configOption = new("--config") { Description = "Inline JSON configuration object (accepts the 'configuration' block from --json output). Mutually exclusive with --config-file." };
    private readonly Option<FileInfo?> _configFileOption = new("--config-file") { Description = "Path to a JSON configuration file. Accepts the 'configuration' block or full --json output envelope. Mutually exclusive with --config." };
    private readonly Option<string[]> _setOption = new("--set") { Description = "Set a configuration key (repeatable, e.g., --set createDucktypeInstance=true). Use --list-keys to see available keys.", AllowMultipleArgumentsPerToken = true };
    private readonly Option<bool> _listKeysOption = new("--list-keys") { Description = "Print available configuration keys with their default values and exit" };

    // Output flags
    private readonly Option<bool> _jsonOption;
    private readonly Option<FileInfo?> _outputOption = new("--output") { Description = "Write output to file instead of stdout" };

    // Auto-detect flag
    private readonly Option<bool> _noAutoDetectOption = new("--no-auto-detect") { Description = "Disable smart defaults (async detection, static method handling)" };

    public GenerateCommand(Option<bool> jsonOption)
        : base("generate", "Generate CallTarget auto-instrumentation code for a method")
    {
        _jsonOption = jsonOption;

        _typeOption.Aliases.Add("-t");
        _methodOption.Aliases.Add("-m");
        _outputOption.Aliases.Add("-o");

        Add(_assemblyPathArg);
        Add(_typeOption);
        Add(_methodOption);
        Add(_parameterTypesOption);
        Add(_overloadIndexOption);
        Add(_noMethodBeginOption);
        Add(_noMethodEndOption);
        Add(_configOption);
        Add(_configFileOption);
        Add(_setOption);
        Add(_listKeysOption);
        Add(_outputOption);
        Add(_noAutoDetectOption);

        SetAction(Execute);
    }

    private static (GenerationConfiguration? Config, string? Error) LoadConfigFile(FileInfo configFile)
    {
        if (!configFile.Exists)
        {
            return (null, $"Error: Config file not found: {configFile.FullName}");
        }

        string fileContent;
        try
        {
            fileContent = File.ReadAllText(configFile.FullName);
        }
        catch (Exception ex)
        {
            return (null, $"Error: Could not read config file: {ex.Message}");
        }

        try
        {
            // Try to parse as full --json output envelope (has "configuration" property)
            using var doc = JsonDocument.Parse(fileContent);
            if (doc.RootElement.TryGetProperty("configuration", out var configElement))
            {
                var config = JsonSerializer.Deserialize<GenerationConfiguration>(configElement.GetRawText(), ConfigDeserializerOptions)
                             ?? new GenerationConfiguration();
                return (config, null);
            }

            // Otherwise treat as a bare configuration object
            var bareConfig = JsonSerializer.Deserialize<GenerationConfiguration>(fileContent, ConfigDeserializerOptions)
                             ?? new GenerationConfiguration();
            return (bareConfig, null);
        }
        catch (JsonException ex)
        {
            return (null, $"Error: Invalid JSON in config file '{configFile.Name}': {ex.Message}");
        }
    }

    private static void PrintAvailableKeys()
    {
        var rows = ConfigurationApplier.GetAvailableKeys()
            .Select(k => (Key: k.Key, Type: k.Type, Default: k.DefaultValue?.ToString() ?? string.Empty))
            .ToList();

        const string keyHeader = "Key";
        const string typeHeader = "Type";
        const string defaultHeader = "Default";

        var keyWidth = Math.Max(keyHeader.Length, rows.Max(r => r.Key.Length));
        var typeWidth = Math.Max(typeHeader.Length, rows.Max(r => r.Type.Length));
        var defaultWidth = Math.Max(defaultHeader.Length, rows.Max(r => r.Default.Length));

        var format = $"  {{0,-{keyWidth}}} {{1,-{typeWidth}}} {{2,-{defaultWidth}}}";

        Console.WriteLine("Available configuration keys for --set (camelCase, same as --json output):");
        Console.WriteLine();
        Console.WriteLine(format, keyHeader, typeHeader, defaultHeader);
        Console.WriteLine(format, new string('-', keyWidth), new string('-', typeWidth), new string('-', defaultWidth));

        foreach (var row in rows)
        {
            Console.WriteLine(format, row.Key, row.Type, row.Default);
        }

        Console.WriteLine();
        Console.WriteLine("Usage: --set key=value (repeatable)");
        Console.WriteLine("Example: --set createDucktypeInstance=true --set ducktypeInstanceMethods=true");
    }

    private int Execute(ParseResult parseResult)
    {
        var jsonMode = parseResult.GetValue(_jsonOption);

        // Handle --list-keys: print and exit (no assembly/type/method required)
        if (parseResult.GetValue(_listKeysOption))
        {
            if (jsonMode)
            {
                var keys = ConfigurationApplier.GetAvailableKeys()
                    .Select(k => new { key = k.Key, type = k.Type, defaultValue = k.DefaultValue?.ToString() })
                    .ToList();
                return OutputHelper.WriteSuccess(true, "generate", new { configurationKeys = keys });
            }

            PrintAvailableKeys();
            return 0;
        }

        var assemblyPath = parseResult.GetValue(_assemblyPathArg);
        var type = parseResult.GetValue(_typeOption);
        var method = parseResult.GetValue(_methodOption);

        if (assemblyPath is null || type is null || method is null)
        {
            return OutputHelper.WriteError(
                jsonMode,
                "generate",
                ErrorCodes.InvalidArgument,
                "Error: assembly-path, --type, and --method are required (unless using --list-keys).");
        }

        if (!assemblyPath.Exists)
        {
            return OutputHelper.WriteError(
                jsonMode,
                "generate",
                ErrorCodes.FileNotFound,
                $"Error: Assembly file not found: {assemblyPath.FullName}");
        }

        // Validate mutual exclusivity of --config and --config-file
        var configJson = parseResult.GetValue(_configOption);
        var configFile = parseResult.GetValue(_configFileOption);
        if (configJson is not null && configFile is not null)
        {
            return OutputHelper.WriteError(
                jsonMode,
                "generate",
                ErrorCodes.InvalidArgument,
                "Error: --config and --config-file are mutually exclusive. Use one or the other.");
        }

        AssemblyBrowser browser;
        try
        {
            browser = new AssemblyBrowser(assemblyPath.FullName);
        }
        catch (Exception ex)
        {
            return OutputHelper.WriteError(
                jsonMode,
                "generate",
                ErrorCodes.BadAssembly,
                $"Error: Failed to load assembly '{assemblyPath.Name}': {ex.Message}");
        }

        using var disposableBrowser = browser;
        var methodDef = browser.ResolveMethod(
            type,
            method,
            parseResult.GetValue(_parameterTypesOption),
            parseResult.GetValue(_overloadIndexOption));

        if (methodDef is null)
        {
            var overloads = browser.ListOverloads(type, method);
            if (overloads.Count == 0)
            {
                return OutputHelper.WriteError(
                    jsonMode,
                    "generate",
                    ErrorCodes.MethodNotFound,
                    $"Error: Method '{method}' not found on type '{type}' in assembly '{assemblyPath.Name}'.");
            }

            var overloadData = overloads.Select((o, i) => new
            {
                index = i,
                fullName = o.FullName,
                parameters = o.Parameters
                    .Where(p => !p.IsHiddenThisParameter)
                    .Select(p => new { name = p.Name, type = p.Type.FullName })
                    .ToList(),
            }).ToList();

            var message = $"Error: Could not resolve method '{method}' on type '{type}'. Found {overloads.Count} overload(s):\n"
                + string.Join("\n", overloads.Select((o, i) => $"  [{i}] {o.FullName}"))
                + "\nUse --parameter-types or --overload-index to disambiguate.";

            return OutputHelper.WriteError(
                jsonMode,
                "generate",
                ErrorCodes.AmbiguousOverload,
                message,
                new { overloads = overloadData });
        }

        // Step 1: Start with auto-detect or blank config
        GenerationConfiguration config;
        if (parseResult.GetValue(_noAutoDetectOption))
        {
            config = new GenerationConfiguration();
        }
        else
        {
            config = GenerationConfiguration.CreateForMethod(methodDef);
        }

        // Step 2: Apply base config from --config-file or --config (replaces layer 1)
        if (configFile is not null)
        {
            var loadResult = LoadConfigFile(configFile);
            if (loadResult.Error is not null)
            {
                return OutputHelper.WriteError(
                    jsonMode,
                    "generate",
                    ErrorCodes.InvalidConfig,
                    loadResult.Error);
            }

            config = loadResult.Config!;
        }
        else if (configJson is not null)
        {
            try
            {
                config = JsonSerializer.Deserialize<GenerationConfiguration>(configJson, ConfigDeserializerOptions)
                         ?? new GenerationConfiguration();
            }
            catch (JsonException ex)
            {
                return OutputHelper.WriteError(
                    jsonMode,
                    "generate",
                    ErrorCodes.InvalidConfig,
                    $"Error: Invalid --config JSON: {ex.Message}");
            }
        }

        // Step 3: Apply shortcut flags
        if (parseResult.GetValue(_noMethodBeginOption))
        {
            config.CreateOnMethodBegin = false;
        }

        if (parseResult.GetValue(_noMethodEndOption))
        {
            config.CreateOnMethodEnd = false;
        }

        // Step 4: Apply --set overrides
        var setValues = parseResult.GetValue(_setOption);
        if (setValues is { Length: > 0 })
        {
            var error = ConfigurationApplier.ApplyOverrides(config, setValues);
            if (error is not null)
            {
                return OutputHelper.WriteError(
                    jsonMode,
                    "generate",
                    ErrorCodes.UnknownKey,
                    error);
            }
        }

        // Step 5: Generate and output
        var generator = new InstrumentationGenerator();
        var result = generator.Generate(methodDef, config);

        string outputText;
        if (jsonMode)
        {
            outputText = JsonOutputFormatter.Format(result, config);
        }
        else if (result.Success)
        {
            outputText = result.SourceCode!;
        }
        else
        {
            return OutputHelper.WriteError(
                jsonMode,
                "generate",
                ErrorCodes.GenerationError,
                $"Error: {result.ErrorMessage}");
        }

        var output = parseResult.GetValue(_outputOption);
        if (output is not null)
        {
            var dir = output.Directory;
            if (dir is not null && !dir.Exists)
            {
                dir.Create();
            }

            File.WriteAllText(output.FullName, outputText);
            Console.WriteLine($"Output written to: {output.FullName}");
        }
        else
        {
            Console.Write(outputText);
        }

        return 0;
    }
}
