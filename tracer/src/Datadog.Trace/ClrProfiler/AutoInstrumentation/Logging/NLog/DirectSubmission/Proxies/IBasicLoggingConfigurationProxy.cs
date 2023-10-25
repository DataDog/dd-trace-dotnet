// <copyright file="IBasicLoggingConfigurationProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Proxies
{
    internal interface IBasicLoggingConfigurationProxy : IDuckType
    {
        /// <summary>
        /// Gets a collection of named targets specified in the configuration.
        /// </summary>
        public IEnumerable ConfiguredNamedTargets { get; }
    }
}
