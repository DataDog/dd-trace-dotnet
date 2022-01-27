// <copyright file="IActionContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore
{
    internal interface IActionContext
    {
        IHttpContext HttpContext { get;  }

        IRouteData RouteData { get; }

        /// <summary>
        /// Delegates to ActionContext.ActionDescriptor;
        /// </summary>
        /// <returns>The ActionDescriptor proxy</returns>
        [Duck(Name = "get_ActionDescriptor", ReturnTypeName = "Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor")]
        IActionDescriptor GetActionDescriptor();
    }
}
#endif
