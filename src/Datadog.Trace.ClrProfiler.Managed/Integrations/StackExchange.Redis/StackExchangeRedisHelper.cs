using System;
using System.Linq;
using System.Reflection;
using Datadog.Trace.ClrProfiler.Emit;

namespace Datadog.Trace.ClrProfiler.Integrations.StackExchange.Redis
{
    /// <summary>
    /// Base class for redis integration.
    /// </summary>
    internal static class StackExchangeRedisHelper
    {
        /// <summary>
        /// Get the configuration for the multiplexer.
        /// </summary>
        /// <param name="multiplexer">The multiplexer</param>
        /// <returns>The configuration</returns>
        public static string GetConfiguration(object multiplexer)
        {
            return multiplexer.GetProperty<string>("Configuration").GetValueOrDefault();
        }

        /// <summary>
        /// Get the host and port from the config
        /// </summary>
        /// <param name="config">The config</param>
        /// <returns>The host and port</returns>
        public static Tuple<string, string> GetHostAndPort(string config)
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

            return new Tuple<string, string>(host, port);
        }

        /// <summary>
        /// Get the raw command.
        /// </summary>
        /// <param name="multiplexer">The multiplexer</param>
        /// <param name="message">The message</param>
        /// <returns>The raw command</returns>
        public static string GetRawCommand(object multiplexer, object message)
        {
            return message.GetProperty<string>("CommandAndKey").GetValueOrDefault() ?? "COMMAND";
        }

        /// <summary>
        /// GetMultiplexer returns the Multiplexer for an object
        /// </summary>
        /// <param name="obj">The object</param>
        /// <returns>The multiplexer</returns>
        public static object GetMultiplexer(object obj)
        {
            object multiplexer = null;
            try
            {
                var fi = obj.GetType().GetField("multiplexer", BindingFlags.NonPublic | BindingFlags.Instance);
                multiplexer = fi.GetValue(obj);
            }
            catch
            {
            }

            return multiplexer;
        }
    }
}
