// <copyright file="NameServerStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.DnsClient
{
    /// <summary>
    /// Duck type for DnsClient.NameServer
    /// </summary>
    [DuckCopy]
    internal struct NameServerStruct
    {
        /// <summary>
        /// The name server address.
        /// </summary>
        public string? Address;

        /// <summary>
        /// The name server port.
        /// </summary>
        public int Port;
    }
}
