#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Nuke.Common.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Logger = Serilog.Log;

namespace SmokeTests;

/// <summary>
/// Queries each registry for the current digest of every pinned tag in
/// smoke-test-images.docker-compose.yml, applies a cooldown based on the image's
/// config.created timestamp, and rewrites the compose file with any updates.
///
/// This exists because Dependabot's docker_registry2 cooldown check performs
/// HEAD /v2/&lt;repo&gt;/blobs/&lt;manifest-digest&gt;, which 404s on MCR. Owning the
/// update flow avoids that bug while preserving the mandatory cooldown.
/// </summary>
public class SmokeTestImageDigestUpdater : IDisposable
{
    static readonly string[] AcceptedManifestMediaTypes =
    {
        "application/vnd.docker.distribution.manifest.v2+json",
        "application/vnd.docker.distribution.manifest.list.v2+json",
        "application/vnd.oci.image.manifest.v1+json",
        "application/vnd.oci.image.index.v1+json",
    };

    readonly TimeSpan _cooldown;
    readonly RegistryClient _registry;

    public SmokeTestImageDigestUpdater(TimeSpan cooldown)
    {
        _cooldown = cooldown;
        _registry = new RegistryClient();
        CooldownReport = new CooldownReport(cooldown);
    }

    /// <summary>
    /// Collected entries for images whose latest digest fell within the cooldown window.
    /// Populated by <see cref="UpdateAsync"/>; intended to be rendered into a markdown
    /// report and attached to the automation PR.
    /// </summary>
    public CooldownReport CooldownReport { get; }

    public async Task UpdateAsync(AbsolutePath composeFile, AbsolutePath? reportPath = null)
    {
        Helpers.LogSection($"Updating pinned digests in {composeFile}");

        if (!File.Exists(composeFile))
        {
            throw new FileNotFoundException($"Compose file not found: {composeFile}", composeFile);
        }

        var entries = ReadEntries(composeFile);
        Logger.Information("Found {Count} pinned images to evaluate", entries.Count);

        var updates = new Dictionary<string, string>(StringComparer.Ordinal);
        int updated = 0, cooled = 0, unchanged = 0, failed = 0;

        foreach (var entry in entries)
        {
            try
            {
                var outcome = await EvaluateAsync(entry);
                switch (outcome.Kind)
                {
                    case UpdateKind.Unchanged:
                        unchanged++;
                        break;
                    case UpdateKind.Cooldown:
                        cooled++;
                        break;
                    case UpdateKind.Update:
                        updated++;
                        updates[entry.ImagePrefix] = outcome.NewDigest!;
                        break;
                }
            }
            catch (Exception ex)
            {
                failed++;
                Logger.Warning(ex, "Failed to evaluate {Image}", entry.ImagePrefix);
                CooldownReport.AddFailure(new CooldownReport.FailureEntry(entry.ImagePrefix, ex.Message));
            }
        }

        if (updates.Count > 0)
        {
            RewriteComposeFile(composeFile, updates);
            Logger.Information("Rewrote {File} with {Count} digest update(s)", composeFile, updates.Count);
        }
        else
        {
            Logger.Information("No digest updates to apply");
        }

        Logger.Information(
            "Summary - updated: {Updated}, cooldown: {Cooldown}, unchanged: {Unchanged}, failed: {Failed}",
            updated, cooled, unchanged, failed);

        // Save the report only when there's an update worth opening a PR for —
        // the report is PR-body fodder, not a standalone artifact.
        if (updated > 0 && reportPath is not null && CooldownReport.HasEntries)
        {
            await CooldownReport.SaveToFile(reportPath);
            Logger.Information("Image digest report saved to {Path}", reportPath);
        }

        // With no updates to ship, failures have no visible channel but the build log —
        // fail the target so the scheduled workflow's failure notification fires.
        if (updated == 0 && failed > 0)
        {
            throw new InvalidOperationException(
                $"{failed} image(s) failed to evaluate and no digest updates were produced; see warnings above for details.");
        }
    }

