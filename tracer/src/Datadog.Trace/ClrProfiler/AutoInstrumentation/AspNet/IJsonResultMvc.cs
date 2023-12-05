// <copyright file="IJsonResultMvc.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
#nullable enable
using System;
using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNet
{
    /// <summary>
    /// IJsonResult interface for duck typing of System.Web.Mvc.JsonResult (mvc, netfx)
    /// </summary>
    internal interface IJsonResultMvc
    {
        /// <summary>
        /// Gets the content object
        /// </summary>
        object? Data { get; }
    }
}
#endif
