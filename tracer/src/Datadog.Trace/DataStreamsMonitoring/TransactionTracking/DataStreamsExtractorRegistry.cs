// <copyright file="DataStreamsExtractorRegistry.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.DataStreamsMonitoring.TransactionTracking;

internal sealed class DataStreamsExtractorRegistry
{
    private readonly Dictionary<DataStreamsTransactionExtractor.Type, List<DataStreamsTransactionExtractor>> _extractors = new();

    internal DataStreamsExtractorRegistry(string extractorsJson)
    {
        if (string.IsNullOrWhiteSpace(extractorsJson))
        {
            return;
        }

        var deserialized = JsonConvert.DeserializeObject<List<DataStreamsTransactionExtractor>>(extractorsJson);
        if (deserialized == null)
        {
            return;
        }

        foreach (var extractor in deserialized)
        {
            var list = _extractors.GetValueOrDefault(extractor.ExtractorType) ?? new();
            list.Add(extractor);
            _extractors[extractor.ExtractorType] = list;
        }
    }

    internal string AsJson()
    {
        return JsonConvert.SerializeObject(_extractors);
    }

    internal List<DataStreamsTransactionExtractor>? GetExtractorsByType(DataStreamsTransactionExtractor.Type extractorType)
    {
        return _extractors.GetValueOrDefault(extractorType);
    }
}
