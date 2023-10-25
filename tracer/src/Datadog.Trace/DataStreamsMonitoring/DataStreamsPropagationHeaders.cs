// <copyright file="DataStreamsPropagationHeaders.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
namespace Datadog.Trace.DataStreamsMonitoring;

internal static class DataStreamsPropagationHeaders
{
    public const string PropagationKey = "dd-pathway-ctx";
    public const string PropagationKeyBase64 = "dd-pathway-ctx-base64";
    public const string TemporaryEdgeTags = "x-datadog-temp-edge-tags";
    public const string TemporaryBase64PathwayContext = "x-datadog-temp-base64-context";
}
