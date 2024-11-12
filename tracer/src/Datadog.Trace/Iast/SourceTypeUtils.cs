// <copyright file="SourceTypeUtils.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.Iast;

internal static class SourceTypeUtils
{
    // Defined in https://github.com/DataDog/experimental/blob/main/teams/asm/vulnerability_schema/vulnerability_schema.json

    internal static string GetString(byte value)
    {
        return GetString((SourceType)value);
    }

    internal static string GetAsTag(SourceType value)
    => value switch
    {
        SourceType.RequestParameterValue => "http_request_parameter",
        SourceType.RequestParameterName => "http_request_parameter_name",
        SourceType.RequestHeaderValue => "http_request_header",
        SourceType.RequestHeaderName => "http_request_header_name",
        SourceType.RequestPath => "http_request_path",
        SourceType.RequestBody => "http_request_body",
        SourceType.RequestQuery => "http_request_query",
        SourceType.RoutedParameterValue => "http_request_path_parameter",
        SourceType.MatrixParameter => "http_request_matrix_parameter",
        SourceType.CookieName => "http_request_cookie_name",
        SourceType.CookieValue => "http_request_cookie_value",
        SourceType.RequestUri => "http_request_uri",
        SourceType.GrpcRequestBody => "grpc_request_body",
        SourceType.SqlRowValue => "sql_row_value",
        _ => throw new Exception($"SourceTypeName TEXT for value {value} not defined in GetAsTag")
    };

    internal static string GetString(SourceType value)
        => value switch
        {
            SourceType.RequestParameterValue => "http.request.parameter",
            SourceType.RequestParameterName => "http.request.parameter.name",
            SourceType.RequestHeaderValue => "http.request.header",
            SourceType.RequestHeaderName => "http.request.header.name",
            SourceType.RequestPath => "http.request.path",
            SourceType.RequestBody => "http.request.body",
            SourceType.RequestQuery => "http.request.query",
            SourceType.RoutedParameterValue => "http.request.path.parameter",
            SourceType.MatrixParameter => "http.request.matrix.parameter",
            SourceType.CookieName => "http.request.cookie.name",
            SourceType.CookieValue => "http.request.cookie.value",
            SourceType.RequestUri => "http.request.uri",
            SourceType.GrpcRequestBody => "grpc.request.body",
            SourceType.SqlRowValue => "sql.row.value",
            _ => throw new Exception($"SourceTypeName TEXT for value {value} not defined")
        };

    internal static SourceType FromString(string? value)
        => value switch
        {
            "http.request.parameter" => SourceType.RequestParameterValue,
            "http.request.parameter.name" => SourceType.RequestParameterName,
            "http.request.header" => SourceType.RequestHeaderValue,
            "http.request.header.name" => SourceType.RequestHeaderName,
            "http.request.path" => SourceType.RequestPath,
            "http.request.body" => SourceType.RequestBody,
            "http.request.query" => SourceType.RequestQuery,
            "http.request.path.parameter" => SourceType.RoutedParameterValue,
            "http.request.matrix.parameter" => SourceType.MatrixParameter,
            "http.request.cookie.name" => SourceType.CookieName,
            "http.request.cookie.value" => SourceType.CookieValue,
            "http.request.uri" => SourceType.RequestUri,
            "grpc.request.body" => SourceType.GrpcRequestBody,
            "sql.row.value" => SourceType.SqlRowValue,
            _ => throw new Exception($"SourceTypeName VALUE for text {value ?? "NULL"} not defined")
        };
}
