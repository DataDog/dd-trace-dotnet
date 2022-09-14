// <copyright file="Product.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Logging;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;

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

        public abstract string Name { get; }

        public Dictionary<string, RemoteConfigurationCache> AppliedConfigurations { get; }

        public void AssignConfigs(List<RemoteConfiguration> changedConfigs)
        {
            List<NamedRawFile> filteredConfigs = null;

            foreach (var config in changedConfigs)
            {
                var remoteConfigurationCache = new RemoteConfigurationCache(config.Path, config.Length, config.Hashes, config.Version);
                AppliedConfigurations[remoteConfigurationCache.Path.Path] = remoteConfigurationCache;

                if (RemoteConfigurationPredicate(config))
                {
                    filteredConfigs ??= new List<NamedRawFile>();
                    filteredConfigs.Add(new NamedRawFile(config.Path.Path, config.Contents));
                }
            }

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
                        e.Error(item.Name, ex.Message);
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

        protected virtual bool RemoteConfigurationPredicate(RemoteConfiguration configuration)
        {
            return true;
        }
    }
}
