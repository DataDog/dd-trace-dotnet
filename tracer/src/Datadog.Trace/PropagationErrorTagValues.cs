// <copyright file="PropagationErrorTagValues.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace;

/// <summary>
/// Error names used in `_dd.propagation_error` as trace-level tag.
/// </summary>
internal static class PropagationErrorTagValues
{
    internal const string ExtractMaxSize = "extract_max_size";

    internal const string InjectMaxSize = "inject_max_size";

    internal const string EncodingError = "encoding_error";

    internal const string DecodingError = "decoding_error";

    internal const string PropagationDisabled = "disabled";
}
