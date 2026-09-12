// <copyright file="CallSite.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.SourceGenerators.StringCaseInterception;

/// <summary>
/// A single <see cref="string.ToUpperInvariant()"/>/<see cref="string.ToLowerInvariant()"/> call site to intercept.
/// </summary>
internal sealed record CallSite
{
    public CallSite(string methodName, string interceptsLocationAttribute)
    {
        MethodName = methodName;
        InterceptsLocationAttribute = interceptsLocationAttribute;
    }

    /// <summary>
    /// Gets either <c>ToUpperInvariant</c> or <c>ToLowerInvariant</c>.
    /// </summary>
    public string MethodName { get; }

    /// <summary>
    /// Gets the full <c>[InterceptsLocation(...)]</c> attribute syntax for this call site, as returned by
    /// <see cref="Microsoft.CodeAnalysis.CSharp.CSharpExtensions.GetInterceptsLocationAttributeSyntax"/>.
    /// </summary>
    public string InterceptsLocationAttribute { get; }
}
