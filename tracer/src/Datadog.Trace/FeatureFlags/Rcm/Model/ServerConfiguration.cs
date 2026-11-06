// <copyright file="ServerConfiguration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.FeatureFlags.Rcm.Model;

internal sealed class ServerConfiguration
{
    private OptionalField<EvaluatorParams> _evaluatorParams;

    [JsonConverter(typeof(StrictBooleanTrueJsonConverter))]
    public bool ObserveFullEvaluationData { get; set; }

    public string? CreatedAt { get; set; }

    public string? Format { get; set; }

    public Environment? Environment { get; set; }

    [JsonConverter(typeof(LenientEvaluatorParamsJsonConverter))]
    public EvaluatorParams? EvaluatorParams
    {
        get => _evaluatorParams.Value;
        set => _evaluatorParams = new OptionalField<EvaluatorParams>(value);
    }

    internal OptionalField<EvaluatorParams> EvaluatorParamsField => _evaluatorParams;

    [JsonConverter(typeof(FlagCollectionJsonConverter))]
    public FlagCollection? Flags { get; set; }

    public bool ShouldSerializeEvaluatorParams() => _evaluatorParams.IsPresent;

    internal void Merge(ServerConfiguration other)
    {
        // One nonconsenting file protects every flag retained in the merged configuration.
        ObserveFullEvaluationData &= other.ObserveFullEvaluationData;

        if (other.CreatedAt is not null)
        {
            CreatedAt = other.CreatedAt;
        }

        if (other.Format is not null)
        {
            Format = other.Format;
        }

        if (other.Environment is not null)
        {
            Environment = other.Environment;
        }

        if (other.EvaluatorParamsField.IsPresent)
        {
            EvaluatorParams = other.EvaluatorParams;
        }

        if (Flags is null)
        {
            Flags = new FlagCollection();
        }

        if (other.Flags is not null)
        {
            Flags.Merge(other.Flags);
        }
    }
}