    async Task<UpdateOutcome> EvaluateAsync(ImageEntry entry)
    {
        Logger.Debug("Evaluating {Image}", entry.ImagePrefix);

        var latestDigest = await _registry.GetTagDigestAsync(entry.Registry, entry.Repository, entry.Tag, AcceptedManifestMediaTypes);
        if (string.IsNullOrEmpty(latestDigest))
        {
            throw new InvalidOperationException($"Registry did not return a Docker-Content-Digest for {entry.ImagePrefix}");
        }

        if (string.Equals(latestDigest, entry.Digest, StringComparison.OrdinalIgnoreCase))
        {
            Logger.Information("Unchanged {Image} (digest {Digest})", entry.ImagePrefix, Shorten(latestDigest));
            return UpdateOutcome.Unchanged;
        }

        var created = await GetImageCreatedAsync(entry.Registry, entry.Repository, latestDigest);
        if (created is null)
        {
            throw new InvalidOperationException($"Unable to determine created timestamp for {entry.ImagePrefix}@{latestDigest}");
        }

        var age = DateTime.UtcNow - created.Value;
        if (age < _cooldown)
        {
            Logger.Warning(
                "Cooldown {Image}: new digest {Digest} is only {AgeH}h old (< {CooldownH}h cooldown); keeping current pin {Current}",
                entry.ImagePrefix, Shorten(latestDigest), (int)age.TotalHours, (int)_cooldown.TotalHours, Shorten(entry.Digest));
            CooldownReport.Add(new CooldownReport.CooldownEntry(
                Image: entry.ImagePrefix,
                CurrentDigest: entry.Digest,
                AvailableDigest: latestDigest,
                PublishedDate: new DateTimeOffset(created.Value, TimeSpan.Zero)));
            return UpdateOutcome.Cooldown;
        }

        Logger.Information(
            "Updating {Image}: {Old} -> {New} (published {AgeDays}d ago)",
            entry.ImagePrefix, Shorten(entry.Digest), Shorten(latestDigest), (int)age.TotalDays);
        return UpdateOutcome.Update(latestDigest);
    }

    async Task<DateTime?> GetImageCreatedAsync(string registry, string repository, string digest)
    {
        using var manifestDoc = await _registry.GetManifestAsync(registry, repository, digest, AcceptedManifestMediaTypes);
        var root = manifestDoc.RootElement;

        // Manifest list / OCI image index: pick a concrete child (prefer linux/amd64, else first).
        if (root.TryGetProperty("manifests", out var children) && children.ValueKind == JsonValueKind.Array)
        {
            string? childDigest = null;
            foreach (var child in children.EnumerateArray())
            {
                if (!child.TryGetProperty("platform", out var platform)) continue;
                var os = platform.TryGetProperty("os", out var osProp) ? osProp.GetString() : null;
                var arch = platform.TryGetProperty("architecture", out var archProp) ? archProp.GetString() : null;
                if (os == "linux" && arch == "amd64")
                {
                    childDigest = child.TryGetProperty("digest", out var d) ? d.GetString() : null;
                    break;
                }
            }

            if (childDigest is null)
            {
                foreach (var child in children.EnumerateArray())
                {
                    if (child.TryGetProperty("digest", out var d))
                    {
                        childDigest = d.GetString();
                        if (!string.IsNullOrEmpty(childDigest)) break;
                    }
                }
            }

            if (string.IsNullOrEmpty(childDigest)) return null;
            return await GetImageCreatedAsync(registry, repository, childDigest!);
        }

        // Single-platform manifest: follow config blob for the 'created' timestamp.
        if (!root.TryGetProperty("config", out var config)) return null;
        if (!config.TryGetProperty("digest", out var configDigestEl)) return null;
        var configDigest = configDigestEl.GetString();
        if (string.IsNullOrEmpty(configDigest)) return null;

        using var blobDoc = await _registry.GetBlobAsync(registry, repository, configDigest!);
        if (!blobDoc.RootElement.TryGetProperty("created", out var createdProp)) return null;
        var createdStr = createdProp.GetString();
        if (string.IsNullOrEmpty(createdStr)) return null;

        return DateTime.Parse(
            createdStr!,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
    }

    static string Shorten(string digest)
    {
        var colon = digest.IndexOf(':');
        var hex = colon >= 0 ? digest[(colon + 1)..] : digest;
        return hex.Length > 12 ? hex.Substring(0, 12) : hex;
    }

    static List<ImageEntry> ReadEntries(string filePath)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        using var reader = new StreamReader(filePath);
        var compose = deserializer.Deserialize<DockerComposeFile?>(reader);

        var result = new List<ImageEntry>();
        if (compose?.Services is null) return result;

        foreach (var (serviceName, details) in compose.Services)
        {
            if (string.IsNullOrEmpty(details.Image)) continue;

            var entry = ImageEntry.TryParse(serviceName, details.Image!);
            if (entry is null)
            {
                Logger.Warning("Skipping malformed entry for service {Service}: {Image}", serviceName, details.Image);
                continue;
            }

            result.Add(entry);
        }

        return result;
    }

