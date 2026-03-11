// <copyright file="GenerateCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
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
    private readonly Option<string?> _typeOption = new("--type", "Fully qualified type name (e.g., MyLib.MyClass)");
    private readonly Option<string?> _methodOption = new("--method", "Method name to instrument");

    // Method resolution
    private readonly Option<string[]?> _parameterTypesOption = new("--parameter-types", "Parameter type full names for overload disambiguation");
    private readonly Option<int?> _overloadIndexOption = new("--overload-index", "0-based overload index for disambiguation");

    // Shortcut flags (3 most common toggles)
    private readonly Option<bool> _noMethodBeginOption = new("--no-method-begin", "Skip OnMethodBegin handler generation");
    private readonly Option<bool> _noMethodEndOption = new("--no-method-end", "Skip OnMethodEnd handler generation");
    private readonly Option<bool> _asyncMethodEndOption = new("--async-method-end", "Generate OnAsyncMethodEnd handler");

    // Configuration mechanisms
    private readonly Option<string?> _configOption = new("--config", "Inline JSON configuration object (accepts the 'configuration' block from --json output). Mutually exclusive with --config-file.");
    private readonly Option<FileInfo?> _configFileOption = new("--config-file", "Path to a JSON configuration file. Accepts the 'configuration' block or full --json output envelope. Mutually exclusive with --config.");
    private readonly Option<string[]> _setOption = new("--set", "Set a configuration key (repeatable, e.g., --set createDucktypeInstance=true). Use --list-keys to see available keys.") { AllowMultipleArgumentsPerToken = true };
    private readonly Option<bool> _listKeysOption = new("--list-keys", "Print available configuration keys with their default values and exit");

    // Output flags
    private readonly Option<bool> _jsonOption = new("--json", "Output structured JSON instead of source code");
    private readonly Option<FileInfo?> _outputOption = new("--output", "Write output to file instead of stdout");

    // Auto-detect flag
    private readonly Option<bool> _noAutoDetectOption = new("--no-auto-detect", "Disable smart defaults (async detection, static method handling)");

    public GenerateCommand()
        : base("generate", "Generate CallTarget auto-instrumentation code for a method")
    {
        _typeOption.AddAlias("-t");
        _methodOption.AddAlias("-m");
        _outputOption.AddAlias("-o");

        AddArgument(_assemblyPathArg);
        AddOption(_typeOption);
        AddOption(_methodOption);
        AddOption(_parameterTypesOption);
        AddOption(_overloadIndexOption);
        AddOption(_noMethodBeginOption);
        AddOption(_noMethodEndOption);
        AddOption(_asyncMethodEndOption);
        AddOption(_configOption);
        AddOption(_configFileOption);
        AddOption(_setOption);
        AddOption(_listKeysOption);
        AddOption(_jsonOption);
        AddOption(_outputOption);
        AddOption(_noAutoDetectOption);

        this.SetHandler(Execute);
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
        Console.WriteLine("Available configuration keys for --set (camelCase, same as --json output):");
        Console.WriteLine();
        Console.WriteLine("  {0,-45} {1,-10} {2}", "Key", "Type", "Default");
        Console.WriteLine("  {0,-45} {1,-10} {2}", new string('-', 45), new string('-', 10), new string('-', 7));

        foreach (var (key, typeName, defaultValue) in ConfigurationApplier.GetAvailableKeys())
        {
            Console.WriteLine("  {0,-45} {1,-10} {2}", key, typeName, defaultValue);
        }

        Console.WriteLine();
        Console.WriteLine("Usage: --set key=value (repeatable)");
        Console.WriteLine("Example: --set createDucktypeInstance=true --set ducktypeInstanceMethods=true");
    }

    private void Execute(InvocationContext ctx)
    {
        // Handle --list-keys: print and exit (no assembly/type/method required)
        if (ctx.ParseResult.GetValueForOption(_listKeysOption))
        {
            PrintAvailableKeys();
            return;
        }

        var assemblyPath = ctx.ParseResult.GetValueForArgument(_assemblyPathArg);
        var type = ctx.ParseResult.GetValueForOption(_typeOption);
        var method = ctx.ParseResult.GetValueForOption(_methodOption);

        if (assemblyPath is null || type is null || method is null)
        {
            Console.Error.WriteLine("Error: assembly-path, --type, and --method are required (unless using --list-keys).");
            ctx.ExitCode = 1;
            return;
        }

        if (!assemblyPath.Exists)
        {
            Console.Error.WriteLine($"Error: Assembly file not found: {assemblyPath.FullName}");
            ctx.ExitCode = 1;
            return;
        }

        // Validate mutual exclusivity of --config and --config-file
        var configJson = ctx.ParseResult.GetValueForOption(_configOption);
        var configFile = ctx.ParseResult.GetValueForOption(_configFileOption);
        if (configJson is not null && configFile is not null)
        {
            Console.Error.WriteLine("Error: --config and --config-file are mutually exclusive. Use one or the other.");
            ctx.ExitCode = 1;
            return;
        }

        using var browser = new AssemblyBrowser(assemblyPath.FullName);
        var methodDef = browser.ResolveMethod(
            type,
            method,
            ctx.ParseResult.GetValueForOption(_parameterTypesOption),
            ctx.ParseResult.GetValueForOption(_overloadIndexOption));

        if (methodDef is null)
        {
            var overloads = browser.ListOverloads(type, method);
            if (overloads.Count == 0)
            {
                Console.Error.WriteLine($"Error: Method '{method}' not found on type '{type}' in assembly '{assemblyPath.Name}'.");
            }
            else
            {
                Console.Error.WriteLine($"Error: Could not resolve method '{method}' on type '{type}'. Found {overloads.Count} overload(s):");
                for (var i = 0; i < overloads.Count; i++)
                {
                    Console.Error.WriteLine($"  [{i}] {overloads[i].FullName}");
                }

                Console.Error.WriteLine("Use --parameter-types or --overload-index to disambiguate.");
            }

            ctx.ExitCode = 1;
            return;
        }

        // Step 1: Start with auto-detect or blank config
        GenerationConfiguration config;
        if (ctx.ParseResult.GetValueForOption(_noAutoDetectOption))
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
                Console.Error.WriteLine(loadResult.Error);
                ctx.ExitCode = 1;
                return;
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
                Console.Error.WriteLine($"Error: Invalid --config JSON: {ex.Message}");
                ctx.ExitCode = 1;
                return;
            }
        }

        // Step 3: Apply shortcut flags
        if (ctx.ParseResult.GetValueForOption(_noMethodBeginOption))
        {
            config.CreateOnMethodBegin = false;
        }

        if (ctx.ParseResult.GetValueForOption(_noMethodEndOption))
        {
            config.CreateOnMethodEnd = false;
        }

        if (ctx.ParseResult.GetValueForOption(_asyncMethodEndOption))
        {
            config.CreateOnAsyncMethodEnd = true;
        }

        // Step 4: Apply --set overrides
        var setValues = ctx.ParseResult.GetValueForOption(_setOption);
        if (setValues is { Length: > 0 })
        {
            var error = ConfigurationApplier.ApplyOverrides(config, setValues);
            if (error is not null)
            {
                Console.Error.WriteLine(error);
                ctx.ExitCode = 1;
                return;
            }
        }

        // Step 5: Generate and output
        var generator = new InstrumentationGenerator();
        var result = generator.Generate(methodDef, config);

        string outputText;
        var json = ctx.ParseResult.GetValueForOption(_jsonOption);
        if (json)
        {
            outputText = JsonOutputFormatter.Format(result, config);
        }
        else if (result.Success)
        {
            outputText = result.SourceCode!;
        }
        else
        {
            Console.Error.WriteLine($"Error: {result.ErrorMessage}");
            ctx.ExitCode = 1;
            return;
        }

        var output = ctx.ParseResult.GetValueForOption(_outputOption);
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
    }
}
