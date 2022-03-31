// <copyright file="DefaultModelBindingContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if !NETFRAMEWORK

using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Datadog.Trace.DuckTyping;
using Microsoft.AspNetCore.Http;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore
{
    /// <summary>
    /// DefaultModelBindingContext
    /// </summary>
    [DuckCopy]
    internal struct DefaultModelBindingContext
    {
        /// <summary>
        /// Gets or sets the Model
        /// </summary>
        public object Model;

        /// <summary>
        /// Gets the HttpContext
        /// </summary>
        public HttpContext HttpContext;

        [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
        public ModelBindingResult Result;

        [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
        public bool IsTopLevelObject;

        [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
        public BindingSource BindingSource;

        /// <summary>
        /// Gets or sets the ValueProvider
        /// </summary>
        public IList ValueProvider;
    }
}
#endif
