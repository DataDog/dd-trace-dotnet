// <copyright file="InternalForTestingAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.SourceGenerators;

/// <summary>
/// A marker attribute added to APIs to indicate they should only be
/// used for testing. Used by analyzers to confirm we're not calling testing convenience methods.
/// </summary>
[System.Diagnostics.Conditional("DEBUG")]
[System.AttributeUsage(
    System.AttributeTargets.Field
  | System.AttributeTargets.Property
  | System.AttributeTargets.Method
  | System.AttributeTargets.Constructor)]
internal sealed class InternalForTestingAttribute : System.Attribute
{
}
