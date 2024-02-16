// <copyright file="DuckTypeAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.DuckTyping;

[AttributeUsage(AttributeTargets.Interface)]
internal class DuckTypeAttribute : Attribute
{
    public DuckTypeAttribute(string targetType, string targetAssembly)
    {
        TargetType = targetType;
        TargetAssembly = targetAssembly;
    }

    public string TargetType { get; set; }

    public string TargetAssembly { get; set; }
}
