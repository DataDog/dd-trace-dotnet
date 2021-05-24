// <copyright file="ControllerContextStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System.Web;
using System.Web.Routing;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.Integrations.AspNet
{
    /// <summary>
    /// ControllerContext struct copy target for ducktyping
    /// </summary>
    [DuckCopy]
    public struct ControllerContextStruct
    {
        /// <summary>
        /// Gets the HttpContext
        /// </summary>
        public HttpContextBase HttpContext;

        /// <summary>
        /// Gets the RouteData
        /// </summary>
        public RouteData RouteData;
    }
}
#endif
