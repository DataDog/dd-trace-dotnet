// <copyright file="IValueProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if !NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore
{
    /// <summary>
    /// IValueProvider for value provider
    /// </summary>
    internal interface IValueProvider
    {
        /// <summary>
        /// Gets or sets BindingSource
        /// </summary>
        IBindingSource BindingSource { get; set; }
    }
}
#endif
