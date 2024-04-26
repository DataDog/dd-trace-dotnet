// <copyright file="FileCoverage.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable  enable

using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Ci.Coverage.Models.Tests;

/// <summary>
/// Source file with executable code
/// </summary>
internal sealed class FileCoverage
{
    /// <summary>
    /// Gets or sets path/name of the file
    /// </summary>
    [JsonProperty("filename")]
    public string? FileName { get; set; }

    /// <summary>
    /// Gets or sets the bitmap of the file with the executed code
    /// </summary>
    [JsonProperty("bitmap")]
    public byte[]? Bitmap { get; set; }
}
