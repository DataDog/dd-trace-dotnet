// <copyright file="IApiResponseTelemetryExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.Agent;

internal static class IApiResponseTelemetryExtensions
{
    public static MetricTags GetTelemetryStatusCodeMetricTag(this IApiResponse response)
        => response.StatusCode switch
        {
            200 => MetricTags.StatusCode_200,
            201 => MetricTags.StatusCode_201,
            202 => MetricTags.StatusCode_202,
            204 => MetricTags.StatusCode_204,
            < 300 => MetricTags.StatusCode_2xx,
            301 => MetricTags.StatusCode_301,
            302 => MetricTags.StatusCode_302,
            307 => MetricTags.StatusCode_307,
            308 => MetricTags.StatusCode_308,
            < 400 => MetricTags.StatusCode_3xx,
            400 => MetricTags.StatusCode_400,
            401 => MetricTags.StatusCode_401,
            403 => MetricTags.StatusCode_403,
            404 => MetricTags.StatusCode_404,
            405 => MetricTags.StatusCode_405,
            < 500 => MetricTags.StatusCode_4xx,
            500 => MetricTags.StatusCode_500,
            501 => MetricTags.StatusCode_501,
            502 => MetricTags.StatusCode_502,
            503 => MetricTags.StatusCode_503,
            504 => MetricTags.StatusCode_504,
            _ => MetricTags.StatusCode_5xx,
        };
}
