// <copyright file="ISpan.Manual.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace;

/// <summary>
/// A Span represents a logical unit of work in the system. It may be
/// related to other spans by parent/children relationships. The span
/// tracks the duration of an operation as well as associated metadata in
/// the form of a resource name, a service name, and user defined tags.
/// </summary>
[DuckType("Datadog.Trace.Span", "Datadog.Trace")]
[DuckAsClass]
public partial interface ISpan
{
}
