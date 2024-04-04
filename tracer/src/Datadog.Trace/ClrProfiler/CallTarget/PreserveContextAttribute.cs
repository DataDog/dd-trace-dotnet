// <copyright file="PreserveContextAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;

namespace Datadog.Trace.ClrProfiler.CallTarget
{
    /// <summary>
    /// Apply on a calltarget async callback to indicate that the method
    /// should execute under the current synchronization context/task scheduler.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    internal class PreserveContextAttribute : Attribute
    {
    }
}
