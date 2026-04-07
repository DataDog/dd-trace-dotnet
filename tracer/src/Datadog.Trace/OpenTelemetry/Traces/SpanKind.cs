// <copyright file="SpanKind.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.OpenTelemetry.Traces;

/// <summary>
/// SpanKind code enum.
/// For the semantics of status codes see
/// Corresponds to opentelemetry.proto.trace.v1.Span.SpanKind
/// </summary>
internal enum SpanKind
{
    Unspecified = 0,
    Internal = 1,
    Server = 2,
    Client = 3,
    Producer = 4,
    Consumer = 5,
}
