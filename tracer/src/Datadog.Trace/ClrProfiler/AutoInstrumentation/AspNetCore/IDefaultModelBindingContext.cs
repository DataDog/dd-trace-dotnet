// <copyright file="IDefaultModelBindingContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if !NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore
{
    /// <summary>
    /// IDefaultModelBindingContext for DefaultModelBindingContext
    /// </summary>
    public interface IDefaultModelBindingContext
    {
        /// <summary>
        /// Gets or sets the Model
        /// </summary>
        public object Model { get; set; }

        /// <summary>
        /// Gets the HttpContext
        /// </summary>
        public HttpContext HttpContext { get; }

        /// <summary>
        /// Gets or sets the Result
        /// </summary>
        public IModelBindingResult Result { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether IsTopLevelObject
        /// </summary>
        public bool IsTopLevelObject { get; set; }

        /// <summary>
        /// Gets or sets the BindingSource
        /// </summary>
        public IBindingSource BindingSource { get; set; }

        /// <summary>
        /// Gets or sets the ValueProvider
        /// </summary>
        public object ValueProvider { get; set; }
    }
}
#endif
