// <copyright file="IRequestContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Wcf
{
    /// <summary>
    /// System.ServiceModel.Channels.RequestContext interface for duck-typing
    /// </summary>
    internal interface IRequestContext
    {
        /// <summary>
        /// Gets the request message
        /// </summary>
        IMessage? RequestMessage { get; }
    }
}
