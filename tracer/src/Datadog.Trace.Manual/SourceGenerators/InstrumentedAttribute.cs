// <copyright file="InstrumentedAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.SourceGenerators;

/// <summary>
/// An attribute applied to members to indicate that they're used by automatic instrumentation and shouldn't be changed
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Constructor)]
internal sealed class InstrumentedAttribute : Attribute
{
}
