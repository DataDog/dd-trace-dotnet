// <copyright file="AotInstrumentOptions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;

namespace Datadog.Trace.Tools.Runner.Aot;

/// <summary>
/// Options of <c>dd-trace aot instrument</c>.
/// </summary>
internal sealed class AotInstrumentOptions
{
    public string NativeTracerPath { get; init; } = string.Empty;

    public string DatadogTracePath { get; init; } = string.Empty;

    public IReadOnlyList<string> Assemblies { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> ReferenceDirectories { get; init; } = Array.Empty<string>();

    public string OutputDirectory { get; init; } = string.Empty;

    public Version RuntimeVersion { get; init; } = new(8, 0, 0);

    /// <summary>Gets the InstrumentationCategory flags (Tracing = 1, AppSec = 2, Iast = 4, Rasp = 8).</summary>
    public uint Categories { get; init; } = 1 | 2 | 8;

    /// <summary>Gets the TargetFrameworks flag of the Datadog.Trace build compiled into the application (NET6_0 = 8).</summary>
    public uint TargetFramework { get; init; } = 8;

    public bool UseEmbeddedDefinitions { get; init; } = true;

    public string? DefinitionsAssembly { get; init; }

    public string? DefinitionsMethod { get; init; }

    public IReadOnlyList<string> Neutralize { get; init; } = Array.Empty<string>();

    /// <summary>Gets a value indicating whether the CallTarget registrations are generated (off only to compare the native rewrite alone).</summary>
    public bool GenerateCallTargetRegistry { get; init; } = true;

    public string? ReportPath { get; init; }

    /// <summary>Gets a value indicating whether the rewritten methods are verified (ILSpy, JIT preparation, ILVerify) against the originals.</summary>
    public bool Verify { get; init; }

    public bool Verbose { get; init; }
}
