// <copyright file="SpanMetadataOTelRules.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using static Datadog.Trace.TestHelpers.SpanMetadataRulesHelpers;

namespace Datadog.Trace.TestHelpers
{
#pragma warning disable SA1601 // Partial elements should be documented
    internal static class SpanMetadataOTelRules
    {
        // See: https://opentelemetry.io/docs/specs/semconv/http/http-spans/
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
    }
}
