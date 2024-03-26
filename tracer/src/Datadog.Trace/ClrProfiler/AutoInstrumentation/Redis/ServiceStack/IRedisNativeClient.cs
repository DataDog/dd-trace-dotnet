// <copyright file="IRedisNativeClient.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Redis.ServiceStack
{
    /// <summary>
    /// Redis native client for duck typing
    /// </summary>
    internal interface IRedisNativeClient
    {
        /// <summary>
        /// Gets Client Hostname
        /// </summary>
        public string Host { get; }

        /// <summary>
        /// Gets Client Port
        /// </summary>
        public int Port { get; }

        /// <summary>
        /// Gets the database index
        /// </summary>
        public long Db { get; }
    }
}
