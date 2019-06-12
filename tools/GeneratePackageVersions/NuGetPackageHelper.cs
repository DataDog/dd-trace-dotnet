using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace GeneratePackageVersions
{
    public class NuGetPackageHelper
    {
        public static async Task<IEnumerable<string>> GetNugetPackageVersions(PackageVersionEntry entry)
        {
            var packageSource = new PackageSource("https://api.nuget.org/v3/index.json");

            var providers = new List<Lazy<INuGetResourceProvider>>();
            providers.AddRange(Repository.Provider.GetCoreV3()); // Add v3 API support
            // providers.AddRange(Repository.Provider.GetCoreV2()); // Add v2 API support

            var sourceRepository = new SourceRepository(packageSource, providers);
            var packageMetadataResource = await sourceRepository.GetResourceAsync<PackageMetadataResource>();
            var downloadResource = await sourceRepository.GetResourceAsync<DownloadResource>();

            var sourceCacheContext = new SourceCacheContext();
            var logger = new Logger();

            var searchMetadata = await packageMetadataResource.GetMetadataAsync(
                                                                     entry.NugetPackageSearchName,
                                                                     includePrerelease: false,
                                                                     includeUnlisted: true,
                                                                     sourceCacheContext,
                                                                     logger,
                                                                     CancellationToken.None);

            SemanticVersion minSemanticVersion, maxSemanticVersionExclusive;

            if (!SemanticVersion.TryParse(entry.MinVersion, out minSemanticVersion))
            {
                throw new ArgumentException($"MinVersion {entry.MinVersion} in integration {entry.IntegrationName} could not be parsed into a NuGet Semantic Version");
            }

            if (!SemanticVersion.TryParse(entry.MaxVersionExclusive, out maxSemanticVersionExclusive))
            {
                throw new ArgumentException($"MaxVersion {entry.MaxVersionExclusive} in integration {entry.IntegrationName} could not be parsed into a NuGet Semantic Version");
            }

            List<string> packageVersions = new List<string>();
            foreach (var md in searchMetadata)
            {
                if (md.Identity.HasVersion && md.Identity.Version.CompareTo(minSemanticVersion) >= 0 && md.Identity.Version.CompareTo(maxSemanticVersionExclusive) < 0)
                {
                    packageVersions.Add(md.Identity.Version.ToNormalizedString());
                }
            }

            return packageVersions;
        }

        public class Logger : ILogger
        {
            public void LogDebug(string data)
            {
                Log(LogLevel.Debug, data);
            }

            public void LogVerbose(string data)
            {
                Log(LogLevel.Verbose, data);
            }

            public void LogInformation(string data)
            {
                Log(LogLevel.Information, data);
            }

            public void LogMinimal(string data)
            {
                Log(LogLevel.Minimal, data);
            }

            public void LogWarning(string data)
            {
                Log(LogLevel.Warning, data);
            }

            public void LogError(string data)
            {
                Log(LogLevel.Error, data);
            }

            public void LogInformationSummary(string data)
            {
                Console.WriteLine($"[Summary] {data}");
            }

            public void Log(LogLevel level, string data)
            {
                Console.WriteLine($"[{level}] {data}");
            }

            public async Task LogAsync(LogLevel level, string data)
            {
                await Task.Run(() => Log(level, data))
                          .ConfigureAwait(false);
            }

            public void Log(ILogMessage message)
            {
                Log(message.Level, message.Message);
            }

            public async Task LogAsync(ILogMessage message)
            {
                await Task.Run(() => Log(message.Level, message.Message))
                          .ConfigureAwait(false);
            }
        }
    }
}