    static void RewriteComposeFile(string filePath, Dictionary<string, string> updatesByRepoTag)
    {
        // Regex-replace on full text preserves original line endings, comments, and formatting.
        var content = File.ReadAllText(filePath);
        foreach (var (repoTag, newDigest) in updatesByRepoTag)
        {
            var escaped = Regex.Escape(repoTag);
            var pattern = $@"(?<prefix>\s*image:\s*){escaped}@sha256:[a-f0-9]+";
            content = Regex.Replace(content, pattern, m => $"{m.Groups["prefix"].Value}{repoTag}@{newDigest}");
        }
        File.WriteAllText(filePath, content);
    }

    public void Dispose() => _registry.Dispose();

    class DockerComposeFile
    {
        public Dictionary<string, DockerComposeService>? Services { get; set; }
    }

    class DockerComposeService
    {
        public string? Image { get; set; }
    }

    class ImageEntry
    {
        ImageEntry(string service, string imagePrefix, string digest, string registry, string repository, string tag)
        {
            Service = service;
            ImagePrefix = imagePrefix;
            Digest = digest;
            Registry = registry;
            Repository = repository;
            Tag = tag;
        }

        public string Service { get; }
        // Literal "<registry>/<repo>:<tag>" portion (everything before '@') — used as the YAML rewrite key.
        public string ImagePrefix { get; }
        // Full digest, including "sha256:" prefix.
        public string Digest { get; }
        // API hostname to contact, e.g. "mcr.microsoft.com" or "registry-1.docker.io".
        public string Registry { get; }
        // Repository path for /v2/ URLs, e.g. "dotnet/aspnet" or "andrewlock/dotnet-centos".
        public string Repository { get; }
        public string Tag { get; }

        public static ImageEntry? TryParse(string service, string imageField)
        {
            var atIdx = imageField.IndexOf('@');
            if (atIdx <= 0) return null;
            var digest = imageField[(atIdx + 1)..];
            if (!digest.StartsWith("sha256:", StringComparison.Ordinal)) return null;

            var prefix = imageField[..atIdx];
            var colonIdx = prefix.LastIndexOf(':');
            var slashIdx = prefix.LastIndexOf('/');
            if (colonIdx < 0 || colonIdx < slashIdx) return null;

            var path = prefix[..colonIdx];
            var tag = prefix[(colonIdx + 1)..];

            string registry;
            string repository;
            var firstSlashIdx = path.IndexOf('/');
            if (firstSlashIdx > 0)
            {
                var firstSegment = path[..firstSlashIdx];
                if (firstSegment.Contains('.') || firstSegment.Contains(':') || firstSegment == "localhost")
                {
                    registry = firstSegment;
                    repository = path[(firstSlashIdx + 1)..];
                }
                else
                {
                    // Implicit Docker Hub repo like "andrewlock/dotnet-centos".
                    registry = "registry-1.docker.io";
                    repository = path;
                }
            }
            else
            {
                // Bare repo name defaults to Docker Hub's official-images namespace.
                registry = "registry-1.docker.io";
                repository = "library/" + path;
            }

            return new ImageEntry(service, prefix, digest, registry, repository, tag);
        }
    }

    enum UpdateKind
    {
        Unchanged,
        Cooldown,
        Update,
    }

    readonly struct UpdateOutcome
    {
        UpdateOutcome(UpdateKind kind, string? digest)
        {
            Kind = kind;
            NewDigest = digest;
        }

        public UpdateKind Kind { get; }
        public string? NewDigest { get; }

        public static readonly UpdateOutcome Unchanged = new(UpdateKind.Unchanged, null);
        public static readonly UpdateOutcome Cooldown = new(UpdateKind.Cooldown, null);
        public static UpdateOutcome Update(string digest) => new(UpdateKind.Update, digest);
    }

    /// <summary>
    /// Minimal Docker Registry v2 client with anonymous bearer-token challenge support.
    /// Supports MCR (typically open) and Docker Hub (requires anonymous pull token).
    /// </summary>
    sealed class RegistryClient : IDisposable
    {
        readonly HttpClient _client;
        readonly Dictionary<string, string> _tokenCache = new(StringComparer.Ordinal);

