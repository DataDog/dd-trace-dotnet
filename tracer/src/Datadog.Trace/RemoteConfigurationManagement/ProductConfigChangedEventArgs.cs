// <copyright file="ProductConfigChangedEventArgs.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.RemoteConfigurationManagement
{
    internal class ProductConfigChangedEventArgs : EventArgs
    {
        private readonly IEnumerable<RemoteConfiguration> _configContents;

        public ProductConfigChangedEventArgs(IEnumerable<RemoteConfiguration> configContents)
        {
            _configContents = configContents;
        }

        public IEnumerable<T> GetDeserializedConfigurations<T>()
        {
            foreach (var configContent in _configContents)
            {
                using var stream = new MemoryStream(configContent.Contents);
                using var streamReader = new StreamReader(stream);
                using var jsonReader = new JsonTextReader(streamReader);
                yield return JsonSerializer.CreateDefault().Deserialize<T>(jsonReader);
            }
        }

        public IDictionary<string, T> GetDeserializedConfigurationsByPath<T>()
        {
            var dic = new Dictionary<string, T>(_configContents.Count());
            foreach (var configContent in _configContents)
            {
                using var stream = new MemoryStream(configContent.Contents);
                using var streamReader = new StreamReader(stream);
                using var jsonReader = new JsonTextReader(streamReader);
                dic.Add(configContent.Path.Path, JsonSerializer.CreateDefault().Deserialize<T>(jsonReader));
                stream.Seek(0, SeekOrigin.Begin);
            }

            return dic;
        }
    }
}
