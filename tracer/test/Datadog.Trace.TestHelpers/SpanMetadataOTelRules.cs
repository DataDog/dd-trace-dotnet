// <copyright file="SpanMetadataOTelRules.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using static Datadog.Trace.TestHelpers.SpanMetadataRulesHelpers;

namespace Datadog.Trace.TestHelpers
{
    /// <summary>
    /// Span schema for <c>DD_TRACE_OTEL_SEMANTICS_ENABLED=true</c>, where HTTP spans carry the
    /// OpenTelemetry semantic convention attributes in place of the Datadog ones.
    /// Only integrations that have been converted are listed here.
    /// </summary>
    internal static class SpanMetadataOTelRules
    {
        // See: https://opentelemetry.io/docs/specs/semconv/http/http-spans/#http-client-span
        public static Result IsHttpClientRequestOTel(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "http.request")
                .Matches(Type, "http"))
            .Tags(s => s
                // Required
                .IsPresent("http.request.method")
                .IsPresent("server.address")
                .IsPresent("url.full")
                // Conditionally required
                .IsOptional("error.type")
                .IsOptional("http.request.method_original")
                .IsOptional("http.response.status_code")
                .IsOptional("network.protocol.name")
                .IsOptional("server.port")
                // Recommended
                .IsOptional("http.request.resend_count")
                .IsOptional("network.peer.address")
                .IsOptional("network.peer.port")
                .IsOptional("network.protocol.version")
                // DD Only
                .IsPresent("component")
                .IsOptional("http-client-handler-type")
                .IsOptional("_dd.base_service")
                .IsOptional("_dd.tags.process")
                .IsOptional("_dd.svc_src")
                .Matches("span.kind", "client"));

        // See: https://opentelemetry.io/docs/specs/semconv/http/http-spans/#http-server
        public static Result IsAspNetCoreOTel(this MockSpan span, ISet<string> excludeTags = null) => Result.FromSpan(span, excludeTags)
            .Properties(s => s
                .Matches(Name, "aspnet_core.request")
                .Matches(Type, "web"))
            .Tags(s => s
                // Required
                .IsPresent("http.request.method")
                .IsPresent("url.path")
                .IsPresent("url.scheme")
                // Conditionally required
                .IsOptional("error.type")
                .IsOptional("http.request.method_original")
                .IsPresent("http.response.status_code")
                .IsOptional("http.route")
                .IsOptional("server.port")
                .IsOptional("url.query")
                // Recommended
                .IsOptional("client.address")
                .IsOptional("network.peer.address")
                .IsOptional("server.address")
                .IsOptional("user_agent.original")
                // In OTel semantics mode exceptions are recorded as span events instead of error.* tags
                .IsOptional("events")
                // Datadog-only attributes with no OpenTelemetry equivalent, which the RFC retains
                .IsOptional("aspnet_core.endpoint")
                .IsOptional("aspnet_core.route")
                .IsOptional("http.endpoint")
                .IsOptional("_dd.code_origin.type")
                .IsOptional("_dd.code_origin.frames.0.index")
                .IsOptional("_dd.code_origin.frames.0.method")
                .IsOptional("_dd.code_origin.frames.0.type")
                .IsOptional("_dd.code_origin.frames.0.file")
                .IsOptional("_dd.code_origin.frames.0.line")
                .IsOptional("_dd.code_origin.frames.0.column")
                .IsOptional("_dd.base_service")
                .IsOptional("_dd.svc_src")
                .IsOptional("_dd.tags.process")
                .Matches("component", "aspnet_core")
                .Matches("span.kind", "server"));
    }
}
