// <copyright file="Tags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace;

/// <summary>
/// Standard span tags used by integrations.
/// </summary>
public static class Tags
{
    /// <summary>
    /// The environment of the instrumented service. Its value is usually constant for the lifetime of a process,
    /// but can technically change for each trace if the user sets it manually.
    /// This tag is added during MessagePack serialization.
    /// </summary>
    public const string Env = "env";

    /// <summary>
    /// The version of the instrumented service. Its value is usually constant for the lifetime of a process,
    /// but can technically change for each trace if the user sets it manually.
    /// This tag is added during MessagePack serialization.
    /// </summary>
    public const string Version = "version";

    /// <summary>
    /// The name of the integration that generated the span.
    /// Use OpenTracing tag "component"
    /// </summary>
    public const string InstrumentationName = "component";

    /// <summary>
    /// The name of the method that was instrumented to generate the span.
    /// </summary>
    public const string InstrumentedMethod = "instrumented.method";

    /// <summary>
    /// The kind of span (e.g. client, server). Not to be confused with <see cref="ISpan.Type"/>.
    /// </summary>
    /// <seealso cref="SpanKinds"/>
    public const string SpanKind = "span.kind";

    /// <summary>
    /// The URL of an HTTP request
    /// </summary>
    public const string HttpUrl = "http.url";

    /// <summary>
    /// The method of an HTTP request
    /// </summary>
    public const string HttpMethod = "http.method";

    /// <summary>
    /// The host of an HTTP request
    /// </summary>
    public const string HttpRequestHeadersHost = "http.request.headers.host";

    /// <summary>
    /// The status code of an HTTP response
    /// </summary>
    public const string HttpStatusCode = "http.status_code";

    /// <summary>
    /// The error message of an exception
    /// </summary>
    public const string ErrorMsg = "error.msg";

    /// <summary>
    /// The type of an exception
    /// </summary>
    public const string ErrorType = "error.type";

    /// <summary>
    /// The stack trace of an exception
    /// </summary>
    public const string ErrorStack = "error.stack";

    /// <summary>
    /// The type of database (e.g. "sql-server", "mysql", "postgres", "sqlite", "oracle")
    /// </summary>
    public const string DbType = "db.type";

    /// <summary>
    /// The user used to sign into a database
    /// </summary>
    public const string DbUser = "db.user";

    /// <summary>
    /// The name of the database.
    /// </summary>
    public const string DbName = "db.name";

    /// <summary>
    /// The query text
    /// </summary>
    public const string SqlQuery = "sql.query";

    /// <summary>
    /// The number of rows returned by a query
    /// </summary>
    public const string SqlRows = "sql.rows";

    /// <summary>
    /// The service name of a remote service.
    /// </summary>
    public const string PeerService = "peer.service";

    /// <summary>
    /// The hostname of a outgoing server connection.
    /// </summary>
    public const string OutHost = "out.host";

    /// <summary>
    /// Remote hostname.
    /// </summary>
    public const string PeerHostname = "peer.hostname";

    /// <summary>
    /// The port of a outgoing server connection.
    /// </summary>
    public const string OutPort = "out.port";

    /// <summary>
    /// The size of the message.
    /// </summary>
    public const string MessageSize = "message.size";

    /// <summary>
    /// The sampling priority for the entire trace.
    /// </summary>
    public const string SamplingPriority = "sampling.priority";

    /// <summary>
    /// A user-friendly tag that sets the sampling priority to <see cref="Trace.SamplingPriority.UserKeep"/>.
    /// </summary>
    public const string ManualKeep = "manual.keep";

    /// <summary>
    /// A user-friendly tag that sets the sampling priority to <see cref="Trace.SamplingPriority.UserReject"/>.
    /// </summary>
    public const string ManualDrop = "manual.drop";

    /// <summary>
    /// Language tag, applied to all spans that are .NET runtime (e.g. ASP.NET).
    /// This tag is added during MessagePack serialization. It's value is always "dotnet".
    /// </summary>
    public const string Language = "language";
}
