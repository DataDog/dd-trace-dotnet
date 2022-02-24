// <copyright file="ICompositeValueProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if !NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore
{
    /// <summary>
    /// ICompositeValueProvider
    /// </summary>
    internal interface ICompositeValueProvider
    {
        /// <summary>
        /// Gets the Count at the specified index
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Gets the ExecutionError at the specified index
        /// </summary>
        /// <param name="index">Index to lookup</param>
        /// <returns>An execution error</returns>
        object this[int index] { get; set; }
    }
}
#endif
