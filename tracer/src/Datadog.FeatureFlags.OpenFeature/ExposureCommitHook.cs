// <copyright file="ExposureCommitHook.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using OpenFeature;
using OpenFeature.Constant;
using OpenFeature.Model;

namespace Datadog.FeatureFlags.OpenFeature;

internal sealed class ExposureCommitHook : Hook
{
    private readonly ConditionalWeakTable<ImmutableMetadata, CommitOwner> _commits = new();

    internal void Capture(ImmutableMetadata? metadata, Action? commit)
    {
        try
        {
            if (metadata is not null && commit is not null)
            {
                // The immutable metadata instance is unique to one OpenFeature invocation. The weak
                // key prevents an abandoned provider result from retaining its pending exposure batch.
                _commits.Add(metadata, new CommitOwner(commit));
            }
        }
        catch
        {
            // Exposure reporting is best effort and must never affect the application result.
        }
    }

    internal void Complete(ImmutableMetadata? metadata, bool success)
    {
        try
        {
            if (metadata is not null && _commits.TryGetValue(metadata, out var owner))
            {
                _commits.Remove(metadata);
                owner.Complete(success);
            }
        }
        catch
        {
            // Exposure reporting is best effort and must never affect the application result.
        }
    }

    public override ValueTask FinallyAsync<T>(
        HookContext<T> context,
        FlagEvaluationDetails<T> details,
        IReadOnlyDictionary<string, object>? hints = null,
        CancellationToken cancellationToken = default)
    {
        Complete(details.FlagMetadata, details.ErrorType == ErrorType.None);

        return default;
    }

    private sealed class CommitOwner(Action commit)
    {
        private Action? _commit = commit;

        public void Complete(bool success)
        {
            var action = Interlocked.Exchange(ref _commit, null);
            if (success)
            {
                action?.Invoke();
            }
        }
    }
}
