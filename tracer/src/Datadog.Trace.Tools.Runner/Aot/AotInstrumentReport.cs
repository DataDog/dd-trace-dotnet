// <copyright file="AotInstrumentReport.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
#if NET6_0_OR_GREATER
using Datadog.Trace.Tools.Runner.Aot.CallTarget;
#endif

namespace Datadog.Trace.Tools.Runner.Aot;

/// <summary>
/// Summary written by <c>dd-trace aot instrument --report</c>.
/// </summary>
internal sealed class AotInstrumentReport
{
    /// <summary>Gets the version of the tracer that instrumented the application.</summary>
    public string TracerVersion { get; } = TracerConstants.AssemblyVersion;

    /// <summary>Gets or sets the version of the runtime that ran the instrumentation (see <c>AotRuntimeSelector</c>).</summary>
    public string InstrumentationRuntime { get; set; } = System.Environment.Version.ToString();

    public int EmbeddedDefinitions { get; set; }

    public int EmbeddedCallSites { get; set; }

    public int CallSiteMethods { get; set; }

    public int JsonModels { get; set; }

    public int ReflectionRoots { get; set; }

    public int DelegateWrappers { get; set; }

    public int Facades { get; set; }

    public int LibraryDescriptors { get; set; }

    public int RuntimeMetricsInstantiations { get; set; }

    public int KafkaDeliveryReports { get; set; }

    public int CompositeInterfaces { get; set; }

    public int DeclaredDuckTypeMappings { get; set; }

    /// <summary>Gets or sets the recorded mappings of Datadog's proxies that aren't declared (see CallTargetDuckTypeRegistry.FindUndeclared).</summary>
    public List<string> UndeclaredDuckTypeMappings { get; set; } = new();

    public int UserStrings { get; set; }

    public int CodeOriginLocations { get; set; }

    public int ReJitProcessed { get; set; }

    public List<AssemblyResult> Assemblies { get; } = new();

    public Dictionary<string, int> NotImplemented { get; set; } = new();

    public DuckTypeRegistryResult? DuckTypeRegistry { get; set; }

    public GenericInstantiationsResult? GenericInstantiations { get; set; }

    public List<string> Errors { get; } = new();

    /// <summary>
    /// The search of the closed instantiations of the instrumented generic methods (C3, H-76).
    /// </summary>
    internal sealed class GenericInstantiationsResult
    {
        public int RelevantMembers { get; set; }

        public int ClosedReferences { get; set; }

        public int Contexts { get; set; }

        public int DepthCuts { get; set; }

        public bool LimitReached { get; set; }

        /// <summary>Gets or sets the instrumented methods with generic shapes left for their instantiations.</summary>
        public List<string> DeferredMethods { get; set; } = new();

        /// <summary>Gets or sets the methods of <see cref="DeferredMethods"/> without any instantiation found.</summary>
        public List<string> UninstantiatedMethods { get; set; } = new();
    }

    internal sealed class DuckTypeRegistryResult
    {
        public string Path { get; set; } = string.Empty;

        public int Mappings { get; set; }

        public int Compatible { get; set; }

        public int ReplayedFailures { get; set; }

        public List<string> Warnings { get; set; } = new();
    }

    internal sealed class AssemblyResult
    {
        public string Name { get; set; } = string.Empty;

        public string Path { get; set; } = string.Empty;

        public int RewrittenMethods { get; set; }
#if NET6_0_OR_GREATER

        public CallTargetRegistryResult? CallTarget { get; set; }
#endif
    }
}
