// <copyright file="BindingSourceValueProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if !NETFRAMEWORK

using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore
{
    /// <summary>
    /// IBindingSourceValueProvider
    /// </summary>
    [DuckTyping.DuckCopy]
    internal struct BindingSourceValueProvider
    {
        /// <summary>
        /// Gets BindingSource
        /// </summary>
        public BindingSource BindingSource;

        /// <summary>
        /// Gets Values
        /// </summary>
        [DuckTyping.DuckField(Name = "_values")]
        public IDictionary<string, Microsoft.Extensions.Primitives.StringValues> Values { get; }
    }
}
#endif
