// <copyright file="ProductConfigChangedEventArgs.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Datadog.Trace.Logging;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
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
            GetOrCreateApplyDetails(filename, applyDetails =>
            {
                // can only move from unack to ack
                if (applyDetails.ApplyState == ApplyStates.UNACKNOWLEDGED)
                {
                    applyDetails.ApplyState = ApplyStates.ACKNOWLEDGED;
                }

                return applyDetails;
            });
        }

        public void Error(string filename, string error)
        {
            GetOrCreateApplyDetails(filename, applyDetails =>
            {
                applyDetails.ApplyState = ApplyStates.ERROR;
                applyDetails.Error = error;

                return applyDetails;
            });
        }

        public IEnumerable<ApplyDetails> GetResults()
        {
            return applyStates.Values;
        }

        private void GetOrCreateApplyDetails(string filename, Func<ApplyDetails, ApplyDetails> update)
        {
            ApplyDetails applyDetails = default;
            if (!applyStates.TryGetValue(filename, out applyDetails))
            {
                applyDetails = new ApplyDetails() { Filename = filename };
            }

            applyDetails = update(applyDetails);

            applyStates[filename] = applyDetails;
        }
    }
}
