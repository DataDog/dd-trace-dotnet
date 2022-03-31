// <copyright file="IHttpActionContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNet
{
    /// <summary>
    /// HttpControllerContext interface for ducktyping
    /// </summary>
    internal interface IHttpActionContext
    {
        Dictionary<string, object> ActionArguments { get; }
    }
}
#endif
