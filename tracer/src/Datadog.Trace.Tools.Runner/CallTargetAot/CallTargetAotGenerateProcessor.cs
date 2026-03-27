// <copyright file="CallTargetAotGenerateProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Tools.Runner.CallTargetAot;

/// <summary>
/// Generates the CallTarget NativeAOT artifacts consumed by the publish-integrated workflow.
/// </summary>
internal static class CallTargetAotGenerateProcessor
{
    /// <summary>
    /// Defines the manifest schema version used by the generated artifacts.
    /// </summary>
    private const string SchemaVersion = "1";

    /// <summary>
    /// Generates the registry assembly, manifest, rewrite plan, props, targets, and compatibility artifacts.
    /// </summary>
    /// <param name="options">The normalized generation options.</param>
    /// <returns>Zero when generation succeeds; otherwise one.</returns>
    internal static int Process(CallTargetAotGenerateOptions options)
    {
        ValidateInputs(options);

        var artifactPaths = CallTargetAotArtifactPaths.Create(options);
        EnsureParentDirectoryExists(artifactPaths.OutputAssemblyPath);
        EnsureParentDirectoryExists(artifactPaths.TargetsPath);
        EnsureParentDirectoryExists(artifactPaths.PropsPath);
        EnsureParentDirectoryExists(artifactPaths.TrimmerDescriptorPath);
        EnsureParentDirectoryExists(artifactPaths.ManifestPath);
        EnsureParentDirectoryExists(artifactPaths.RewritePlanPath);
        EnsureParentDirectoryExists(artifactPaths.CompatibilityReportPath);
        EnsureParentDirectoryExists(artifactPaths.CompatibilityMatrixPath);

        var discoveredDefinitions = CallTargetAotDefinitionDiscovery.Discover(options.TracerAssemblyPath);
        var evaluatedDefinitions = CallTargetAotMethodMatcher.Match(discoveredDefinitions, options);
        var matchedDefinitions = evaluatedDefinitions.Where(static match => match.IsSupported).ToList();
        var duckTypeGenerationResult = CallTargetAotDuckTypeSupport.GenerateIfNeeded(options, artifactPaths, matchedDefinitions);
        var targetAssemblyNames = matchedDefinitions
                                 .Select(static match => match.TargetAssemblyName)
                                 .Distinct(StringComparer.OrdinalIgnoreCase)
                                 .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
                                 .ToList();
        var targetAssemblyFileNames = matchedDefinitions
                                     .Select(static match => Path.GetFileName(match.TargetAssemblyPath))
                                     .Where(static fileName => !string.IsNullOrWhiteSpace(fileName))
                                     .Distinct(StringComparer.OrdinalIgnoreCase)
                                     .OrderBy(static fileName => fileName, StringComparer.OrdinalIgnoreCase)
                                     .ToList();
        var rewritePlan = new CallTargetAotRewritePlan
        {
            TargetAssemblyNames = targetAssemblyNames,
            TargetAssemblyFileNames = targetAssemblyFileNames
        };
        var manifest = new CallTargetAotManifest
        {
            SchemaVersion = SchemaVersion,
            TracerAssemblyPath = Path.GetFullPath(options.TracerAssemblyPath),
            RegistryAssemblyPath = artifactPaths.OutputAssemblyPath,
            RegistryBootstrapTypeName = CallTargetAotRegistryAssemblyEmitter.BootstrapNamespace + "." + CallTargetAotRegistryAssemblyEmitter.BootstrapTypeName,
            RegistryBootstrapMethodName = CallTargetAotRegistryAssemblyEmitter.BootstrapMethodName,
            BootstrapMarker = CallTargetAotRegistryAssemblyEmitter.BootstrapMarker,
            RewritePlanPath = artifactPaths.RewritePlanPath,
            RewritePlan = rewritePlan,
            EvaluatedDefinitions = evaluatedDefinitions,
            MatchedDefinitions = matchedDefinitions
        };
        manifest.DuckTypeDependency = duckTypeGenerationResult?.Dependency;
        manifest.RegistryAssemblyName = CallTargetAotRegistryAssemblyEmitter.Emit(artifactPaths, manifest, options.AssemblyName, duckTypeGenerationResult);

        WriteManifest(artifactPaths.ManifestPath, manifest);
        WriteRewritePlan(artifactPaths.RewritePlanPath, rewritePlan);
        WriteCompatibilityArtifacts(artifactPaths, manifest);
        WriteTrimmerDescriptor(artifactPaths.TrimmerDescriptorPath, manifest);
        WritePropsFile(artifactPaths, manifest);
        WriteTargetsFile(artifactPaths, manifest);
        return 0;
    }

