// <copyright file="IApiResponseTelemetryExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.Agent;

internal static class IApiResponseTelemetryExtensions
{
    public static MetricTags.StatusCode GetTelemetryStatusCodeMetricTag(this IApiResponse response)
        => response.StatusCode switch
        {
            200 => MetricTags.StatusCode.Code200,
            201 => MetricTags.StatusCode.Code201,
            202 => MetricTags.StatusCode.Code202,
            204 => MetricTags.StatusCode.Code204,
            < 300 => MetricTags.StatusCode.Code2xx,
            301 => MetricTags.StatusCode.Code301,
            302 => MetricTags.StatusCode.Code302,
            307 => MetricTags.StatusCode.Code307,
            308 => MetricTags.StatusCode.Code308,
            < 400 => MetricTags.StatusCode.Code3xx,
            400 => MetricTags.StatusCode.Code400,
            401 => MetricTags.StatusCode.Code401,
            403 => MetricTags.StatusCode.Code403,
            404 => MetricTags.StatusCode.Code404,
            405 => MetricTags.StatusCode.Code405,
            < 500 => MetricTags.StatusCode.Code4xx,
            500 => MetricTags.StatusCode.Code500,
            501 => MetricTags.StatusCode.Code501,
            502 => MetricTags.StatusCode.Code502,
            503 => MetricTags.StatusCode.Code503,
            504 => MetricTags.StatusCode.Code504,
            _ => MetricTags.StatusCode.Code5xx,
        };
}
