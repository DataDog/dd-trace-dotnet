// <copyright file="ProductConfigChangedEventArgs.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Datadog.Trace.RemoteConfigurationManagement
{
    internal class ProductConfigChangedEventArgs : EventArgs
    {
        private readonly Dictionary<string, ApplyDetails> _applyStates = new();

        public ProductConfigChangedEventArgs(IEnumerable<NamedRawFile> configContents)
        {
            ConfigContents = configContents;
        }

        public IEnumerable<NamedRawFile> ConfigContents { get; }

        public IEnumerable<NamedTypedFile<T>> GetDeserializedConfigurations<T>()
        {
            return ConfigContents.Select(configContent => configContent.Deserialize<T>());
        }

        public IEnumerable<NamedTypedFile<string>> GetConfigurationAsString()
        {
            foreach (var configContent in ConfigContents)
            {
                using var stream = new MemoryStream(configContent.RawFile);
                using var streamReader = new StreamReader(stream);
                var contents = streamReader.ReadToEnd();
                yield return new NamedTypedFile<string>(configContent.Path.Path, contents);
            }
        }

        public void Acknowledge(string filename)
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
            return _applyStates.Values;
        }

        private void GetOrCreateApplyDetails(string filename, Func<ApplyDetails, ApplyDetails> update)
        {
            ApplyDetails applyDetails = default;
            if (!_applyStates.TryGetValue(filename, out applyDetails))
            {
                applyDetails = new ApplyDetails(filename);
            }

            applyDetails = update(applyDetails);

            _applyStates[filename] = applyDetails;
        }
    }
}
