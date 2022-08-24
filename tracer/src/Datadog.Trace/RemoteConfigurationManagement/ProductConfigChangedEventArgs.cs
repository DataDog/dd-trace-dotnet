// <copyright file="ProductConfigChangedEventArgs.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.RemoteConfigurationManagement
{
    internal class ProductConfigChangedEventArgs : EventArgs
    {
        private readonly IEnumerable<byte[]> _configContents;

        public ProductConfigChangedEventArgs(IEnumerable<byte[]> configContents)
        {
            _configContents = configContents;
        }

        public IEnumerable<T> GetDeserializedConfigurations<T>()
        {
            return
                _configContents
                   .Select(bytes => JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(bytes)));
        }
    }
}
