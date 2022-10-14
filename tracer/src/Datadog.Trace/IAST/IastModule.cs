// <copyright file="IastModule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using Datadog.Trace.Configuration;
using Datadog.Trace.Util;

namespace Datadog.Trace.Iast
{
    internal class IastModule
    {
        private const string OperationNameWeakHash = "weak_hashing";
        private const string OperationNameWeakCipher = "weak_cipher";
        private static bool isLinux;

        public IastModule()
        {
            isLinux = string.Equals(FrameworkDescription.Instance.OSPlatform, "Linux", StringComparison.OrdinalIgnoreCase);
        }

        public static Scope? OnCipherAlgorithm(Type type, IntegrationId integrationId, Iast iast)
        {
            var algorithm = type.BaseType?.Name;
            if (algorithm == null || !InvalidCipherAlgorithm(type, algorithm, iast))
            {
                return null;
            }

            return GetScope(Tracer.Instance, algorithm, integrationId, VulnerabilityType.WeakCipher, OperationNameWeakCipher);
        }

        public static Scope? OnHashingAlgorithm(string? algorithm, IntegrationId integrationId, Iast iast)
        {
            if (algorithm == null || !InvalidHashAlgorithm(algorithm, iast))
            {
                return null;
            }

            return GetScope(Tracer.Instance, algorithm, integrationId, VulnerabilityType.WeakHash, OperationNameWeakHash);
        }

        private static Scope? GetScope(Tracer tracer, string evidenceValue, IntegrationId integrationId, string vulnerabilityType, string operationName)
        {
            var frameInfo = StackWalker.GetFrame();

            if (!frameInfo.IsValid)
            {
                return null;
            }

            // Sometimes we do not have the file/line but we have the method/class.
            var filename = frameInfo.StackFrame?.GetFileName();
            var vulnerability = new Vulnerability(vulnerabilityType, new Location(filename ?? GetMethodName(frameInfo.StackFrame), filename != null ? frameInfo.StackFrame?.GetFileLineNumber() : null), new Evidence(evidenceValue));
            // The VulnerabilityBatch class is not very useful right now, but we will need it when handling requests
            var batch = new VulnerabilityBatch();
            batch.Add(vulnerability);

            // Right now, we always set the IastEnabled tag to "1", but in the future, it might be zero to indicate that a request has not been analyzed
            var tags = new IastTags()
            {
                IastJson = batch.ToString(),
                IastEnabled = "1"
            };

            var scope = tracer.StartActiveInternal(operationName, tags: tags);
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

        private static bool InvalidCipherAlgorithm(Type type, string algorithm, Iast iast)
        {
            if (ProviderBlock(type.Name))
            {
                foreach (var weakCipherAlgorithm in iast.Settings.WeakCipherAlgorithmsArray)
                {
                    if (string.Equals(algorithm, weakCipherAlgorithm, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool ProviderBlock(string name)
        {
            // TripleDESCryptoServiceProvider internally creates a DES algorithm instance.
            if (name == "TripleDESCryptoServiceProvider" || (isLinux && name.ToLower().EndsWith("provider")))
            {
                return true;
            }

            return false;
        }
    }
}
