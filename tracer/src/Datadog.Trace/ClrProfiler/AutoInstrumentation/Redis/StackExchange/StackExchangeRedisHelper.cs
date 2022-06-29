// <copyright file="StackExchangeRedisHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Redis.StackExchange
{
    /// <summary>
    /// Base class for redis integration.
    /// </summary>
    internal static class StackExchangeRedisHelper
    {
        internal const string IntegrationName = nameof(Configuration.IntegrationId.StackExchangeRedis);
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.StackExchangeRedis;

        /// <summary>
        /// Get the host and port from the config
        /// </summary>
        /// <param name="config">The config</param>
        /// <returns>The host and port</returns>
        public static HostAndPort GetHostAndPort(string config)
        {
            string host = null;
            string port = null;

            if (config != null)
            {
                // config can contain several settings separated by commas:
                // hostname:port,name=MyName,keepAlive=180,syncTimeout=10000,abortConnect=False
                // split in commas, find the one without '=', split that one on ':'
                string[] hostAndPort = config.Split(',')
                                             .FirstOrDefault(p => !p.Contains("="))
                                            ?.Split(':');

                if (hostAndPort != null)
                {
                    host = hostAndPort[0];
                }

                // check length because port is optional
                if (hostAndPort?.Length > 1)
                {
                    port = hostAndPort[1];
                }
            }

            return new HostAndPort(host, port);
        }

        internal readonly struct HostAndPort
        {
            public readonly string Host;
            public readonly string Port;

            public HostAndPort(string host, string port)
            {
                Host = host;
                Port = port;
            }
        }
    }
}
