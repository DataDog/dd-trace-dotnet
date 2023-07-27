// <copyright file="AerospikeCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Aerospike
{
    internal class AerospikeCommon
    {
        private const string DatabaseType = "aerospike";
        private const string OperationName = "aerospike.command";
        public const string IntegrationName = nameof(Configuration.IntegrationId.Aerospike);
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.Aerospike;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AerospikeCommon));

        public static Scope CreateScope<TTarget>(Tracer tracer, TTarget target)
        {
            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            Scope scope = null;

            try
            {
                var serviceName = tracer.CurrentTraceSettings.Schema.Database.GetServiceName(DatabaseType);
                var tags = tracer.CurrentTraceSettings.Schema.Database.CreateAerospikeTags();

                scope = tracer.StartActiveInternal(OperationName, tags: tags, serviceName: serviceName);
                var span = scope.Span;

                if (target.TryDuckCast<HasKey>(out var hasKey))
                {
                    var key = hasKey.Key;

                    tags.Key = FormatKey(key);
                    tags.Namespace = key.Ns;
                    tags.SetName = key.SetName;
                    tags.UserKey = key.UserKey.ToString();
                }
                else if (target.TryDuckCast<HasKeys>(out var hasKeys))
                {
                    bool isFirstKey = true;
                    var sb = StringBuilderCache.Acquire(0);

                    foreach (var obj in hasKeys.Keys)
                    {
                        var key = obj.DuckCast<Key>();

                        // All keys will be in the same namespace (namespace > set > record > key), so we can apply the namespace from the first key we see
                        if (isFirstKey)
                        {
                            tags.Namespace = key.Ns;
                            isFirstKey = false;
                        }

                        if (sb.Length != 0)
                        {
                            sb.Append(';');
                        }

                        sb.Append(FormatKey(key));
                    }

                    tags.Key = StringBuilderCache.GetStringAndRelease(sb);
                }
                else if (target.TryDuckCast<HasStatement>(out var hasStatement))
                {
                    tags.Key = hasStatement.Statement.Ns + ":" + hasStatement.Statement.SetName;
                    tags.Namespace = hasStatement.Statement.Ns;
                    tags.SetName = hasStatement.Statement.SetName;
                }

                span.Type = SpanTypes.Aerospike;
                span.ResourceName = ExtractResourceName(target.GetType());

                tags.SetAnalyticsSampleRate(IntegrationId, tracer.Settings, enabledWithGlobalSetting: false);
                tracer.CurrentTraceSettings.Schema.RemapPeerService(tags);
                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            return scope;
        }

        private static string FormatKey(Key key) => key.Ns + ":" + key.SetName + ":" + key.UserKey;

        private static string ExtractResourceName(Type type)
        {
            const string asyncPrefix = "Async";
            const string commandSuffix = "Command";

            var typeName = type.Name;
            var startIndex = 0;

            if (typeName.StartsWith(asyncPrefix))
            {
                startIndex = asyncPrefix.Length;
            }

            var length = typeName.Length - startIndex;

            if (typeName.EndsWith(commandSuffix))
            {
                length -= commandSuffix.Length;
            }

            return typeName.Substring(startIndex, length);
        }
    }
}
