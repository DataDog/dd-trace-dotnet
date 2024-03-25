// <copyright file="DatadogTraceConstants.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Coverage.Collector;

/// <summary>
/// Contains names for types that are defined in Datadog.Trace. _Must_ be kept in sync with the actual types.
/// </summary>
public static class DatadogTraceConstants
{
    /// <summary>
    /// The simple name of the Datadog.Trace assembly, as returned by
    /// typeof(Tracer).Assembly.GetName().Name
    /// </summary>
    public const string AssemblyName = "Datadog.Trace";

    /// <summary>
    /// The filename of the Datadog.Trace assembly, as returned by
    /// Path.GetFileName(typeof(Tracer).Assembly.Location)
    /// </summary>
    public const string AssemblyFileName = "Datadog.Trace.dll";

    /// <summary>
    /// The version of the Datadog.Trace assembly, as returned by
    /// typeof(Tracer).Assembly.GetName().Version
    /// </summary>
    public static readonly Version AssemblyVersion = Version.Parse(TracerConstants.AssemblyVersion);

    /// <summary>
    /// Namespaces from Datadog.Trace
    /// </summary>
    public static class Namespaces
    {
        /// <summary>
        /// The namespace of the ModuleCoverageMetadata type in Datadog.Trace, as returned by
        /// typeof(ModuleCoverageMetadata).Namespace
        /// </summary>
        public const string ModuleCoverageMetadata = "Datadog.Trace.Ci.Coverage.Metadata";
    }

    /// <summary>
    /// Namespaces from Datadog.Trace
    /// </summary>
    public static class TypeNames
    {
        /// <summary>
        /// The full name of the ModuleCoverageMetadata type in Datadog.Trace, as returned by
        /// typeof(ModuleCoverageMetadata).FullName
        /// </summary>
        public const string ModuleCoverageMetadata = Namespaces.ModuleCoverageMetadata + ".ModuleCoverageMetadata";

        /// <summary>
        /// The full name of the CoverageReporter type in Datadog.Trace, as returned by
        /// typeof(CoverageReporter&lt;&gt;).FullName
        /// </summary>
        public const string CoverageReporter = "Datadog.Trace.Ci.Coverage.CoverageReporter`1";

        /// <summary>
        /// The full name of the FileCoverageMetadata type in Datadog.Trace, as returned by
        /// typeof(FileCoverageMetadata).FullName
        /// </summary>
        public const string FileCoverageMetadata = "Datadog.Trace.Ci.Coverage.Metadata.FileCoverageMetadata";
    }
}
