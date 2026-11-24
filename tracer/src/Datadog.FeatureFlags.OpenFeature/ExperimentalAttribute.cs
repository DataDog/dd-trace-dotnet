// <copyright file="ExperimentalAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NET8_0_OR_GREATER
// C# 12 recognizes this attribute by its fully qualified name on older target frameworks too.
// Keep it internal so the compatibility definition does not add a public package type.
namespace System.Diagnostics.CodeAnalysis;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
internal sealed class ExperimentalAttribute : Attribute
{
    public ExperimentalAttribute(string diagnosticId)
    {
        DiagnosticId = diagnosticId;
    }

    public string DiagnosticId { get; }
}
#endif
