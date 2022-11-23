// <copyright file="SourceType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Iast;

// Defined in https://github.com/DataDog/experimental/blob/main/teams/asm/vulnerability_schema/vulnerability_schema.json

internal static class SourceType
{
    public static Tuple<byte, string> RequestBody { get; } = new Tuple<byte, string>(1, "http.request.body");

    public static Tuple<byte, string> RequestPath { get; } = new Tuple<byte, string>(2, "http.request.path");

    public static Tuple<byte, string> RequestParameterName { get; } = new Tuple<byte, string>(3, "http.request.parameter.name");

    public static Tuple<byte, string> RequestParameterValue { get; } = new Tuple<byte, string>(4, "http.request.parameter.value");

    public static Tuple<byte, string> RoutedParameterValue { get; } = new Tuple<byte, string>(5, "http.request.path.parameter");

    public static Tuple<byte, string> RequestHeader { get; } = new Tuple<byte, string>(6, "http.request.header");

    public static Tuple<byte, string> RequestQueryString { get; } = new Tuple<byte, string>(2, "http.request.querystring");
}
