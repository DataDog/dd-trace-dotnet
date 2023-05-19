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

    internal static string GetString(SourceTypeName value)
        => value switch
        {
            SourceTypeName.RequestParameterValue => "http.request.parameter",
            SourceTypeName.RequestParameterName => "http.request.parameter.name",
            SourceTypeName.RequestHeader => "http.request.header",
            SourceTypeName.RequestHeaderName => "http.request.header.name",
            SourceTypeName.RequestPath => "http.request.path",
            SourceTypeName.RequestBody => "http.request.body",
            SourceTypeName.RequestQuery => "http.request.query",
            SourceTypeName.RoutedParameterValue => "http.request.path.parameter",
            SourceTypeName.MatrixParameter => "http.request.matrix.parameter",
            SourceTypeName.CookieName => "http.cookie.name",
            SourceTypeName.CookieValue => "http.cookie.value",
            _ => string.Empty
        };

    internal static SourceTypeName FromString(string? value)
        => value switch
        {
            "http.request.parameter" => SourceTypeName.RequestParameterValue,
            "http.request.parameter.name" => SourceTypeName.RequestParameterName,
            "http.request.header" => SourceTypeName.RequestHeader,
            "http.request.header.name" => SourceTypeName.RequestHeaderName,
            "http.request.path" => SourceTypeName.RequestPath,
            "http.request.body" => SourceTypeName.RequestBody,
            "http.request.query" => SourceTypeName.RequestQuery,
            "http.request.path.parameter" => SourceTypeName.RoutedParameterValue,
            "http.request.matrix.parameter" => SourceTypeName.MatrixParameter,
            "http.cookie.name" => SourceTypeName.CookieName,
            "http.cookie.value" => SourceTypeName.CookieValue,
            _ => default(SourceTypeName)
        };
}
