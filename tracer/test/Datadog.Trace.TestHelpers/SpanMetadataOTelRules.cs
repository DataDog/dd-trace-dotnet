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

        // See: https://opentelemetry.io/docs/specs/semconv/database/database-spans/
        //  and https://opentelemetry.io/docs/specs/semconv/database/sql/
        /// <param name="span">The database client span.</param>
        /// <param name="operationName">The Datadog operation name, or <c>null</c> for a custom ADO.NET provider, whose operation name is derived from its command type.</param>
        /// <param name="dbSystemName">The expected "db.system.name", or <c>null</c> for a custom ADO.NET provider, which has no fixed value.</param>
        public static Result IsDatabaseClientOTel(this MockSpan span, string operationName = null, string dbSystemName = null) => Result.FromSpan(span)
            .Properties(s =>
            {
                if (operationName is not null)
                {
                    s.Matches(Name, operationName);
                }

                s.Matches(Type, "sql");
            })
            .Tags(s =>
            {
                // Required. A custom ADO.NET provider reports the name we derived from its command
                // type, so the caller only supplies one when the specification names the provider.
                if (dbSystemName is null)
                {
                    s.IsPresent("db.system.name");
                }
                else
                {
                    s.Matches("db.system.name", dbSystemName);
                }

                s
                // Conditionally required
                .IsOptional("db.namespace")
                .IsOptional("db.response.status_code")
                .IsOptional("error.type")
                .IsOptional("server.port")
                // Recommended
                .IsOptional("db.collection.name")
                .IsOptional("db.operation.name")
                .IsOptional("db.query.summary")
                .IsOptional("db.query.text")
                .IsOptional("db.stored_procedure.name")
                .IsOptional("server.address")
                // DD Only
                .IsPresent("component")
                .IsOptional("_dd.base_service")
                .IsOptional("_dd.dbm_trace_injected")
                .IsOptional("_dd.propagated_hash")
                .IsOptional("_dd.svc_src")
                .IsOptional("_dd.tags.process")
                .IsOptional("dd.instrumentation.time_ms")
                .Matches("span.kind", "client");
            });
    }
}
