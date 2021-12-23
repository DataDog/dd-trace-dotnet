﻿// <copyright file="ITargetProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Proxies
{
    /// <summary>
    /// Duck type for TargetWithContext
    /// Represents target that supports context capture using MDLC, MDC, NDLC and NDC
    /// </summary>
    internal interface ITargetProxy
    {
        /// <summary>
        /// Initializes this instance.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        [Duck(ExplicitInterfaceTypeName = "NLog.Internal.ISupportsInitialize", ParameterTypeNames = new[] { "NLog.Config.LoggingConfiguration" })]
        public void Initialize(object configuration);
    }
}