    /// <summary>
    /// Validates the supplied generation inputs before any files are emitted.
    /// </summary>
    /// <param name="options">The generation options to validate.</param>
    private static void ValidateInputs(CallTargetAotGenerateOptions options)
    {
        if (!File.Exists(options.TracerAssemblyPath))
        {
            throw new FileNotFoundException("The tracer assembly path does not exist.", options.TracerAssemblyPath);
        }

        foreach (var targetFolder in options.TargetFolders)
        {
            if (!Directory.Exists(targetFolder))
            {
                throw new DirectoryNotFoundException($"The target folder does not exist: {targetFolder}");
            }
        }
    }

    /// <summary>
    /// Writes the manifest consumed by the rewrite step.
    /// </summary>
    /// <param name="manifestPath">The manifest output path.</param>
    /// <param name="manifest">The manifest content to persist.</param>
    private static void WriteManifest(string manifestPath, CallTargetAotManifest manifest)
    {
        var json = JsonConvert.SerializeObject(manifest, Formatting.Indented);
        File.WriteAllText(manifestPath, json);
    }

    /// <summary>
    /// Writes the standalone rewrite-plan artifact consumed by tests and debugging workflows.
    /// </summary>
    /// <param name="rewritePlanPath">The standalone rewrite-plan output path.</param>
    /// <param name="rewritePlan">The rewrite plan to persist.</param>
    private static void WriteRewritePlan(string rewritePlanPath, CallTargetAotRewritePlan rewritePlan)
    {
        var json = JsonConvert.SerializeObject(rewritePlan, Formatting.Indented);
        File.WriteAllText(rewritePlanPath, json);
    }

