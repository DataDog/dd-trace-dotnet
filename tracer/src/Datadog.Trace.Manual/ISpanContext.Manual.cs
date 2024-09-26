// <copyright file="ISpanContext.Manual.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace;

/// <summary>
/// Span context interface.
/// </summary>
[DuckType("Datadog.Trace.SpanContext", "Datadog.Trace")]
[DuckAsClass]
public partial interface ISpanContext
{
}
