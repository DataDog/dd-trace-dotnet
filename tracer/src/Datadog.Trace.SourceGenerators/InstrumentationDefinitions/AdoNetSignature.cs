// <copyright file="AdoNetSignature.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.SourceGenerators.InstrumentationDefinitions;

internal record AdoNetSignature
{
    public AdoNetSignature(string targetMethodName, string? targetReturnType, string[] targetParameterTypes, string instrumentationTypeName, int callTargetIntegrationType, int returnType)
    {
        TargetMethodName = targetMethodName;
        TargetReturnType = targetReturnType;
        TargetParameterTypes = targetParameterTypes;
        InstrumentationTypeName = instrumentationTypeName;
        CallTargetIntegrationType = callTargetIntegrationType;
        ReturnType = returnType;
    }

    public string TargetMethodName { get; }

    public string? TargetReturnType { get; }

    public string[] TargetParameterTypes { get; }

    public string InstrumentationTypeName { get; }

    public int CallTargetIntegrationType { get; }

    public int ReturnType { get; }
}
