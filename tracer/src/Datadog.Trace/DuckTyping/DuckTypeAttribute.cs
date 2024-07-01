// <copyright file="DuckTypeAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;

namespace Datadog.Trace.DuckTyping;

/// <summary>
/// The type is used as a duck type.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
internal class DuckTypeAttribute(string targetType, string targetAssembly) : Attribute
{
    public string? TargetType { get; set; } = targetType;

    public string? TargetAssembly { get; set; } = targetAssembly;
}
