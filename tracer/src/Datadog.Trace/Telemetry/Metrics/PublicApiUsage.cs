// <copyright file="PublicApiUsage.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Telemetry.Metrics;

[SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1134:Attributes should not share line", Justification = "It's easier to read")]
[SuppressMessage("ReSharper", "InconsistentNaming", Justification = "It's easier to read")]
[EnumExtensions]
internal enum PublicApiUsage
{
    [Description("tracer_ctor")] Tracer_Ctor,
    [Description("tracer_ctor_settings")] Tracer_Ctor_Settings,
    [Description("tracer_instance_set")] Tracer_Instance_Set,
    [Description("tracer_configure")] Tracer_Configure,
    // TODO: more will be added soon, these are just for testing initially
}
