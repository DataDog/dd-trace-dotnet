// <copyright file="FacadeDirectives.cs" company="Datadog">
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
/// Some libraries store type names with the portable assembly name <c>mscorlib</c> and resolve them at runtime: Hangfire
/// serializes job parameters so, among them the propagation context the Hangfire integration adds to each job. A NativeAOT
/// application only keeps the metadata of <c>mscorlib</c> (a facade of type forwarders) when the compiler sees a constant
/// name, so <c>Assembly.Load("mscorlib")</c> fails. This writes a runtime directives file (rd.xml) that keeps it when the
/// application has one of those libraries.
/// </summary>
internal static class FacadeDirectives
{
    private const string Facade = "mscorlib";

    // The libraries that resolve the type names they store through mscorlib.
    private static readonly HashSet<string> Libraries = new(StringComparer.OrdinalIgnoreCase) { "Hangfire.Core" };

    /// <returns>The number of facades kept (0 or 1).</returns>
    public static int Write(IEnumerable<ModuleDef> modules, string runtimeDirectivesPath)
    {
        if (!modules.Any(m => m.Assembly is { } assembly && Libraries.Contains(assembly.Name.String)))
        {
            File.Delete(runtimeDirectivesPath);
            return 0;
        }

        XNamespace directives = "http://schemas.microsoft.com/netfx/2013/01/metadata";
        new XDocument(new XElement(directives + "Directives", new XElement(directives + "Application", new XElement(directives + "Assembly", new XAttribute("Name", Facade))))).Save(runtimeDirectivesPath);
        return 1;
    }
}
#endif
