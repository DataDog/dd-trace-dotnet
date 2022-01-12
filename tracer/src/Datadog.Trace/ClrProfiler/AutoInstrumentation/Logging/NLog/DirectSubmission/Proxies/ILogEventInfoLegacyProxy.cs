// <copyright file="ILogEventInfoLegacyProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Proxies
{
    /// <summary>
    /// Duck type for LogEventInfo  for NLog &lt; 4.5
    /// Using virtual members, as will need to be boxed, so no advantage from using a struct
    /// </summary>
    internal interface ILogEventInfoLegacyProxy : ILogEventInfoProxyBase
    {
        /// <summary>
        /// Gets the dictionary of per-event context properties
        /// </summary>
        [DuckField(Name = "properties")]
        public IDictionary<object, object> Properties { get; }
    }
}
