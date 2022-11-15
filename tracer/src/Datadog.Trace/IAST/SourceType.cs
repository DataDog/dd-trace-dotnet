using System;

namespace Datadog.Trace.Iast;

internal static class SourceType
{
    public static Tuple<byte, string> RequestQueryParameter { get; } = new Tuple<byte, string>(1, "http.url_details.queryString");

    public static Tuple<byte, string> RequestPath { get; } = new Tuple<byte, string>(2, "http.url_details.path");

    public static Tuple<byte, string> RequestParameterName { get; } = new Tuple<byte, string>(3, "http.param.name");

    public static Tuple<byte, string> RequestParameterValue { get; } = new Tuple<byte, string>(4, "http.param.value");
}
