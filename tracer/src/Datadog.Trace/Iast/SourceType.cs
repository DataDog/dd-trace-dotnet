// <copyright file="SourceType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Iast;

internal enum SourceType : byte
{
    RequestBody = 0,
    RequestPath = 1,
    RequestParameterName = 2,
    RequestParameterValue = 3,
    RoutedParameterValue = 4,
    RequestHeaderValue = 5,
    RequestHeaderName = 6,
    RequestQuery = 7,
    CookieName = 8,
    CookieValue = 9,
    MatrixParameter = 10,
    RequestUri = 11,
    GrpcRequestBody = 12,
    SqlRowValue = 13
}
