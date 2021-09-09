// <copyright file="DuckIgnoreAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.DuckTyping
{
    /// <summary>
    /// Ignores the member when DuckTyping
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Field, AllowMultiple = false)]
    public class DuckIgnoreAttribute : Attribute
    {
    }
}