        public RegistryClient()
        {
            _client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30),
            };
            _client.DefaultRequestHeaders.UserAgent.ParseAdd("dd-trace-dotnet-smoke-test-updater/1.0");
        }

        public async Task<string?> GetTagDigestAsync(string registry, string repository, string tag, string[] acceptMediaTypes)
        {
            using var response = await SendWithAuthAsync(
                () => BuildRequest(HttpMethod.Head, registry, $"/v2/{repository}/manifests/{tag}", acceptMediaTypes),
                repository);
            response.EnsureSuccessStatusCode();

            if (response.Headers.TryGetValues("Docker-Content-Digest", out var values))
            {
                foreach (var v in values)
                {
                    if (!string.IsNullOrEmpty(v)) return v;
                }
            }
            return null;
        }

        public async Task<JsonDocument> GetManifestAsync(string registry, string repository, string reference, string[] acceptMediaTypes)
        {
            using var response = await SendWithAuthAsync(
                () => BuildRequest(HttpMethod.Get, registry, $"/v2/{repository}/manifests/{reference}", acceptMediaTypes),
                repository);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync();
            return await JsonDocument.ParseAsync(stream);
        }

        public async Task<JsonDocument> GetBlobAsync(string registry, string repository, string digest)
        {
            using var response = await SendWithAuthAsync(
                () => BuildRequest(HttpMethod.Get, registry, $"/v2/{repository}/blobs/{digest}", acceptMediaTypes: null),
                repository);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync();
            return await JsonDocument.ParseAsync(stream);
        }

        static HttpRequestMessage BuildRequest(HttpMethod method, string registry, string path, string[]? acceptMediaTypes)
        {
            var request = new HttpRequestMessage(method, $"https://{registry}{path}");
            if (acceptMediaTypes is not null)
            {
                foreach (var mt in acceptMediaTypes)
                {
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(mt));
                }
            }
            return request;
        }

        async Task<HttpResponseMessage> SendWithAuthAsync(Func<HttpRequestMessage> buildRequest, string repository)
        {
            var request = buildRequest();
            var host = request.RequestUri!.Host;
            var authKey = $"{host}::{repository}";

            if (_tokenCache.TryGetValue(authKey, out var cachedToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", cachedToken);
            }

            var response = await _client.SendAsync(request);
            request.Dispose();

            if (response.StatusCode != HttpStatusCode.Unauthorized)
            {
                return response;
            }

            var challenge = response.Headers.WwwAuthenticate
                .FirstOrDefault(h => string.Equals(h.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase));
            if (challenge is null || string.IsNullOrEmpty(challenge.Parameter))
            {
                return response;
            }

            response.Dispose();

            var parameters = ParseChallenge(challenge.Parameter!);
            if (!parameters.TryGetValue("realm", out var realm))
            {
                throw new InvalidOperationException($"Bearer challenge from {host} did not include a realm");
            }

            var scope = parameters.TryGetValue("scope", out var s) ? s : $"repository:{repository}:pull";
            var query = new List<string> { $"scope={Uri.EscapeDataString(scope)}" };
            if (parameters.TryGetValue("service", out var svc))
            {
                query.Add($"service={Uri.EscapeDataString(svc)}");
            }
            var tokenUrl = $"{realm}?{string.Join("&", query)}";

            using var tokenResponse = await _client.GetAsync(tokenUrl);
            tokenResponse.EnsureSuccessStatusCode();
            await using var tokenStream = await tokenResponse.Content.ReadAsStreamAsync();
            using var tokenDoc = await JsonDocument.ParseAsync(tokenStream);
            string? newToken = null;
            if (tokenDoc.RootElement.TryGetProperty("token", out var t))
            {
                newToken = t.GetString();
            }
            else if (tokenDoc.RootElement.TryGetProperty("access_token", out var at))
            {
                newToken = at.GetString();
            }
            if (string.IsNullOrEmpty(newToken))
            {
                throw new InvalidOperationException($"Auth endpoint {realm} did not return a token");
            }
            _tokenCache[authKey] = newToken!;

            var retry = buildRequest();
            retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newToken);
            var retryResponse = await _client.SendAsync(retry);
            retry.Dispose();
            return retryResponse;
        }

        static Dictionary<string, string> ParseChallenge(string parameter)
        {
            // Example: realm="https://auth.docker.io/token",service="registry.docker.io",scope="repository:foo:pull"
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var i = 0;
            while (i < parameter.Length)
            {
                while (i < parameter.Length && (char.IsWhiteSpace(parameter[i]) || parameter[i] == ',')) i++;
                if (i >= parameter.Length) break;

                var nameStart = i;
                while (i < parameter.Length && parameter[i] != '=') i++;
                if (i >= parameter.Length) break;
                var name = parameter[nameStart..i].Trim();
                i++; // skip '='

                string value;
                if (i < parameter.Length && parameter[i] == '"')
                {
                    i++;
                    var valStart = i;
                    while (i < parameter.Length && parameter[i] != '"') i++;
                    value = parameter[valStart..i];
                    if (i < parameter.Length) i++; // closing quote
                }
                else
                {
                    var valStart = i;
                    while (i < parameter.Length && parameter[i] != ',') i++;
                    value = parameter[valStart..i].Trim();
                }

                if (!string.IsNullOrEmpty(name))
                {
                    result[name] = value;
                }
            }
            return result;
        }

        public void Dispose() => _client.Dispose();
    }
}
