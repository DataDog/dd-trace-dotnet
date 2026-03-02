// <copyright file="ActionDescriptorWithMethodInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
#nullable enable

using System.Reflection;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNet
{
    /// <summary>
    /// Duck type for MVC/WebApi2 descriptors that expose a MethodInfo.
    /// </summary>
    [DuckCopy]
    internal struct ActionDescriptorWithMethodInfo
    {
        [Duck]
        public MethodInfo MethodInfo;
    }
}
#endif

