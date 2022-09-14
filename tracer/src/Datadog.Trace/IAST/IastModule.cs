// <copyright file="IastModule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics;
using System.Linq;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.HashAlgorithm;
using Datadog.Trace.Configuration;
using Datadog.Trace.Iast;
using Datadog.Trace.Util;

namespace Datadog.Trace.Iast
{
    internal class IastModule
    {
        private const string OperationNameHash = "weak_hashing";

        public IastModule()
        {
        }

        public static Scope? OnHashingAlgorithm(string? algorithm, IntegrationId integrationId, Datadog.Trace.Iast.Iast iast)
        {
            if (algorithm == null || !InvalidHashAlgorithm(algorithm, iast))
            {
                return null;
            }

            return GetScope(Tracer.Instance, algorithm, integrationId);
        }

        private static Scope? GetScope(Tracer tracer, string evidenceValue, IntegrationId integrationId)
        {
            var frameInfo = StackWalker.GetFrame();

            if (!frameInfo.IsValid)
            {
                return null;
            }

            // Sometimes we do not have the file/line but we have the method/class.
            var filename = frameInfo.StackFrame?.GetFileName();
            var vulnerability = new Vulnerability(VulnerabilityType.WeakHash, new Location(filename ?? GetMethodName(frameInfo.StackFrame), filename != null ? frameInfo.StackFrame?.GetFileLineNumber() : null), new Evidence(evidenceValue));
            // The VulnerabilityBatch class is not very useful right now, but we will need it when handling requests
            var batch = new VulnerabilityBatch();
            batch.Add(vulnerability);

            // Right now, we always set the IastEnabled tag to "1", but in the future, it might be zero to indicate that a request has not been analyzed
            var tags = new IastTags()
            {
                IastJson = batch.ToString(),
                IastEnabled = "1"
            };

            var scope = tracer.StartActiveInternal(OperationNameHash, tags: tags);
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

            return $"{namespaceName}.{typeName}::{methodName}";
        }

        private static bool InvalidHashAlgorithm(string algorithm, Iast iast)
        {
            foreach (var weakHashAlgorithm in iast.Settings.WeakHashAlgorithmsArray)
            {
                if (string.Equals(algorithm, weakHashAlgorithm, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
