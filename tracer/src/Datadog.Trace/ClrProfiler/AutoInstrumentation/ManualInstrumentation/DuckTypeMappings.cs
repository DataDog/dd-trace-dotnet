// <copyright file="DuckTypeMappings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

[assembly: DuckTypeMapping("Datadog.Trace.IScope", "Datadog.Trace.Manual", "Datadog.Trace.Scope", "Datadog.Trace")]
[assembly: DuckTypeMapping("Datadog.Trace.ISpan", "Datadog.Trace.Manual", "Datadog.Trace.Span", "Datadog.Trace")]
[assembly: DuckTypeMapping("Datadog.Trace.ISpanContext", "Datadog.Trace.Manual", "Datadog.Trace.SpanContext", "Datadog.Trace")]
