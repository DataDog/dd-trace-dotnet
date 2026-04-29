#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nuke.Common.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Logger = Serilog.Log;

namespace SmokeTests;

/// <summary>
/// Reads pinned Docker image digests from a smoke-test-images docker-compose file.
/// This file is maintained by Dependabot and maps each repo:tag to a specific digest.
/// </summary>
public class SmokeTestImageDigests
{
    readonly Dictionary<string, string> Digests;

    public SmokeTestImageDigests(AbsolutePath tracerDirectory)
    {
        var imageDigestsFile = tracerDirectory / "build" / "_build" / "SmokeTests" / "smoke-test-images.docker-compose.yml";
        if (!File.Exists(imageDigestsFile))
        {
            throw new FileNotFoundException(
                $"Smoke test image digest file not found at '{imageDigestsFile}'. "
              + "This file is required for pinning Docker images by digest.",
                imageDigestsFile);
        }

        Digests = LoadDigests(imageDigestsFile);
    }

    /// <summary>
    /// Returns the full image reference including the pinned digest for the given repo:tag.
    /// For example, given "mcr.microsoft.com/dotnet/aspnet:9.0-noble", returns
    /// "mcr.microsoft.com/dotnet/aspnet:9.0-noble@sha256:abc123...".
    /// Throws if the image is not found in the docker-compose file.
    /// </summary>
    public string GetImageWithDigest(string repoTag)
    {
        if (Digests.TryGetValue(repoTag, out var imageWithDigest))
        {
            return imageWithDigest;
        }

        var available = string.Join(Environment.NewLine, Digests.Keys.OrderBy(k => k));
        throw new InvalidOperationException(
            $"Image '{repoTag}' not found in smoke-test-images.docker-compose.yml. "
          + $"Add an entry for this image to the docker-compose file and resolve its digest. "
          + $"Available images:{Environment.NewLine}{available}");
    }

    /// <summary>
    /// Validates that every unique runtime image in the given scenarios has a corresponding
    /// digest entry. Throws if images are missing.
    /// </summary>
    public void VerifyAllImagesAreTracked(IEnumerable<SmokeTestScenario> scenarios)
    {
        var missing = scenarios
            .Select(s => s.RuntimeImage)
            .Distinct(StringComparer.Ordinal)
            .Where(img => !Digests.ContainsKey(img))
            .OrderBy(img => img)
            .ToList();

        if (missing.Count > 0)
        {
            var missingList = string.Join(Environment.NewLine, missing.Select(m => $"  - {m}"));
            throw new InvalidOperationException(
                $"The following smoke test images are missing from smoke-test-images.docker-compose.yml:{Environment.NewLine}"
                + $"{missingList}{Environment.NewLine}"
                + $"Add entries for these images to smoke-test-images.docker-compose.yml, and resolve their digests with:{Environment.NewLine}"
                + "  docker buildx imagetools inspect <image> --format '{{json .Manifest.Digest}}'");
        }

        Logger.Information("All {Count} smoke test images have pinned digests", Digests.Keys.Count);
    }

    static Dictionary<string, string> LoadDigests(string filePath)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        using var reader = new StreamReader(filePath);
        var compose = deserializer.Deserialize<DockerComposeFile?>(reader);

        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        if (compose?.Services is null)
        {
            return result;
        }

        foreach (var (serviceName, details) in compose.Services)
        {
            if (string.IsNullOrEmpty(details.Image))
            {
                Logger.Warning("Missing image details for service {ServiceName}", serviceName);
                continue;
            }

            var image = details.Image;

            // Extract the repo:tag portion (before @sha256:)
            var atIndex = image.IndexOf('@');
            if (atIndex < 0)
            {
                const string message = "Missing sha256 digest for service {ServiceName} ({Image}). "
                                     + "Add entries for these images to smoke-test-images.docker-compose.yml, and resolve their digests with:\n"
                                     + "  docker buildx imagetools inspect <image> --format '{{json .Manifest.Digest}}'";
                Logger.Warning(message, serviceName, image);
                continue;
            }

            var repoTag = image[..atIndex];
            result[repoTag] = image;
        }

        return result;
    }

    // YAML model for docker-compose file
    class DockerComposeFile
    {
        public Dictionary<string, DockerComposeService>? Services { get; set; }
    }

    class DockerComposeService
    {
        public string? Image { get; set; }
    }
}
