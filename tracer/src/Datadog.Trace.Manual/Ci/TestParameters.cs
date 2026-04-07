// <copyright file="TestParameters.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Ci;

/// <summary>
/// Test parameters
/// </summary>
#pragma warning  disable DDSEAL001 // Types should be sealed where possible, but we can't seal this because it's already public - breaking change
public class TestParameters
#pragma warning restore DDSEAL001
{
    /// <summary>
    /// Gets or sets the test parameters metadata
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Gets or sets the test arguments
    /// </summary>
    public Dictionary<string, object>? Arguments { get; set; }
}
