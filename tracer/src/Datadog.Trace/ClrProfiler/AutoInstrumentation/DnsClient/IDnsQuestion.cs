// <copyright file="IDnsQuestion.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.DnsClient
{
    /// <summary>
    /// Duck type for DnsClient.DnsQuestion
    /// </summary>
    internal interface IDnsQuestion
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
    /// Duck type for DnsClient.NameServer
    /// </summary>
    internal interface INameServer
    {
        /// <summary>
        /// Gets the name server address.
        /// </summary>
        string? Address { get; }

        /// <summary>
        /// Gets the name server port.
        /// </summary>
        int Port { get; }
    }

    /// <summary>
    /// Duck type for DnsClient.DnsResponseHeader
    /// </summary>
    internal interface IDnsResponseHeader
    {
        /// <summary>
        /// Gets the response code (a DnsClient.DnsHeaderResponseCode enum).
        /// </summary>
        object? ResponseCode { get; }
    }

    /// <summary>
    /// Duck type for DnsClient.IDnsQueryResponse
    /// </summary>
    internal interface IDnsQueryResponse
    {
        /// <summary>
        /// Gets the response header.
        /// </summary>
        IDnsResponseHeader? Header { get; }

        /// <summary>
        /// Gets the answer records.
        /// </summary>
        IReadOnlyList<object>? Answers { get; }

        /// <summary>
        /// Gets the name server that answered the query (a DnsClient.NameServer).
        /// </summary>
        object? NameServer { get; }
    }
}
