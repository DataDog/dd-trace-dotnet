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

        #pragma warning disable CS0067
        public event EventHandler<ProductConfigChangedEventArgs> ConfigChanged;

        public event EventHandler<ProductConfigChangedEventArgs> ConfigRemoved;

        public abstract string Name { get; }

        public void RemoveConfigs(List<RemoteConfigurationCache> removedConfigs)
        {
            var e = new ProductConfigChangedEventArgs(removedConfigs.Select(cache => new NamedRawFile(cache.Path, Array.Empty<byte>())));
            ConfigRemoved?.Invoke(this, e);
        }
    }
}
