// <copyright file="MessageTemplateProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.Serilog.DirectSubmission
{
    /// <summary>
    /// Duck typing for MessageTemplate
    /// </summary>
    [DuckCopy]
    internal struct MessageTemplateProxy
    {
        /// <summary>
        /// Gets the raw text describing the template
        /// </summary>
        public string Text;
    }
}
