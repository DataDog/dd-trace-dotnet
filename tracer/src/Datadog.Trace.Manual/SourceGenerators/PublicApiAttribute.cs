// <copyright file="PublicApiAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Diagnostics;

namespace Datadog.Trace.SourceGenerators;

/// <summary>
/// This attribute is purely to keep the compiler happy
/// </summary>
[Conditional("KEEP_ALL_ATTRIBUTES_EVEN_THOUGH_ITS_NOT_NECESSARY")]
internal class PublicApiAttribute : Attribute
{
}
