// <copyright file="IJson5LayoutProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Proxies
{
    internal interface IJson5LayoutProxy : IDuckType
    {
        /// <summary>
        /// Gets or sets a value indicating whether to include the contents of the ScopeContext properties-dictionary.
        /// </summary>
        /// <docgen category='Layout Options' order='10' />
        bool IncludeScopeProperties { get; set; }
    }
}
