// <copyright file="ProductsData.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
namespace Datadog.Trace.Telemetry;

/// <summary>
/// V2 Products object
/// </summary>
internal class ProductsData
{
    // ReSharper disable once IdentifierTypo (Must be this way for serialization reasons
    public ProductData? Appsec { get; set; }

    public ProductData? Profiler { get; set; }

    public ProductData? DynamicInstrumentation { get; set; }
}
