// <copyright file="IApplicationBuilder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
#if !NETFRAMEWORK
using System.Collections.Generic;
using Datadog.Trace.DuckTyping;
using Microsoft.AspNetCore.Http;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore
{
    /// <summary>
    /// appbuilder
    /// </summary>
    public interface IApplicationBuilder
    {
        /// <summary>
        /// Gets ss
        /// </summary>
        [DuckField(Name = "_components")]
        IList<Func<RequestDelegate, RequestDelegate>> Components { get; }
    }
}
#endif
