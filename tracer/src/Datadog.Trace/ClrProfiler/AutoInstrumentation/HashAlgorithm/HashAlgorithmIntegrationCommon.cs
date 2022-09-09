// <copyright file="HashAlgorithmIntegrationCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Security.Cryptography;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.HashAlgorithm
{
    internal class HashAlgorithmIntegrationCommon
    {
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.HashAlgorithm;
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(HashAlgorithmIntegrationCommon));
        internal const string OperationName = "insecure_hashing";
        internal const string ServiceName = "hash";

        internal static Scope CreateScope(System.Security.Cryptography.HashAlgorithm instance)
        {
            var tracer = Tracer.Instance;
            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId) || !InvalidHashAlgorithm(instance))
            {
                // skip this span
                return null;
            }

            Scope scope = null;

            try
            {
                var tags = new InsecureHashingTags
                {
                };

                var serviceName = tracer.Settings.GetServiceName(tracer, ServiceName);
                scope = tracer.StartActiveInternal(OperationName, serviceName: serviceName, tags: tags);
                scope.Span.ResourceName = "hashing";
                scope.Span.Type = "type";
                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating insecure hashing scope.");
            }

            return scope;
        }

        private static bool InvalidHashAlgorithm(System.Security.Cryptography.HashAlgorithm target)
        {
            return (target is HMACMD5) || (target is MD5) || (target is HMACSHA1) || (target is SHA1);
        }
    }
}
