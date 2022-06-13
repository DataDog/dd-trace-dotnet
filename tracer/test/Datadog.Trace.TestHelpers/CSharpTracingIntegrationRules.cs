// <copyright file="CSharpTracingIntegrationRules.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.TestHelpers
{
#pragma warning disable SA1601 // Partial elements should be documented
    public static partial class CSharpTracingIntegrationRules
    {
        public static Result IsCouchbase(this MockSpan span) => Result.FromSpan(span)
            .PropertyMatches(Name, "couchbase.query")
            .PropertyMatches(Type, "db")
            .TagIsOptional("couchbase.operation.bucket")
            .TagIsPresent("couchbase.operation.code")
            .TagIsPresent("couchbase.operation.key")
            .TagIsOptional("out.port")
            .TagIsOptional("out.host")
            .TagMatches("component", "Couchbase")
            .TagMatches("span.kind", "client");

        public static Result IsKafka(this MockSpan span) => Result.FromSpan(span)
            .PropertyMatchesOneOf(Name, "kafka.consume", "kafka.produce")
            .PropertyMatches(Type, "queue")
            .TagIsOptional("kafka.offset")
            .TagIsOptional("kafka.partition")
            .TagIsOptional("kafka.tombstone")
            .TagIsOptional("message.queue_time_ms")
            .TagMatches("component", "kafka")
            .TagIsPresent("span.kind");

        public static Result IsWebRequest(this MockSpan span) => Result.FromSpan(span)
            .PropertyMatches(Name, "http.request")
            .PropertyMatches(Type, "http")
            .TagIsPresent("http.method")
            .TagIsPresent("http.status_code")
            .TagIsPresent("http.url")
            .TagMatchesOneOf("component", "HttpMessageHandler", "WebRequest")
            .TagMatches("span.kind", "client");
    }
}
