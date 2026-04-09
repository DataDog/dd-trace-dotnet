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
/// Reads pinned Docker image digests from the smoke-test-images docker-compose file.
/// This file is maintained by Dependabot and maps each repo:tag to a specific digest.
/// </summary>
public static class SmokeTestImageDigests
{
    static readonly Lazy<Dictionary<string, string>> LazyDigests = new(LoadDigests);

    /// <summary>
    /// Returns the full image reference including the pinned digest for the given repo:tag.
    /// For example, given "mcr.microsoft.com/dotnet/aspnet:9.0-noble", returns
    /// "mcr.microsoft.com/dotnet/aspnet:9.0-noble@sha256:abc123...".
    /// Throws if the image is not found in the docker-compose file.
    /// </summary>
    public static string GetImageWithDigest(string repoTag)
    {
        var digests = LazyDigests.Value;
        if (digests.TryGetValue(repoTag, out var imageWithDigest))
        {
            return imageWithDigest;
        }

        var available = string.Join(Environment.NewLine, digests.Keys.OrderBy(k => k));
        throw new InvalidOperationException(
            $"Image '{repoTag}' not found in smoke-test-images.docker-compose.yml. "
          + $"Add an entry for this image to the docker-compose file and resolve its digest. "
          + $"Available images:{Environment.NewLine}{available}");
    }

    /// <summary>
    /// Validates that every unique runtime image in the given scenarios has a corresponding
    /// digest entry. Throws if images are missing
    /// </summary>
    public static void VerifyAllImagesAreTracked(IEnumerable<SmokeTestScenario> scenarios)
    {
        var digests = LazyDigests.Value;
        var missing = scenarios
            .Select(s => s.RuntimeImage)
            .Distinct(StringComparer.Ordinal)
            .Where(img => !digests.ContainsKey(img))
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

        Logger.Information("All {Count} smoke test images have pinned digests", digests.Keys.Count);
    }

    static Dictionary<string, string> LoadDigests()
    {
        // Walk up from the executing assembly to find the SmokeTests directory
        var smokeTestsDir = FindSmokeTestsDirectory();
        var filePath = Path.Combine(smokeTestsDir, "smoke-test-images.docker-compose.yml");

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                $"Smoke test image digest file not found at '{filePath}'. "
              + "This file is required for pinning Docker images by digest.",
                filePath);
        }

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
                // No digest - store as-is (maps to itself)
                const string message = "Missing sha256 digest for service {ServiceName} ({Image})"
                                       + "Add entries for these images to smoke-test-images.docker-compose.yml, and resolve their digests with:\n"
                                       + "  docker buildx imagetools inspect <image> --format '{{json .Manifest.Digest}}'";
                Logger.Warning(message, serviceName, image);
                result[image] = image;
                continue;
            }

            var repoTag = image[..atIndex];
            result[repoTag] = image;
        }

        return result;
    }

    static string FindSmokeTestsDirectory()
    {
        // Try relative to the current directory first (typical for Nuke builds)
        var candidates = new[]
        {
            Path.Combine(Environment.CurrentDirectory, "tracer", "build", "_build", "SmokeTests"),
            Path.Combine(Environment.CurrentDirectory, "build", "_build", "SmokeTests"),
            Path.GetDirectoryName(typeof(SmokeTestImageDigests).Assembly.Location) ?? "",
        };

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate) &&
                File.Exists(Path.Combine(candidate, "smoke-test-images.docker-compose.yml")))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException(
            "Could not locate the SmokeTests directory containing smoke-test-images.docker-compose.yml. "
          + $"Searched: {string.Join(", ", candidates)}");
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
