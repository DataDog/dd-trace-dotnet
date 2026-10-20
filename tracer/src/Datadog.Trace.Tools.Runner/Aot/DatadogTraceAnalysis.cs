// <copyright file="DatadogTraceAnalysis.cs" company="Datadog">
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
using Datadog.Trace.Tools.Runner.Aot.Native;
using Datadog.Trace.Tools.Runner.DuckTypeAot;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using dnlib.DotNet;

namespace Datadog.Trace.Tools.Runner.Aot;

/// <summary>
/// The analyses of a Datadog.Trace build that don't depend on the application: the JSON models and the delegate wrappers ILC
/// must keep, the type names Datadog.Trace looks up, and its duck typing declarations. `dd-trace aot prepare` writes them next
/// to Datadog.Trace.dll (Datadog.Trace.Aot packs them), so the instrumentation of an application doesn't scan the IL of
/// Datadog.Trace again: several seconds of each publish.
/// </summary>
internal sealed class DatadogTraceAnalysis
{
    /// <summary>
    /// The file name of the analysis, next to Datadog.Trace.dll.
    /// </summary>
    internal const string FileName = "Datadog.Trace.aot.json";

    /// <summary>
    /// Gets or sets the MVID of the Datadog.Trace build analyzed.
    /// </summary>
    public Guid DatadogTraceModuleVersionId { get; set; }

    /// <summary>
    /// Gets or sets the MVID of the runner that analyzed it: the analyses change with the runner.
    /// </summary>
    public Guid RunnerModuleVersionId { get; set; }

    public int JsonModels { get; set; }

    public string JsonModelsDescriptor { get; set; } = string.Empty;

    public string JsonModelsDirectives { get; set; } = string.Empty;

    public int DelegateWrappers { get; set; }

    public string DelegateWrappersDirectives { get; set; } = string.Empty;

    public List<TypeNameEntry> ReflectionTypeNames { get; set; } = new();

    public List<MappingEntry> DuckTypeMappings { get; set; } = new();

    public List<string> DuckTypeErrors { get; set; } = new();

    private static Guid RunnerMvid => typeof(DatadogTraceAnalysis).Assembly.ManifestModule.ModuleVersionId;

    /// <summary>
    /// Analyzes a Datadog.Trace build.
    /// </summary>
    /// <param name="datadogTrace">The Datadog.Trace module.</param>
    /// <param name="datadogTracePath">The Datadog.Trace path.</param>
    /// <returns>The analysis.</returns>
    internal static DatadogTraceAnalysis Create(ModuleDef datadogTrace, string datadogTracePath)
    {
        var (jsonModels, jsonDescriptor, jsonDirectives) = JsonModelDescriptor.Create(datadogTrace);
        var (delegateWrappers, delegateDirectives) = DelegateWrapperDirectives.Create(datadogTrace);
        var discovery = DuckTypeAotAttributeDiscovery.Discover([Path.GetFullPath(datadogTracePath)]);
        return new DatadogTraceAnalysis
        {
            DatadogTraceModuleVersionId = datadogTrace.Mvid ?? Guid.Empty,
            RunnerModuleVersionId = RunnerMvid,
            JsonModels = jsonModels,
            JsonModelsDescriptor = jsonDescriptor.ToString(),
            JsonModelsDirectives = jsonDirectives.ToString(),
            DelegateWrappers = delegateWrappers,
            DelegateWrappersDirectives = delegateDirectives.ToString(),
            ReflectionTypeNames = ReflectionRootDescriptor.CollectTypeNames(datadogTrace)
                                                          .Select(name => new TypeNameEntry { Name = name.TypeName, Assembly = name.AssemblyName })
                                                          .OrderBy(name => name.Name, StringComparer.Ordinal)
                                                          .ThenBy(name => name.Assembly, StringComparer.Ordinal)
                                                          .ToList(),
            DuckTypeMappings = discovery.Mappings.Select(MappingEntry.From).ToList(),
            DuckTypeErrors = discovery.Errors.ToList(),
        };
    }