    /// <summary>
    /// Writes compatibility artifacts that describe each generated binding selected for the current CallTarget AOT registry.
    /// </summary>
    /// <param name="artifactPaths">The artifact paths to populate.</param>
    /// <param name="manifest">The manifest that describes the current generation output.</param>
    private static void WriteCompatibilityArtifacts(CallTargetAotArtifactPaths artifactPaths, CallTargetAotManifest manifest)
    {
        var compatibilityEntries = manifest.EvaluatedDefinitions
                                           .OrderBy(static match => match.TargetAssemblyName, StringComparer.OrdinalIgnoreCase)
                                           .ThenBy(static match => match.TargetTypeName, StringComparer.Ordinal)
                                           .ThenBy(static match => match.TargetMethodName, StringComparer.Ordinal)
                                           .Select(
                                                static match => new
                                                {
                                                    status = match.Status,
                                                    targetAssembly = match.TargetAssemblyName,
                                                    targetType = match.TargetTypeName,
                                                    targetMethod = match.TargetMethodName,
                                                    returnType = match.ReturnTypeName,
                                                    parameterTypes = match.ParameterTypeNames,
                                                    integrationType = match.IntegrationTypeName,
                                                    handlerKind = match.HandlerKind,
                                                    diagnosticCode = match.DiagnosticCode,
                                                    diagnosticMessage = match.DiagnosticMessage,
                                                    usesSlowBegin = match.UsesSlowBegin,
                                                    returnsValue = match.ReturnsValue,
                                                    requiresAsyncContinuation = match.RequiresAsyncContinuation,
                                                    asyncResultType = match.AsyncResultTypeName,
                                                    duckInstanceMappingKey = match.DuckInstanceMappingKey,
                                                    duckParameterMappingKeys = match.DuckParameterMappingKeys,
                                                    duckReturnMappingKey = match.DuckReturnMappingKey,
                                                    duckAsyncResultMappingKey = match.DuckAsyncResultMappingKey
                                                })
                                           .ToList();
        var compatibleBindingCount = manifest.EvaluatedDefinitions.Count(static match => match.IsSupported);
        var incompatibleBindingCount = compatibilityEntries.Count - compatibleBindingCount;
        var compatibilityMatrixJson = JsonConvert.SerializeObject(
            new
            {
                schemaVersion = manifest.SchemaVersion,
                registryAssembly = manifest.RegistryAssemblyPath,
                rewritePlanPath = manifest.RewritePlanPath,
                targetAssemblies = manifest.EvaluatedDefinitions
                                           .Select(static match => match.TargetAssemblyName)
                                           .Distinct(StringComparer.OrdinalIgnoreCase)
                                           .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
                                           .ToList(),
                totalBindings = compatibilityEntries.Count,
                compatibleBindings = compatibleBindingCount,
                incompatibleBindings = incompatibleBindingCount,
                bindings = compatibilityEntries
            },
            Formatting.Indented);
        File.WriteAllText(artifactPaths.CompatibilityMatrixPath, compatibilityMatrixJson);

        var markdownLines = new List<string>
        {
            "# CallTarget AOT Compatibility Report" + Environment.NewLine +
            Environment.NewLine +
            $"- Registry assembly: `{manifest.RegistryAssemblyPath}`" + Environment.NewLine +
            $"- Standalone rewrite plan: `{manifest.RewritePlanPath}`" + Environment.NewLine +
            $"- Target assemblies: `{string.Join(", ", manifest.RewritePlan.TargetAssemblyNames)}`" + Environment.NewLine +
            $"- Evaluated bindings: `{compatibilityEntries.Count}`" + Environment.NewLine +
            $"- Compatible bindings: `{compatibleBindingCount}`" + Environment.NewLine +
            $"- Incompatible bindings: `{incompatibleBindingCount}`" + Environment.NewLine +
            Environment.NewLine +
            "| Status | Target | Method | Integration | Handler | Diagnostic |" + Environment.NewLine +
            "| --- | --- | --- | --- | --- | --- |" + Environment.NewLine
        };
        markdownLines.AddRange(
            compatibilityEntries.Select(
                static entry =>
                    $"| {entry.status} | `{entry.targetAssembly}` | `{entry.targetType}.{entry.targetMethod}` | `{entry.integrationType}` | `{entry.handlerKind}` | `{entry.diagnosticCode ?? string.Empty}` {entry.diagnosticMessage ?? string.Empty} |"));
        File.WriteAllText(artifactPaths.CompatibilityReportPath, string.Join(Environment.NewLine, markdownLines));
    }

    /// <summary>
    /// Writes the trimmer descriptor that keeps the generated bootstrap rooted during NativeAOT publish.
    /// </summary>
    /// <param name="trimmerDescriptorPath">The trimmer descriptor output path.</param>
    /// <param name="manifest">The manifest that describes the generated registry bootstrap.</param>
    private static void WriteTrimmerDescriptor(string trimmerDescriptorPath, CallTargetAotManifest manifest)
    {
        var preservedIntegrationTypes = GetPreservedIntegrationTypes(manifest);
        var content =
            "<linker>" + Environment.NewLine +
            $"  <assembly fullname=\"{EscapeXml(manifest.RegistryAssemblyName)}\">" + Environment.NewLine +
            $"    <type fullname=\"{EscapeXml(manifest.RegistryBootstrapTypeName)}\" preserve=\"all\" />" + Environment.NewLine +
            "  </assembly>" + Environment.NewLine +
            "  <assembly fullname=\"Datadog.Trace\">" + Environment.NewLine +
            string.Join(
                Environment.NewLine,
                preservedIntegrationTypes.Select(typeName => $"    <type fullname=\"{EscapeXml(typeName)}\" preserve=\"all\" />")) + Environment.NewLine +
            "  </assembly>" + Environment.NewLine +
            "</linker>" + Environment.NewLine;
        File.WriteAllText(trimmerDescriptorPath, content);
    }

