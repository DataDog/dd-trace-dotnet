// <copyright file="SourceLinkInformationExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Datadog.Trace.Logging;
using Datadog.Trace.Pdb.SourceLink;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

#nullable enable

namespace Datadog.Trace.Pdb;

internal static class SourceLinkInformationExtractor
{
    private static IDatadogLogger Log { get; } = DatadogLogging.GetLoggerFor(typeof(SourceLinkInformationExtractor));

    public static bool TryGetSourceLinkInfo(Assembly assembly, [NotNullWhen(true)] out string? commitSha, [NotNullWhen(true)] out string? repositoryUrl)
    {
        // Extracting the SourceLink information from the assembly attributes will only work if:
        // 1. The assembly was built using the .NET Core SDK 2.1.300 or newer or MSBuild 15.7 or newer.
        // 2. The assembly was built using an SDK-Style project file (i.e. <Project Sdk="Microsoft.NET.Sdk">).
        // If these conditions weren't met, the attributes won't be there, so we'll need to extract the information from the PDB file.

        return TryExtractFromAssemblyAttributes(assembly, out commitSha, out repositoryUrl) ||
               TryExtractFromPdb(assembly, out commitSha, out repositoryUrl);
    }

    private static bool TryExtractFromPdb(Assembly assembly, [NotNullWhen(true)] out string? commitSha, [NotNullWhen(true)] out string? repositoryUrl)
    {
        commitSha = null;
        repositoryUrl = null;

        try
        {
            var pdbReader = DatadogMetadataReader.CreatePdbReader(assembly);
            if (pdbReader is not { IsPdbExist: true })
            {
                Log.Information("PDB file for assembly {AssemblyFullPath} could not be found", assembly.Location);
                return false;
            }

            var sourceLinkJsonDocument = pdbReader.GetSourceLinkJsonDocument();
            if (sourceLinkJsonDocument == null)
            {
                Log.Information("PDB file {PdbFullPath} does not contain SourceLink information", pdbReader.PdbFullPath);
                return false;
            }

            if (!TryExtractSourceLinkMappingUrl(sourceLinkJsonDocument, pdbReader?.PdbFullPath, out var sourceLinkMappedUri))
            {
                return false;
            }

            return CompositeSourceLinkUrlParser.Instance.TryParseSourceLinkUrl(sourceLinkMappedUri, out commitSha, out repositoryUrl);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while trying to extract SourceLink information from PDB file for assembly {AssemblyFullName}", assembly.FullName);
        }

        return false;
    }

    // Grab the SourceLink mapping URL. For example,
    // From:
    //       {"documents":{"C:\\dev\\dd-trace-dotnet\\*":"https://raw.githubusercontent.com/DataDog/dd-trace-dotnet/dd35903c688a74b62d1c6a9e4f41371c65704db8/*"}}
    // Extract:
    //       https://raw.githubusercontent.com/DataDog/dd-trace-dotnet/dd35903c688a74b62d1c6a9e4f41371c65704db8/*
    private static bool TryExtractSourceLinkMappingUrl(string sourceLinkJsonDocument, string? pdbFullPath, [NotNullWhen(true)] out Uri? sourceLinkMappedUri)
    {
        sourceLinkMappedUri = null;
        pdbFullPath ??= "Unknown";

        try
        {
            var sourceLinkMappedUrl = JObject.Parse(sourceLinkJsonDocument).SelectTokens("$.documents.*").FirstOrDefault()?.ToString();
            if (string.IsNullOrWhiteSpace(sourceLinkMappedUrl) || !Uri.TryCreate(sourceLinkMappedUrl, UriKind.Absolute, out sourceLinkMappedUri))
            {
                Log.Information("PDB file {PdbFullPath} contained SourceLink information, but we failed to parse it.", pdbFullPath);
                return false;
            }

            Log.Information("PDB file {PdbFullPath} contained SourceLink information, and we successfully parsed it. The mapping uri is {SourceLinkMappedUri}.", pdbFullPath, sourceLinkMappedUri);
            return true;
        }
        catch (Exception e)
        {
            Log.Warning(e, "PDB file {PdbFullPath} contained SourceLink document {Document}, but we failed to parse it.", pdbFullPath, sourceLinkJsonDocument);
        }

        return false;
    }

    /// <summary>
    /// Extract the SourceLink information from the assembly attributes "AssemblyMetadataAttribute" and "AssemblyInformationalVersionAttribute".
    /// </summary>
    private static bool TryExtractFromAssemblyAttributes(Assembly assembly, [NotNullWhen(true)] out string? commitSha, [NotNullWhen(true)] out string? repositoryUrl)
    {
        commitSha = null;
        repositoryUrl = null;

        try
        {
            foreach (var attribute in assembly.GetCustomAttributes())
            {
                switch (attribute)
                {
                    case AssemblyMetadataAttribute { Key: "RepositoryUrl" } amAttr:
                        repositoryUrl = amAttr.Value;
                        break;
                    case AssemblyInformationalVersionAttribute { InformationalVersion: { } informationalVersion }:
                    {
                        var parts = informationalVersion.Split('+');
                        if (parts.Length == 2)
                        {
                            commitSha = parts[1];
                        }

                        break;
                    }
                }

                if (repositoryUrl != null && commitSha != null)
                {
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error trying to extract SourceLink information from assembly attributes");
        }

        return false;
    }
}
