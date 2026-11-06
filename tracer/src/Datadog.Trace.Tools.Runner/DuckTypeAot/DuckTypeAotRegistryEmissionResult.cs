// <copyright file="DuckTypeAotRegistryEmissionResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
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
        /// <param name="runtimeRegistrations">The runtime registrations value.</param>
        /// <param name="warnings">The warnings value.</param>
        public DuckTypeAotRegistryEmissionResult(
            DuckTypeAotRegistryAssemblyInfo registryAssemblyInfo,
            IReadOnlyDictionary<string, DuckTypeAotMappingEmissionResult> mappingResultsByKey,
            IReadOnlyList<DuckTypeAotRuntimeRegistration> runtimeRegistrations,
            IReadOnlyList<string>? warnings = null)
        {
            RegistryAssemblyInfo = registryAssemblyInfo;
            MappingResultsByKey = mappingResultsByKey;
            RuntimeRegistrations = runtimeRegistrations;
            Warnings = warnings ?? [];
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

        /// <summary>
        /// Gets runtime registrations emitted into the generated registry.
        /// </summary>
        public IReadOnlyList<DuckTypeAotRuntimeRegistration> RuntimeRegistrations { get; }

        /// <summary>
        /// Gets generation warnings.
        /// </summary>
        /// <value>The warnings value.</value>
        public IReadOnlyList<string> Warnings { get; }
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
            string? generatedProxyTypeName,
            bool replaysDynamicFailure = false,
            bool checkedAgainstMetadataOnly = false,
            bool failsOnlyForOtherRuntimeTypes = false,
            bool runtimeSpecific = false)
        {
            Mapping = mapping;
            Status = status;
            DiagnosticCode = diagnosticCode;
            Detail = detail;
            GeneratedProxyAssemblyName = generatedProxyAssemblyName;
            GeneratedProxyTypeName = generatedProxyTypeName;
            ReplaysDynamicFailure = replaysDynamicFailure;
            CheckedAgainstMetadataOnly = checkedAgainstMetadataOnly;
            FailsOnlyForOtherRuntimeTypes = failsOnlyForOtherRuntimeTypes;
            RuntimeSpecific = runtimeSpecific;
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
        /// Gets a value indicating whether the registry replays the failure dynamic duck typing has for this mapping: the
        /// mapping fails, but exactly like in dynamic mode, so it isn't a parity gap.
        /// </summary>
        /// <value>true if the dynamic failure is replayed; otherwise, false.</value>
        public bool ReplaysDynamicFailure { get; }

        /// <summary>
        /// Gets a value indicating whether the mapping behaves like in dynamic duck typing: it's compatible, or the registry
        /// replays the failure dynamic duck typing has.
        /// </summary>
        public bool BehavesLikeDynamicDuckTyping => string.Equals(Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.OrdinalIgnoreCase) || ReplaysDynamicFailure;

        /// <summary>
        /// Gets a value indicating whether the generator couldn't evaluate the mapping with dynamic duck typing (types it can't
        /// load, contracts with their own duck attributes): it's checked against metadata only, which may differ from dynamic duck
        /// typing.
        /// </summary>
        /// <value>true if the mapping is checked against metadata only; otherwise, false.</value>
        public bool CheckedAgainstMetadataOnly { get; }

        /// <summary>
        /// Gets a value indicating whether the mapping itself behaves like in dynamic duck typing, but the registry can't create
        /// the proxy of another runtime type of its target (a derived type, a reverse proxy type...) like dynamic duck typing does.
        /// </summary>
        /// <value>true if only other runtime types of the target fail; otherwise, false.</value>
        public bool FailsOnlyForOtherRuntimeTypes { get; }

        /// <summary>
        /// Gets a value indicating whether the mapping targets, or its proxy binds, a type or member of the core library that
        /// isn't public: other runtimes (e.g. NativeAOT) may not have it.
        /// </summary>
        /// <value>true if the mapping is specific to the generator's runtime; otherwise, false.</value>
        public bool RuntimeSpecific { get; }

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

        /// <summary>
        /// Gets the same result for another spelling of the mapping (the same runtime types).
        /// </summary>
        /// <param name="mapping">The other mapping value.</param>
        /// <returns>The result produced by this operation.</returns>
        public DuckTypeAotMappingEmissionResult WithMapping(DuckTypeAotMapping mapping)
        {
            return new DuckTypeAotMappingEmissionResult(
                mapping,
                Status,
                DiagnosticCode,
                Detail,
                GeneratedProxyAssemblyName,
                GeneratedProxyTypeName,
                ReplaysDynamicFailure,
                CheckedAgainstMetadataOnly,
                FailsOnlyForOtherRuntimeTypes,
                RuntimeSpecific);
        }

        /// <summary>
        /// Gets the same failure, marked as replaying the failure of dynamic duck typing.
        /// </summary>
        /// <returns>The result produced by this operation.</returns>
        public DuckTypeAotMappingEmissionResult WithDynamicFailureReplayed()
        {
            return new DuckTypeAotMappingEmissionResult(
                Mapping,
                Status,
                DiagnosticCode,
                Detail,
                GeneratedProxyAssemblyName,
                GeneratedProxyTypeName,
                replaysDynamicFailure: true,
                CheckedAgainstMetadataOnly,
                FailsOnlyForOtherRuntimeTypes,
                RuntimeSpecific);
        }

        /// <summary>
        /// Gets the same result, marked as checked against metadata only (see <see cref="CheckedAgainstMetadataOnly"/>).
        /// </summary>
        /// <returns>The result produced by this operation.</returns>
        public DuckTypeAotMappingEmissionResult WithCheckedAgainstMetadataOnly()
        {
            return new DuckTypeAotMappingEmissionResult(
                Mapping,
                Status,
                DiagnosticCode,
                Detail,
                GeneratedProxyAssemblyName,
                GeneratedProxyTypeName,
                ReplaysDynamicFailure,
                checkedAgainstMetadataOnly: true,
                FailsOnlyForOtherRuntimeTypes,
                RuntimeSpecific);
        }

        /// <summary>
        /// Gets the same result, marked as specific to the generator's runtime (see <see cref="RuntimeSpecific"/>).
        /// </summary>
        /// <returns>The result produced by this operation.</returns>
        public DuckTypeAotMappingEmissionResult WithRuntimeSpecific()
        {
            return new DuckTypeAotMappingEmissionResult(
                Mapping,
                Status,
                DiagnosticCode,
                Detail,
                GeneratedProxyAssemblyName,
                GeneratedProxyTypeName,
                ReplaysDynamicFailure,
                CheckedAgainstMetadataOnly,
                FailsOnlyForOtherRuntimeTypes,
                runtimeSpecific: true);
        }

        /// <summary>
        /// Gets the result of the mapping when it behaves like in dynamic duck typing, but another runtime type of its target
        /// fails (see <see cref="FailsOnlyForOtherRuntimeTypes"/>): the failure of that runtime type, with the generated proxy
        /// type of the mapping, which the registry still registers.
        /// </summary>
        /// <param name="otherRuntimeTypeFailure">The failure of the other runtime type.</param>
        /// <returns>The result produced by this operation.</returns>
        public DuckTypeAotMappingEmissionResult WithOtherRuntimeTypeFailure(DuckTypeAotMappingEmissionResult otherRuntimeTypeFailure)
        {
            return new DuckTypeAotMappingEmissionResult(
                Mapping,
                otherRuntimeTypeFailure.Status,
                otherRuntimeTypeFailure.DiagnosticCode,
                otherRuntimeTypeFailure.Detail,
                GeneratedProxyAssemblyName,
                GeneratedProxyTypeName,
                replaysDynamicFailure: false,
                CheckedAgainstMetadataOnly || otherRuntimeTypeFailure.CheckedAgainstMetadataOnly,
                failsOnlyForOtherRuntimeTypes: true,
                RuntimeSpecific);
        }
    }
}
