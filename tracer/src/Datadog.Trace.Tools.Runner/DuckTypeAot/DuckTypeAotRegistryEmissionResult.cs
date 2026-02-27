// <copyright file="DuckTypeAotRegistryEmissionResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

#pragma warning disable SA1402 // File may only contain a single type

namespace Datadog.Trace.Tools.Runner.DuckTypeAot
{
    internal sealed class DuckTypeAotRegistryEmissionResult
    {
        public DuckTypeAotRegistryEmissionResult(
            DuckTypeAotRegistryAssemblyInfo registryAssemblyInfo,
            IReadOnlyDictionary<string, DuckTypeAotMappingEmissionResult> mappingResultsByKey)
        {
            RegistryAssemblyInfo = registryAssemblyInfo;
            MappingResultsByKey = mappingResultsByKey;
        }

        public DuckTypeAotRegistryAssemblyInfo RegistryAssemblyInfo { get; }

        public IReadOnlyDictionary<string, DuckTypeAotMappingEmissionResult> MappingResultsByKey { get; }
    }

    internal sealed class DuckTypeAotRegistryAssemblyInfo
    {
        public DuckTypeAotRegistryAssemblyInfo(string assemblyName, string bootstrapTypeFullName, string outputAssemblyPath, System.Guid mvid)
        {
            AssemblyName = assemblyName;
            BootstrapTypeFullName = bootstrapTypeFullName;
            OutputAssemblyPath = outputAssemblyPath;
            Mvid = mvid;
        }

        public string AssemblyName { get; }

        public string BootstrapTypeFullName { get; }

        public string OutputAssemblyPath { get; }

        public System.Guid Mvid { get; }
    }

    internal sealed class DuckTypeAotMappingEmissionResult
    {
        private DuckTypeAotMappingEmissionResult(
            DuckTypeAotMapping mapping,
            string status,
            string? diagnosticCode,
            string? detail,
            string? generatedProxyAssemblyName,
            string? generatedProxyTypeName)
        {
            Mapping = mapping;
            Status = status;
            DiagnosticCode = diagnosticCode;
            Detail = detail;
            GeneratedProxyAssemblyName = generatedProxyAssemblyName;
            GeneratedProxyTypeName = generatedProxyTypeName;
        }

        public DuckTypeAotMapping Mapping { get; }

        public string Status { get; }

        public string? DiagnosticCode { get; }

        public string? Detail { get; }

        public string? GeneratedProxyAssemblyName { get; }

        public string? GeneratedProxyTypeName { get; }

        public static DuckTypeAotMappingEmissionResult Compatible(
            DuckTypeAotMapping mapping,
            string generatedProxyAssemblyName,
            string generatedProxyTypeName)
        {
            return new DuckTypeAotMappingEmissionResult(
                mapping,
                DuckTypeAotCompatibilityStatuses.Compatible,
                diagnosticCode: null,
                detail: null,
                generatedProxyAssemblyName,
                generatedProxyTypeName);
        }

        public static DuckTypeAotMappingEmissionResult NotCompatible(DuckTypeAotMapping mapping, string status, string diagnosticCode, string detail)
        {
            return new DuckTypeAotMappingEmissionResult(
                mapping,
                status,
                diagnosticCode,
                detail,
                generatedProxyAssemblyName: null,
                generatedProxyTypeName: null);
        }
    }
}
