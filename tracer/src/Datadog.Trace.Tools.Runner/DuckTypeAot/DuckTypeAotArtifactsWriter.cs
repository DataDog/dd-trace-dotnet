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
    internal static class DuckTypeAotArtifactsWriter
    {
        private const string SchemaVersion = "1";

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
                    return new DuckTypeAotCompatibilityMapping
                    {
                        Id = $"MAP-{index + 1:D4}",
                        Mode = mapping.Mode.ToString().ToLowerInvariant(),
                        ProxyType = mapping.ProxyTypeName,
                        ProxyAssembly = mapping.ProxyAssemblyName,
                        TargetType = mapping.TargetTypeName,
                        TargetAssembly = mapping.TargetAssemblyName,
                        Source = mapping.Source.ToString().ToLowerInvariant(),
                        Status = hasResult ? mappingResult!.Status : DuckTypeAotCompatibilityStatuses.PendingProxyEmission,
                        DiagnosticCode = hasResult ? mappingResult!.DiagnosticCode : null,
                        Details = hasResult ? mappingResult!.Detail : null,
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
            WriteManifest(artifactPaths.ManifestPath, mappingResolutionResult, registryAssemblyInfo, generatedAtUtc, toolVersion);
            WriteTrimmerDescriptor(artifactPaths.TrimmerDescriptorPath, mappingResolutionResult, emissionResult);
            WritePropsFile(artifactPaths.PropsPath, artifactPaths, registryAssemblyInfo);

            return new DuckTypeAotCompatibilityArtifacts(
                artifactPaths.CompatibilityMatrixPath,
                artifactPaths.CompatibilityReportPath,
                compatibilityMatrix.TotalMappings,
                compatibilityMatrix.Mappings.Count(mapping => string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal)),
                compatibilityMatrix.Mappings.Count(mapping => !string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal)));
        }

        private static void WriteManifest(
            string manifestPath,
            DuckTypeAotMappingResolutionResult mappingResolutionResult,
            DuckTypeAotRegistryAssemblyInfo registryAssemblyInfo,
            DateTime generatedAtUtc,
            string toolVersion)
        {
            var manifest = new DuckTypeAotManifest
            {
                SchemaVersion = SchemaVersion,
                ToolVersion = toolVersion,
                GeneratedAtUtc = generatedAtUtc.ToString("O", CultureInfo.InvariantCulture),
                RegistryAssembly = registryAssemblyInfo.OutputAssemblyPath,
                RegistryAssemblyName = registryAssemblyInfo.AssemblyName,
                RegistryBootstrapType = registryAssemblyInfo.BootstrapTypeFullName,
                RegistryMvid = registryAssemblyInfo.Mvid.ToString("D"),
                Mappings = mappingResolutionResult.Mappings
                    .OrderBy(m => m.Key, StringComparer.Ordinal)
                    .Select(mapping => new DuckTypeAotManifestMapping
                    {
                        Mode = mapping.Mode.ToString().ToLowerInvariant(),
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

        private static List<DuckTypeAotAssemblyFingerprint> CreateAssemblyFingerprints(IEnumerable<string> assemblyPaths)
        {
            return assemblyPaths
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(CreateAssemblyFingerprint)
                .ToList();
        }

        private static DuckTypeAotAssemblyFingerprint CreateAssemblyFingerprint(string assemblyPath)
        {
            var fullPath = Path.GetFullPath(assemblyPath);
            var assemblyName = AssemblyName.GetAssemblyName(fullPath);
            using var module = ModuleDefMD.Load(fullPath);
            return new DuckTypeAotAssemblyFingerprint
            {
                Name = assemblyName.Name ?? string.Empty,
                Version = assemblyName.Version?.ToString() ?? "0.0.0.0",
                Path = fullPath,
                Mvid = module.Mvid?.ToString("D") ?? string.Empty,
                Sha256 = ComputeSha256(fullPath)
            };
        }

        private static string ComputeSha256(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = sha256.ComputeHash(stream);
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var hashByte in hash)
            {
                _ = sb.Append(hashByte.ToString("x2", CultureInfo.InvariantCulture));
            }

            return sb.ToString();
        }

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

                if (!string.IsNullOrWhiteSpace(mapping.GeneratedProxyType) || !string.IsNullOrWhiteSpace(mapping.GeneratedProxyAssembly))
                {
                    _ = sb.Append("|  |  |  |  |  | generated: ")
                        .Append(mapping.GeneratedProxyType ?? "-")
                        .Append(", ")
                        .Append(mapping.GeneratedProxyAssembly ?? "-")
                        .AppendLine(" |  |");
                }

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
                if (!emissionResult.MappingResultsByKey.TryGetValue(mapping.Key, out var mappingResult) ||
                    !string.Equals(mappingResult.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal))
                {
                    continue;
                }

                AddTypeRoot(typesByAssembly, mapping.ProxyAssemblyName, mapping.ProxyTypeName);
                AddTypeRoot(typesByAssembly, mapping.TargetAssemblyName, mapping.TargetTypeName);

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

        private static void AddTypeRoot(IDictionary<string, HashSet<string>> typesByAssembly, string assemblyName, string typeName)
        {
            if (string.IsNullOrWhiteSpace(assemblyName) || string.IsNullOrWhiteSpace(typeName))
            {
                return;
            }

            if (!typesByAssembly.TryGetValue(assemblyName, out var assemblyTypes))
            {
                assemblyTypes = new HashSet<string>(StringComparer.Ordinal);
                typesByAssembly[assemblyName] = assemblyTypes;
            }

            _ = assemblyTypes.Add(NormalizeTypeNameForLinker(typeName));
        }

        private static string NormalizeTypeNameForLinker(string typeName)
        {
            return typeName.Replace('+', '/');
        }

        private static string EscapeXml(string value)
        {
            return value
                .Replace("&", "&amp;")
                .Replace("\"", "&quot;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }

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

        private static string EscapeMsBuildPath(string value)
        {
            return value.Replace("$", "$$");
        }

        private static void WriteJson<T>(string path, T value)
        {
            var json = JsonConvert.SerializeObject(value, Formatting.Indented);
            File.WriteAllText(path, json);
        }
    }

    internal sealed class DuckTypeAotCompatibilityArtifacts
    {
        public DuckTypeAotCompatibilityArtifacts(string matrixPath, string reportPath, int totalMappings, int compatibleMappings, int nonCompatibleMappings)
        {
            MatrixPath = matrixPath;
            ReportPath = reportPath;
            TotalMappings = totalMappings;
            CompatibleMappings = compatibleMappings;
            NonCompatibleMappings = nonCompatibleMappings;
        }

        public string MatrixPath { get; }

        public string ReportPath { get; }

        public int TotalMappings { get; }

        public int CompatibleMappings { get; }

        public int NonCompatibleMappings { get; }
    }

    internal sealed class DuckTypeAotCompatibilityMatrix
    {
        [JsonProperty("schemaVersion")]
        public string? SchemaVersion { get; set; }

        [JsonProperty("generatedAtUtc")]
        public string? GeneratedAtUtc { get; set; }

        [JsonProperty("registryAssembly")]
        public string? RegistryAssembly { get; set; }

        [JsonProperty("totalMappings")]
        public int TotalMappings { get; set; }

        [JsonProperty("mappings")]
        public List<DuckTypeAotCompatibilityMapping> Mappings { get; set; } = new();
    }

    internal sealed class DuckTypeAotCompatibilityMapping
    {
        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("mode")]
        public string? Mode { get; set; }

        [JsonProperty("proxyType")]
        public string? ProxyType { get; set; }

        [JsonProperty("proxyAssembly")]
        public string? ProxyAssembly { get; set; }

        [JsonProperty("targetType")]
        public string? TargetType { get; set; }

        [JsonProperty("targetAssembly")]
        public string? TargetAssembly { get; set; }

        [JsonProperty("source")]
        public string? Source { get; set; }

        [JsonProperty("status")]
        public string? Status { get; set; }

        [JsonProperty("diagnosticCode")]
        public string? DiagnosticCode { get; set; }

        [JsonProperty("details")]
        public string? Details { get; set; }

        [JsonProperty("generatedProxyAssembly")]
        public string? GeneratedProxyAssembly { get; set; }

        [JsonProperty("generatedProxyType")]
        public string? GeneratedProxyType { get; set; }
    }

    internal sealed class DuckTypeAotManifest
    {
        [JsonProperty("schemaVersion")]
        public string? SchemaVersion { get; set; }

        [JsonProperty("toolVersion")]
        public string? ToolVersion { get; set; }

        [JsonProperty("generatedAtUtc")]
        public string? GeneratedAtUtc { get; set; }

        [JsonProperty("registryAssembly")]
        public string? RegistryAssembly { get; set; }

        [JsonProperty("registryAssemblyName")]
        public string? RegistryAssemblyName { get; set; }

        [JsonProperty("registryBootstrapType")]
        public string? RegistryBootstrapType { get; set; }

        [JsonProperty("registryMvid")]
        public string? RegistryMvid { get; set; }

        [JsonProperty("mappings")]
        public List<DuckTypeAotManifestMapping> Mappings { get; set; } = new();

        [JsonProperty("genericInstantiations")]
        public List<DuckTypeAotManifestTypeReference> GenericInstantiations { get; set; } = new();

        [JsonProperty("proxyAssemblies")]
        public List<DuckTypeAotAssemblyFingerprint> ProxyAssemblies { get; set; } = new();

        [JsonProperty("targetAssemblies")]
        public List<DuckTypeAotAssemblyFingerprint> TargetAssemblies { get; set; } = new();

        [JsonProperty("datadogTraceAssembly")]
        public DuckTypeAotAssemblyFingerprint? DatadogTraceAssembly { get; set; }
    }

    internal sealed class DuckTypeAotManifestMapping
    {
        [JsonProperty("mode")]
        public string? Mode { get; set; }

        [JsonProperty("proxyType")]
        public string? ProxyType { get; set; }

        [JsonProperty("proxyAssembly")]
        public string? ProxyAssembly { get; set; }

        [JsonProperty("targetType")]
        public string? TargetType { get; set; }

        [JsonProperty("targetAssembly")]
        public string? TargetAssembly { get; set; }

        [JsonProperty("source")]
        public string? Source { get; set; }
    }

    internal sealed class DuckTypeAotManifestTypeReference
    {
        [JsonProperty("type")]
        public string? Type { get; set; }

        [JsonProperty("assembly")]
        public string? Assembly { get; set; }
    }

    internal sealed class DuckTypeAotAssemblyFingerprint
    {
        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("version")]
        public string? Version { get; set; }

        [JsonProperty("path")]
        public string? Path { get; set; }

        [JsonProperty("mvid")]
        public string? Mvid { get; set; }

        [JsonProperty("sha256")]
        public string? Sha256 { get; set; }
    }
}
