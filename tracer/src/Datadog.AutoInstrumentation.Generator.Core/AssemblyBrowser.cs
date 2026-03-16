// <copyright file="AssemblyBrowser.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using dnlib.DotNet;

namespace Datadog.AutoInstrumentation.Generator.Core;

/// <summary>
/// Thin wrapper around dnlib for resolving methods from assemblies.
/// Assembly browsing (type listing, etc.) is handled by dotnet-inspect.
/// </summary>
public class AssemblyBrowser : IDisposable
{
    private readonly AssemblyDef _assemblyDef;

    public AssemblyBrowser(string path)
    {
        _assemblyDef = AssemblyDef.Load(path);
    }

    public AssemblyBrowser(Stream stream)
    {
        _assemblyDef = AssemblyDef.Load(stream);
    }

    /// <summary>
    /// Resolves a method by type full name and method name.
    /// Supports overload disambiguation via parameter types or overload index.
    /// </summary>
    /// <param name="typeFullName">Fully qualified type name (e.g., "MyLib.MyClass")</param>
    /// <param name="methodName">Method name</param>
    /// <param name="parameterTypes">Optional parameter type full names for overload disambiguation</param>
    /// <param name="overloadIndex">Optional 0-based index among matching overloads</param>
    /// <returns>The resolved MethodDef, or null if not found</returns>
    public MethodDef? ResolveMethod(string typeFullName, string methodName, string[]? parameterTypes = null, int? overloadIndex = null)
    {
        TypeDef? typeDef = null;

        foreach (var module in _assemblyDef.Modules)
        {
            typeDef = FindType(module, typeFullName);
            if (typeDef is not null)
            {
                break;
            }
        }

        if (typeDef is null)
        {
            return null;
        }

        var methods = typeDef.Methods.Where(m => m.Name.String == methodName).ToList();

        if (methods.Count == 0)
        {
            return null;
        }

        // Apply disambiguation
        if (parameterTypes is { Length: > 0 })
        {
            return methods.FirstOrDefault(m => MatchesParameterTypes(m, parameterTypes));
        }

        if (overloadIndex.HasValue)
        {
            if (overloadIndex.Value >= 0 && overloadIndex.Value < methods.Count)
            {
                return methods[overloadIndex.Value];
            }

            return null;
        }

        if (methods.Count == 1)
        {
            return methods[0];
        }

        // Multiple overloads with no disambiguation — return null to trigger helpful error
        return null;
    }

    /// <summary>
    /// Lists all available overloads for a method, useful for disambiguation.
    /// </summary>
    public IReadOnlyList<MethodDef> ListOverloads(string typeFullName, string methodName)
    {
        TypeDef? typeDef = null;

        foreach (var module in _assemblyDef.Modules)
        {
            typeDef = FindType(module, typeFullName);
            if (typeDef is not null)
            {
                break;
            }
        }

        if (typeDef is null)
        {
            return Array.Empty<MethodDef>();
        }

        return typeDef.Methods.Where(m => m.Name == methodName).ToList();
    }

    /// <summary>
    /// Lists all non-compiler-generated types in the assembly.
    /// </summary>
    public IReadOnlyList<TypeDef> ListTypes()
    {
        var result = new List<TypeDef>();
        foreach (var module in _assemblyDef.Modules)
        {
            foreach (var type in module.GetTypes())
            {
                if (IsCompilerGenerated(type) || type.Name == "<Module>")
                {
                    continue;
                }

                result.Add(type);
            }
        }

        return result;
    }

    /// <summary>
    /// Lists all instrumentable methods on a type (excludes compiler-generated and special-name methods).
    /// </summary>
    public IReadOnlyList<MethodDef> ListMethods(string typeFullName)
    {
        TypeDef? typeDef = null;
        foreach (var module in _assemblyDef.Modules)
        {
            typeDef = FindType(module, typeFullName);
            if (typeDef is not null)
            {
                break;
            }
        }

        if (typeDef is null)
        {
            return Array.Empty<MethodDef>();
        }

        return typeDef.Methods
            .Where(m => !m.IsSpecialName && !IsCompilerGenerated(m))
            .ToList();
    }

    public void Dispose()
    {
        // AssemblyDef doesn't implement IDisposable, but we free module contexts
        foreach (var module in _assemblyDef.Modules)
        {
            module?.Dispose();
        }
    }

    private static TypeDef? FindType(ModuleDef module, string typeFullName)
    {
        // Handle nested types: "Outer+Inner" or "Outer/Inner"
        var parts = typeFullName.Split('+', '/');

        TypeDef? current = null;
        foreach (var part in parts)
        {
            if (current is null)
            {
                current = module.Find(part, isReflectionName: false)
                       ?? module.Types.FirstOrDefault(t => t.FullName == part);
            }
            else
            {
                current = current.NestedTypes.FirstOrDefault(t => t.Name == part);
            }

            if (current is null)
            {
                return null;
            }
        }

        return current;
    }

    private static bool IsCompilerGenerated(IHasCustomAttribute member)
    {
        return member.CustomAttributes.Any(a => a.TypeFullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute");
    }

    private static bool MatchesParameterTypes(MethodDef method, string[] parameterTypes)
    {
        var parameters = method.Parameters.Where(p => !p.IsHiddenThisParameter).ToArray();
        if (parameters.Length != parameterTypes.Length)
        {
            return false;
        }

        for (var i = 0; i < parameters.Length; i++)
        {
            var paramFullName = parameters[i].Type.FullName;
            if (!string.Equals(paramFullName, parameterTypes[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
