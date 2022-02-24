// <copyright file="IBindingSourceValueProvider.cs" company="Datadog">
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
    /// IBindingSourceValueProvider
    /// </summary>
    public interface IBindingSourceValueProvider
    {
        /// <summary>
        /// Gets BindingSource
        /// </summary>
        public IBindingSource BindingSource { get; }

        /// <summary>
        /// Gets Values
        /// </summary>
        [DuckTyping.DuckField(Name = "_values")]
        IDictionary<string, Microsoft.Extensions.Primitives.StringValues> Values { get; }
    }
}
#endif
