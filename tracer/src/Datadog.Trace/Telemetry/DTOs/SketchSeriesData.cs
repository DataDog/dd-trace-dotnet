// <copyright file="SketchSeriesData.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Telemetry;

internal class SketchSeriesData
{
    public SketchSeriesData(string metric, string sketchBase64, bool common)
    {
        Metric = metric;
        SketchBase64 = sketchBase64;
        Common = common;
    }

    public SketchSeriesData(string metric, byte[] sketch, bool common)
    {
        Metric = metric;
        Sketch = sketch;
        Common = common;
    }

    /// <summary>
    /// Gets or sets the Metric name. This value will be prefixed with dd.app_telemetry.{namespace}.*
    /// or dd.instrumentation_telemetry_data.{namespace}.{language}.*
    /// </summary>
    public string Metric { get; set; }

    /// <summary>
    /// Gets or sets a Base64 encoded protobuf representation of a DDSketch. Ignored if <see cref="Sketch"/> is non-empty.
    /// </summary>
    public string? SketchBase64 { get; set; }

    /// <summary>
    /// Gets or sets a binary protobuf representation of a DDSketch. If empty, <see cref="SketchBase64"/> must be non-empty
    /// </summary>
    public byte[]? Sketch { get; set; }

    /// <summary>
    /// Gets or sets a list of tags that will be associated with the points
    /// </summary>
    public string[]? Tags { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether indicates whether the metric is common or language specific
    /// This will affect the tags and prefix of the metric, as explained above.
    /// If this field is missing it defaults to true.
    /// </summary>
    public bool Common { get; set; }

    /// <summary>
    /// Gets or sets one of the following values: “tracers”, “profilers”, “rum”, “appsec”. Per series override for the namespace field on the payload object
    /// </summary>
    public string? Namespace { get; set; }
}
