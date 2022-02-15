// <copyright file="TraceTagNames.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace;

/// <summary>
/// Tags names use for trace-level tags.
/// </summary>
internal static class TraceTagNames
{
    internal static class Propagation
    {
        internal const string PropagationHeadersError = "_dd.propagation_error";
    }
}
