// <copyright file="TracesTransportType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Agent
{
    /// <summary>
    /// Available types of transports.
    /// </summary>
    internal enum TracesTransportType
    {
        /// <summary>
        /// Default transport.
        /// Defers transport logic to agent API.
        /// </summary>
        Default,

        /// <summary>
        /// Experimental TCP strategy.
        /// Potential candidate for removing reliance on System.Net.Http.
        /// </summary>
        CustomTcpProvider,

        /// <summary>
        /// Windows Named Pipe strategy.
        /// Transport used primarily for Azure App Service.
        /// </summary>
        WindowsNamedPipe,

        /// <summary>
        /// Unix Domain Socket strategy.
        /// Transport used primarily for kubernetes
        /// </summary>
        UnixDomainSocket
    }
}
