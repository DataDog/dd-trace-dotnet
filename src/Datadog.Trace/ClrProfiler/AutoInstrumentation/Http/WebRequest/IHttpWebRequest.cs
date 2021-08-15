// <copyright file="IHttpWebRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.ComponentModel;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Http.WebRequest
{
    /// <summary>
    /// Duck type interface for HttpWebRequest
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IHttpWebRequest
    {
        /// <summary>
        /// Gets the time the HttpWebRequest was created in Ticks (UTC)
        /// </summary>
        [DuckField(Name = "m_StartTimestamp")]
        long RequestStartTicks { get; }
    }
}
