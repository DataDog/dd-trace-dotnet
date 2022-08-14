// <copyright file="Product.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;

namespace Datadog.Trace.RemoteConfigurationManagement
{
    internal class Product
    {
        public Product(string name)
        {
            Name = name;
            AppliedConfigurations = new Dictionary<string, RemoteConfigurationCache>();
        }

        public event EventHandler<ProductConfigChangedEventArgs> ConfigChanged;

        public string Name { get; }

        public Dictionary<string, RemoteConfigurationCache> AppliedConfigurations { get; }

        public void AssignConfigs(List<RemoteConfiguration> changedConfigs)
        {
            ConfigChanged?.Invoke(this, new ProductConfigChangedEventArgs(Name, changedConfigs));
        }
    }
}
