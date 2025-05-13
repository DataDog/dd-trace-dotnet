// <copyright file="DuckAsClassAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;

namespace Datadog.Trace.DuckTyping;

/// <summary>
/// Applied to an interface to indicate that it should be duck typed as a class instead of as a struct.
/// </summary>
[AttributeUsage(AttributeTargets.Interface)]
internal class DuckAsClassAttribute : Attribute
{
}
