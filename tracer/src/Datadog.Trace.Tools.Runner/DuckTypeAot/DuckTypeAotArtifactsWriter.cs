// <copyright file="DuckTypeAotArtifactsWriter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using dnlib.DotNet;

#pragma warning disable SA1402 // File may only contain a single type

namespace Datadog.Trace.Tools.Runner.DuckTypeAot
{
    /// <summary>
    /// Provides helper operations for duck type aot artifacts writer.
    /// </summary>
    internal static class DuckTypeAotArtifactsWriter
    {
        /// <summary>
        /// Defines the schema version constant.
        /// </summary>
        private const string SchemaVersion = "1";

        /// <summary>
        /// Writes write all.
        /// </summary>
        /// <param name="artifactPaths">The artifact paths value.</param>
        /// <param name="mappingResolutionResult">The mapping resolution result value.</param>
        /// <param name="emissionResult">The emission result value.</param>
        /// <returns>The result produced by this operation.</returns>
        internal static DuckTypeAotCompatibilityArtifacts WriteAll(
            DuckTypeAotArtifactPaths artifactPaths,
            DuckTypeAotMappingResolutionResult mappingResolutionResult,
            DuckTypeAotRegistryEmissionResult emissionResult)
        {
            var generatedAtUtc = DateTime.UtcNow;
            var mappings = mappingResolutionResult.Mappings.OrderBy(m => m.Key, StringComparer.Ordinal).ToList();
            var toolVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0.0";
            var registryAssemblyInfo = emissionResult.RegistryAssemblyInfo;

            var compatibilityMappings = mappings
                .Select((mapping, index) =>
                {
                    var hasResult = emissionResult.MappingResultsByKey.TryGetValue(mapping.Key, out var mappingResult);
                    var effectiveStatus = ResolveEffectiveCompatibilityStatus(hasResult ? mappingResult : null);
                    return new DuckTypeAotCompatibilityMapping
                    {
                        Id = mapping.ScenarioId ?? $"MAP-{index + 1:D4}",
                        MappingIdentityChecksum = ComputeMappingIdentityChecksum(mapping.Key),
                        Mode = mapping.Mode.ToString().ToLowerInvariant(),
                        ProxyType = mapping.ProxyTypeName,
                        ProxyAssembly = mapping.ProxyAssemblyName,
                        TargetType = mapping.TargetTypeName,
                        TargetAssembly = mapping.TargetAssemblyName,
                        Source = mapping.Source.ToString().ToLowerInvariant(),
                        Status = effectiveStatus,
                        DiagnosticCode = hasResult ? mappingResult!.DiagnosticCode : null,
                        Details = BuildEffectiveCompatibilityDetails(hasResult ? mappingResult : null),
                        GeneratedProxyAssembly = hasResult ? mappingResult!.GeneratedProxyAssemblyName : null,
                        GeneratedProxyType = hasResult ? mappingResult!.GeneratedProxyTypeName : null
                    };
                })
                .ToList();

            var compatibilityMatrix = new DuckTypeAotCompatibilityMatrix
            {
                SchemaVersion = SchemaVersion,
                GeneratedAtUtc = generatedAtUtc.ToString("O", CultureInfo.InvariantCulture),
                RegistryAssembly = registryAssemblyInfo.OutputAssemblyPath,
                TotalMappings = compatibilityMappings.Count,
                Mappings = compatibilityMappings
            };

            WriteJson(artifactPaths.CompatibilityMatrixPath, compatibilityMatrix);
            WriteCompatibilityMarkdown(artifactPaths.CompatibilityReportPath, compatibilityMatrix);
            WriteTrimmerDescriptor(artifactPaths.TrimmerDescriptorPath, mappingResolutionResult, emissionResult);
            WritePropsFile(artifactPaths.PropsPath, artifactPaths, registryAssemblyInfo);
            WriteManifest(
                artifactPaths.ManifestPath,
                artifactPaths.TrimmerDescriptorPath,
                artifactPaths.PropsPath,
                mappingResolutionResult,
                registryAssemblyInfo,
                generatedAtUtc,
                toolVersion);

            return new DuckTypeAotCompatibilityArtifacts(
                artifactPaths.CompatibilityMatrixPath,
                artifactPaths.CompatibilityReportPath,
                compatibilityMatrix.TotalMappings,
                compatibilityMatrix.Mappings.Count(mapping => string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal)),
                compatibilityMatrix.Mappings.Count(mapping => !string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal)));
        }

        /// <summary>
        /// Writes write manifest.
        /// </summary>
        /// <param name="manifestPath">The manifest path value.</param>
        /// <param name="trimmerDescriptorPath">The trimmer descriptor path value.</param>
        /// <param name="propsPath">The props path value.</param>
        /// <param name="mappingResolutionResult">The mapping resolution result value.</param>
        /// <param name="registryAssemblyInfo">The registry assembly info value.</param>
        /// <param name="generatedAtUtc">The generated at utc value.</param>
        /// <param name="toolVersion">The tool version value.</param>
        private static void WriteManifest(
            string manifestPath,
            string trimmerDescriptorPath,
            string propsPath,
            DuckTypeAotMappingResolutionResult mappingResolutionResult,
            DuckTypeAotRegistryAssemblyInfo registryAssemblyInfo,
            DateTime generatedAtUtc,
            string toolVersion)
        {
            var registryAssemblyFingerprint = CreateAssemblyFingerprint(registryAssemblyInfo.OutputAssemblyPath);
            var manifest = new DuckTypeAotManifest
            {
                SchemaVersion = SchemaVersion,
                ToolVersion = toolVersion,
                GeneratedAtUtc = generatedAtUtc.ToString("O", CultureInfo.InvariantCulture),
                RegistryAssembly = registryAssemblyInfo.OutputAssemblyPath,
                RegistryAssemblyName = registryAssemblyInfo.AssemblyName,
                RegistryAssemblyVersion = registryAssemblyFingerprint.Version,
                RegistryBootstrapType = registryAssemblyInfo.BootstrapTypeFullName,
                RegistryMvid = registryAssemblyInfo.Mvid.ToString("D"),
                RegistryAssemblySha256 = registryAssemblyFingerprint.Sha256,
                RegistryStrongNameSigned = !string.IsNullOrWhiteSpace(registryAssemblyFingerprint.PublicKeyToken),
                RegistryPublicKeyToken = registryAssemblyFingerprint.PublicKeyToken,
                TrimmerDescriptorPath = trimmerDescriptorPath,
                TrimmerDescriptorSha256 = ComputeSha256(trimmerDescriptorPath),
                PropsPath = propsPath,
                PropsSha256 = ComputeSha256(propsPath),
                Mappings = mappingResolutionResult.Mappings
                    .OrderBy(m => m.Key, StringComparer.Ordinal)
                    .Select(mapping => new DuckTypeAotManifestMapping
                    {
                        Mode = mapping.Mode.ToString().ToLowerInvariant(),
                        ScenarioId = mapping.ScenarioId,
                        MappingIdentityChecksum = ComputeMappingIdentityChecksum(mapping.Key),
                        ProxyType = mapping.ProxyTypeName,
                        ProxyAssembly = mapping.ProxyAssemblyName,
                        TargetType = mapping.TargetTypeName,
                        TargetAssembly = mapping.TargetAssemblyName,
                        Source = mapping.Source.ToString().ToLowerInvariant()
                    })
                    .ToList(),
                GenericInstantiations = mappingResolutionResult.GenericTypeRoots
                    .OrderBy(root => root.Key, StringComparer.Ordinal)
                    .Select(root => new DuckTypeAotManifestTypeReference
                    {
                        Type = root.TypeName,
                        Assembly = root.AssemblyName
                    })
                    .ToList(),
                ProxyAssemblies = CreateAssemblyFingerprints(mappingResolutionResult.ProxyAssemblyPathsByName.Values),
                TargetAssemblies = CreateAssemblyFingerprints(mappingResolutionResult.TargetAssemblyPathsByName.Values),
                DatadogTraceAssembly = CreateAssemblyFingerprint(typeof(Datadog.Trace.Tracer).Assembly.Location)
            };

            WriteJson(manifestPath, manifest);
        }

        /// <summary>
        /// Creates create assembly fingerprints.
        /// </summary>
        /// <param name="assemblyPaths">The assembly paths value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static List<DuckTypeAotAssemblyFingerprint> CreateAssemblyFingerprints(IEnumerable<string> assemblyPaths)
        {
            return assemblyPaths
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(CreateAssemblyFingerprint)
                .ToList();
        }

        /// <summary>
        /// Creates create assembly fingerprint.
        /// </summary>
        /// <param name="assemblyPath">The assembly path value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static DuckTypeAotAssemblyFingerprint CreateAssemblyFingerprint(string assemblyPath)
        {
            var fullPath = Path.GetFullPath(assemblyPath);
            var assemblyName = AssemblyName.GetAssemblyName(fullPath);
            using var module = ModuleDefMD.Load(fullPath);
            var publicKeyTokenBytes = assemblyName.GetPublicKeyToken();
            return new DuckTypeAotAssemblyFingerprint
            {
                Name = assemblyName.Name ?? string.Empty,
                Version = assemblyName.Version?.ToString() ?? "0.0.0.0",
                Path = fullPath,
                Mvid = module.Mvid?.ToString("D") ?? string.Empty,
                Sha256 = ComputeSha256(fullPath),
                PublicKeyToken = publicKeyTokenBytes is { Length: > 0 } ? BitConverter.ToString(publicKeyTokenBytes).Replace("-", string.Empty).ToLowerInvariant() : null
            };
        }

        /// <summary>
        /// Computes compute sha256.
        /// </summary>
        /// <param name="filePath">The file path value.</param>
        /// <returns>The resulting string value.</returns>
        private static string ComputeSha256(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = sha256.ComputeHash(stream);
            return ConvertToLowerHex(hash);
        }

        /// <summary>
        /// Computes compute mapping identity checksum.
        /// </summary>
        /// <param name="mappingKey">The mapping key value.</param>
        /// <returns>The resulting string value.</returns>
        private static string ComputeMappingIdentityChecksum(string mappingKey)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(mappingKey));
            return ConvertToLowerHex(hash);
        }

        /// <summary>
        /// Executes convert to lower hex.
        /// </summary>
        /// <param name="hash">The hash value.</param>
        /// <returns>The resulting string value.</returns>
        private static string ConvertToLowerHex(byte[] hash)
        {
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var hashByte in hash)
            {
                _ = sb.Append(hashByte.ToString("x2", CultureInfo.InvariantCulture));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Writes write compatibility markdown.
        /// </summary>
        /// <param name="path">The path value.</param>
        /// <param name="matrix">The matrix value.</param>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        private static void WriteCompatibilityMarkdown(string path, DuckTypeAotCompatibilityMatrix matrix)
        {
            var sb = new StringBuilder();
            _ = sb.AppendLine("# DuckType AOT Compatibility Report")
                .AppendLine()
                .AppendLine($"- Schema version: `{matrix.SchemaVersion}`")
                .AppendLine($"- Generated (UTC): `{matrix.GeneratedAtUtc}`")
                .AppendLine($"- Registry assembly: `{matrix.RegistryAssembly}`")
                .AppendLine($"- Total mappings: `{matrix.TotalMappings}`")
                .AppendLine()
                .AppendLine("| Id | Mode | Source | Status | Diagnostic | Proxy | Target |")
                .AppendLine("| --- | --- | --- | --- | --- | --- | --- |");

            foreach (var mapping in matrix.Mappings)
            {
                _ = sb.Append("| ")
                    .Append(mapping.Id)
                    .Append(" | ")
                    .Append(mapping.Mode)
                    .Append(" | ")
                    .Append(mapping.Source)
                    .Append(" | ")
                    .Append(mapping.Status)
                    .Append(" | ")
                    .Append(mapping.DiagnosticCode ?? "-")
                    .Append(" | ")
                    .Append(mapping.ProxyType)
                    .Append(", ")
                    .Append(mapping.ProxyAssembly)
                    .Append(" | ")
                    .Append(mapping.TargetType)
                    .Append(", ")
                    .Append(mapping.TargetAssembly)
                    .AppendLine(" |");

                // Branch: take this path when (!string.IsNullOrWhiteSpace(mapping.GeneratedProxyType) || !string.IsNullOrWhiteSpace(mapping.GeneratedProxyAssembly)) evaluates to true.
                if (!string.IsNullOrWhiteSpace(mapping.GeneratedProxyType) || !string.IsNullOrWhiteSpace(mapping.GeneratedProxyAssembly))
                {
                    _ = sb.Append("|  |  |  |  |  | generated: ")
                        .Append(mapping.GeneratedProxyType ?? "-")
                        .Append(", ")
                        .Append(mapping.GeneratedProxyAssembly ?? "-")
                        .AppendLine(" |  |");
                }

                // Branch: take this path when (!string.IsNullOrWhiteSpace(mapping.Details)) evaluates to true.
                if (!string.IsNullOrWhiteSpace(mapping.Details))
                {
                    var details = mapping.Details;
                    _ = sb.Append("|  |  |  |  |  | detail: ")
                        .Append(details!.Replace("|", "\\|"))
                        .AppendLine(" |  |");
                }
            }

            File.WriteAllText(path, sb.ToString());
        }

        /// <summary>
        /// Writes write trimmer descriptor.
        /// </summary>
        /// <param name="path">The path value.</param>
        /// <param name="mappingResolutionResult">The mapping resolution result value.</param>
        /// <param name="emissionResult">The emission result value.</param>
        private static void WriteTrimmerDescriptor(
            string path,
            DuckTypeAotMappingResolutionResult mappingResolutionResult,
            DuckTypeAotRegistryEmissionResult emissionResult)
        {
            var typesByAssembly = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            AddTypeRoot(
                typesByAssembly,
                emissionResult.RegistryAssemblyInfo.AssemblyName,
                emissionResult.RegistryAssemblyInfo.BootstrapTypeFullName);

            foreach (var mapping in mappingResolutionResult.Mappings.OrderBy(m => m.Key, StringComparer.Ordinal))
            {
                // Branch: take this path when (!emissionResult.MappingResultsByKey.TryGetValue(mapping.Key, out var mappingResult)) evaluates to true.
                if (!emissionResult.MappingResultsByKey.TryGetValue(mapping.Key, out var mappingResult))
                {
                    continue;
                }

                var effectiveStatus = ResolveEffectiveCompatibilityStatus(mappingResult);
                // Branch: take this path when (!string.Equals(effectiveStatus, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal)) evaluates to true.
                if (!string.Equals(effectiveStatus, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal))
                {
                    continue;
                }

                AddTypeRoot(typesByAssembly, mapping.ProxyAssemblyName, mapping.ProxyTypeName);
                AddTypeRoot(typesByAssembly, mapping.TargetAssemblyName, mapping.TargetTypeName);

                // Branch: take this path when (!string.IsNullOrWhiteSpace(mappingResult.GeneratedProxyAssemblyName) && evaluates to true.
                if (!string.IsNullOrWhiteSpace(mappingResult.GeneratedProxyAssemblyName) &&
                    !string.IsNullOrWhiteSpace(mappingResult.GeneratedProxyTypeName))
                {
                    AddTypeRoot(typesByAssembly, mappingResult.GeneratedProxyAssemblyName!, mappingResult.GeneratedProxyTypeName!);
                }
            }

            foreach (var genericTypeRoot in mappingResolutionResult.GenericTypeRoots.OrderBy(root => root.Key, StringComparer.Ordinal))
            {
                AddTypeRoot(typesByAssembly, genericTypeRoot.AssemblyName, genericTypeRoot.TypeName);
            }

            var sb = new StringBuilder();
            _ = sb.AppendLine("<linker>");
            foreach (var (assemblyName, typeNames) in typesByAssembly.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase))
            {
                _ = sb.Append("  <assembly fullname=\"")
                      .Append(EscapeXml(assemblyName))
                      .AppendLine("\">");

                foreach (var typeName in typeNames.OrderBy(name => name, StringComparer.Ordinal))
                {
                    _ = sb.Append("    <type fullname=\"")
                          .Append(EscapeXml(typeName))
                          .AppendLine("\" preserve=\"all\" />");
                }

                _ = sb.AppendLine("  </assembly>");
            }

            _ = sb.AppendLine("</linker>");
            File.WriteAllText(path, sb.ToString());
        }

        /// <summary>
        /// Resolves resolve effective compatibility status.
        /// </summary>
        /// <param name="mappingResult">The mapping result value.</param>
        /// <returns>The resulting string value.</returns>
        private static string ResolveEffectiveCompatibilityStatus(DuckTypeAotMappingEmissionResult? mappingResult)
        {
            // Branch: take this path when (mappingResult is null) evaluates to true.
            if (mappingResult is null)
            {
                return DuckTypeAotCompatibilityStatuses.PendingProxyEmission;
            }

            return mappingResult.Status;
        }

        /// <summary>
        /// Builds build effective compatibility details.
        /// </summary>
        /// <param name="mappingResult">The mapping result value.</param>
        /// <returns>The resulting string value.</returns>
        private static string? BuildEffectiveCompatibilityDetails(DuckTypeAotMappingEmissionResult? mappingResult)
        {
            return mappingResult?.Detail;
        }

        /// <summary>
        /// Adds add type root.
        /// </summary>
        /// <param name="typesByAssembly">The types by assembly value.</param>
        /// <param name="assemblyName">The assembly name value.</param>
        /// <param name="typeName">The type name value.</param>
        private static void AddTypeRoot(IDictionary<string, HashSet<string>> typesByAssembly, string assemblyName, string typeName)
        {
            // Branch: take this path when (string.IsNullOrWhiteSpace(assemblyName) || string.IsNullOrWhiteSpace(typeName)) evaluates to true.
            if (string.IsNullOrWhiteSpace(assemblyName) || string.IsNullOrWhiteSpace(typeName))
            {
                return;
            }

            // Branch: take this path when (!typesByAssembly.TryGetValue(assemblyName, out var assemblyTypes)) evaluates to true.
            if (!typesByAssembly.TryGetValue(assemblyName, out var assemblyTypes))
            {
                assemblyTypes = new HashSet<string>(StringComparer.Ordinal);
                typesByAssembly[assemblyName] = assemblyTypes;
            }

            _ = assemblyTypes.Add(NormalizeTypeNameForLinker(typeName));
        }

        /// <summary>
        /// Normalizes normalize type name for linker.
        /// </summary>
        /// <param name="typeName">The type name value.</param>
        /// <returns>The resulting string value.</returns>
        private static string NormalizeTypeNameForLinker(string typeName)
        {
            return typeName.Replace('+', '/');
        }

        /// <summary>
        /// Executes escape xml.
        /// </summary>
        /// <param name="value">The value value.</param>
        /// <returns>The resulting string value.</returns>
        private static string EscapeXml(string value)
        {
            return value
                .Replace("&", "&amp;")
                .Replace("\"", "&quot;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }

        /// <summary>
        /// Writes write props file.
        /// </summary>
        /// <param name="path">The path value.</param>
        /// <param name="artifactPaths">The artifact paths value.</param>
        /// <param name="registryAssemblyInfo">The registry assembly info value.</param>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        private static void WritePropsFile(string path, DuckTypeAotArtifactPaths artifactPaths, DuckTypeAotRegistryAssemblyInfo registryAssemblyInfo)
        {
            var outputAssemblyPath = EscapeMsBuildPath(artifactPaths.OutputAssemblyPath);
            var trimmerDescriptorPath = EscapeMsBuildPath(artifactPaths.TrimmerDescriptorPath);

            var propsContent =
                "<Project>" + Environment.NewLine +
                "  <ItemGroup>" + Environment.NewLine +
                $"    <Reference Include=\"{registryAssemblyInfo.AssemblyName}\">" + Environment.NewLine +
                $"      <HintPath>{outputAssemblyPath}</HintPath>" + Environment.NewLine +
                "      <Private>true</Private>" + Environment.NewLine +
                "    </Reference>" + Environment.NewLine +
                "  </ItemGroup>" + Environment.NewLine +
                "  <ItemGroup>" + Environment.NewLine +
                $"    <TrimmerRootDescriptor Include=\"{trimmerDescriptorPath}\" />" + Environment.NewLine +
                "  </ItemGroup>" + Environment.NewLine +
                "</Project>" + Environment.NewLine;

            File.WriteAllText(path, propsContent);
        }

        /// <summary>
        /// Executes escape ms build path.
        /// </summary>
        /// <param name="value">The value value.</param>
        /// <returns>The resulting string value.</returns>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        private static string EscapeMsBuildPath(string value)
        {
            return value.Replace("$", "$$");
        }

        /// <summary>
        /// Writes write json.
        /// </summary>
        /// <param name="path">The path value.</param>
        /// <param name="value">The value value.</param>
        private static void WriteJson<T>(string path, T value)
        {
            var json = JsonConvert.SerializeObject(value, Formatting.Indented);
            File.WriteAllText(path, json);
        }
    }

    /// <summary>
    /// Represents duck type aot compatibility artifacts.
    /// </summary>
    internal sealed class DuckTypeAotCompatibilityArtifacts
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DuckTypeAotCompatibilityArtifacts"/> class.
        /// </summary>
        /// <param name="matrixPath">The matrix path value.</param>
        /// <param name="reportPath">The report path value.</param>
        /// <param name="totalMappings">The total mappings value.</param>
        /// <param name="compatibleMappings">The compatible mappings value.</param>
        /// <param name="nonCompatibleMappings">The non compatible mappings value.</param>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        public DuckTypeAotCompatibilityArtifacts(string matrixPath, string reportPath, int totalMappings, int compatibleMappings, int nonCompatibleMappings)
        {
            MatrixPath = matrixPath;
            ReportPath = reportPath;
            TotalMappings = totalMappings;
            CompatibleMappings = compatibleMappings;
            NonCompatibleMappings = nonCompatibleMappings;
        }

        /// <summary>
        /// Gets matrix path.
        /// </summary>
        /// <value>The matrix path value.</value>
        public string MatrixPath { get; }

        /// <summary>
        /// Gets report path.
        /// </summary>
        /// <value>The report path value.</value>
        public string ReportPath { get; }

        /// <summary>
        /// Gets total mappings.
        /// </summary>
        /// <value>The total mappings value.</value>
        public int TotalMappings { get; }

        /// <summary>
        /// Gets compatible mappings.
        /// </summary>
        /// <value>The compatible mappings value.</value>
        public int CompatibleMappings { get; }

        /// <summary>
        /// Gets non compatible mappings.
        /// </summary>
        /// <value>The non compatible mappings value.</value>
        public int NonCompatibleMappings { get; }
    }

    /// <summary>
    /// Represents duck type aot compatibility matrix.
    /// </summary>
    internal sealed class DuckTypeAotCompatibilityMatrix
    {
        /// <summary>
        /// Gets or sets schema version.
        /// </summary>
        /// <value>The schema version value.</value>
        [JsonProperty("schemaVersion")]
        public string? SchemaVersion { get; set; }

        /// <summary>
        /// Gets or sets generated at utc.
        /// </summary>
        /// <value>The generated at utc value.</value>
        [JsonProperty("generatedAtUtc")]
        public string? GeneratedAtUtc { get; set; }

        /// <summary>
        /// Gets or sets registry assembly.
        /// </summary>
        /// <value>The registry assembly value.</value>
        [JsonProperty("registryAssembly")]
        public string? RegistryAssembly { get; set; }

        /// <summary>
        /// Gets or sets total mappings.
        /// </summary>
        /// <value>The total mappings value.</value>
        [JsonProperty("totalMappings")]
        public int TotalMappings { get; set; }

        /// <summary>
        /// Gets or sets mappings.
        /// </summary>
        /// <value>The mappings value.</value>
        [JsonProperty("mappings")]
        public List<DuckTypeAotCompatibilityMapping> Mappings { get; set; } = new();
    }

    /// <summary>
    /// Represents duck type aot compatibility mapping.
    /// </summary>
    internal sealed class DuckTypeAotCompatibilityMapping
    {
        /// <summary>
        /// Gets or sets id.
        /// </summary>
        /// <value>The id value.</value>
        [JsonProperty("id")]
        public string? Id { get; set; }

        /// <summary>
        /// Gets or sets mapping identity checksum.
        /// </summary>
        /// <value>The mapping identity checksum value.</value>
        [JsonProperty("mappingIdentityChecksum")]
        public string? MappingIdentityChecksum { get; set; }

        /// <summary>
        /// Gets or sets mode.
        /// </summary>
        /// <value>The mode value.</value>
        [JsonProperty("mode")]
        public string? Mode { get; set; }

        /// <summary>
        /// Gets or sets proxy type.
        /// </summary>
        /// <value>The proxy type value.</value>
        [JsonProperty("proxyType")]
        public string? ProxyType { get; set; }

        /// <summary>
        /// Gets or sets proxy assembly.
        /// </summary>
        /// <value>The proxy assembly value.</value>
        [JsonProperty("proxyAssembly")]
        public string? ProxyAssembly { get; set; }

        /// <summary>
        /// Gets or sets target type.
        /// </summary>
        /// <value>The target type value.</value>
        [JsonProperty("targetType")]
        public string? TargetType { get; set; }

        /// <summary>
        /// Gets or sets target assembly.
        /// </summary>
        /// <value>The target assembly value.</value>
        [JsonProperty("targetAssembly")]
        public string? TargetAssembly { get; set; }

        /// <summary>
        /// Gets or sets source.
        /// </summary>
        /// <value>The source value.</value>
        [JsonProperty("source")]
        public string? Source { get; set; }

        /// <summary>
        /// Gets or sets status.
        /// </summary>
        /// <value>The status value.</value>
        [JsonProperty("status")]
        public string? Status { get; set; }

        /// <summary>
        /// Gets or sets diagnostic code.
        /// </summary>
        /// <value>The diagnostic code value.</value>
        [JsonProperty("diagnosticCode")]
        public string? DiagnosticCode { get; set; }

        /// <summary>
        /// Gets or sets details.
        /// </summary>
        /// <value>The details value.</value>
        [JsonProperty("details")]
        public string? Details { get; set; }

        /// <summary>
        /// Gets or sets generated proxy assembly.
        /// </summary>
        /// <value>The generated proxy assembly value.</value>
        [JsonProperty("generatedProxyAssembly")]
        public string? GeneratedProxyAssembly { get; set; }

        /// <summary>
        /// Gets or sets generated proxy type.
        /// </summary>
        /// <value>The generated proxy type value.</value>
        [JsonProperty("generatedProxyType")]
        public string? GeneratedProxyType { get; set; }
    }

    /// <summary>
    /// Represents duck type aot manifest.
    /// </summary>
    internal sealed class DuckTypeAotManifest
    {
        /// <summary>
        /// Gets or sets schema version.
        /// </summary>
        /// <value>The schema version value.</value>
        [JsonProperty("schemaVersion")]
        public string? SchemaVersion { get; set; }

        /// <summary>
        /// Gets or sets tool version.
        /// </summary>
        /// <value>The tool version value.</value>
        [JsonProperty("toolVersion")]
        public string? ToolVersion { get; set; }

        /// <summary>
        /// Gets or sets generated at utc.
        /// </summary>
        /// <value>The generated at utc value.</value>
        [JsonProperty("generatedAtUtc")]
        public string? GeneratedAtUtc { get; set; }

        /// <summary>
        /// Gets or sets registry assembly.
        /// </summary>
        /// <value>The registry assembly value.</value>
        [JsonProperty("registryAssembly")]
        public string? RegistryAssembly { get; set; }

        /// <summary>
        /// Gets or sets registry assembly name.
        /// </summary>
        /// <value>The registry assembly name value.</value>
        [JsonProperty("registryAssemblyName")]
        public string? RegistryAssemblyName { get; set; }

        /// <summary>
        /// Gets or sets registry assembly version.
        /// </summary>
        /// <value>The registry assembly version value.</value>
        [JsonProperty("registryAssemblyVersion")]
        public string? RegistryAssemblyVersion { get; set; }

        /// <summary>
        /// Gets or sets registry bootstrap type.
        /// </summary>
        /// <value>The registry bootstrap type value.</value>
        [JsonProperty("registryBootstrapType")]
        public string? RegistryBootstrapType { get; set; }

        /// <summary>
        /// Gets or sets registry mvid.
        /// </summary>
        /// <value>The registry mvid value.</value>
        [JsonProperty("registryMvid")]
        public string? RegistryMvid { get; set; }

        /// <summary>
        /// Gets or sets registry assembly sha256.
        /// </summary>
        /// <value>The registry assembly sha256 value.</value>
        [JsonProperty("registryAssemblySha256")]
        public string? RegistryAssemblySha256 { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether registry strong name signed.
        /// </summary>
        /// <value>The registry strong name signed value.</value>
        [JsonProperty("registryStrongNameSigned")]
        public bool? RegistryStrongNameSigned { get; set; }

        /// <summary>
        /// Gets or sets registry public key token.
        /// </summary>
        /// <value>The registry public key token value.</value>
        [JsonProperty("registryPublicKeyToken")]
        public string? RegistryPublicKeyToken { get; set; }

        /// <summary>
        /// Gets or sets trimmer descriptor path.
        /// </summary>
        /// <value>The trimmer descriptor path value.</value>
        [JsonProperty("trimmerDescriptorPath")]
        public string? TrimmerDescriptorPath { get; set; }

        /// <summary>
        /// Gets or sets trimmer descriptor sha256.
        /// </summary>
        /// <value>The trimmer descriptor sha256 value.</value>
        [JsonProperty("trimmerDescriptorSha256")]
        public string? TrimmerDescriptorSha256 { get; set; }

        /// <summary>
        /// Gets or sets props path.
        /// </summary>
        /// <value>The props path value.</value>
        [JsonProperty("propsPath")]
        public string? PropsPath { get; set; }

        /// <summary>
        /// Gets or sets props sha256.
        /// </summary>
        /// <value>The props sha256 value.</value>
        [JsonProperty("propsSha256")]
        public string? PropsSha256 { get; set; }

        /// <summary>
        /// Gets or sets mappings.
        /// </summary>
        /// <value>The mappings value.</value>
        [JsonProperty("mappings")]
        public List<DuckTypeAotManifestMapping> Mappings { get; set; } = new();

        /// <summary>
        /// Gets or sets generic instantiations.
        /// </summary>
        /// <value>The generic instantiations value.</value>
        [JsonProperty("genericInstantiations")]
        public List<DuckTypeAotManifestTypeReference> GenericInstantiations { get; set; } = new();

        /// <summary>
        /// Gets or sets proxy assemblies.
        /// </summary>
        /// <value>The proxy assemblies value.</value>
        [JsonProperty("proxyAssemblies")]
        public List<DuckTypeAotAssemblyFingerprint> ProxyAssemblies { get; set; } = new();

        /// <summary>
        /// Gets or sets target assemblies.
        /// </summary>
        /// <value>The target assemblies value.</value>
        [JsonProperty("targetAssemblies")]
        public List<DuckTypeAotAssemblyFingerprint> TargetAssemblies { get; set; } = new();

        /// <summary>
        /// Gets or sets datadog trace assembly.
        /// </summary>
        /// <value>The datadog trace assembly value.</value>
        [JsonProperty("datadogTraceAssembly")]
        public DuckTypeAotAssemblyFingerprint? DatadogTraceAssembly { get; set; }
    }

    /// <summary>
    /// Represents duck type aot manifest mapping.
    /// </summary>
    internal sealed class DuckTypeAotManifestMapping
    {
        /// <summary>
        /// Gets or sets mode.
        /// </summary>
        /// <value>The mode value.</value>
        [JsonProperty("mode")]
        public string? Mode { get; set; }

        /// <summary>
        /// Gets or sets scenario id.
        /// </summary>
        /// <value>The scenario id value.</value>
        [JsonProperty("scenarioId")]
        public string? ScenarioId { get; set; }

        /// <summary>
        /// Gets or sets mapping identity checksum.
        /// </summary>
        /// <value>The mapping identity checksum value.</value>
        [JsonProperty("mappingIdentityChecksum")]
        public string? MappingIdentityChecksum { get; set; }

        /// <summary>
        /// Gets or sets proxy type.
        /// </summary>
        /// <value>The proxy type value.</value>
        [JsonProperty("proxyType")]
        public string? ProxyType { get; set; }

        /// <summary>
        /// Gets or sets proxy assembly.
        /// </summary>
        /// <value>The proxy assembly value.</value>
        [JsonProperty("proxyAssembly")]
        public string? ProxyAssembly { get; set; }

        /// <summary>
        /// Gets or sets target type.
        /// </summary>
        /// <value>The target type value.</value>
        [JsonProperty("targetType")]
        public string? TargetType { get; set; }

        /// <summary>
        /// Gets or sets target assembly.
        /// </summary>
        /// <value>The target assembly value.</value>
        [JsonProperty("targetAssembly")]
        public string? TargetAssembly { get; set; }

        /// <summary>
        /// Gets or sets source.
        /// </summary>
        /// <value>The source value.</value>
        [JsonProperty("source")]
        public string? Source { get; set; }
    }

    /// <summary>
    /// Represents duck type aot manifest type reference.
    /// </summary>
    internal sealed class DuckTypeAotManifestTypeReference
    {
        /// <summary>
        /// Gets or sets type.
        /// </summary>
        /// <value>The type value.</value>
        [JsonProperty("type")]
        public string? Type { get; set; }

        /// <summary>
        /// Gets or sets assembly.
        /// </summary>
        /// <value>The assembly value.</value>
        [JsonProperty("assembly")]
        public string? Assembly { get; set; }
    }

    /// <summary>
    /// Represents duck type aot assembly fingerprint.
    /// </summary>
    internal sealed class DuckTypeAotAssemblyFingerprint
    {
        /// <summary>
        /// Gets or sets name.
        /// </summary>
        /// <value>The name value.</value>
        [JsonProperty("name")]
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets version.
        /// </summary>
        /// <value>The version value.</value>
        [JsonProperty("version")]
        public string? Version { get; set; }

        /// <summary>
        /// Gets or sets path.
        /// </summary>
        /// <value>The path value.</value>
        [JsonProperty("path")]
        public string? Path { get; set; }

        /// <summary>
        /// Gets or sets mvid.
        /// </summary>
        /// <value>The mvid value.</value>
        [JsonProperty("mvid")]
        public string? Mvid { get; set; }

        /// <summary>
        /// Gets or sets sha256.
        /// </summary>
        /// <value>The sha256 value.</value>
        [JsonProperty("sha256")]
        public string? Sha256 { get; set; }

        /// <summary>
        /// Gets or sets public key token.
        /// </summary>
        /// <value>The public key token value.</value>
        [JsonProperty("publicKeyToken")]
        public string? PublicKeyToken { get; set; }
    }
}
