// <copyright file="TestingOnlyAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.SourceGenerators;

/// <summary>
/// A marker attribute added to APIs to indicate they must only be
/// used for testing. Used by analyzers to confirm we're not calling testing convenience methods.
/// Contrasts with <see cref="TestingAndPrivateOnlyAttribute"/> which indicates members
/// can also be used in production code if called from the same type
/// </summary>
[System.Diagnostics.Conditional("DEBUG")]
[System.AttributeUsage(
    System.AttributeTargets.Field
  | System.AttributeTargets.Property
  | System.AttributeTargets.Method
  | System.AttributeTargets.Constructor)]
internal sealed class TestingOnlyAttribute : System.Attribute
{
}
