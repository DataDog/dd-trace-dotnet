// <copyright file="Meziantou.Analyzer.Annotations.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

// This file contains annotation attributes recognised by Meziantou.Analyzer. The analyzer matches them by name, so we
// define them here instead of referencing the Meziantou.Analyzer.Annotations package. They are [Conditional], so they
// aren't compiled into the assembly.

#pragma warning disable SA1649 // file name should match first type name

// ReSharper disable once CheckNamespace
namespace Meziantou.Analyzer.Annotations;

/// <summary>
/// Indicates that the value of a property, field or parameter is formatted in a culture-insensitive way, which
/// suppresses the culture-sensitivity rules (e.g. MA0076) for it.
/// </summary>
[System.Diagnostics.Conditional("MEZIANTOU_ANALYZER_ANNOTATIONS")]
[System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Field | System.AttributeTargets.Parameter)]
internal sealed class CultureInsensitiveAttribute : System.Attribute
{
}
