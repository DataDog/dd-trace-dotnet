// <copyright file="IDnsQuestion.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

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
}
