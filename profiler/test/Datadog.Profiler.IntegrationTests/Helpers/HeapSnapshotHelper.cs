// <copyright file="HeapSnapshotHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Net;

namespace Datadog.Profiler.IntegrationTests.Helpers
{
    public class HeapSnapshotHelper
    {
        public static bool HasHeapSnapshot(HttpListenerRequest request)
        {
            // The histogram may be compacted, so only the part name is checked.
            return MultiPartReader.TryParse(request, out var upload)
                && upload.ContainsFile("histogram.json");
        }
    }
}
