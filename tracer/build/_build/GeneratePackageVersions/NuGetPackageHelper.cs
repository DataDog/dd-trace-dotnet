// <copyright file="NuGetPackageHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace GeneratePackageVersions
{
    public class NuGetPackageHelper
    {
        /// <summary>
        /// Returns all available versions for a package with their publish dates, unfiltered by version range.
        /// Suitable for caching since the result is independent of any specific entry's version bounds.
        /// </summary>
        public static async Task<List<VersionWithDate>> GetAllNugetPackageVersions(string packageName)
        {
            var searchMetadata = await GetPackageMetadatas(packageName);

            var packageVersions = new List<VersionWithDate>();
            foreach (var md in searchMetadata)
            {
                if (md.Identity.HasVersion)
                {
                    packageVersions.Add(new VersionWithDate(
                        md.Identity.Version.ToNormalizedString(),
                        md.Published));
                }
            }

            return packageVersions;
        }

        /// <summary>
        /// Filters a list of versions to only those within the entry's [MinVersion, MaxVersionExclusive) range.
        /// Preserves publish date metadata through the pipeline.
        /// </summary>
        public static List<VersionWithDate> FilterVersions(IEnumerable<VersionWithDate> allVersions, IPackageVersionEntry entry)
        {
            if (!NuGetVersion.TryParse(entry.MinVersion, out var minVersion))
            {
                throw new ArgumentException($"MinVersion {entry.MinVersion} in integration {entry.IntegrationName} could not be parsed into a NuGet Version");
            }

            if (!NuGetVersion.TryParse(entry.MaxVersionExclusive, out var maxVersionExclusive))
            {
                throw new ArgumentException($"MaxVersion {entry.MaxVersionExclusive} in integration {entry.IntegrationName} could not be parsed into a NuGet Version");
            }

            var result = new List<VersionWithDate>();
            foreach (var item in allVersions)
            {
                if (NuGetVersion.TryParse(item.Version, out var version)
                    && version.CompareTo(minVersion) >= 0
                    && version.CompareTo(maxVersionExclusive) < 0)
                {
                    result.Add(item);
                }
            }

            return result;
        }

        public static async Task<IEnumerable<IPackageSearchMetadata>> GetPackageMetadatas(string packageName)
        {
            var packageSource = new PackageSource("https://api.nuget.org/v3/index.json");

            var providers = new List<Lazy<INuGetResourceProvider>>();
            providers.AddRange(Repository.Provider.GetCoreV3()); // Add v3 API support
            // providers.AddRange(Repository.Provider.GetCoreV2()); // Add v2 API support

            var sourceRepository = new SourceRepository(packageSource, providers);
            var packageMetadataResource = await sourceRepository.GetResourceAsync<PackageMetadataResource>();

            var sourceCacheContext = new SourceCacheContext();
            var logger = new Logger();

            var searchMetadata = await packageMetadataResource.GetMetadataAsync(
                                                                     packageName,
                                                                     includePrerelease: false,
                                                                     includeUnlisted: true,
                                                                     sourceCacheContext,
                                                                     logger,
                                                                     CancellationToken.None);
            return searchMetadata;
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
