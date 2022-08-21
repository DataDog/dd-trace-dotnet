// <copyright file="Product.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;

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
            var configurations =
                changedConfigs
                   .Where(RemoteConfigurationPredicate)
                   .Select(configuration => configuration.Contents)
                   .ToList()
                ;

            if (configurations.Any())
            {
                ConfigChanged?.Invoke(this, new ProductConfigChangedEventArgs(configurations));
            }
        }

        protected virtual bool RemoteConfigurationPredicate(RemoteConfiguration configuration)
        {
            return true;
        }
    }
}
