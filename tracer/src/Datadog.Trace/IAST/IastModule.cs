// <copyright file="IastModule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics;
using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;

namespace Datadog.Trace.IAST
{
    internal class IastModule
    {
        private const string OperationNameHash = "weak_hashing";
        private const string ServiceHash = "weak_hashing";

        public IastModule()
        {
        }

        public static Scope? OnHashingAlgorithm(string? algorithm, IntegrationId integrationId, Datadog.Trace.IAST.IAST iast)
        {
            if (algorithm == null || !InvalidHashAlgorithm(algorithm, iast))
            {
                return null;
            }

            return GetScope(algorithm, integrationId);
        }

        private static Scope GetScope(string evidenceValue, IntegrationId integrationId)
        {
            var tracer = Tracer.Instance;
            var frame = StackWalker.GetFrame();
            // Sometimes we do not have the file/line but we have the method/class.
            var vulnerability = new Vulnerability(VulnerabilityType.WEAK_HASH, new Location(frame?.GetFileName() ?? GetMethodName(frame), frame?.GetFileLineNumber()), new Evidence(evidenceValue));
            // The VulnerabilityBatch class is not very useful right now, but we will need it when handling requests
            var batch = new VulnerabilityBatch();
            batch.Add(vulnerability);

            // Right now, we always set the IastEnabled tag to "1", but in the future, it might be zero to indicate that a request has not been analyzed
            var tags = new InsecureHashingTags()
            {
                IastJson = batch.ToString(),
                IastEnabled = "1"
            };

            var serviceName = tracer.Settings.GetServiceName(tracer, ServiceHash);
            var scope = tracer.StartActiveInternal(OperationNameHash, serviceName: serviceName, tags: tags);
            tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(integrationId);

            return scope;
        }

        private static string? GetMethodName(StackFrame? frame)
        {
            var method = frame?.GetMethod();
            var declaringType = method?.DeclaringType;
            var namespaceName = declaringType?.Namespace;
            var typeName = declaringType?.Name;
            var methodName = method?.Name;

            if (methodName == null || typeName == null || namespaceName == null)
            {
                return null;
            }

            return StringBuilderCache.GetStringAndRelease(StringBuilderCache.Acquire(0).Append(namespaceName).Append('.').Append(typeName).Append("::").Append(methodName));
        }

        private static bool InvalidHashAlgorithm(string algorithm, Datadog.Trace.IAST.IAST iast)
        {
            return iast.Settings.InsecureHashingAlgorithms.Contains(algorithm);
        }
    }
}
