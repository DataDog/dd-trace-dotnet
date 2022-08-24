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
            List<byte[]> configurations = null;
            foreach(var config in changedConfigs)
            {
                if(RemoteConfigurationPredicate(config))
                {
                    configurations ??= new List<byte[]>();
                    configurations.Add(config.Contents);
                }
            }

            if (configurations is not null)
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
