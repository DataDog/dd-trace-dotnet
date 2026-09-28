// <copyright file="IDnsQuestion.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.DnsClient
{
    /// <summary>
    /// Duck type for DnsClient.DnsQuestion
    /// </summary>
    internal interface IDnsQuestion : IDuckType
    {
        /// <summary>
        /// Gets the query name (a DnsClient.DnsString, read as a string via ToString()).
        /// </summary>
        object? QueryName { get; }

        /// <summary>
        /// Gets the query record type (a DnsClient.QueryType enum).
        /// </summary>
        object? QuestionType { get; }

        /// <summary>
        /// Gets the query class (a DnsClient.QueryClass enum).
        /// </summary>
        object? QuestionClass { get; }
    }

    /// <summary>
    /// Duck type for DnsClient.IDnsQueryResponse
    /// </summary>
    internal interface IDnsQueryResponse : IDuckType
    {
        /// <summary>
        /// Gets the response header.
        /// </summary>
        DnsResponseHeaderStruct? Header { get; }

        /// <summary>
        /// Gets the answer records.
        /// </summary>
        IReadOnlyList<object>? Answers { get; }

        /// <summary>
        /// Gets the name server that answered the query (a DnsClient.NameServer).
        /// </summary>
        NameServerStruct? NameServer { get; }
    }

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

    /// <summary>
    /// Duck type for DnsClient.DnsResponseHeader
    /// </summary>
    [DuckCopy]
    internal struct DnsResponseHeaderStruct
    {
        /// <summary>
        /// The response code (a DnsClient.DnsHeaderResponseCode enum).
        /// </summary>
        public object? ResponseCode;
    }
}
