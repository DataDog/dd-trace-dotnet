// <copyright file="IExceptionContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNet
{
    /// <summary>
    /// System.Web.Http.ExceptionHandling.ExceptionContext interface for ducktyping
    /// </summary>
    internal interface IExceptionContext
    {
        /// <summary>
        /// Gets the exception
        /// </summary>
        Exception Exception { get; }
    }
}
