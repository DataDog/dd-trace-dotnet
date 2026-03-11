// <copyright file="GenerateCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using Datadog.AutoInstrumentation.Generator.Cli.Output;
using Datadog.AutoInstrumentation.Generator.Core;

namespace Datadog.AutoInstrumentation.Generator.Cli.Commands;

internal class GenerateCommand : Command
{
    public GenerateCommand()
        : base("generate", "Generate CallTarget auto-instrumentation code for a method")
    {
        // Required arguments
        var assemblyPathArg = new Argument<FileInfo>("assembly-path", "Path to the .NET assembly (.dll) file");
        AddArgument(assemblyPathArg);

        // Required options
        var typeOption = new Option<string>("--type", "Fully qualified type name (e.g., MyLib.MyClass)") { IsRequired = true };
        typeOption.AddAlias("-t");
        AddOption(typeOption);

        var methodOption = new Option<string>("--method", "Method name to instrument") { IsRequired = true };
        methodOption.AddAlias("-m");
        AddOption(methodOption);

        // Method resolution
        var parameterTypesOption = new Option<string[]?>("--parameter-types", "Parameter type full names for overload disambiguation");
        AddOption(parameterTypesOption);

        var overloadIndexOption = new Option<int?>("--overload-index", "0-based overload index for disambiguation");
        AddOption(overloadIndexOption);

        // Generation flags
        var noMethodBeginOption = new Option<bool>("--no-method-begin", "Skip OnMethodBegin handler generation");
        AddOption(noMethodBeginOption);

        var noMethodEndOption = new Option<bool>("--no-method-end", "Skip OnMethodEnd handler generation");
        AddOption(noMethodEndOption);

        var asyncMethodEndOption = new Option<bool>("--async-method-end", "Generate OnAsyncMethodEnd handler");
        AddOption(asyncMethodEndOption);

        var duckCopyStructOption = new Option<bool>("--duck-copy-struct", "Use DuckCopy structs instead of interfaces");
        AddOption(duckCopyStructOption);

        // Duck type: Instance
        var duckInstanceOption = new Option<bool>("--duck-instance", "Generate duck type proxy for instance");
        AddOption(duckInstanceOption);

        var duckInstanceFieldsOption = new Option<bool>("--duck-instance-fields", "Include fields in instance duck type");
        AddOption(duckInstanceFieldsOption);

        var duckInstancePropertiesOption = new Option<bool>("--duck-instance-properties", "Include properties in instance duck type (default when --duck-instance is set)");
        AddOption(duckInstancePropertiesOption);

        var duckInstanceMethodsOption = new Option<bool>("--duck-instance-methods", "Include methods in instance duck type");
        AddOption(duckInstanceMethodsOption);

        var duckInstanceChainingOption = new Option<bool>("--duck-instance-chaining", "Enable duck chaining for instance duck type");
        AddOption(duckInstanceChainingOption);

        // Duck type: Arguments
        var duckArgsOption = new Option<bool>("--duck-args", "Generate duck type proxies for arguments");
        AddOption(duckArgsOption);

        var duckArgsFieldsOption = new Option<bool>("--duck-args-fields", "Include fields in argument duck types");
        AddOption(duckArgsFieldsOption);

        var duckArgsPropertiesOption = new Option<bool>("--duck-args-properties", "Include properties in argument duck types (default when --duck-args is set)");
        AddOption(duckArgsPropertiesOption);

        var duckArgsMethodsOption = new Option<bool>("--duck-args-methods", "Include methods in argument duck types");
        AddOption(duckArgsMethodsOption);

        var duckArgsChainingOption = new Option<bool>("--duck-args-chaining", "Enable duck chaining for argument duck types");
        AddOption(duckArgsChainingOption);

        // Duck type: Return Value
        var duckReturnOption = new Option<bool>("--duck-return", "Generate duck type proxy for return value");
        AddOption(duckReturnOption);

        var duckReturnFieldsOption = new Option<bool>("--duck-return-fields", "Include fields in return value duck type");
        AddOption(duckReturnFieldsOption);

        var duckReturnPropertiesOption = new Option<bool>("--duck-return-properties", "Include properties in return value duck type (default when --duck-return is set)");
        AddOption(duckReturnPropertiesOption);

        var duckReturnMethodsOption = new Option<bool>("--duck-return-methods", "Include methods in return value duck type");
        AddOption(duckReturnMethodsOption);

        var duckReturnChainingOption = new Option<bool>("--duck-return-chaining", "Enable duck chaining for return value duck type");
        AddOption(duckReturnChainingOption);

        // Duck type: Async Return Value
        var duckAsyncReturnOption = new Option<bool>("--duck-async-return", "Generate duck type proxy for async return value");
        AddOption(duckAsyncReturnOption);

        var duckAsyncReturnFieldsOption = new Option<bool>("--duck-async-return-fields", "Include fields in async return value duck type");
        AddOption(duckAsyncReturnFieldsOption);

        var duckAsyncReturnPropertiesOption = new Option<bool>("--duck-async-return-properties", "Include properties in async return value duck type (default when --duck-async-return is set)");
        AddOption(duckAsyncReturnPropertiesOption);

        var duckAsyncReturnMethodsOption = new Option<bool>("--duck-async-return-methods", "Include methods in async return value duck type");
        AddOption(duckAsyncReturnMethodsOption);

        var duckAsyncReturnChainingOption = new Option<bool>("--duck-async-return-chaining", "Enable duck chaining for async return value duck type");
        AddOption(duckAsyncReturnChainingOption);

        // Output flags
        var jsonOption = new Option<bool>("--json", "Output structured JSON instead of source code");
        AddOption(jsonOption);

        var outputOption = new Option<FileInfo?>("--output", "Write output to file instead of stdout");
        outputOption.AddAlias("-o");
        AddOption(outputOption);

        // Auto-detect flag
        var noAutoDetectOption = new Option<bool>("--no-auto-detect", "Disable smart defaults (async detection, static method handling)");
        AddOption(noAutoDetectOption);

        this.SetHandler((InvocationContext ctx) =>
        {
            var assemblyPath = ctx.ParseResult.GetValueForArgument(assemblyPathArg);
            var type = ctx.ParseResult.GetValueForOption(typeOption)!;
            var method = ctx.ParseResult.GetValueForOption(methodOption)!;
            var parameterTypes = ctx.ParseResult.GetValueForOption(parameterTypesOption);
            var overloadIndex = ctx.ParseResult.GetValueForOption(overloadIndexOption);
            var noMethodBegin = ctx.ParseResult.GetValueForOption(noMethodBeginOption);
            var noMethodEnd = ctx.ParseResult.GetValueForOption(noMethodEndOption);
            var asyncMethodEnd = ctx.ParseResult.GetValueForOption(asyncMethodEndOption);
            var duckCopyStruct = ctx.ParseResult.GetValueForOption(duckCopyStructOption);
            var duckInstance = ctx.ParseResult.GetValueForOption(duckInstanceOption);
            var duckInstanceFields = ctx.ParseResult.GetValueForOption(duckInstanceFieldsOption);
            var duckInstanceProperties = ctx.ParseResult.GetValueForOption(duckInstancePropertiesOption);
            var duckInstanceMethods = ctx.ParseResult.GetValueForOption(duckInstanceMethodsOption);
            var duckInstanceChaining = ctx.ParseResult.GetValueForOption(duckInstanceChainingOption);
            var duckArgs = ctx.ParseResult.GetValueForOption(duckArgsOption);
            var duckArgsFields = ctx.ParseResult.GetValueForOption(duckArgsFieldsOption);
            var duckArgsProperties = ctx.ParseResult.GetValueForOption(duckArgsPropertiesOption);
            var duckArgsMethods = ctx.ParseResult.GetValueForOption(duckArgsMethodsOption);
            var duckArgsChaining = ctx.ParseResult.GetValueForOption(duckArgsChainingOption);
            var duckReturn = ctx.ParseResult.GetValueForOption(duckReturnOption);
            var duckReturnFields = ctx.ParseResult.GetValueForOption(duckReturnFieldsOption);
            var duckReturnProperties = ctx.ParseResult.GetValueForOption(duckReturnPropertiesOption);
            var duckReturnMethods = ctx.ParseResult.GetValueForOption(duckReturnMethodsOption);
            var duckReturnChaining = ctx.ParseResult.GetValueForOption(duckReturnChainingOption);
            var duckAsyncReturn = ctx.ParseResult.GetValueForOption(duckAsyncReturnOption);
            var duckAsyncReturnFields = ctx.ParseResult.GetValueForOption(duckAsyncReturnFieldsOption);
            var duckAsyncReturnProperties = ctx.ParseResult.GetValueForOption(duckAsyncReturnPropertiesOption);
            var duckAsyncReturnMethods = ctx.ParseResult.GetValueForOption(duckAsyncReturnMethodsOption);
            var duckAsyncReturnChaining = ctx.ParseResult.GetValueForOption(duckAsyncReturnChainingOption);
            var json = ctx.ParseResult.GetValueForOption(jsonOption);
            var output = ctx.ParseResult.GetValueForOption(outputOption);
            var noAutoDetect = ctx.ParseResult.GetValueForOption(noAutoDetectOption);

            ctx.ExitCode = Execute(
                assemblyPath,
                type,
                method,
                parameterTypes,
                overloadIndex,
                noMethodBegin,
                noMethodEnd,
                asyncMethodEnd,
                duckCopyStruct,
                duckInstance,
                duckInstanceFields,
                duckInstanceProperties,
                duckInstanceMethods,
                duckInstanceChaining,
                duckArgs,
                duckArgsFields,
                duckArgsProperties,
                duckArgsMethods,
                duckArgsChaining,
                duckReturn,
                duckReturnFields,
                duckReturnProperties,
                duckReturnMethods,
                duckReturnChaining,
                duckAsyncReturn,
                duckAsyncReturnFields,
                duckAsyncReturnProperties,
                duckAsyncReturnMethods,
                duckAsyncReturnChaining,
                json,
                output,
                noAutoDetect);
        });
    }

