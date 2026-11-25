// <copyright file="EEHeapHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Linq;
using System.Net;

namespace Datadog.Profiler.IntegrationTests.Helpers
{
    public class EEHeapHelper
    {
        public static bool HasEEHeap(HttpListenerRequest request)
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

            var eeHeapFileInfo = mpReader.Files.FirstOrDefault(f => f.FileName == "eeheap.json");
            return eeHeapFileInfo != null;
        }
    }
}
