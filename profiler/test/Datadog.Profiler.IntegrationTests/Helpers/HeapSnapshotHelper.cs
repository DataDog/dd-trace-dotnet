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
        }
    }
}
