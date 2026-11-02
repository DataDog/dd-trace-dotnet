// <copyright file="ExposureCommitHook.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenFeature;
using OpenFeature.Constant;
using OpenFeature.Model;

namespace Datadog.FeatureFlags.OpenFeature;

/// <summary>
/// Records and sends a deferred exposure once OpenFeature has completed the evaluation. OpenFeature
/// runs provider finally hooks before application finally hooks, so <see cref="ExposureHook"/> sees
/// this verdict. An after-hook failure reaches this stage as an error, and nothing is committed.
/// </summary>
internal sealed class ExposureCommitHook : Hook
{
    private readonly Func<IDictionary<string, string>, int> _commit;

    internal ExposureCommitHook(Func<IDictionary<string, string>, int> commit)
    {
        _commit = commit;
    }

    public override ValueTask FinallyAsync<T>(HookContext<T> context, FlagEvaluationDetails<T> details, IReadOnlyDictionary<string, object>? hints = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (details.ErrorType == ErrorType.None)
            {
                FeatureFlagsSdk.CommitPendingExposure(details.FlagMetadata, _commit);
            }
        }
        catch (Exception)
        {
            // Exposure reporting must never change the application's evaluation.
        }

        return default;
    }
}