    /// <summary>
    /// Gets the analysis of a Datadog.Trace build: the one next to it when it's of this build and of this runner, else a new one.
    /// </summary>
    /// <param name="datadogTrace">The Datadog.Trace module.</param>
    /// <param name="datadogTracePath">The Datadog.Trace path.</param>
    /// <returns>The analysis.</returns>
    internal static DatadogTraceAnalysis Get(ModuleDef datadogTrace, string datadogTracePath)
    {
        var path = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(datadogTracePath))!, FileName);
        if (File.Exists(path))
        {
            try
            {
                if (JsonConvert.DeserializeObject<DatadogTraceAnalysis>(File.ReadAllText(path)) is { } analysis &&
                    analysis.DatadogTraceModuleVersionId == datadogTrace.Mvid &&
                    analysis.RunnerModuleVersionId == RunnerMvid)
                {
                    AotLog.Debug($"Analysis of Datadog.Trace: {path}");
                    return analysis;
                }

                AotLog.Debug($"'{path}' is the analysis of another build of Datadog.Trace or of the runner: analyzing it again.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                AotLog.Debug($"'{path}' can't be read ({ex.Message}): analyzing Datadog.Trace again.");
            }
        }

        return Create(datadogTrace, datadogTracePath);
    }

    /// <summary>
    /// Writes the analysis.
    /// </summary>
    /// <param name="path">The path.</param>
    internal void Write(string path) => File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));

    /// <summary>
    /// Writes the descriptor and the runtime directives of the JSON models (see <see cref="JsonModelDescriptor"/>).
    /// </summary>
    /// <param name="descriptorPath">The descriptor path.</param>
    /// <param name="runtimeDirectivesPath">The runtime directives path.</param>
    /// <returns>The number of types preserved.</returns>
    internal int WriteJsonModels(string descriptorPath, string runtimeDirectivesPath)
    {
        XDocument.Parse(JsonModelsDescriptor).Save(descriptorPath);
        XDocument.Parse(JsonModelsDirectives).Save(runtimeDirectivesPath);
        return JsonModels;
    }

    /// <summary>
    /// Writes the runtime directives of the delegate wrappers (see <see cref="DelegateWrapperDirectives"/>).
    /// </summary>
    /// <param name="runtimeDirectivesPath">The runtime directives path.</param>
    /// <returns>The number of wrapper instantiations.</returns>
    internal int WriteDelegateWrappers(string runtimeDirectivesPath)
    {
        XDocument.Parse(DelegateWrappersDirectives).Save(runtimeDirectivesPath);
        return DelegateWrappers;
    }

    /// <summary>
    /// A type name Datadog.Trace looks up (see <see cref="ReflectionRootDescriptor.CollectTypeNames"/>).
    /// </summary>
    internal sealed class TypeNameEntry
    {
        public string Name { get; set; } = string.Empty;

        public string? Assembly { get; set; }
    }

    /// <summary>
    /// A duck typing mapping Datadog.Trace declares.
    /// </summary>
    internal sealed class MappingEntry
    {
        public string ProxyType { get; set; } = string.Empty;

        public string ProxyAssembly { get; set; } = string.Empty;

        public string TargetType { get; set; } = string.Empty;

        public string TargetAssembly { get; set; } = string.Empty;

        public DuckTypeAotMappingMode Mode { get; set; }

        public DuckTypeAotMappingSource Source { get; set; }

        public bool IncludesDerivedTypes { get; set; }

        internal static MappingEntry From(DuckTypeAotMapping mapping)
            => new()
            {
                ProxyType = mapping.ProxyTypeName,
                ProxyAssembly = mapping.ProxyAssemblyName,
                TargetType = mapping.TargetTypeName,
                TargetAssembly = mapping.TargetAssemblyName,
                Mode = mapping.Mode,
                Source = mapping.Source,
                IncludesDerivedTypes = mapping.IncludesDerivedTypes,
            };

        internal DuckTypeAotMapping ToMapping()
            => new(ProxyType, ProxyAssembly, TargetType, TargetAssembly, Mode, Source, scenarioId: null, IncludesDerivedTypes);
    }
}
#endif
