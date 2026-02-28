// <copyright file="DuckTypeAotRegistryEmissionResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

#pragma warning disable SA1402 // File may only contain a single type

namespace Datadog.Trace.Tools.Runner.DuckTypeAot
{
    /// <summary>
    /// Represents duck type aot registry emission result.
    /// </summary>
    internal sealed class DuckTypeAotRegistryEmissionResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DuckTypeAotRegistryEmissionResult"/> class.
        /// </summary>
        /// <param name="registryAssemblyInfo">The registry assembly info value.</param>
        /// <param name="mappingResultsByKey">The mapping results by key value.</param>
        public DuckTypeAotRegistryEmissionResult(
            DuckTypeAotRegistryAssemblyInfo registryAssemblyInfo,
            IReadOnlyDictionary<string, DuckTypeAotMappingEmissionResult> mappingResultsByKey)
        {
            RegistryAssemblyInfo = registryAssemblyInfo;
            MappingResultsByKey = mappingResultsByKey;
        }

        /// <summary>
        /// Gets registry assembly info.
        /// </summary>
        /// <value>The registry assembly info value.</value>
        public DuckTypeAotRegistryAssemblyInfo RegistryAssemblyInfo { get; }

        /// <summary>
        /// Gets mapping results by key.
        /// </summary>
        /// <value>The mapping results by key value.</value>
        public IReadOnlyDictionary<string, DuckTypeAotMappingEmissionResult> MappingResultsByKey { get; }
    }

    /// <summary>
    /// Represents duck type aot registry assembly info.
    /// </summary>
    internal sealed class DuckTypeAotRegistryAssemblyInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DuckTypeAotRegistryAssemblyInfo"/> class.
        /// </summary>
        /// <param name="assemblyName">The assembly name value.</param>
        /// <param name="bootstrapTypeFullName">The bootstrap type full name value.</param>
        /// <param name="outputAssemblyPath">The output assembly path value.</param>
        /// <param name="mvid">The mvid value.</param>
        public DuckTypeAotRegistryAssemblyInfo(string assemblyName, string bootstrapTypeFullName, string outputAssemblyPath, System.Guid mvid)
        {
            AssemblyName = assemblyName;
            BootstrapTypeFullName = bootstrapTypeFullName;
            OutputAssemblyPath = outputAssemblyPath;
            Mvid = mvid;
        }

        /// <summary>
        /// Gets assembly name.
        /// </summary>
        /// <value>The assembly name value.</value>
        public string AssemblyName { get; }

        /// <summary>
        /// Gets bootstrap type full name.
        /// </summary>
        /// <value>The bootstrap type full name value.</value>
        public string BootstrapTypeFullName { get; }

        /// <summary>
        /// Gets output assembly path.
        /// </summary>
        /// <value>The output assembly path value.</value>
        public string OutputAssemblyPath { get; }

        /// <summary>
        /// Gets mvid.
        /// </summary>
        /// <value>The mvid value.</value>
        public System.Guid Mvid { get; }
    }

    /// <summary>
    /// Represents duck type aot mapping emission result.
    /// </summary>
    internal sealed class DuckTypeAotMappingEmissionResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DuckTypeAotMappingEmissionResult"/> class.
        /// </summary>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="status">The status value.</param>
        /// <param name="diagnosticCode">The diagnostic code value.</param>
        /// <param name="detail">The detail value.</param>
        /// <param name="generatedProxyAssemblyName">The generated proxy assembly name value.</param>
        /// <param name="generatedProxyTypeName">The generated proxy type name value.</param>
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

        /// <summary>
        /// Gets mapping.
        /// </summary>
        /// <value>The mapping value.</value>
        public DuckTypeAotMapping Mapping { get; }

        /// <summary>
        /// Gets status.
        /// </summary>
        /// <value>The status value.</value>
        public string Status { get; }

        /// <summary>
        /// Gets diagnostic code.
        /// </summary>
        /// <value>The diagnostic code value.</value>
        public string? DiagnosticCode { get; }

        /// <summary>
        /// Gets detail.
        /// </summary>
        /// <value>The detail value.</value>
        public string? Detail { get; }

        /// <summary>
        /// Gets generated proxy assembly name.
        /// </summary>
        /// <value>The generated proxy assembly name value.</value>
        public string? GeneratedProxyAssemblyName { get; }

        /// <summary>
        /// Gets generated proxy type name.
        /// </summary>
        /// <value>The generated proxy type name value.</value>
        public string? GeneratedProxyTypeName { get; }

        /// <summary>
        /// Executes compatible.
        /// </summary>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="generatedProxyAssemblyName">The generated proxy assembly name value.</param>
        /// <param name="generatedProxyTypeName">The generated proxy type name value.</param>
        /// <returns>The result produced by this operation.</returns>
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

        /// <summary>
        /// Executes not compatible.
        /// </summary>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="status">The status value.</param>
        /// <param name="diagnosticCode">The diagnostic code value.</param>
        /// <param name="detail">The detail value.</param>
        /// <returns>The result produced by this operation.</returns>
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
