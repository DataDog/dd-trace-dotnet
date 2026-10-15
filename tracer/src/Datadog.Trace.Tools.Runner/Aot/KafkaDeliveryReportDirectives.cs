// <copyright file="KafkaDeliveryReportDirectives.cs" company="Datadog">
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
using Datadog.Trace.Tools.Runner.Aot.CallTarget;
using dnlib.DotNet;

namespace Datadog.Trace.Tools.Runner.Aot;

/// <summary>
/// The Kafka integration creates the default delivery handler of a producer with <c>MakeGenericType</c> of
/// <c>DeliveryReport&lt;TKey, TValue&gt;</c> and of its <c>Action&lt;&gt;</c>, over the producer's type arguments
/// (ProducerCache). NativeAOT builds the instantiations over reference types from their canonical code, but has no code for an
/// instantiation over a value type (e.g. <c>Producer&lt;int, string&gt;</c>) unless the application uses it, which it doesn't
/// when it only calls <c>ProduceAsync</c>: this writes a runtime directives file (rd.xml) with the ones of the instantiations of
/// <c>Producer&lt;TKey, TValue&gt;</c> over value types the search of generic instantiations found.
/// </summary>
internal static class KafkaDeliveryReportDirectives
{
    private const string KafkaAssembly = "Confluent.Kafka";
    private const string ProducerType = "Confluent.Kafka.Producer`2";
    private const string DeliveryReportType = "Confluent.Kafka.DeliveryReport`2";

    /// <summary>
    /// Writes the directives, or deletes the file when there's none.
    /// </summary>
    /// <param name="instantiations">The closed instantiations the search found, by method.</param>
    /// <param name="resolveType">Resolves a type reference across the loaded modules.</param>
    /// <param name="runtimeDirectivesPath">The path of the runtime directives file.</param>
    /// <returns>The number of instantiations of <c>DeliveryReport&lt;TKey, TValue&gt;</c> kept.</returns>
    public static int Write(IReadOnlyDictionary<MethodDef, List<GenericInstantiationDiscovery.Instantiation>> instantiations, Func<ITypeDefOrRef, TypeDef?> resolveType, string runtimeDirectivesPath)
    {
        var deliveryReports = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var (method, found) in instantiations)
        {
            if (method.DeclaringType is not { } type ||
                type.FullName != ProducerType ||
                type.Module?.Assembly?.Name != KafkaAssembly)
            {
                continue;
            }

            foreach (var instantiation in found)
            {
                // The instantiations over reference types share their canonical code.
                if (instantiation.TypeArguments.Count == 2 &&
                    instantiation.TypeArguments.Any(argument => argument.IsValueType) &&
                    AssemblyQualifiedName(instantiation.TypeArguments[0], resolveType) is { } key &&
                    AssemblyQualifiedName(instantiation.TypeArguments[1], resolveType) is { } value)
                {
                    deliveryReports.Add($"{DeliveryReportType}[[{key}],[{value}]]");
                }
            }
        }

        if (deliveryReports.Count == 0)
        {
            File.Delete(runtimeDirectivesPath);
            return 0;
        }

        XNamespace directives = "http://schemas.microsoft.com/netfx/2013/01/metadata";
        new XDocument(
            new XElement(
                directives + "Directives",
                new XElement(
                    directives + "Application",
                    new XElement(
                        directives + "Assembly",
                        new XAttribute("Name", KafkaAssembly),
                        deliveryReports.Select(name => new XElement(directives + "Type", new XAttribute("Name", name), new XAttribute("Dynamic", "Required All")))),
                    new XElement(
                        directives + "Assembly",
                        new XAttribute("Name", "System.Private.CoreLib"),
                        deliveryReports.Select(name => new XElement(directives + "Type", new XAttribute("Name", $"System.Action`1[[{name}, {KafkaAssembly}]]"), new XAttribute("Dynamic", "Required All")))))))
            .Save(runtimeDirectivesPath);
        return deliveryReports.Count;
    }

    /// <summary>
    /// Gets the assembly-qualified reflection name of a closed type, with the assembly that defines it (not the reference
    /// assembly the application compiled against), or null when it can't be named.
    /// </summary>
    private static string? AssemblyQualifiedName(TypeSig type, Func<ITypeDefOrRef, TypeDef?> resolveType)
    {
        switch (type)
        {
            case GenericInstSig instance:
                if (resolveType(instance.GenericType.TypeDefOrRef) is not { Module.Assembly: { } genericAssembly } genericDefinition)
                {
                    return null;
                }

                var arguments = new List<string>();
                foreach (var argument in instance.GenericArguments)
                {
                    if (AssemblyQualifiedName(argument, resolveType) is not { } argumentName)
                    {
                        return null;
                    }

                    arguments.Add($"[{argumentName}]");
                }

                return $"{genericDefinition.ReflectionFullName}[{string.Join(",", arguments)}], {genericAssembly.Name}";
            case TypeDefOrRefSig { TypeDefOrRef: { } reference } when resolveType(reference) is { Module.Assembly: { } assembly } definition && !definition.HasGenericParameters:
                return $"{definition.ReflectionFullName}, {assembly.Name}";
            default:
                return null;
        }
    }
}
#endif
