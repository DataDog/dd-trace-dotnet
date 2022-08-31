// <copyright file="Product.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;

namespace Datadog.Trace.RemoteConfigurationManagement
{
    internal abstract class Product
    {
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
                        case ApplyState.ACKNOWLEDGED:
                            AppliedConfigurations[result.Filename].Applied();
                            break;
                        case ApplyState.ERROR:
                            AppliedConfigurations[result.Filename].ErrorOccured(result.Error);
                            break;
                        default:
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
