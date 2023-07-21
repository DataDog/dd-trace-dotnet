// <copyright file="IScopeContextSetterProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.LogsInjection
{
    /// <summary>
    /// Duck type for IScopeContextSetterProxy in NLog 5.0+
    /// </summary>
    internal interface IScopeContextSetterProxy
    {
        /// <summary>
        /// Updates the logical scope context with provided properties
        /// </summary>
        /// <param name="properties">Properties being added to the scope dictionary</param>
        /// <returns>A disposable object that removes the properties from logical context scope on dispose.</returns>
        /// <remarks>Scope dictionary keys are case-insensitive</remarks>
        IDisposable PushProperties(IReadOnlyList<KeyValuePair<string, object>> properties);
    }
}
