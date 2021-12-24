// <copyright file="ILoggerFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.ComponentModel;
using System.Reflection;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.ILogger.DirectSubmission
{
    /// <summary>
    /// Duck type for ILogLevel
    /// </summary>
    internal interface ILoggerFactory : IDuckType
    {
        /// <summary>
        /// Used to add the ILoggerProvider
        /// </summary>
        /// <param name="provider">The ILoggerProvider to add</param>
        [Duck]
        public void AddProvider(object provider);
    }
}
