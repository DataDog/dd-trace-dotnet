// <copyright file="InstrumentationLoader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.ComponentModel;

namespace Datadog.Trace.ClrProfiler;

/// <summary>
/// Instrumentation Loader
/// Needs to be public as invoked from managed loader
/// </summary>
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class InstrumentationLoader
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InstrumentationLoader"/> class.
    /// </summary>
    public InstrumentationLoader()
    {
        Instrumentation.Initialize();
    }
}
