// <copyright file="Product.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.RemoteConfigurationManagement;

internal class Product
{
    public Product(string name)
    {
        Name = name;
        Configs = new List<RcmConfig>();
    }

    public event EventHandler<ProductConfigChangedEventArgs> ConfigChanged;

    public string Name { get; }

    public IReadOnlyList<RcmConfig> Configs { get; private set; }

    public void AssignConfigs(List<RcmConfig> newConfigs)
    {
        ConfigChanged?.Invoke(this, new ProductConfigChangedEventArgs(Name, newConfigs));
    }
}
