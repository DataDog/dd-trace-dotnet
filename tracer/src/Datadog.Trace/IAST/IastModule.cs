// <copyright file="IastModule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.IAST.Settings;
using Datadog.Trace.Tagging;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.IAST
{
    internal class IastModule
    {
        private const string OperationNameHash = "insecure_hashing";
        private const string ServiceHash = "hash";

        public IastModule()
        {
        }

        public static Scope OnHashingAlgorithm(string algorithm, IntegrationId integrationId)
        {
            if (!InvalidHashAlgorithm(algorithm))
            {
                return null;
            }

            var tracer = Tracer.Instance;
            var algorithmId = algorithm.ToUpper();

            var stack = Environment.StackTrace;
            var stackTrace = new StackTrace(0, true);
            var frame = stackTrace.GetFrame(0);
            var vulnerability = new Vulnerability(VulnerabilityType.WEAK_HASH, new Location(frame.GetFileName(), frame.GetFileLineNumber()), new Evidence(algorithm));
            var json = JsonConvert.SerializeObject(vulnerability, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            var tags = new InsecureHashingTags()
            {
                IastJson = json
            };

            var serviceName = tracer.Settings.GetServiceName(tracer, ServiceHash);
            var scope = tracer.StartActiveInternal(OperationNameHash, serviceName: serviceName, tags: tags);
            scope.Span.Tags = tags;
            scope.Span.ResourceName = "hashing";
            scope.Span.Type = "type";
            tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(integrationId);

            return scope;
        }

        private static bool InvalidHashAlgorithm(string algorithm)
        {
            return IASTSettings.InsecureHashingAlgorithms.Contains(algorithm);
        }
    }
}
