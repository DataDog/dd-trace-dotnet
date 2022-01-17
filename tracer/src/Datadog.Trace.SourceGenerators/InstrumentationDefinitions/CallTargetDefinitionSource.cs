// <copyright file="CallTargetDefinitionSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.SourceGenerators.InstrumentationDefinitions;

internal record CallTargetDefinitionSource
{
    public CallTargetDefinitionSource(string integrationName, string assemblyName, string targetTypeName, string targetMethodName, string targetReturnType, string[] targetParameterTypes, (ushort Major, ushort Minor, ushort Patch) minimumVersion, (ushort Major, ushort Minor, ushort Patch) maximumVersion, string instrumentationTypeName, int integrationType)
    {
        IntegrationName = integrationName;
        AssemblyName = assemblyName;
        TargetTypeName = targetTypeName;
        TargetMethodName = targetMethodName;
        TargetReturnType = targetReturnType;
        TargetParameterTypes = targetParameterTypes;
        MinimumVersion = minimumVersion;
        MaximumVersion = maximumVersion;
        InstrumentationTypeName = instrumentationTypeName;
        IntegrationType = integrationType;
    }

    public string IntegrationName { get; }

    public string AssemblyName { get; }

    public string TargetTypeName { get; }

    public string TargetMethodName { get; }

    public string TargetReturnType { get; }

    public string[] TargetParameterTypes { get; }

    public (ushort Major, ushort Minor, ushort Patch) MinimumVersion { get; }

    public (ushort Major, ushort Minor, ushort Patch) MaximumVersion { get; }

    public string InstrumentationTypeName { get; }

    public int IntegrationType { get; }
}
