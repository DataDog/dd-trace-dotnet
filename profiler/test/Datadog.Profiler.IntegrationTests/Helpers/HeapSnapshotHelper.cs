// <copyright file="HeapSnapshotHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Linq;
using System.Net;

namespace Datadog.Profiler.IntegrationTests.Helpers
{
    public class HeapSnapshotHelper
    {
        public static bool HasHeapSnapshot(HttpListenerRequest request)
        {
            if (!request.ContentType.StartsWith("multipart/form-data"))
            {
                return false;
            }

            var mpReader = new MultiPartReader(request);
            if (!mpReader.Parse())
            {
                return false;
            }

            var files = mpReader.Files;
            var heapSnapshotFileInfo = files.FirstOrDefault(f => f.FileName == "histogram.json");
            return (heapSnapshotFileInfo != null);

            // we can't check the file content because it may be compacted
        }
    }
}
