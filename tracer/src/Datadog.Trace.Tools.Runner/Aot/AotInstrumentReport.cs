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

    public int CompositeInterfaces { get; set; }

    public int UserStrings { get; set; }

    public int CodeOriginLocations { get; set; }

    public int ReJitProcessed { get; set; }

    public List<AssemblyResult> Assemblies { get; } = new();

    public Dictionary<string, int> NotImplemented { get; set; } = new();

    public DuckTypeRegistryResult? DuckTypeRegistry { get; set; }

    public List<string> Errors { get; } = new();

    internal sealed class DuckTypeRegistryResult
    {
        public string Path { get; set; } = string.Empty;

        public int Mappings { get; set; }

        public int Compatible { get; set; }

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
