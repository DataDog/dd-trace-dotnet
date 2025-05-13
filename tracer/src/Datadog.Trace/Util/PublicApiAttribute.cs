// <copyright file="PublicApiAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.SourceGenerators;

/// <summary>
/// A marker attribute added to a public API to indicate it should only be
/// called by consumers. Used by analyzers to confirm we're not calling a public API method.
/// </summary>
[System.Diagnostics.Conditional("DEBUG")]
[System.AttributeUsage(
    System.AttributeTargets.Field
  | System.AttributeTargets.Property
  | System.AttributeTargets.Method
  | System.AttributeTargets.Constructor)]
internal sealed class PublicApiAttribute : System.Attribute
{
}
