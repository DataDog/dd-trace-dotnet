// <copyright file="InspectCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using Datadog.AutoInstrumentation.Generator.Cli.Output;
using Datadog.AutoInstrumentation.Generator.Core;

namespace Datadog.AutoInstrumentation.Generator.Cli.Commands;

internal class InspectCommand : Command
{
    private readonly Argument<FileInfo?> _assemblyPathArg = new("assembly-path") { Arity = ArgumentArity.ExactlyOne, Description = "Path to the .NET assembly (.dll) file" };
    private readonly Option<bool> _listTypesOption = new("--list-types") { Description = "List all non-compiler-generated types in the assembly" };
    private readonly Option<string?> _listMethodsOption = new("--list-methods") { Description = "List all instrumentable methods on the specified type" };
    private readonly Option<bool> _jsonOption;

    public InspectCommand(Option<bool> jsonOption)
        : base("inspect", "Inspect an assembly to discover types and methods for instrumentation")
    {
        _jsonOption = jsonOption;

        Add(_assemblyPathArg);
        Add(_listTypesOption);
        Add(_listMethodsOption);

        SetAction(Execute);
    }

    private static int ExecuteListTypes(AssemblyBrowser browser, bool jsonMode)
    {
        var types = browser.ListTypes();
        var dtos = types.Select(t => new TypeInfoDto
        {
            FullName = t.FullName,
            Namespace = t.Namespace?.String ?? string.Empty,
            Name = t.Name.String,
            IsPublic = t.IsPublic || t.IsNestedPublic,
            IsInterface = t.IsInterface,
            IsAbstract = t.IsAbstract && !t.IsInterface,
            IsSealed = t.IsSealed,
            IsValueType = t.IsValueType,
            MethodCount = t.Methods.Count,
            NestedTypes = t.NestedTypes.Count,
        }).ToList();

        if (jsonMode)
        {
            return OutputHelper.WriteSuccess(true, "inspect", new { types = dtos });
        }

        Console.WriteLine($"{"Type",-60} {"Vis",-7} {"Kind",-12} {"Methods",7}");
        Console.WriteLine($"{new string('-', 60)} {new string('-', 7)} {new string('-', 12)} {new string('-', 7)}");
        foreach (var dto in dtos)
        {
            var vis = dto.IsPublic ? "public" : "non-pub";
            var kind = dto.IsInterface ? "interface"
                : dto.IsValueType ? "struct"
                : dto.IsAbstract ? "abstract"
                : dto.IsSealed ? "sealed"
                : "class";
            Console.WriteLine($"{dto.FullName,-60} {vis,-7} {kind,-12} {dto.MethodCount,7}");
        }

        return 0;
    }

    private static int ExecuteListMethods(AssemblyBrowser browser, string typeFullName, bool jsonMode)
    {
        var methods = browser.ListMethods(typeFullName);
        if (methods.Count == 0)
        {
            return OutputHelper.WriteError(
                jsonMode,
                "inspect",
                ErrorCodes.TypeNotFound,
                $"Error: Type '{typeFullName}' not found or has no instrumentable methods.");
        }

        // Group by name to compute overload indices
        var byName = methods.GroupBy(m => m.Name.String).ToDictionary(g => g.Key, g => g.ToList());

        var dtos = new List<MethodInfoDto>();
        foreach (var method in methods)
        {
            var group = byName[method.Name.String];
            var overloadIndex = group.IndexOf(method);
            var parameters = method.Parameters
                .Where(p => !p.IsHiddenThisParameter)
                .Select((p, i) => new ParameterInfoDto
                {
                    Name = p.Name ?? $"arg{i}",
                    Type = p.Type.FullName,
                    Index = i,
                })
                .ToList();

            var isAsync = GenerationConfiguration.IsAsyncReturnType(method.ReturnType.FullName);

            dtos.Add(new MethodInfoDto
            {
                Name = method.Name.String,
                FullName = method.FullName,
                ReturnType = method.ReturnType.FullName,
                IsPublic = method.IsPublic,
                IsStatic = method.IsStatic,
                IsVirtual = method.IsVirtual,
                IsAsync = isAsync,
                Parameters = parameters,
                OverloadIndex = overloadIndex,
                OverloadCount = group.Count,
            });
        }

        if (jsonMode)
        {
            return OutputHelper.WriteSuccess(true, "inspect", new { type = typeFullName, methods = dtos });
        }

        Console.WriteLine($"Methods on {typeFullName}:");
        Console.WriteLine();
        foreach (var dto in dtos)
        {
            var overloadHint = dto.OverloadCount > 1 ? $" [overload {dto.OverloadIndex}/{dto.OverloadCount}]" : string.Empty;
            var modifiers = string.Join(" ", new[]
            {
                dto.IsPublic ? "public" : "non-public",
                dto.IsStatic ? "static" : null,
                dto.IsVirtual ? "virtual" : null,
                dto.IsAsync ? "async" : null,
            }.Where(s => s is not null));

            var paramList = string.Join(", ", dto.Parameters.Select(p => $"{p.Type} {p.Name}"));
            Console.WriteLine($"  {dto.ReturnType} {dto.Name}({paramList})  [{modifiers}]{overloadHint}");
        }

        return 0;
    }

    private int Execute(ParseResult parseResult)
    {
        var jsonMode = parseResult.GetValue(_jsonOption);
        var assemblyPath = parseResult.GetValue(_assemblyPathArg);
        var listTypes = parseResult.GetValue(_listTypesOption);
        var listMethods = parseResult.GetValue(_listMethodsOption);

        if (!listTypes && listMethods is null)
        {
            return OutputHelper.WriteError(
                jsonMode,
                "inspect",
                ErrorCodes.InvalidArgument,
                "Error: Specify --list-types or --list-methods <type>.");
        }

        if (assemblyPath is null || !assemblyPath.Exists)
        {
            return OutputHelper.WriteError(
                jsonMode,
                "inspect",
                ErrorCodes.FileNotFound,
                $"Error: Assembly file not found: {assemblyPath?.FullName ?? "<not specified>"}");
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
                "inspect",
                ErrorCodes.BadAssembly,
                $"Error: Failed to load assembly '{assemblyPath.Name}': {ex.Message}");
        }

        using var disposableBrowser = browser;

        if (listTypes)
        {
            return ExecuteListTypes(browser, jsonMode);
        }

        return ExecuteListMethods(browser, listMethods!, jsonMode);
    }
}
