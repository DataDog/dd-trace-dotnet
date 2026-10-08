// <copyright file="ReflectionRootDescriptor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Datadog.Trace.Tools.Runner.Aot;

/// <summary>
/// Some integrations of Datadog.Trace find types of the libraries they instrument by name (<c>Type.GetType</c>,
/// <c>Assembly.GetType</c>) and then their members by reflection (constructors, methods, properties): ILC only keeps that
/// metadata for what it can see or is told about. This writes an ILLink descriptor that preserves the types of the
/// application named by the string literals of the Datadog.Trace methods that look types up by name.
/// </summary>
internal static class ReflectionRootDescriptor
{
    private static readonly HashSet<string> TypeLookupDeclaringTypes = new(StringComparer.Ordinal) { "System.Type", "System.Reflection.Assembly", "System.Reflection.Module" };

    /// <returns>The number of types preserved.</returns>
    public static int Write(ModuleDef datadogTrace, IEnumerable<ModuleDef> modules, string descriptorPath)
    {
        var names = CollectTypeNames(datadogTrace);
        var linker = new XElement("linker");
        var count = 0;
        foreach (var module in modules.Where(m => m != datadogTrace && m.Assembly is not null).OrderBy(m => m.Assembly.Name.String, StringComparer.Ordinal))
        {
            var assemblyName = module.Assembly.Name.String;
            var types = names.Where(n => n.AssemblyName is null || string.Equals(n.AssemblyName, assemblyName, StringComparison.OrdinalIgnoreCase))
                             .Select(n => module.Find(n.TypeName, isReflectionName: true))
                             .OfType<TypeDef>()
                             .Select(t => t.FullName)
                             .Distinct(StringComparer.Ordinal)
                             .OrderBy(t => t, StringComparer.Ordinal)
                             .ToList();
            if (types.Count == 0)
            {
                continue;
            }

            var assembly = new XElement("assembly", new XAttribute("fullname", assemblyName));
            foreach (var type in types)
            {
                assembly.Add(new XElement("type", new XAttribute("fullname", type), new XAttribute("preserve", "all")));
            }

            linker.Add(assembly);
            count += types.Count;
        }

        new XDocument(linker).Save(descriptorPath);
        return count;
    }

    /// <summary>
    /// Gets the string literals of the methods that look types up by name, as type names with their assembly when they
    /// are qualified: most aren't type names, and only those that name a type of the application are kept.
    /// </summary>
    internal static HashSet<(string TypeName, string? AssemblyName)> CollectTypeNames(ModuleDef datadogTrace)
    {
        var names = new HashSet<(string TypeName, string? AssemblyName)>();
        foreach (var method in datadogTrace.GetTypes().SelectMany(t => t.Methods).Where(m => m.HasBody))
        {
            var instructions = method.Body.Instructions;
            if (!instructions.Any(IsTypeLookup))
            {
                continue;
            }

            foreach (var instruction in instructions)
            {
                if (instruction.OpCode.Code == Code.Ldstr && instruction.Operand is string value && ParseTypeName(value) is { } name)
                {
                    names.Add(name);
                }
            }
        }

        return names;
    }

    private static bool IsTypeLookup(Instruction instruction)
        => instruction.OpCode.FlowControl == FlowControl.Call
        && instruction.Operand is IMethod { Name.String: "GetType", MethodSig.Params.Count: > 0 } method
        && method.MethodSig.Params[0].ElementType == ElementType.String
        && method.DeclaringType is { } declaringType
        && TypeLookupDeclaringTypes.Contains(declaringType.FullName);

    private static (string TypeName, string? AssemblyName)? ParseTypeName(string value)
    {
        // "Namespace.Type" or "Namespace.Type, Assembly[, Version=…]"; generic arguments aren't looked up this way.
        var comma = value.IndexOf(',');
        var typeName = (comma < 0 ? value : value.Substring(0, comma)).Trim();
        var assemblyName = comma < 0 ? null : value.Substring(comma + 1).Split(',')[0].Trim();
        if (typeName.Length == 0 || typeName.IndexOfAny([' ', '[', '/', '\\', '{', '<']) >= 0 || typeName.IndexOf('.') < 0)
        {
            return null;
        }

        return (typeName, StringUtil.IsNullOrEmpty(assemblyName) ? null : assemblyName);
    }
}
#endif
