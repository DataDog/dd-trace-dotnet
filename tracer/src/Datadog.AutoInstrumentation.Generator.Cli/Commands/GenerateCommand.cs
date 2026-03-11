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

    // Required arguments
    private readonly Argument<FileInfo> _assemblyPathArg = new("assembly-path", "Path to the .NET assembly (.dll) file");

    // Required options
    private readonly Option<string> _typeOption = new("--type", "Fully qualified type name (e.g., MyLib.MyClass)") { IsRequired = true };
    private readonly Option<string> _methodOption = new("--method", "Method name to instrument") { IsRequired = true };

    // Method resolution
    private readonly Option<string[]?> _parameterTypesOption = new("--parameter-types", "Parameter type full names for overload disambiguation");
    private readonly Option<int?> _overloadIndexOption = new("--overload-index", "0-based overload index for disambiguation");

    // Generation flags
    private readonly Option<bool> _noMethodBeginOption = new("--no-method-begin", "Skip OnMethodBegin handler generation");
    private readonly Option<bool> _noMethodEndOption = new("--no-method-end", "Skip OnMethodEnd handler generation");
    private readonly Option<bool> _asyncMethodEndOption = new("--async-method-end", "Generate OnAsyncMethodEnd handler");
    private readonly Option<bool> _duckCopyStructOption = new("--duck-copy-struct", "Use DuckCopy structs instead of interfaces");

    // Duck type: Instance
    private readonly Option<bool> _duckInstanceOption = new("--duck-instance", "Generate duck type proxy for instance");
    private readonly Option<bool> _duckInstanceFieldsOption = new("--duck-instance-fields", "Include fields in instance duck type");
    private readonly Option<bool> _duckInstancePropertiesOption = new("--duck-instance-properties", "Include properties in instance duck type (default when --duck-instance is set)");
    private readonly Option<bool> _duckInstanceMethodsOption = new("--duck-instance-methods", "Include methods in instance duck type");
    private readonly Option<bool> _duckInstanceChainingOption = new("--duck-instance-chaining", "Enable duck chaining for instance duck type");

    // Duck type: Arguments
    private readonly Option<bool> _duckArgsOption = new("--duck-args", "Generate duck type proxies for arguments");
    private readonly Option<bool> _duckArgsFieldsOption = new("--duck-args-fields", "Include fields in argument duck types");
    private readonly Option<bool> _duckArgsPropertiesOption = new("--duck-args-properties", "Include properties in argument duck types (default when --duck-args is set)");
    private readonly Option<bool> _duckArgsMethodsOption = new("--duck-args-methods", "Include methods in argument duck types");
    private readonly Option<bool> _duckArgsChainingOption = new("--duck-args-chaining", "Enable duck chaining for argument duck types");

    // Duck type: Return Value
    private readonly Option<bool> _duckReturnOption = new("--duck-return", "Generate duck type proxy for return value");
    private readonly Option<bool> _duckReturnFieldsOption = new("--duck-return-fields", "Include fields in return value duck type");
    private readonly Option<bool> _duckReturnPropertiesOption = new("--duck-return-properties", "Include properties in return value duck type (default when --duck-return is set)");
    private readonly Option<bool> _duckReturnMethodsOption = new("--duck-return-methods", "Include methods in return value duck type");
    private readonly Option<bool> _duckReturnChainingOption = new("--duck-return-chaining", "Enable duck chaining for return value duck type");

    // Duck type: Async Return Value
    private readonly Option<bool> _duckAsyncReturnOption = new("--duck-async-return", "Generate duck type proxy for async return value");
    private readonly Option<bool> _duckAsyncReturnFieldsOption = new("--duck-async-return-fields", "Include fields in async return value duck type");
    private readonly Option<bool> _duckAsyncReturnPropertiesOption = new("--duck-async-return-properties", "Include properties in async return value duck type (default when --duck-async-return is set)");
    private readonly Option<bool> _duckAsyncReturnMethodsOption = new("--duck-async-return-methods", "Include methods in async return value duck type");
    private readonly Option<bool> _duckAsyncReturnChainingOption = new("--duck-async-return-chaining", "Enable duck chaining for async return value duck type");

    // Output flags
    private readonly Option<bool> _jsonOption = new("--json", "Output structured JSON instead of source code");
    private readonly Option<FileInfo?> _outputOption = new("--output", "Write output to file instead of stdout");

    // Configuration override
    private readonly Option<string?> _configOption = new("--config", "JSON configuration object to use as base config instead of auto-detect. Accepts the 'configuration' block from --json output. Individual CLI flags still override on top.");

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
        AddOption(_duckCopyStructOption);
        AddOption(_duckInstanceOption);
        AddOption(_duckInstanceFieldsOption);
        AddOption(_duckInstancePropertiesOption);
        AddOption(_duckInstanceMethodsOption);
        AddOption(_duckInstanceChainingOption);
        AddOption(_duckArgsOption);
        AddOption(_duckArgsFieldsOption);
        AddOption(_duckArgsPropertiesOption);
        AddOption(_duckArgsMethodsOption);
        AddOption(_duckArgsChainingOption);
        AddOption(_duckReturnOption);
        AddOption(_duckReturnFieldsOption);
        AddOption(_duckReturnPropertiesOption);
        AddOption(_duckReturnMethodsOption);
        AddOption(_duckReturnChainingOption);
        AddOption(_duckAsyncReturnOption);
        AddOption(_duckAsyncReturnFieldsOption);
        AddOption(_duckAsyncReturnPropertiesOption);
        AddOption(_duckAsyncReturnMethodsOption);
        AddOption(_duckAsyncReturnChainingOption);
        AddOption(_jsonOption);
        AddOption(_outputOption);
        AddOption(_configOption);
        AddOption(_noAutoDetectOption);

        this.SetHandler(Execute);
    }

    private void Execute(InvocationContext ctx)
    {
        var assemblyPath = ctx.ParseResult.GetValueForArgument(_assemblyPathArg);
        var type = ctx.ParseResult.GetValueForOption(_typeOption)!;
        var method = ctx.ParseResult.GetValueForOption(_methodOption)!;

        if (!assemblyPath.Exists)
        {
            Console.Error.WriteLine($"Error: Assembly file not found: {assemblyPath.FullName}");
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

        var configJson = ctx.ParseResult.GetValueForOption(_configOption);
        GenerationConfiguration config;
        if (configJson is not null)
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
        else if (ctx.ParseResult.GetValueForOption(_noAutoDetectOption))
        {
            config = new GenerationConfiguration();
        }
        else
        {
            config = GenerationConfiguration.CreateForMethod(methodDef);
        }

        ApplyCliOverrides(ctx, config);

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

    private void ApplyCliOverrides(InvocationContext ctx, GenerationConfiguration config)
    {
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

        if (ctx.ParseResult.GetValueForOption(_duckCopyStructOption))
        {
            config.UseDuckCopyStruct = true;
        }

        if (ctx.ParseResult.GetValueForOption(_duckInstanceOption))
        {
            config.CreateDucktypeInstance = true;
        }

        if (ctx.ParseResult.GetValueForOption(_duckInstanceFieldsOption))
        {
            config.DucktypeInstanceFields = true;
        }

        if (ctx.ParseResult.GetValueForOption(_duckInstancePropertiesOption))
        {
            config.DucktypeInstanceProperties = true;
        }

        if (ctx.ParseResult.GetValueForOption(_duckInstanceMethodsOption))
        {
            config.DucktypeInstanceMethods = true;
        }

        if (ctx.ParseResult.GetValueForOption(_duckInstanceChainingOption))
        {
            config.DucktypeInstanceDuckChaining = true;
        }

        if (ctx.ParseResult.GetValueForOption(_duckArgsOption))
        {
            config.CreateDucktypeArguments = true;
        }

        if (ctx.ParseResult.GetValueForOption(_duckArgsFieldsOption))
        {
            config.DucktypeArgumentsFields = true;
        }

        if (ctx.ParseResult.GetValueForOption(_duckArgsPropertiesOption))
        {
            config.DucktypeArgumentsProperties = true;
        }

        if (ctx.ParseResult.GetValueForOption(_duckArgsMethodsOption))
        {
            config.DucktypeArgumentsMethods = true;
        }

        if (ctx.ParseResult.GetValueForOption(_duckArgsChainingOption))
        {
            config.DucktypeArgumentsDuckChaining = true;
        }

        if (ctx.ParseResult.GetValueForOption(_duckReturnOption))
        {
            config.CreateDucktypeReturnValue = true;
        }

        if (ctx.ParseResult.GetValueForOption(_duckReturnFieldsOption))
        {
            config.DucktypeReturnValueFields = true;
        }

        if (ctx.ParseResult.GetValueForOption(_duckReturnPropertiesOption))
        {
            config.DucktypeReturnValueProperties = true;
        }

        if (ctx.ParseResult.GetValueForOption(_duckReturnMethodsOption))
        {
            config.DucktypeReturnValueMethods = true;
        }

        if (ctx.ParseResult.GetValueForOption(_duckReturnChainingOption))
        {
            config.DucktypeReturnValueDuckChaining = true;
        }

        if (ctx.ParseResult.GetValueForOption(_duckAsyncReturnOption))
        {
            config.CreateDucktypeAsyncReturnValue = true;
        }

        if (ctx.ParseResult.GetValueForOption(_duckAsyncReturnFieldsOption))
        {
            config.DucktypeAsyncReturnValueFields = true;
        }

        if (ctx.ParseResult.GetValueForOption(_duckAsyncReturnPropertiesOption))
        {
            config.DucktypeAsyncReturnValueProperties = true;
        }

        if (ctx.ParseResult.GetValueForOption(_duckAsyncReturnMethodsOption))
        {
            config.DucktypeAsyncReturnValueMethods = true;
        }

        if (ctx.ParseResult.GetValueForOption(_duckAsyncReturnChainingOption))
        {
            config.DucktypeAsyncReturnValueDuckChaining = true;
        }
    }
}
