// <copyright file="ServerConfiguration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Runtime.Serialization;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.FeatureFlags.Rcm.Model;

internal sealed class ServerConfiguration
{
    public string? CreatedAt { get; set; }

    public string? Format { get; set; }

    public Environment? Environment { get; set; }

    [JsonConverter(typeof(StrictBooleanTrueJsonConverter))]
    public bool ObserveFullEvaluationData { get; set; }

    [JsonConverter(typeof(FlagCollectionJsonConverter))]
    public FlagCollection? Flags { get; set; }

    [JsonIgnore]
    internal bool HasSnapshottedPrivacyConsent { get; private set; }

    [JsonIgnore]
    private bool HasPrivacyConsentSource { get; set; }

    internal void SnapshotPrivacyConsent()
    {
        if (HasSnapshottedPrivacyConsent)
        {
            return;
        }

        if (Flags is not null)
        {
            foreach (var pair in Flags.ValidFlags)
            {
                pair.Value.ObserveFullEvaluationData = ObserveFullEvaluationData;
            }
        }

        HasSnapshottedPrivacyConsent = true;
        HasPrivacyConsentSource = true;
    }

    internal void Merge(ServerConfiguration other)
    {
        var hasExistingPrivacyConsentSource = HasPrivacyConsentSource
                                            || HasSnapshottedPrivacyConsent
                                            || Flags is not null
                                            || CreatedAt is not null
                                            || Format is not null
                                            || Environment is not null;
        SnapshotPrivacyConsent();
        other.SnapshotPrivacyConsent();

        // A missing/invalid flag has no per-flag snapshot, so it uses the merged UFC root consent.
        // Fail closed unless every contributing source opted in. The first merge into an empty
        // accumulator adopts the source value so the result is independent of source order.
        ObserveFullEvaluationData = hasExistingPrivacyConsentSource
                                        ? ObserveFullEvaluationData && other.ObserveFullEvaluationData
                                        : other.ObserveFullEvaluationData;
        HasPrivacyConsentSource = true;

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

        if (Flags is null)
        {
            Flags = new FlagCollection();
        }

        if (other.Flags is not null)
        {
            Flags.Merge(other.Flags);
        }

        HasSnapshottedPrivacyConsent = true;
    }

    [OnDeserialized]
    internal void OnDeserialized(StreamingContext context) => SnapshotPrivacyConsent();
}
