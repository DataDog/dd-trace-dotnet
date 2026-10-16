// <copyright file="CompositeInterfaceAssembly.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using dnlib.DotNet;

namespace Datadog.Trace.Tools.Runner.Aot;

/// <summary>
/// Generates the composite interfaces Datadog.Trace declares with <c>DuckTypeCompositeInterfaceAttribute</c> (an interface
/// that inherits several interfaces of a library, for a reverse proxy that implements all of them), which dynamic duck
/// typing emits at runtime: an assembly with the same name and types, whose mappings the JIT runs record. Only the
/// interfaces whose inherited interfaces are all in the application are generated; the runtime finds them by name.
/// </summary>
internal static class CompositeInterfaceAssembly
{
    // DuckType.CompositeInterfacesAssemblyName and DuckType.CompositeInterfacesNamespace.
    public const string AssemblyName = "Datadog.Trace.DuckType.Composites";
    public const string Namespace = "Datadog.Trace.DuckTyping.Composites";

    private const string AttributeType = "Datadog.Trace.DuckTyping.DuckTypeCompositeInterfaceAttribute";

    /// <summary>
    /// Writes the assembly, and a descriptor that keeps its interfaces for the lookups by name, when there is any.
    /// </summary>
    /// <returns>The path of the assembly, or null when no composite interface applies to the application.</returns>
    public static string? Write(ModuleDef datadogTrace, IReadOnlyCollection<ModuleDef> modules, ModuleDef application, string outputDirectory, out int count)
    {
        var interfaces = Collect(datadogTrace, modules);
        count = interfaces.Count;
        var path = Path.Combine(outputDirectory, AssemblyName + ".dll");
        var descriptorPath = Path.Combine(outputDirectory, "Datadog.Trace.Composites.linker.xml");
        if (count == 0)
        {
            File.Delete(path);
            File.Delete(descriptorPath);
            return null;
        }

        var module = new ModuleDefUser(AssemblyName + ".dll", Guid.NewGuid(), application.CorLibTypes.AssemblyRef) { Kind = ModuleKind.Dll, RuntimeVersion = application.RuntimeVersion };
        new AssemblyDefUser(AssemblyName, new Version(1, 0, 0, 0)).Modules.Add(module);
        var descriptor = new XElement("assembly", new XAttribute("fullname", AssemblyName));
        foreach (var (name, inherited) in interfaces)
        {
            var type = new TypeDefUser(Namespace, name) { Attributes = TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract };
            foreach (var parent in inherited)
            {
                type.Interfaces.Add(new InterfaceImplUser(module.Import(parent)));
            }

            module.Types.Add(type);
            descriptor.Add(new XElement("type", new XAttribute("fullname", type.FullName), new XAttribute("preserve", "all")));
        }

        module.Write(path);
        new XDocument(new XElement("linker", descriptor)).Save(descriptorPath);
        return path;
    }

    /// <summary>
    /// Gets the composite interfaces Datadog.Trace declares whose inherited interfaces are all types of the modules.
    /// </summary>
    internal static List<(string Name, List<TypeDef> Interfaces)> Collect(ModuleDef datadogTrace, IEnumerable<ModuleDef> modules)
    {
        var byAssembly = modules.Where(m => m.Assembly is not null)
                                .GroupBy(m => m.Assembly.Name.String, StringComparer.OrdinalIgnoreCase)
                                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var result = new List<(string Name, List<TypeDef> Interfaces)>();
        foreach (var attribute in datadogTrace.Assembly.CustomAttributes.Where(a => a.AttributeType.FullName == AttributeType))
        {
            if (attribute.ConstructorArguments.Count != 2
             || attribute.ConstructorArguments[0].Value is not UTF8String name
             || attribute.ConstructorArguments[1].Value is not IList<CAArgument> names)
            {
                continue;
            }

            var inherited = new List<TypeDef>();
            foreach (var typeName in names.Select(n => n.Value?.ToString() ?? string.Empty))
            {
                var comma = typeName.IndexOf(',');
                if (comma > 0
                 && byAssembly.TryGetValue(typeName.Substring(comma + 1).Split(',')[0].Trim(), out var module)
                 && module.Find(typeName.Substring(0, comma).Trim(), isReflectionName: true) is { IsInterface: true } type)
                {
                    inherited.Add(type);
                }
            }

            if (inherited.Count == names.Count)
            {
                result.Add((name.String, inherited));
            }
        }

        return result;
    }
}
#endif
