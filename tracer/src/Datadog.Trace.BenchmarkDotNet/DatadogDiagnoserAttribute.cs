// <copyright file="DatadogDiagnoserAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using BenchmarkDotNet.Configs;

namespace Datadog.Trace.BenchmarkDotNet;

/// <summary>
/// Datadog BenchmarkDotNet diagnoser attribute
/// </summary>
public class DatadogDiagnoserAttribute : Attribute, IConfigSource
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DatadogDiagnoserAttribute"/> class.
    /// </summary>
    public DatadogDiagnoserAttribute()
    {
        Config = ManualConfig.CreateEmpty()
                             .WithDatadog();
    }

    /// <summary>
    /// Gets the configuration
    /// </summary>
    public IConfig Config { get; }
}
