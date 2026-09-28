// <copyright file="FlagEvaluationPayload.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Datadog.Trace.Util.Json;

namespace Datadog.Trace.FeatureFlags.FlagEvaluation;

internal static class FlagEvaluationPayload
{
    internal static FlagEvaluationPayloadResult Encode(DrainResult state, IReadOnlyDictionary<string, string> serviceContext, long flushTimeMs, int payloadLimitBytes)
    {
        var context = new Dictionary<string, string>();
        foreach (var name in new[] { "service", "env", "version" })
        {
            if (serviceContext.TryGetValue(name, out var value) && value is not null)
            {
                context[name] = value;
            }
        }

        var prefix = Encoding.UTF8.GetBytes("{\"context\":" + JsonHelper.SerializeObject(context) + ",\"flagEvaluations\":[");
        var payloads = new List<byte[]>();
        long payloadDegraded = 0;
        long payloadDropped = 0;
        long serializationDropped = 0;
        using var batch = new MemoryStream();
        var rows = 0;
        foreach (var pair in state.Full)
        {
            Append(pair.Key.Dimensions, pair.Value, pair.Key.TargetingKey, pair.Key.ObserveFullEvaluationData, degraded: false);
        }

        foreach (var pair in state.Degraded)
        {
            Append(pair.Key, pair.Value, null, false, degraded: true);
        }

        FinishBatch();
        return new FlagEvaluationPayloadResult(payloads, payloadDegraded, payloadDropped, serializationDropped);

        void Append(DegradedKey dimensions, EvaluationEntry entry, string? targetingKey, bool consent, bool degraded)
        {
            byte[] encoded;
            try
            {
                encoded = EncodeRow(dimensions, entry, targetingKey, consent, degraded, flushTimeMs);
                // Measure a row against an empty batch before adding it. Degradation is only
                // needed when the row cannot fit by itself, not when the current batch is full.
                if ((long)prefix.Length + encoded.Length + 2 > payloadLimitBytes)
                {
                    if (!degraded)
                    {
                        encoded = EncodeRow(dimensions, entry, null, false, degraded: true, flushTimeMs);
                    }

                    if ((long)prefix.Length + encoded.Length + 2 > payloadLimitBytes)
                    {
                        payloadDropped += entry.Count;
                        return;
                    }

                    payloadDegraded += entry.Count;
                }
            }
            catch (Exception)
            {
                // Never include exception text: it may contain customer input.
                serializationDropped += entry.Count;
                return;
            }

            if (rows > 0 && batch.Length + 1 + encoded.Length + 2 > payloadLimitBytes)
            {
                FinishBatch();
            }

            if (rows == 0)
            {
                batch.Write(prefix, 0, prefix.Length);
            }
            else
            {
                batch.WriteByte((byte)',');
            }

            batch.Write(encoded, 0, encoded.Length);
            rows++;
        }

        void FinishBatch()
        {
            if (rows == 0)
            {
                return;
            }

            batch.WriteByte((byte)']');
            batch.WriteByte((byte)'}');
            payloads.Add(batch.ToArray());
            batch.SetLength(0);
            batch.Position = 0;
            rows = 0;
        }
    }

    private static byte[] EncodeRow(DegradedKey dimensions, EvaluationEntry entry, string? targetingKey, bool consent, bool degraded, long flushTimeMs)
    {
        if (dimensions.FlagKey is null || entry.Count < 1)
        {
            throw new InvalidOperationException("Invalid flag evaluation entry");
        }

        var row = new Dictionary<string, object>
        {
            ["timestamp"] = flushTimeMs,
            ["flag"] = Key(dimensions.FlagKey),
            ["first_evaluation"] = entry.FirstEvaluationMs,
            ["last_evaluation"] = entry.LastEvaluationMs,
            ["evaluation_count"] = entry.Count,
        };
        if (dimensions.Variant is null)
        {
            row["runtime_default_used"] = true;
        }
        else
        {
            row["variant"] = Key(dimensions.Variant);
        }

        if (dimensions.AllocationKey is not null)
        {
            row["allocation"] = Key(dimensions.AllocationKey);
        }

        if (FlagEvaluationPrivacy.ErrorCodeForOutput(dimensions.ErrorCode) is { } errorCode)
        {
            row["error"] = new Dictionary<string, string> { ["message"] = errorCode };
        }

        if (!degraded)
        {
            // Serialization remains a privacy boundary even if an upstream caller bypasses
            // normalization. Either consent check can make the entry protected.
            consent &= entry.ObserveFullEvaluationData;
            if (FlagEvaluationPrivacy.TargetingKeyForOutput(targetingKey, consent) is { } outputKey)
            {
                row["targeting_key"] = outputKey;
            }

            if (consent && FlagEvaluationContext.Copy(entry.ContextAttrs, out _) is { } attributes)
            {
                row["context"] = new Dictionary<string, object> { ["evaluation"] = attributes };
            }
        }

        return Encoding.UTF8.GetBytes(JsonHelper.SerializeObject(row));
    }

    private static Dictionary<string, string> Key(string value) => new() { ["key"] = value };
}
