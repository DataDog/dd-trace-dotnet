// <copyright file="IastModule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.Iast;

internal class IastModule
{
    private const string OperationNameWeakHash = "weak_hashing";
    private const string OperationNameWeakCipher = "weak_cipher";

    public IastModule()
    {
    }

    public static Scope? OnCipherAlgorithm(Type type, IntegrationId integrationId, Iast iast)
    {
        var algorithm = type.BaseType?.Name;

        if (algorithm is null || !InvalidCipherAlgorithm(type, algorithm, iast))
        {
            return null;
        }

        return GetScope(Tracer.Instance, algorithm, integrationId, VulnerabilityType.WeakCipher, OperationNameWeakCipher, iast);
    }

    public static Scope? OnHashingAlgorithm(string? algorithm, IntegrationId integrationId, Iast iast)
    {
        if (algorithm == null || !InvalidHashAlgorithm(algorithm, iast))
        {
            return null;
        }

        return GetScope(Tracer.Instance, algorithm, integrationId, VulnerabilityType.WeakHash, OperationNameWeakHash, iast);
    }

    private static Scope? GetScope(Tracer tracer, string evidenceValue, IntegrationId integrationId, string vulnerabilityType, string operationName, Iast iast)
    {
        if (!tracer.Settings.IsIntegrationEnabled(integrationId))
        {
            // integration disabled, don't create a scope, skip this span
            return null;
        }

        var frameInfo = StackWalker.GetFrame();

        if (!frameInfo.IsValid)
        {
            return null;
        }

        // Sometimes we do not have the file/line but we have the method/class.
        var filename = frameInfo.StackFrame?.GetFileName();
        var vulnerability = new Vulnerability(vulnerabilityType, new Location(filename ?? GetMethodName(frameInfo.StackFrame), filename != null ? frameInfo.StackFrame?.GetFileLineNumber() : null), new Evidence(evidenceValue));

        if (!iast.Settings.DeduplicationEnabled || HashBasedDeduplication.Instance.Add(vulnerability))
        {
            return AddVulnerability(tracer, integrationId, operationName, vulnerability);
        }

        return null;
    }

    private static Scope? AddVulnerability(Tracer tracer, IntegrationId integrationId, string operationName, Vulnerability vulnerability)
    {
        if (tracer.ActiveScope is Scope { Span.Context.TraceContext: { RootSpan.Type: SpanTypes.Web } traceContext })
        {
            traceContext.IastRequestContext?.AddVulnerability(vulnerability);
            return null;
        }
        else
        {
            // we either are not in a request or the distributed tracer returned a scope that cannot be casted to Scope and we cannot access the root span.
            var batch = new VulnerabilityBatch();
            batch.Add(vulnerability);

            var tags = new IastTags()
            {
                IastJson = batch.ToString(),
                IastEnabled = "1"
            };

            var scope = tracer.StartActiveInternal(operationName, tags: tags);
            tracer?.TracerManager?.Telemetry.IntegrationGeneratedSpan(integrationId);
            return scope;
        }
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
        if (ProviderValid(type.Name))
        {
            return false;
        }

        foreach (var weakCipherAlgorithm in iast.Settings.WeakCipherAlgorithmsArray)
        {
            if (string.Equals(algorithm, weakCipherAlgorithm, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ProviderValid(string name)
        => name switch
        {
            // TripleDESCryptoServiceProvider is a SymetricAlgorithm that internally creates a TripleDES instance, which is also a weak SymmetricAlgorithm. In order to avoid launching two spans for a single vulnerability,
            // we skip the one that would be launched when instantiating the TripleDESCryptoServiceProvider class.
            "TripleDESCryptoServiceProvider" => true,
            _ => string.Equals(FrameworkDescription.Instance.OSPlatform, OSPlatformName.Linux, StringComparison.Ordinal) && name.EndsWith("provider", StringComparison.OrdinalIgnoreCase)
        };
}
