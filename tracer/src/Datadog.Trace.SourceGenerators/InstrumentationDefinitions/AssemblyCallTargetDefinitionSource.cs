// <copyright file="AssemblyCallTargetDefinitionSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.ClrProfiler;
using Datadog.Trace.SourceGenerators.Helpers;

namespace Datadog.Trace.SourceGenerators.InstrumentationDefinitions;

internal record AssemblyCallTargetDefinitionSource
{
    public AssemblyCallTargetDefinitionSource(string signatureAttributeName, string integrationName, string assemblyName, string targetTypeName, (ushort Major, ushort Minor, ushort Patch) minimumVersion, (ushort Major, ushort Minor, ushort Patch) maximumVersion, bool isAdoNetIntegration, InstrumentationCategory instrumentationCategory, LocationInfo? location, string? dataReaderTypeName, string? dataReaderTaskTypeName)
    {
        SignatureAttributeName = signatureAttributeName;
        IntegrationName = integrationName;
        AssemblyName = assemblyName;
        TargetTypeName = targetTypeName;
        MinimumVersion = minimumVersion;
        MaximumVersion = maximumVersion;
        IsAdoNetIntegration = isAdoNetIntegration;
        InstrumentationCategory = instrumentationCategory;
        Location = location;
        DataReaderTypeName = dataReaderTypeName;
        DataReaderTaskTypeName = dataReaderTaskTypeName;
    }

    public string SignatureAttributeName { get; }

    public string IntegrationName { get; }

    public string AssemblyName { get; }

    public string TargetTypeName { get; }

    public (ushort Major, ushort Minor, ushort Patch) MinimumVersion { get; }

    public (ushort Major, ushort Minor, ushort Patch) MaximumVersion { get; }

    public bool IsAdoNetIntegration { get; }

    public InstrumentationCategory InstrumentationCategory { get; }

    public LocationInfo? Location { get; }

    public string? DataReaderTypeName { get; }

    public string? DataReaderTaskTypeName { get; }
}
