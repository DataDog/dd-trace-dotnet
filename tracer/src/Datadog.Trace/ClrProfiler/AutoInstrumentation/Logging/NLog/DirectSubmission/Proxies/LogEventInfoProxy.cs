// <copyright file="LogEventInfoProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Proxies
{
    /// <summary>
    /// Duck type for LogEventInfo  for NLog &gt; 4.5
    /// Using virtual members, as will need to be boxed, so no advantage from using a struct
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public abstract class LogEventInfoProxy : LogEventInfoProxyBase
    {
        /// <summary>
        /// Gets a value indicating whether there are any per-event properties (Without allocation)
        /// </summary>
        public virtual bool HasProperties { get; }

        /// <summary>
        /// Gets the dictionary of per-event context properties
        /// </summary>
        public virtual IDictionary<object, object> Properties { get; }
    }
}
