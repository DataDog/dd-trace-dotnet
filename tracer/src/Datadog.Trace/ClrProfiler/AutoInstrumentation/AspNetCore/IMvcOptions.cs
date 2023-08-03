// <copyright file="IMvcOptions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System;
using System.ComponentModel;
#if !NETFRAMEWORK
using System.Collections.Generic;
using Datadog.Trace.DuckTyping;
using Microsoft.AspNetCore.Http;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore
{
    /// <summary>
    /// aspnet core IMvcOptions
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IMvcOptions
    {
        /// <summary>
        /// Gets filters
        /// </summary>
        System.Collections.ObjectModel.Collection<Microsoft.AspNetCore.Mvc.Filters.IFilterMetadata> Filters { get; }
    }
}
#endif
