// <copyright file="RedisHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Linq;
using System.Text;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Redis
{
    internal static class RedisHelper
    {
        private const string OperationName = "redis.command";
        private const string ServiceName = "redis";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(RedisHelper));

        internal static Scope? CreateScope(Tracer tracer, IntegrationId integrationId, string integrationName, string? host, string? port, string rawCommand, long? databaseIndex)
        {
            if (!Tracer.Instance.Settings.IsIntegrationEnabled(integrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            var parent = tracer.ActiveScope?.Span;
            if (parent != null &&
                parent.Type == SpanTypes.Redis &&
                parent.GetTag(Tags.InstrumentationName) != null)
            {
                return null;
            }

            string serviceName = tracer.CurrentTraceSettings.Schema.Database.GetServiceName(ServiceName);
            Scope? scope = null;

            try
            {
                var tags = tracer.CurrentTraceSettings.Schema.Database.CreateRedisTags();
                tags.InstrumentationName = integrationName;

                scope = tracer.StartActiveInternal(OperationName, serviceName: serviceName, tags: tags);
                int separatorIndex = rawCommand.IndexOf(' ');
                string command;

                if (separatorIndex >= 0)
                {
                    command = rawCommand.Substring(0, separatorIndex);
                }
                else
                {
                    command = rawCommand;
                }

                var span = scope.Span;
                span.Type = SpanTypes.Redis;
                span.ResourceName = command;
                tags.RawCommand = rawCommand;
                tags.Host = host;
                tags.Port = port;
                if (databaseIndex.HasValue)
                {
                    tags.DatabaseIndex = databaseIndex.Value;
                }

                tags.SetAnalyticsSampleRate(integrationId, tracer.Settings, enabledWithGlobalSetting: false);
                tracer.CurrentTraceSettings.Schema.RemapPeerService(tags);
                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(integrationId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            return scope;
        }

        internal static string GetRawCommand(byte[][] cmdWithBinaryArgs)
        {
            return string.Join(
                " ",
                cmdWithBinaryArgs.Select(
                    bs =>
                    {
                        try
                        {
                            return Encoding.UTF8.GetString(bs);
                        }
                        catch
                        {
                            return string.Empty;
                        }
                    }));
        }
    }
}
