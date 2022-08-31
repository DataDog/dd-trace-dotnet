// <copyright file="ProductConfigChangedEventArgs.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.RemoteConfigurationManagement
{
    internal class ProductConfigChangedEventArgs : EventArgs
    {
        private readonly IEnumerable<NamedRawFile> _configContents;

        private Dictionary<string, ApplyDetails> applyStates = new();

        public ProductConfigChangedEventArgs(IEnumerable<NamedRawFile> configContents)
        {
            _configContents = configContents;
        }

        public IEnumerable<NamedTypedFile<T>> GetDeserializedConfigurations<T>()
        {
            foreach (var configContent in _configContents)
            {
                using var stream = new MemoryStream(configContent);
                using var streamReader = new StreamReader(stream);
                using var jsonReader = new JsonTextReader(streamReader);
                yield return new NamedTypedFile<T>(c.Name, JsonSerializer.CreateDefault().Deserialize<T>(jsonReader));
            }
        }

        public void Acknowldge(string filename)
        {
            var applyDetails = GetOrCreateApplyDetails(filename);

            // can only move from unack to ack
            if (applyDetails.ApplyState == ApplyState.UNACKNOWLEDGED)
            {
                applyDetails.ApplyState = ApplyState.ACKNOWLEDGED;
            }
        }

        public void Error(string filename, string error)
        {
            var applyDetails = GetOrCreateApplyDetails(filename);
            applyDetails.ApplyState = ApplyState.ERROR;
            applyDetails.Error = error;
        }

        public IEnumerable<ApplyDetails> GetResults()
        {
            return applyStates.Values;
        }

        private ApplyDetails GetOrCreateApplyDetails(string filename)
        {
            ApplyDetails applyDetails = null;
            if (!applyStates.TryGetValue(filename, out applyDetails))
            {
                applyDetails = new ApplyDetails() { Filename = filename };
                applyStates.Add(filename, applyDetails);
            }

            return applyDetails;
        }
    }
}