    private static int Execute(
        FileInfo assemblyPath,
        string type,
        string method,
        string[]? parameterTypes,
        int? overloadIndex,
        bool noMethodBegin,
        bool noMethodEnd,
        bool asyncMethodEnd,
        bool duckCopyStruct,
        bool duckInstance,
        bool duckInstanceFields,
        bool duckInstanceProperties,
        bool duckInstanceMethods,
        bool duckInstanceChaining,
        bool duckArgs,
        bool duckArgsFields,
        bool duckArgsProperties,
        bool duckArgsMethods,
        bool duckArgsChaining,
        bool duckReturn,
        bool duckReturnFields,
        bool duckReturnProperties,
        bool duckReturnMethods,
        bool duckReturnChaining,
        bool duckAsyncReturn,
        bool duckAsyncReturnFields,
        bool duckAsyncReturnProperties,
        bool duckAsyncReturnMethods,
        bool duckAsyncReturnChaining,
        bool json,
        FileInfo? output,
        bool noAutoDetect)
    {
        if (!assemblyPath.Exists)
        {
            Console.Error.WriteLine($"Error: Assembly file not found: {assemblyPath.FullName}");
            return 1;
        }

        using var browser = new AssemblyBrowser(assemblyPath.FullName);
        var methodDef = browser.ResolveMethod(type, method, parameterTypes, overloadIndex);

        if (methodDef is null)
        {
            // If method not found, try listing overloads to provide helpful error
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

            return 1;
        }

        // Build configuration: start with auto-detect defaults, then apply CLI overrides
        GenerationConfiguration config;
        if (noAutoDetect)
        {
            config = new GenerationConfiguration();
        }
        else
        {
            config = GenerationConfiguration.CreateForMethod(methodDef);
        }

        // Apply explicit CLI overrides
        if (noMethodBegin)
        {
            config.CreateOnMethodBegin = false;
        }

        if (noMethodEnd)
        {
            config.CreateOnMethodEnd = false;
        }

        if (asyncMethodEnd)
        {
            config.CreateOnAsyncMethodEnd = true;
        }

        if (duckCopyStruct)
        {
            config.UseDuckCopyStruct = true;
        }

        if (duckInstance)
        {
            config.CreateDucktypeInstance = true;
        }

        if (duckInstanceFields)
        {
            config.DucktypeInstanceFields = true;
        }

        if (duckInstanceProperties)
        {
            config.DucktypeInstanceProperties = true;
        }

        if (duckInstanceMethods)
        {
            config.DucktypeInstanceMethods = true;
        }

        if (duckInstanceChaining)
        {
            config.DucktypeInstanceDuckChaining = true;
        }

        if (duckArgs)
        {
            config.CreateDucktypeArguments = true;
        }

        if (duckArgsFields)
        {
            config.DucktypeArgumentsFields = true;
        }

        if (duckArgsProperties)
        {
            config.DucktypeArgumentsProperties = true;
        }

        if (duckArgsMethods)
        {
            config.DucktypeArgumentsMethods = true;
        }

        if (duckArgsChaining)
        {
            config.DucktypeArgumentsDuckChaining = true;
        }

        if (duckReturn)
        {
            config.CreateDucktypeReturnValue = true;
        }

        if (duckReturnFields)
        {
            config.DucktypeReturnValueFields = true;
        }

        if (duckReturnProperties)
        {
            config.DucktypeReturnValueProperties = true;
        }

        if (duckReturnMethods)
        {
            config.DucktypeReturnValueMethods = true;
        }

        if (duckReturnChaining)
        {
            config.DucktypeReturnValueDuckChaining = true;
        }

        if (duckAsyncReturn)
        {
            config.CreateDucktypeAsyncReturnValue = true;
        }

        if (duckAsyncReturnFields)
        {
            config.DucktypeAsyncReturnValueFields = true;
        }

        if (duckAsyncReturnProperties)
        {
            config.DucktypeAsyncReturnValueProperties = true;
        }

        if (duckAsyncReturnMethods)
        {
            config.DucktypeAsyncReturnValueMethods = true;
        }

        if (duckAsyncReturnChaining)
        {
            config.DucktypeAsyncReturnValueDuckChaining = true;
        }

        var generator = new InstrumentationGenerator();
        var result = generator.Generate(methodDef, config);

        string outputText;
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
            return 1;
        }

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
