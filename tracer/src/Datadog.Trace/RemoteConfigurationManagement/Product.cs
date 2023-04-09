// <copyright file="Product.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Logging;

namespace Datadog.Trace.RemoteConfigurationManagement
{
    internal abstract class Product
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<Product>();

        protected Product()
        {
            AppliedConfigurations = new Dictionary<string, RemoteConfigurationCache>();
        }

        public event EventHandler<ProductConfigChangedEventArgs> ConfigChanged;

        public event EventHandler<ProductConfigChangedEventArgs> ConfigRemoved;

        public abstract string Name { get; }

        public Dictionary<string, RemoteConfigurationCache> AppliedConfigurations { get; }

        public void AssignConfigs(List<RemoteConfiguration> changedConfigs)
        {
            List<NamedRawFile> filteredConfigs = null;

            foreach (var config in changedConfigs)
            {
                var remoteConfigurationCache = new RemoteConfigurationCache(config.Path, config.Length, config.Hashes, config.Version);
                AppliedConfigurations[remoteConfigurationCache.Path.Path] = remoteConfigurationCache;

                filteredConfigs ??= new List<NamedRawFile>();
                filteredConfigs.Add(new NamedRawFile(config.Path, config.Contents));
            }

            Log.Debug<int, string, int>(
                "Received {ConfigsAmount} Remote Configuration records for product {Name}, " +
                      "of which {FilteredAmount} matched the predicate.",
                changedConfigs.Count,
                Name,
                filteredConfigs?.Count ?? 0);

            if (filteredConfigs is not null)
            {
                var e = new ProductConfigChangedEventArgs(filteredConfigs);
                try
                {
                    ConfigChanged?.Invoke(this, e);
                }
                catch (Exception ex)
                {
                    foreach (var item in filteredConfigs)
                    {
                        Log.Debug(ex, "Failed to apply Remote Configuration record {RecordName} for product {ProductName}", item.Path, Name);
                        e.Error(item.Path.Path, ex.Message);
                    }
                }

                var results = e.GetResults();
                foreach (var result in results)
                {
                    switch (result.ApplyState)
                    {
                        case ApplyStates.UNACKNOWLEDGED:
                            // Do nothing
                            break;
                        case ApplyStates.ACKNOWLEDGED:
                            AppliedConfigurations[result.Filename].Applied();
                            break;
                        case ApplyStates.ERROR:
                            AppliedConfigurations[result.Filename].ErrorOccured(result.Error);
                            break;
                        default:
                            Log.Warning("Unexpected ApplyState: {ApplyState}", result.ApplyState);
                            break;
                    }
                }
            }
        }

        public void RemoveConfigs(List<RemoteConfigurationCache> removedConfigs)
        {
            var e = new ProductConfigChangedEventArgs(removedConfigs.Select(cache => new NamedRawFile(cache.Path, Array.Empty<byte>())));
            ConfigRemoved?.Invoke(this, e);
        }
    }
}
