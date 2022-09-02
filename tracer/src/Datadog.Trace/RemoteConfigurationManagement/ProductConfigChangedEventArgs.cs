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
            return
                _configContents
                   .Select(c => new NamedTypedFile<T>(c.Name, JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(c.RawFile))));
        }

        public void Acknowldge(string filename)
        {
            var applyDetails = GetOrCreateApplyDetails(filename);

            // can only move from unack to ack
            if (applyDetails.ApplyState == ApplyStates.UNACKNOWLEDGED)
            {
                applyDetails.ApplyState = ApplyStates.ACKNOWLEDGED;
            }
        }

        public void Error(string filename, string error)
        {
            var applyDetails = GetOrCreateApplyDetails(filename);
            applyDetails.ApplyState = ApplyStates.ERROR;
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
