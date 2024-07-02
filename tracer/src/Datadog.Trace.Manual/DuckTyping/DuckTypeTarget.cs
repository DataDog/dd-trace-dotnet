// <copyright file="DuckTypeTarget.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping;

/// <summary>
/// An attribute applied to indicate that they're used by automatic instrumentation duck typing and shouldn't be changed
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Constructor)]
internal sealed class DuckTypeTarget : Attribute
{
}
