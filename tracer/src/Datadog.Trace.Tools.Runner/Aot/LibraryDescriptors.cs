// <copyright file="LibraryDescriptors.cs" company="Datadog">
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
/// Some instrumented libraries don't run in NativeAOT unless ILC is told what they use by reflection: Confluent.Kafka binds
/// librdkafka by reflection over its <c>NativeMethods</c> classes (<c>Librdkafka.SetDelegates</c>), and building a producer
/// or a consumer fails without them. This writes an ILLink descriptor that keeps it for the libraries of the application.
/// </summary>
internal static class LibraryDescriptors
{
    private static readonly Dictionary<string, Func<XElement>> Libraries = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Confluent.Kafka"] = () => new XElement(
            "assembly",
            new XAttribute("fullname", "Confluent.Kafka"),
            new XElement("type", new XAttribute("fullname", "Confluent.Kafka.Impl.Librdkafka"), new XAttribute("preserve", "all")),
            new XElement("namespace", new XAttribute("fullname", "Confluent.Kafka.Impl.NativeMethods"))),
    };

    /// <returns>The number of libraries the descriptor is for.</returns>
    public static int Write(IEnumerable<ModuleDef> modules, string descriptorPath)
    {
        var assemblies = modules.Where(m => m.Assembly is not null && Libraries.ContainsKey(m.Assembly.Name.String))
                                .Select(m => m.Assembly.Name.String)
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .OrderBy(n => n, StringComparer.Ordinal)
                                .Select(n => Libraries[n]())
                                .ToList();
        if (assemblies.Count == 0)
        {
            File.Delete(descriptorPath);
            return 0;
        }

        new XDocument(new XElement("linker", assemblies)).Save(descriptorPath);
        return assemblies.Count;
    }
}
#endif
