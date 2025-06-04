// <copyright file="IScope.Manual.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace;

/// <summary>
/// A scope is a handle used to manage the concept of an active span.
/// Meaning that at a given time at most one span is considered active and
/// all newly created spans that are not created with the ignoreActiveSpan
/// parameter will be automatically children of the active span.
/// </summary>
// [DuckType("Datadog.Trace.IScope", "Datadog.Trace")] // This one is weird, the unit tests fail, will address later as may need duck typing changes
[DuckType("Datadog.Trace.Scope", "Datadog.Trace")]
[DuckAsClass]
public partial interface IScope
{
}
