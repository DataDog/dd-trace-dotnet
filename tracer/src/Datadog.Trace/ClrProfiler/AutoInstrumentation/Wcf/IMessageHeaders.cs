// <copyright file="IMessageHeaders.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Wcf
{
    /// <summary>
    /// System.ServiceModel.Channels.MessageHeaders interface for duck-typing
    /// </summary>
    internal interface IMessageHeaders
    {
        /// <summary>
        /// Gets the Action header
        /// </summary>
        string? Action { get; }

        /// <summary>
        /// Gets the To header
        /// </summary>
        Uri? To { get; }

        /// <summary>
        /// Gets a given header
        /// </summary>
        T? GetHeader<T>(string? name, string? ns);

        /// <summary>
        /// Finds a given header
        /// </summary>
        int FindHeader(string? name, string? ns);
    }
}
