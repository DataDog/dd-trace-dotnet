// <copyright file="DataStreamsExtractorRegistry.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Util.Json;

namespace Datadog.Trace.DataStreamsMonitoring.TransactionTracking;

internal sealed class DataStreamsExtractorRegistry
{
    private readonly Dictionary<DataStreamsTransactionExtractor.ExtractorType, List<DataStreamsTransactionExtractor>> _extractors;

    internal DataStreamsExtractorRegistry(IReadOnlyList<DataStreamsTransactionExtractor> extractors)
    {
        _extractors = new(extractors.Count);
        foreach (var extractor in extractors)
        {
            var list = _extractors.GetValueOrDefault(extractor.ParsedType) ?? new();
            list.Add(extractor);
            _extractors[extractor.ParsedType] = list;
        }
    }

    internal string AsJson()
    {
        return JsonHelper.SerializeObject(_extractors);
    }

    internal List<DataStreamsTransactionExtractor>? GetExtractorsByType(DataStreamsTransactionExtractor.ExtractorType extractorType)
    {
        return _extractors.GetValueOrDefault(extractorType);
    }
}
