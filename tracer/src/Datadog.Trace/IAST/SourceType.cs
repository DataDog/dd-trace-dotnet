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
            SourceTypeName.RequestBody => "http.request.body",
            SourceTypeName.RequestPath => "http.request.path",
            SourceTypeName.RequestParameterName => "http.request.parameter.name",
            SourceTypeName.RequestParameterValue => "http.request.parameter.value",
            SourceTypeName.RoutedParameterValue => "http.request.path.parameter",
            SourceTypeName.RequestHeader => "http.request.header",
            SourceTypeName.RequestHeaderName => "http.request.header.name",
            SourceTypeName.RequestQueryString => "http.request.querystring",
            _ => string.Empty
        };
}
