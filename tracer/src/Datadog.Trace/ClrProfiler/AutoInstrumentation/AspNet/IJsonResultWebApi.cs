// <copyright file="IJsonResultWebApi.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

#if NETFRAMEWORK
using System;
using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNet
{
    /// <summary>
    /// IJsonResult interface for duck typing of System.Web.Http.Results.JsonResult (webapi, netfx)
    /// </summary>
    internal interface IJsonResultWebApi
    {
        /// <summary>
        /// Gets the content object
        /// </summary>
        object? Content { get; }
    }
}
#endif
