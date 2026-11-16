// <copyright file="IastMetricTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.ComponentModel;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Iast;

#pragma warning disable SA1134 // Keeping each metric tag and enum value together makes the mapping easier to audit.

[EnumExtensions]
internal enum IastSourceType
{
    [Description("source_type:http.request.body")] RequestBody = 0,
    [Description("source_type:http.request.path")] RequestPath = 1,
    [Description("source_type:http.request.parameter.name")] RequestParameterName = 2,
    [Description("source_type:http.request.parameter")] RequestParameterValue = 3,
    [Description("source_type:http.request.path.parameter")] RoutedParameterValue = 4,
    [Description("source_type:http.request.header")] RequestHeaderValue = 5,
    [Description("source_type:http.request.header.name")] RequestHeaderName = 6,
    [Description("source_type:http.request.query")] RequestQuery = 7,
    [Description("source_type:http.request.cookie.name")] CookieName = 8,
    [Description("source_type:http.request.cookie.value")] CookieValue = 9,
    [Description("source_type:http.request.matrix.parameter")] MatrixParameter = 10,
    [Description("source_type:http.request.uri")] RequestUri = 11,
    [Description("source_type:grpc.request.body")] GrpcRequestBody = 12,
    [Description("source_type:sql.row.value")] SqlRowValue = 13,
}

[EnumExtensions]
internal enum IastVulnerabilityType
{
    [Description("vulnerability_type:none")] None = 0,
    [Description("vulnerability_type:weak_cipher")] WeakCipher = 1,
    [Description("vulnerability_type:weak_hash")] WeakHash = 2,
    [Description("vulnerability_type:sql_injection")] SqlInjection = 3,
    [Description("vulnerability_type:command_injection")] CommandInjection = 4,
    [Description("vulnerability_type:path_traversal")] PathTraversal = 5,
    [Description("vulnerability_type:ldap_injection")] LdapInjection = 6,
    [Description("vulnerability_type:ssrf")] Ssrf = 7,
    [Description("vulnerability_type:unvalidated_redirect")] UnvalidatedRedirect = 8,
    [Description("vulnerability_type:insecure_cookie")] InsecureCookie = 9,
    [Description("vulnerability_type:no_httponly_cookie")] NoHttpOnlyCookie = 10,
    [Description("vulnerability_type:no_samesite_cookie")] NoSameSiteCookie = 11,
    [Description("vulnerability_type:weak_randomness")] WeakRandomness = 12,
    [Description("vulnerability_type:xcontenttype_header_missing")] XContentTypeHeaderMissing = 13,
    [Description("vulnerability_type:trust_boundary_violation")] TrustBoundaryViolation = 14,
    [Description("vulnerability_type:hsts_header_missing")] HstsHeaderMissing = 15,
    [Description("vulnerability_type:header_injection")] HeaderInjection = 16,
    [Description("vulnerability_type:stacktrace_leak")] StackTraceLeak = 17,
    [Description("vulnerability_type:nosql_mongodb_injection")] NoSqlMongoDbInjection = 18,
    [Description("vulnerability_type:xpath_injection")] XPathInjection = 19,
    [Description("vulnerability_type:reflection_injection")] ReflectionInjection = 20,
    [Description("vulnerability_type:insecure_auth_protocol")] InsecureAuthProtocol = 21,
    [Description("vulnerability_type:xss")] Xss = 22,
    [Description("vulnerability_type:directory_listing_leak")] DirectoryListingLeak = 23,
    [Description("vulnerability_type:session_timeout")] SessionTimeout = 24,
    [Description("vulnerability_type:email_html_injection")] EmailHtmlInjection = 25,
}

#pragma warning restore SA1134
