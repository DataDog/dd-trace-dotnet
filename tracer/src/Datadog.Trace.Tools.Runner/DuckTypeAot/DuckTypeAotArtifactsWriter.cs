// <copyright file="DuckTypeAotArtifactsWriter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            var profile = DuckTypeAotGenerateProcessor.IsProfilingEnabled() ? new ArtifactsProfile() : null;
            var generatedAtUtc = DateTime.UtcNow;
            var mappings = Measure(profile, static p => p.SortMappingsSeconds, static (p, value) => p.SortMappingsSeconds = value, () => mappingResolutionResult.Mappings.OrderBy(m => m.Key, StringComparer.Ordinal).ToList());
            var toolVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0.0";
            var registryAssemblyInfo = emissionResult.RegistryAssemblyInfo;

            var compatibilityMappings = Measure(profile, static p => p.BuildCompatibilityMappingsSeconds, static (p, value) => p.BuildCompatibilityMappingsSeconds = value, () =>
                mappings
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
                            DynamicFailureReplayed = hasResult && mappingResult!.ReplaysDynamicFailure,
                            CheckedAgainstMetadataOnly = hasResult && mappingResult!.CheckedAgainstMetadataOnly,
                            FailsOnlyForOtherRuntimeTypes = hasResult && mappingResult!.FailsOnlyForOtherRuntimeTypes,
                            RuntimeSpecific = hasResult && mappingResult!.RuntimeSpecific,
                            Details = BuildEffectiveCompatibilityDetails(hasResult ? mappingResult : null),
                            GeneratedProxyAssembly = hasResult ? mappingResult!.GeneratedProxyAssemblyName : null,
                            GeneratedProxyType = hasResult ? mappingResult!.GeneratedProxyTypeName : null
                        };
                    })
                    .ToList());

            var compatibilityMatrix = new DuckTypeAotCompatibilityMatrix
            {
                SchemaVersion = SchemaVersion,
                GeneratedAtUtc = generatedAtUtc.ToString("O", CultureInfo.InvariantCulture),
                RegistryAssembly = registryAssemblyInfo.OutputAssemblyPath,
                TotalMappings = compatibilityMappings.Count,
                Mappings = compatibilityMappings
            };

            Measure(profile, static p => p.WriteCompatibilityMatrixJsonSeconds, static (p, value) => p.WriteCompatibilityMatrixJsonSeconds = value, () => WriteJson(artifactPaths.CompatibilityMatrixPath, compatibilityMatrix));
            Measure(profile, static p => p.WriteCompatibilityMarkdownSeconds, static (p, value) => p.WriteCompatibilityMarkdownSeconds = value, () => WriteCompatibilityMarkdown(artifactPaths.CompatibilityReportPath, compatibilityMatrix));
            Measure(profile, static p => p.WriteTrimmerDescriptorSeconds, static (p, value) => p.WriteTrimmerDescriptorSeconds = value, () => WriteTrimmerDescriptor(artifactPaths.TrimmerDescriptorPath, mappingResolutionResult, emissionResult));
            Measure(profile, static p => p.WritePropsFileSeconds, static (p, value) => p.WritePropsFileSeconds = value, () => WritePropsFile(artifactPaths.PropsPath, artifactPaths, registryAssemblyInfo));
            Measure(profile, static p => p.WriteManifestSeconds, static (p, value) => p.WriteManifestSeconds = value, () => WriteManifest(
                artifactPaths.ManifestPath,
                artifactPaths.TrimmerDescriptorPath,
                artifactPaths.PropsPath,
                mappingResolutionResult,
                registryAssemblyInfo,
                emissionResult,
                generatedAtUtc,
                toolVersion,
                profile));

            if (profile is not null)
            {
                DuckTypeAotGenerateProcessor.WriteProfileMetric(
                    $"artifacts.profile sortMappings={profile.SortMappingsSeconds:F3}s buildCompatibilityMappings={profile.BuildCompatibilityMappingsSeconds:F3}s writeMatrixJson={profile.WriteCompatibilityMatrixJsonSeconds:F3}s writeCompatibilityMarkdown={profile.WriteCompatibilityMarkdownSeconds:F3}s writeTrimmerDescriptor={profile.WriteTrimmerDescriptorSeconds:F3}s writeProps={profile.WritePropsFileSeconds:F3}s writeManifest={profile.WriteManifestSeconds:F3}s");
                DuckTypeAotGenerateProcessor.WriteProfileMetric(
                    $"artifacts.profile manifest registryFingerprint={profile.CreateRegistryAssemblyFingerprintSeconds:F3}s proxyFingerprints={profile.CreateProxyAssemblyFingerprintsSeconds:F3}s targetFingerprints={profile.CreateTargetAssemblyFingerprintsSeconds:F3}s datadogTraceFingerprint={profile.CreateDatadogTraceAssemblyFingerprintSeconds:F3}s manifestJson={profile.WriteManifestJsonSeconds:F3}s mappings={compatibilityMappings.Count}");
            }

            return new DuckTypeAotCompatibilityArtifacts(
                artifactPaths.CompatibilityMatrixPath,
                artifactPaths.CompatibilityReportPath,
                compatibilityMatrix.TotalMappings,
                compatibilityMatrix.Mappings.Count(mapping => string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal)),
                compatibilityMatrix.Mappings.Count(mapping => !mapping.BehavesLikeDynamicDuckTyping),
                compatibilityMatrix.Mappings.Count(mapping => !string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal) && mapping.DynamicFailureReplayed));
        }

        /// <summary>
        /// Writes write manifest.
        /// </summary>
        /// <param name="manifestPath">The manifest path value.</param>
        /// <param name="trimmerDescriptorPath">The trimmer descriptor path value.</param>
        /// <param name="propsPath">The props path value.</param>
        /// <param name="mappingResolutionResult">The mapping resolution result value.</param>
        /// <param name="registryAssemblyInfo">The registry assembly info value.</param>
        /// <param name="emissionResult">The emission result value.</param>
        /// <param name="generatedAtUtc">The generated at utc value.</param>
        /// <param name="toolVersion">The tool version value.</param>
        private static void WriteManifest(
            string manifestPath,
            string trimmerDescriptorPath,
            string propsPath,
            DuckTypeAotMappingResolutionResult mappingResolutionResult,
            DuckTypeAotRegistryAssemblyInfo registryAssemblyInfo,
            DuckTypeAotRegistryEmissionResult emissionResult,
            DateTime generatedAtUtc,
            string toolVersion,
            ArtifactsProfile? profile)
        {
            var registryAssemblyFingerprint = Measure(profile, static p => p.CreateRegistryAssemblyFingerprintSeconds, static (p, value) => p.CreateRegistryAssemblyFingerprintSeconds = value, () => CreateAssemblyFingerprint(registryAssemblyInfo.OutputAssemblyPath));
            var aliasRegistrationCount = emissionResult.RuntimeRegistrations.Count(registration => !registration.IsCanonical);
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
                RegistryStrongNameSigned = !StringUtil.IsNullOrWhiteSpace(registryAssemblyFingerprint.PublicKeyToken),
                RegistryPublicKeyToken = registryAssemblyFingerprint.PublicKeyToken,
                TrimmerDescriptorPath = trimmerDescriptorPath,
                TrimmerDescriptorSha256 = ComputeSha256(trimmerDescriptorPath),
                PropsPath = propsPath,
                PropsSha256 = ComputeSha256(propsPath),
                TotalRuntimeRegistrations = emissionResult.RuntimeRegistrations.Count,
                AliasRegistrations = aliasRegistrationCount,
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
                ProxyAssemblies = Measure(profile, static p => p.CreateProxyAssemblyFingerprintsSeconds, static (p, value) => p.CreateProxyAssemblyFingerprintsSeconds = value, () => CreateAssemblyFingerprints(mappingResolutionResult.ProxyAssemblyPathsByName.Values)),
                TargetAssemblies = Measure(profile, static p => p.CreateTargetAssemblyFingerprintsSeconds, static (p, value) => p.CreateTargetAssemblyFingerprintsSeconds = value, () => CreateAssemblyFingerprints(mappingResolutionResult.TargetAssemblyPathsByName.Values)),
                DatadogTraceAssembly = Measure(profile, static p => p.CreateDatadogTraceAssemblyFingerprintSeconds, static (p, value) => p.CreateDatadogTraceAssemblyFingerprintSeconds = value, () => CreateAssemblyFingerprint(mappingResolutionResult.GetDatadogTraceAssemblyPath(out _)))
            };

            Measure(profile, static p => p.WriteManifestJsonSeconds, static (p, value) => p.WriteManifestJsonSeconds = value, () => WriteJson(manifestPath, manifest));
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
                .AppendLine("A status marked `(replayed)` is a failure dynamic duck typing has too, which the registry replays. One marked")
                .AppendLine("`(metadata only)` couldn't be evaluated with dynamic duck typing in the generator, and may differ from it. One marked")
                .AppendLine("`(other runtime types)` behaves like dynamic duck typing for its target, but not for another runtime type of it (a derived type, a reverse")
                .AppendLine("proxy type...). One marked `(runtime specific)` targets, or binds, a type or member of the core library that isn't public, which")
                .AppendLine("other runtimes (e.g. NativeAOT) may not have.")
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
                    .Append(mapping.DynamicFailureReplayed ? " (replayed)" : string.Empty)
                    .Append(mapping.CheckedAgainstMetadataOnly ? " (metadata only)" : string.Empty)
                    .Append(mapping.FailsOnlyForOtherRuntimeTypes ? " (other runtime types)" : string.Empty)
                    .Append(mapping.RuntimeSpecific ? " (runtime specific)" : string.Empty)
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

                if (!StringUtil.IsNullOrWhiteSpace(mapping.GeneratedProxyType) || !StringUtil.IsNullOrWhiteSpace(mapping.GeneratedProxyAssembly))
                {
                    _ = sb.Append("|  |  |  |  |  | generated: ")
                        .Append(mapping.GeneratedProxyType ?? "-")
                        .Append(", ")
                        .Append(mapping.GeneratedProxyAssembly ?? "-")
                        .AppendLine(" |  |");
                }

                if (!StringUtil.IsNullOrWhiteSpace(mapping.Details))
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
        /// Writes the trimmer descriptor: it roots the bootstrap type (which references the generated proxy types and the target
        /// members they use) and the proxy types of the registered mappings. Target types and generated proxy types aren't
        /// rooted: a type a trimmer or NativeAOT's compiler can't load (e.g. a type of the generator's core library NativeAOT's
        /// doesn't define) would fail the whole build, where its registration only fails alone at runtime.
        /// </summary>
        /// <param name="path">The path value.</param>
        /// <param name="mappingResolutionResult">The mapping resolution result value.</param>
        /// <param name="emissionResult">The emission result value.</param>
        internal static void WriteTrimmerDescriptor(
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
                if (!emissionResult.MappingResultsByKey.TryGetValue(mapping.Key, out var mappingResult))
                {
                    continue;
                }

                // A mapping that fails only for other runtime types of its target still registers its proxy type.
                var effectiveStatus = ResolveEffectiveCompatibilityStatus(mappingResult);
                if (!string.Equals(effectiveStatus, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal) && !mappingResult.FailsOnlyForOtherRuntimeTypes)
                {
                    continue;
                }

                AddTypeRoot(typesByAssembly, mapping.ProxyAssemblyName, mapping.ProxyTypeName);
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
            if (StringUtil.IsNullOrWhiteSpace(assemblyName) ||
                StringUtil.IsNullOrWhiteSpace(typeName) ||
                DuckTypeAotNameHelpers.GetTrimmerDescriptorTypeName(typeName) is not { } descriptorTypeName)
            {
                return;
            }

            if (!typesByAssembly.TryGetValue(assemblyName, out var assemblyTypes))
            {
                assemblyTypes = new HashSet<string>(StringComparer.Ordinal);
                typesByAssembly[assemblyName] = assemblyTypes;
            }

            _ = assemblyTypes.Add(descriptorTypeName);
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
        private static void WritePropsFile(string path, DuckTypeAotArtifactPaths artifactPaths, DuckTypeAotRegistryAssemblyInfo registryAssemblyInfo)
        {
            var outputAssemblyPath = EscapeXml(EscapeMsBuildPath(artifactPaths.OutputAssemblyPath));
            var trimmerDescriptorPath = EscapeXml(EscapeMsBuildPath(artifactPaths.TrimmerDescriptorPath));
            var assemblyName = EscapeXml(EscapeMsBuildPath(registryAssemblyInfo.AssemblyName));

            var propsContent =
                "<Project>" + Environment.NewLine +
                "  <ItemGroup>" + Environment.NewLine +
                $"    <Reference Include=\"{assemblyName}\">" + Environment.NewLine +
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
        private static string EscapeMsBuildPath(string value)
        {
            // MSBuild unescapes these after evaluating properties and item expressions.
            // Escape '%' first so literal escape sequences are preserved too.
            return value
                .Replace("%", "%25")
                .Replace("$", "%24")
                .Replace("@", "%40")
                .Replace(";", "%3B")
                .Replace("'", "%27")
                .Replace("(", "%28")
                .Replace(")", "%29")
                .Replace("?", "%3F")
                .Replace("*", "%2A");
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

        private static T Measure<T>(ArtifactsProfile? profile, Func<ArtifactsProfile, double> getter, Action<ArtifactsProfile, double> setter, Func<T> action)
        {
            if (profile is null)
            {
                return action();
            }

            var stopwatch = Stopwatch.StartNew();
            var result = action();
            stopwatch.Stop();
            setter(profile, getter(profile) + stopwatch.Elapsed.TotalSeconds);
            return result;
        }

        private static void Measure(ArtifactsProfile? profile, Func<ArtifactsProfile, double> getter, Action<ArtifactsProfile, double> setter, Action action)
        {
            if (profile is null)
            {
                action();
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            action();
            stopwatch.Stop();
            setter(profile, getter(profile) + stopwatch.Elapsed.TotalSeconds);
        }

        private sealed class ArtifactsProfile
        {
            internal double SortMappingsSeconds { get; set; }

            internal double BuildCompatibilityMappingsSeconds { get; set; }

            internal double WriteCompatibilityMatrixJsonSeconds { get; set; }

            internal double WriteCompatibilityMarkdownSeconds { get; set; }

            internal double WriteTrimmerDescriptorSeconds { get; set; }

            internal double WritePropsFileSeconds { get; set; }

            internal double WriteManifestSeconds { get; set; }

            internal double CreateRegistryAssemblyFingerprintSeconds { get; set; }

            internal double CreateProxyAssemblyFingerprintsSeconds { get; set; }

            internal double CreateTargetAssemblyFingerprintsSeconds { get; set; }

            internal double CreateDatadogTraceAssemblyFingerprintSeconds { get; set; }

            internal double WriteManifestJsonSeconds { get; set; }
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
        /// <param name="nonCompatibleMappings">The mappings that aren't compatible, without the ones that replay a dynamic failure.</param>
        /// <param name="replayedDynamicFailureMappings">The mappings whose dynamic duck typing failure the registry replays.</param>
        public DuckTypeAotCompatibilityArtifacts(string matrixPath, string reportPath, int totalMappings, int compatibleMappings, int nonCompatibleMappings, int replayedDynamicFailureMappings)
        {
            MatrixPath = matrixPath;
            ReportPath = reportPath;
            TotalMappings = totalMappings;
            CompatibleMappings = compatibleMappings;
            NonCompatibleMappings = nonCompatibleMappings;
            ReplayedDynamicFailureMappings = replayedDynamicFailureMappings;
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

        /// <summary>
        /// Gets the mappings whose dynamic duck typing failure the registry replays.
        /// </summary>
        /// <value>The replayed dynamic failure mappings value.</value>
        public int ReplayedDynamicFailureMappings { get; }
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
        /// Gets or sets a value indicating whether the registry replays the failure dynamic duck typing has for this mapping
        /// (the mapping fails in both modes, the same way).
        /// </summary>
        /// <value>true if the dynamic failure is replayed; otherwise, false.</value>
        [JsonProperty("dynamicFailureReplayed")]
        public bool DynamicFailureReplayed { get; set; }

        /// <summary>
        /// Gets a value indicating whether the mapping behaves like in dynamic duck typing: it's compatible, or the registry
        /// replays the failure dynamic duck typing has.
        /// </summary>
        [JsonIgnore]
        public bool BehavesLikeDynamicDuckTyping => string.Equals(Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.OrdinalIgnoreCase) || DynamicFailureReplayed;

        /// <summary>
        /// Gets or sets a value indicating whether the generator couldn't evaluate the mapping with dynamic duck typing, so it's
        /// checked against metadata only, which may differ from dynamic duck typing.
        /// </summary>
        /// <value>true if the mapping is checked against metadata only; otherwise, false.</value>
        [JsonProperty("checkedAgainstMetadataOnly", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool CheckedAgainstMetadataOnly { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the mapping behaves like dynamic duck typing for its target (it's compatible,
        /// or replays the failure of dynamic duck typing), but the registry can't create the proxy of another runtime type of it
        /// (a derived type, a reverse proxy type...) like dynamic duck typing does.
        /// </summary>
        /// <value>true if only other runtime types of the target fail; otherwise, false.</value>
        [JsonProperty("failsOnlyForOtherRuntimeTypes", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool FailsOnlyForOtherRuntimeTypes { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the mapping targets, or its proxy binds, a type or member of the core library
        /// that isn't public (DTAOT0216): other runtimes (e.g. NativeAOT) may not have it, where the registration or the call fails.
        /// </summary>
        /// <value>true if the mapping is specific to the generator's runtime; otherwise, false.</value>
        [JsonProperty("runtimeSpecific", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool RuntimeSpecific { get; set; }

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
        /// Gets or sets total runtime registrations.
        /// </summary>
        [JsonProperty("totalRuntimeRegistrations")]
        public int TotalRuntimeRegistrations { get; set; }

        /// <summary>
        /// Gets or sets alias registrations.
        /// </summary>
        [JsonProperty("aliasRegistrations")]
        public int AliasRegistrations { get; set; }

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
