// <copyright file="InstrumentationLoader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

namespace Datadog.Trace.ClrProfiler;

/// <summary>
/// Instrumentation Loader
/// </summary>
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
