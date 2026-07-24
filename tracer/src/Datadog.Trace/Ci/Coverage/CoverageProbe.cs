// <copyright file="CoverageProbe.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;

namespace Datadog.Trace.Ci.Coverage;

/// <summary>
/// Holds a coverage counter pointer for one instrumented method invocation.
/// </summary>
/// <remarks>
/// This type is public because rewritten customer assemblies reference it directly. It is not a customer-facing API.
/// </remarks>
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public unsafe struct CoverageProbe : IDisposable
{
    private ModuleValue? _moduleValue;

    internal CoverageProbe(ModuleValue? moduleValue, void* pointer)
    {
        _moduleValue = moduleValue;
        Pointer = pointer;
    }

    /// <summary>
    /// Gets the first counter for the instrumented source file.
    /// </summary>
    public void* Pointer { get; }

    /// <summary>
    /// Releases the native counter buffer used by this invocation.
    /// </summary>
    public void Dispose()
    {
        var moduleValue = _moduleValue;
        _moduleValue = null;
        moduleValue?.ReleaseProbe();
    }
}
