// <copyright file="AdoNetSignature.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators.Helpers;

namespace Datadog.Trace.SourceGenerators.InstrumentationDefinitions;

internal record AdoNetSignature
{
    public AdoNetSignature(string targetMethodName, string? targetReturnType, string[] targetParameterTypes, string instrumentationTypeName, int callTargetIntegrationKind, int returnType)
    {
        TargetMethodName = targetMethodName;
        TargetReturnType = targetReturnType;
        TargetParameterTypes = new(targetParameterTypes);
        InstrumentationTypeName = instrumentationTypeName;
        CallTargetIntegrationKind = callTargetIntegrationKind;
        ReturnType = returnType;
    }

    public string TargetMethodName { get; }

    public string? TargetReturnType { get; }

    public EquatableArray<string> TargetParameterTypes { get; }

    public string InstrumentationTypeName { get; }

    public int CallTargetIntegrationKind { get; }

    public int ReturnType { get; }
}
