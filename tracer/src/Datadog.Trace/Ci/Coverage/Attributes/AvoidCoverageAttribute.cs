// <copyright file="AvoidCoverageAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Ci.Coverage.Attributes;

[assembly: AvoidCoverage]

namespace Datadog.Trace.Ci.Coverage.Attributes
{
    /// <summary>
    /// Avoid coverage attribute
    /// Used to ignore processing an assembly
    /// </summary>
    public class AvoidCoverageAttribute : Attribute
    {
    }
}
