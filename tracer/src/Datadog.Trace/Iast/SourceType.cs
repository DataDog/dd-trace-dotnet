// <copyright file="SourceType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Iast;

internal static class SourceType
{
    internal static byte GetByte(SourceTypeName value)
    {
        return (byte)value;
    }

    // Defined in https://github.com/DataDog/experimental/blob/main/teams/asm/vulnerability_schema/vulnerability_schema.json

    internal static string GetString(byte value)
    {
        return GetString((SourceTypeName)value);
    }

    internal static string GetAsTag(SourceTypeName value)
    => value switch
    {
        SourceTypeName.RequestParameterValue => "http_request_parameter",
        SourceTypeName.RequestParameterName => "http_request_parameter_name",
        SourceTypeName.RequestHeaderValue => "http_request_header",
        SourceTypeName.RequestHeaderName => "http_request_header_name",
        SourceTypeName.RequestPath => "http_request_path",
        SourceTypeName.RequestBody => "http_request_body",
        SourceTypeName.RequestQuery => "http_request_query",
        SourceTypeName.RoutedParameterValue => "http_request_path_parameter",
        SourceTypeName.MatrixParameter => "http_request_matrix_parameter",
        SourceTypeName.CookieName => "http_request_cookie_name",
        SourceTypeName.CookieValue => "http_request_cookie_value",
        SourceTypeName.RequestUri => "http_request_uri",
        _ => throw new System.Exception($"SourceTypeName TEXT for value {value} not defined in GetAsTag")
    };

    internal static string GetString(SourceTypeName value)
        => value switch
        {
            SourceTypeName.RequestParameterValue => "http.request.parameter",
            SourceTypeName.RequestParameterName => "http.request.parameter.name",
            SourceTypeName.RequestHeaderValue => "http.request.header",
            SourceTypeName.RequestHeaderName => "http.request.header.name",
            SourceTypeName.RequestPath => "http.request.path",
            SourceTypeName.RequestBody => "http.request.body",
            SourceTypeName.RequestQuery => "http.request.query",
            SourceTypeName.RoutedParameterValue => "http.request.path.parameter",
            SourceTypeName.MatrixParameter => "http.request.matrix.parameter",
            SourceTypeName.CookieName => "http.request.cookie.name",
            SourceTypeName.CookieValue => "http.request.cookie.value",
            SourceTypeName.RequestUri => "http.request.uri",
            _ => throw new System.Exception($"SourceTypeName TEXT for value {value} not defined")
        };

    internal static SourceTypeName FromString(string? value)
        => value switch
        {
            "http.request.parameter" => SourceTypeName.RequestParameterValue,
            "http.request.parameter.name" => SourceTypeName.RequestParameterName,
            "http.request.header" => SourceTypeName.RequestHeaderValue,
            "http.request.header.name" => SourceTypeName.RequestHeaderName,
            "http.request.path" => SourceTypeName.RequestPath,
            "http.request.body" => SourceTypeName.RequestBody,
            "http.request.query" => SourceTypeName.RequestQuery,
            "http.request.path.parameter" => SourceTypeName.RoutedParameterValue,
            "http.request.matrix.parameter" => SourceTypeName.MatrixParameter,
            "http.request.cookie.name" => SourceTypeName.CookieName,
            "http.request.cookie.value" => SourceTypeName.CookieValue,
            "http.request.uri" => SourceTypeName.RequestUri,
            _ => throw new System.Exception($"SourceTypeName VALUE for text {value ?? "NULL"} not defined")
        };
}
