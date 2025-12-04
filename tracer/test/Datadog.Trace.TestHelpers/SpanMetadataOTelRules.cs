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
        public static Result IsHttpMessageHandlerOTel(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "http.client.request")
                .Matches(Type, "http"))
            .Tags(s => s
                .MatchesOneOf("http.request.method", "_OTHER", "CONNECT", "DELETE", "GET", "HEAD", "OPTIONS", "PATCH", "POST", "PUT", "TRACE")
                .IsPresent("server.address")
                .IsPresent("server.port")
                .IsPresent("url.full")
                // .IsOptional("error.type") // TODO: This might be covered already
                // .IsOptional("http.request.header.<key>") // TODO: Find a way to represent this
                .IsOptional("http.request.method_original")
                .IsOptional("http.request.resend_count")
                .IsOptional("http.request.size")
                // .IsOptional("http.response.header.<key>") // TODO: Find a way to represent this
                .IsOptional("http.response.status_code")
                .IsOptional("network.peer.address")
                .IsOptional("network.peer.port")
                .IsOptional("network.protocol.name")
                .IsOptional("network.protocol.version")
                .IfPresentMatchesOneOf("network.transport", "pipe", "quic", "tcp", "udp", "unix")
                .IsOptional("url.scheme")
                .IsOptional("user_agent.original")
                .IsOptional("_dd.base_service")
                .Matches("span.kind", "client"));

        public static Result IsWebRequestOTel(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "http.client.request")
                .Matches(Type, "http"))
            .Tags(s => s
                .MatchesOneOf("http.request.method", "_OTHER", "CONNECT", "DELETE", "GET", "HEAD", "OPTIONS", "PATCH", "POST", "PUT", "TRACE")
                .IsPresent("server.address")
                .IsPresent("server.port")
                .IsPresent("url.full")
                // .IsOptional("error.type") // TODO: This might be covered already
                // .IsOptional("http.request.header.<key>") // TODO: Find a way to represent this
                .IsOptional("http.request.method_original")
                .IsOptional("http.request.resend_count")
                .IsOptional("http.request.size")
                // .IsOptional("http.response.header.<key>") // TODO: Find a way to represent this
                .IsOptional("http.response.status_code")
                .IsOptional("network.peer.address")
                .IsOptional("network.peer.port")
                .IsOptional("network.protocol.name")
                .IsOptional("network.protocol.version")
                .IfPresentMatchesOneOf("network.transport", "pipe", "quic", "tcp", "udp", "unix")
                .IsOptional("url.scheme")
                .IsOptional("user_agent.original")
                .IsOptional("_dd.base_service")
                .MatchesOneOf("_dd.peer.service.source", "out.host", "peer.service")
                .IsPresent("peer.service")
                .IsOptional("peer.service.remapped_from")
                .Matches("span.kind", "client"));
    }
}
