// <copyright file="PendingExposureBatch.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.FeatureFlags.Exposure.Model;

namespace Datadog.Trace.FeatureFlags
{
    internal sealed class PendingExposureBatch
    {
        private readonly ExposureEvent _root;

        internal PendingExposureBatch(string rootFlagKey, IReadOnlyList<ExposureEvent> exposures)
        {
            RootFlagKey = rootFlagKey;
            Exposures = exposures;
        }

        internal PendingExposureBatch(string rootFlagKey, in ExposureEvent root)
        {
            RootFlagKey = rootFlagKey;
            _root = root;
        }

        internal string RootFlagKey { get; }

        internal IReadOnlyList<ExposureEvent>? Exposures { get; }

        internal ref readonly ExposureEvent Root => ref _root;
    }
}
