// <copyright file="ITracer.Manual.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace;

/// <summary>
/// The tracer is responsible for creating spans and flushing them to the Datadog agent
/// </summary>
[DuckType("Datadog.Trace.Tracer", "Datadog.Trace")]
[DuckAsClass]
public partial interface ITracer
{
}