    /// <summary>
    /// Resolves the matched integration types that must be preserved during NativeAOT publish, including nested
    /// compiler-generated state-machine types required by async callbacks.
    /// </summary>
    /// <param name="manifest">The manifest that describes the matched integrations.</param>
    /// <returns>The full set of Datadog.Trace type names that should be rooted.</returns>
    private static List<string> GetPreservedIntegrationTypes(CallTargetAotManifest manifest)
    {
        var requestedTypeNames = manifest.MatchedDefinitions
                                        .Select(static match => match.IntegrationTypeName)
                                        .Distinct(StringComparer.Ordinal)
                                        .OrderBy(static name => name, StringComparer.Ordinal)
                                        .ToList();
        var preservedTypeNames = new SortedSet<string>(requestedTypeNames, StringComparer.Ordinal);
        var tracerAssembly = Assembly.LoadFrom(manifest.TracerAssemblyPath);
        foreach (var integrationTypeName in requestedTypeNames)
        {
            var integrationType = tracerAssembly.GetType(integrationTypeName, throwOnError: false);
            if (integrationType is null)
            {
                continue;
            }

            foreach (var nestedType in integrationType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
            {
                _ = preservedTypeNames.Add(nestedType.FullName ?? nestedType.Name);
            }
        }

        return preservedTypeNames.ToList();
    }

    /// <summary>
    /// Writes the generated props file that references the proof registry assembly and imports the sibling targets file.
    /// </summary>
    /// <param name="artifactPaths">The artifact paths to reference from the props file.</param>
    /// <param name="manifest">The manifest that describes the generated registry assembly.</param>
    private static void WritePropsFile(CallTargetAotArtifactPaths artifactPaths, CallTargetAotManifest manifest)
    {
        var outputAssemblyPath = EscapeMsBuildPath(artifactPaths.OutputAssemblyPath);
        var tracerAssemblyPath = EscapeMsBuildPath(manifest.TracerAssemblyPath);
        var trimmerDescriptorPath = EscapeMsBuildPath(artifactPaths.TrimmerDescriptorPath);
        var targetsPath = EscapeMsBuildPath(artifactPaths.TargetsPath);
        var manifestPath = EscapeMsBuildPath(artifactPaths.ManifestPath);
        var runnerAssemblyPath = EscapeMsBuildPath(typeof(Program).Assembly.Location);
        var duckTypePropsPath = manifest.DuckTypeDependency is null ? null : EscapeMsBuildPath(manifest.DuckTypeDependency.PropsPath);

        var content =
            "<Project>" + Environment.NewLine +
            "  <PropertyGroup>" + Environment.NewLine +
            $"    <DatadogCallTargetAotManifestPath>{manifestPath}</DatadogCallTargetAotManifestPath>" + Environment.NewLine +
            $"    <DatadogCallTargetAotRunnerPath>{runnerAssemblyPath}</DatadogCallTargetAotRunnerPath>" + Environment.NewLine +
            "  </PropertyGroup>" + Environment.NewLine +
            "  <ItemGroup>" + Environment.NewLine +
            "    <Reference Include=\"Datadog.Trace\">" + Environment.NewLine +
            $"      <HintPath>{tracerAssemblyPath}</HintPath>" + Environment.NewLine +
            "      <Private>true</Private>" + Environment.NewLine +
            "    </Reference>" + Environment.NewLine +
            $"    <Reference Include=\"{EscapeXml(manifest.RegistryAssemblyName)}\">" + Environment.NewLine +
            $"      <HintPath>{outputAssemblyPath}</HintPath>" + Environment.NewLine +
            "      <Private>true</Private>" + Environment.NewLine +
            "    </Reference>" + Environment.NewLine +
            $"    <TrimmerRootDescriptor Include=\"{trimmerDescriptorPath}\" />" + Environment.NewLine +
            "  </ItemGroup>" + Environment.NewLine +
            (duckTypePropsPath is null
                 ? string.Empty
                 : $"  <Import Project=\"{duckTypePropsPath}\" Condition=\"Exists('{EscapeXml(duckTypePropsPath)}')\" />" + Environment.NewLine) +
            $"  <Import Project=\"{targetsPath}\" Condition=\"Exists('{EscapeXml(targetsPath)}')\" />" + Environment.NewLine +
            "</Project>" + Environment.NewLine;
        File.WriteAllText(artifactPaths.PropsPath, content);
    }

    /// <summary>
    /// Writes the generated targets file that rewrites the compiled app assembly and selected references before
    /// NativeAOT publish consumes them.
    /// </summary>
    /// <param name="artifactPaths">The artifact paths referenced by the targets file.</param>
    /// <param name="manifest">The manifest that describes the selected rewrite inputs.</param>
    private static void WriteTargetsFile(CallTargetAotArtifactPaths artifactPaths, CallTargetAotManifest manifest)
    {
        var rewrittenAssemblyPath = "$(IntermediateOutputPath)datadog/calltarget-aot/rewritten/$([System.IO.Path]::GetFileName($(_DatadogCallTargetAotApplicationAssembly)))";
        var content =
            "<Project>" + Environment.NewLine +
            "  <Target Name=\"_DatadogCallTargetAotRewriteAssemblies\" AfterTargets=\"CoreCompile\" BeforeTargets=\"ComputeFilesToPublish\">" + Environment.NewLine +
            "    <PropertyGroup>" + Environment.NewLine +
            "      <_DatadogCallTargetAotRewriteDirectory>$(IntermediateOutputPath)datadog/calltarget-aot/rewritten</_DatadogCallTargetAotRewriteDirectory>" + Environment.NewLine +
            "      <_DatadogCallTargetAotReferenceAssemblyList>@(ReferenceCopyLocalPaths,';')</_DatadogCallTargetAotReferenceAssemblyList>" + Environment.NewLine +
            "      <_DatadogCallTargetAotApplicationAssembly Condition=\"'$(IntermediateAssembly)' != ''\">$(IntermediateAssembly)</_DatadogCallTargetAotApplicationAssembly>" + Environment.NewLine +
            "      <_DatadogCallTargetAotApplicationAssembly Condition=\"'$(_DatadogCallTargetAotApplicationAssembly)' == ''\">$(IntermediateOutputPath)$(TargetFileName)</_DatadogCallTargetAotApplicationAssembly>" + Environment.NewLine +
            "    </PropertyGroup>" + Environment.NewLine +
            "    <MakeDir Directories=\"$(_DatadogCallTargetAotRewriteDirectory)\" />" + Environment.NewLine +
            "    <Exec Command=\"dotnet exec --roll-forward Major &quot;$(DatadogCallTargetAotRunnerPath)&quot; calltarget-aot rewrite --manifest &quot;$(DatadogCallTargetAotManifestPath)&quot; --application-assembly &quot;$(_DatadogCallTargetAotApplicationAssembly)&quot; --reference-assembly-list &quot;$(_DatadogCallTargetAotReferenceAssemblyList)&quot; --output-directory &quot;$(_DatadogCallTargetAotRewriteDirectory)&quot;\" />" + Environment.NewLine +
            $"    <Copy SourceFiles=\"{rewrittenAssemblyPath}\" DestinationFiles=\"$(_DatadogCallTargetAotApplicationAssembly)\" OverwriteReadOnlyFiles=\"true\" />" + Environment.NewLine +
            "    <ItemGroup>" + Environment.NewLine +
            "      <_DatadogCallTargetAotReferenceCandidate Include=\"@(ReferenceCopyLocalPaths)\">" + Environment.NewLine +
            "        <RewrittenPath>$(_DatadogCallTargetAotRewriteDirectory)/%(Filename)%(Extension)</RewrittenPath>" + Environment.NewLine +
            "        <OriginalIdentity>%(Identity)</OriginalIdentity>" + Environment.NewLine +
            "        <DestinationSubDirectory>%(ReferenceCopyLocalPaths.DestinationSubDirectory)</DestinationSubDirectory>" + Environment.NewLine +
            "        <DestinationSubPath>%(ReferenceCopyLocalPaths.DestinationSubPath)</DestinationSubPath>" + Environment.NewLine +
            "        <ReferenceSourceTarget>%(ReferenceCopyLocalPaths.ReferenceSourceTarget)</ReferenceSourceTarget>" + Environment.NewLine +
            "        <Private>%(ReferenceCopyLocalPaths.Private)</Private>" + Environment.NewLine +
            "      </_DatadogCallTargetAotReferenceCandidate>" + Environment.NewLine +
            "      <_DatadogCallTargetAotRewrittenReference Include=\"@(_DatadogCallTargetAotReferenceCandidate->'%(RewrittenPath)')\" Condition=\"Exists('%(_DatadogCallTargetAotReferenceCandidate.RewrittenPath)')\">" + Environment.NewLine +
            "        <OriginalIdentity>%(_DatadogCallTargetAotReferenceCandidate.OriginalIdentity)</OriginalIdentity>" + Environment.NewLine +
            "        <DestinationSubDirectory>%(_DatadogCallTargetAotReferenceCandidate.DestinationSubDirectory)</DestinationSubDirectory>" + Environment.NewLine +
            "        <DestinationSubPath>%(_DatadogCallTargetAotReferenceCandidate.DestinationSubPath)</DestinationSubPath>" + Environment.NewLine +
            "        <ReferenceSourceTarget>%(_DatadogCallTargetAotReferenceCandidate.ReferenceSourceTarget)</ReferenceSourceTarget>" + Environment.NewLine +
            "        <Private>%(_DatadogCallTargetAotReferenceCandidate.Private)</Private>" + Environment.NewLine +
            "      </_DatadogCallTargetAotRewrittenReference>" + Environment.NewLine +
            "      <ReferenceCopyLocalPaths Remove=\"@(_DatadogCallTargetAotRewrittenReference->'%(OriginalIdentity)')\" />" + Environment.NewLine +
            "      <ReferenceCopyLocalPaths Include=\"@(_DatadogCallTargetAotRewrittenReference)\">" + Environment.NewLine +
            "        <DestinationSubDirectory>%(_DatadogCallTargetAotRewrittenReference.DestinationSubDirectory)</DestinationSubDirectory>" + Environment.NewLine +
            "        <DestinationSubPath>%(_DatadogCallTargetAotRewrittenReference.DestinationSubPath)</DestinationSubPath>" + Environment.NewLine +
            "        <ReferenceSourceTarget>%(_DatadogCallTargetAotRewrittenReference.ReferenceSourceTarget)</ReferenceSourceTarget>" + Environment.NewLine +
            "        <Private>%(_DatadogCallTargetAotRewrittenReference.Private)</Private>" + Environment.NewLine +
            "      </ReferenceCopyLocalPaths>" + Environment.NewLine +
            "    </ItemGroup>" + Environment.NewLine +
            "  </Target>" + Environment.NewLine +
            "  <Target Name=\"_DatadogCallTargetAotReplaceResolvedFilesToPublish\" AfterTargets=\"ComputeFilesToPublish\" BeforeTargets=\"_HandleFileConflictsForPublish\">" + Environment.NewLine +
            "    <PropertyGroup>" + Environment.NewLine +
            "      <_DatadogCallTargetAotRewriteDirectory>$(IntermediateOutputPath)datadog/calltarget-aot/rewritten</_DatadogCallTargetAotRewriteDirectory>" + Environment.NewLine +
            "    </PropertyGroup>" + Environment.NewLine +
            "    <ItemGroup>" + Environment.NewLine +
            "      <_DatadogCallTargetAotPublishCandidate Include=\"@(ResolvedFileToPublish)\">" + Environment.NewLine +
            "        <RewrittenPath>$(_DatadogCallTargetAotRewriteDirectory)/%(Filename)%(Extension)</RewrittenPath>" + Environment.NewLine +
            "        <OriginalIdentity>%(Identity)</OriginalIdentity>" + Environment.NewLine +
            "        <RelativePath>%(ResolvedFileToPublish.RelativePath)</RelativePath>" + Environment.NewLine +
            "        <CopyToPublishDirectory>%(ResolvedFileToPublish.CopyToPublishDirectory)</CopyToPublishDirectory>" + Environment.NewLine +
            "        <TargetPath>%(ResolvedFileToPublish.TargetPath)</TargetPath>" + Environment.NewLine +
            "        <AssetType>%(ResolvedFileToPublish.AssetType)</AssetType>" + Environment.NewLine +
            "      </_DatadogCallTargetAotPublishCandidate>" + Environment.NewLine +
            "      <_DatadogCallTargetAotRewrittenPublishFile Include=\"@(_DatadogCallTargetAotPublishCandidate->'%(RewrittenPath)')\" Condition=\"Exists('%(_DatadogCallTargetAotPublishCandidate.RewrittenPath)')\">" + Environment.NewLine +
            "        <OriginalIdentity>%(_DatadogCallTargetAotPublishCandidate.OriginalIdentity)</OriginalIdentity>" + Environment.NewLine +
            "        <RelativePath>%(_DatadogCallTargetAotPublishCandidate.RelativePath)</RelativePath>" + Environment.NewLine +
            "        <CopyToPublishDirectory>%(_DatadogCallTargetAotPublishCandidate.CopyToPublishDirectory)</CopyToPublishDirectory>" + Environment.NewLine +
            "        <TargetPath>%(_DatadogCallTargetAotPublishCandidate.TargetPath)</TargetPath>" + Environment.NewLine +
            "        <AssetType>%(_DatadogCallTargetAotPublishCandidate.AssetType)</AssetType>" + Environment.NewLine +
            "      </_DatadogCallTargetAotRewrittenPublishFile>" + Environment.NewLine +
            "      <ResolvedFileToPublish Remove=\"@(_DatadogCallTargetAotRewrittenPublishFile->'%(OriginalIdentity)')\" />" + Environment.NewLine +
            "      <ResolvedFileToPublish Include=\"@(_DatadogCallTargetAotRewrittenPublishFile)\">" + Environment.NewLine +
            "        <RelativePath>%(_DatadogCallTargetAotRewrittenPublishFile.RelativePath)</RelativePath>" + Environment.NewLine +
            "        <CopyToPublishDirectory>%(_DatadogCallTargetAotRewrittenPublishFile.CopyToPublishDirectory)</CopyToPublishDirectory>" + Environment.NewLine +
            "        <TargetPath>%(_DatadogCallTargetAotRewrittenPublishFile.TargetPath)</TargetPath>" + Environment.NewLine +
            "        <AssetType>%(_DatadogCallTargetAotRewrittenPublishFile.AssetType)</AssetType>" + Environment.NewLine +
            "      </ResolvedFileToPublish>" + Environment.NewLine +
            "    </ItemGroup>" + Environment.NewLine +
            "  </Target>" + Environment.NewLine +
            "</Project>" + Environment.NewLine;
        File.WriteAllText(artifactPaths.TargetsPath, content);
    }

    /// <summary>
    /// Ensures the parent directory for an artifact path exists before the artifact is written.
    /// </summary>
    /// <param name="path">The artifact path whose parent directory should exist.</param>
    private static void EnsureParentDirectoryExists(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory!);
        }
    }

    /// <summary>
    /// Escapes an XML value written into the generated linker descriptor.
    /// </summary>
    /// <param name="value">The value to escape.</param>
    /// <returns>The escaped XML value.</returns>
    private static string EscapeXml(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
    }

    /// <summary>
    /// Escapes MSBuild property tokens in literal paths so the generated files keep their original meaning.
    /// </summary>
    /// <param name="value">The value to escape for MSBuild file emission.</param>
    /// <returns>The escaped path value.</returns>
    private static string EscapeMsBuildPath(string value)
    {
        return value.Replace("$", "$$", StringComparison.Ordinal);
    }
}
