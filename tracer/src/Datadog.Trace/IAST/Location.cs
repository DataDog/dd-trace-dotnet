// <copyright file="Location.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Iast;

internal readonly struct Location
{
    public Location(string? stackFile, string? method, int? line, ulong? spanId)
    {
        if (!string.IsNullOrEmpty(stackFile))
        {
            this.Path = System.IO.Path.GetFileName(stackFile);
            this.Line = line;
        }
        else
        {
            // If we do not have a file name, we add the namespace to Path and methodName to Method
            ExtractNamespaceFromMethod(method, out string? methodName, out string? namespaceName);
            this.Method = methodName;
            this.Path = namespaceName;
        }

        this.SpanId = spanId == 0 ? null : spanId;
    }

    public ulong? SpanId { get; }

    public string? Path { get; }

    public string? Method { get; }

    public int? Line { get; }

    public override int GetHashCode()
    {
        // We do not calculate the hash including the spanId
        return IastUtils.GetHashCode(Path, Line, Method);
    }

    // A typical method name would look like this: Samples.InstrumentedTests.Iast.Vulnerabilities.CommandInjectionTests::<GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable>b__6_0
    private void ExtractNamespaceFromMethod(string? method, out string? methodName, out string? namespaceName)
    {
        if (string.IsNullOrEmpty(method))
        {
            namespaceName = null;
            methodName = null;
            return;
        }

#pragma warning disable CS8602 // Dereference of a possibly null reference.
        var index1 = method.IndexOf("::");
#pragma warning restore CS8602 // Dereference of a possibly null reference.

        if (index1 > 0)
        {
            namespaceName = method.Substring(0, index1);
            var methodCompleteName = method.Substring(index1 + 2);

            var methodStartIndex = methodCompleteName.IndexOf("<");
            var methodEndIndex = methodCompleteName.IndexOf(">");

            if (methodStartIndex >= 0 && methodEndIndex > 0 && methodStartIndex < methodEndIndex)
            {
                methodName = methodCompleteName.Substring(methodStartIndex + 1, methodEndIndex - methodStartIndex - 1);
            }
            else
            {
                // We have a method without <methodName> notation
                methodName = methodCompleteName;
            }
        }
        else
        {
            // We have a method without the :: notation.
            methodName = method;
            namespaceName = null;
        }
    }
}
