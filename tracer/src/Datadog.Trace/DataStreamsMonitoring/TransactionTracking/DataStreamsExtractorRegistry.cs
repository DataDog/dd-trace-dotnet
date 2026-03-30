// <copyright file="DataStreamsExtractorRegistry.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Logging;
using Datadog.Trace.Util.Json;

namespace Datadog.Trace.DataStreamsMonitoring.TransactionTracking;

internal sealed class DataStreamsExtractorRegistry
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<DataStreamsExtractorRegistry>();

    private readonly Dictionary<DataStreamsTransactionExtractor.Type, List<DataStreamsTransactionExtractor>> _extractors = new();

    internal DataStreamsExtractorRegistry(string extractorsJson)
    {
        if (string.IsNullOrWhiteSpace(extractorsJson))
        {
            return;
        }

        List<DataStreamsTransactionExtractor>? deserialized;
        try
        {
            deserialized = JsonHelper.DeserializeObject<List<DataStreamsTransactionExtractor>>(extractorsJson);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to parse DD_DATA_STREAMS_TRANSACTION_EXTRACTORS value. Transaction tracking extractors will be disabled.");
            return;
        }

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
        return JsonHelper.SerializeObject(_extractors);
    }

    internal List<DataStreamsTransactionExtractor>? GetExtractorsByType(DataStreamsTransactionExtractor.Type extractorType)
    {
        return _extractors.GetValueOrDefault(extractorType);
    }
}
