// <copyright file="DynamicInstrumentationHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable
namespace Datadog.Trace.ClrProfiler
{
    internal class DynamicInstrumentationHelper
    {
        /// <summary>
        /// Gets or sets the service name.
        /// Hack to maneuver the service name for Exception Debugging. Will be removed later.
        /// </summary>
        public static string ServiceName { get; set; } = string.Empty;
    }
}
