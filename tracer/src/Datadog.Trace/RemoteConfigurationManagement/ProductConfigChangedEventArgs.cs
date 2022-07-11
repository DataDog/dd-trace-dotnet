// <copyright file="ProductConfigChangedEventArgs.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;

namespace Datadog.Trace.RemoteConfigurationManagement;

internal class ProductConfigChangedEventArgs : EventArgs
{
    public ProductConfigChangedEventArgs(string name, IReadOnlyList<RemoteConfiguration> newConfigs)
    {
        Name = name;
        NewConfigs = newConfigs;
    }

    public string Name { get; }

    public IReadOnlyList<RemoteConfiguration> NewConfigs { get; }
}
