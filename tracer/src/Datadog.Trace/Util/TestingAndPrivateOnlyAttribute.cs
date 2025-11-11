// <copyright file="TestingAndPrivateOnlyAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.SourceGenerators;

/// <summary>
/// A marker attribute added to APIs to indicate that they are only non-private
/// for testing purposes. Members with this attribute can be called from within
/// the type, just like a private member, or from test code, but not from other
/// types. Used by analyzers to confirm we're not calling testing convenience methods.
/// Contrasts with <see cref="TestingOnlyAttribute"/> which indicates members
/// should never be called in production code.
/// </summary>
[System.Diagnostics.Conditional("DEBUG")]
[System.AttributeUsage(
    System.AttributeTargets.Field
  | System.AttributeTargets.Property
  | System.AttributeTargets.Method
  | System.AttributeTargets.Constructor)]
internal sealed class TestingAndPrivateOnlyAttribute : System.Attribute
{
}
