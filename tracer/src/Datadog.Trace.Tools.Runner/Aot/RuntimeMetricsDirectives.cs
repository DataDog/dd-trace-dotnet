// <copyright file="RuntimeMetricsDirectives.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using dnlib.DotNet;

namespace Datadog.Trace.Tools.Runner.Aot;

/// <summary>
/// Before .NET 9, Datadog.Trace publishes the <c>System.Runtime</c> meter itself (RuntimeMetricsPolyfill), whose
/// <c>ObservableUpDownCounter&lt;long&gt;</c> instruments it creates with <c>MakeGenericMethod</c>: it compiles against the
/// net6.0 DiagnosticSource, without <c>Meter.CreateObservableUpDownCounter</c>. A NativeAOT application has no code for that
/// instantiation unless the compiler is told: this writes a runtime directives file (rd.xml) with it when the application's
/// runtime is older than .NET 9 and its DiagnosticSource has the method.
/// </summary>
internal static class RuntimeMetricsDirectives
{
    private const string DiagnosticSource = "System.Diagnostics.DiagnosticSource";
    private const string MeterType = "System.Diagnostics.Metrics.Meter";
    private const string CreateMethod = "CreateObservableUpDownCounter";

    /// <returns>The number of instantiations kept (0 or 1).</returns>
    public static int Write(IEnumerable<ModuleDef> modules, string runtimeDirectivesPath)
    {
        var list = modules.ToList();
        var coreLib = list.FirstOrDefault(m => m.Assembly?.Name == "System.Private.CoreLib");
        var meter = list.FirstOrDefault(m => m.Assembly?.Name == DiagnosticSource)?.Find(MeterType, isReflectionName: false);
        if (coreLib is null || coreLib.Assembly.Version.Major >= 9 || meter is null || !meter.Methods.Any(m => m.Name == CreateMethod && m.GenericParameters.Count == 1))
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
                        new XAttribute("Name", DiagnosticSource),
                        new XElement(
                            directives + "Type",
                            new XAttribute("Name", MeterType),
                            new XElement(
                                directives + "Method",
                                new XAttribute("Name", CreateMethod),
                                new XAttribute("Dynamic", "Required"),
                                new XElement(directives + "GenericArgument", new XAttribute("Name", "System.Int64, System.Private.CoreLib"))))))))
            .Save(runtimeDirectivesPath);
        return 1;
    }
}
#endif
