using System.IO;

namespace Datadog.Trace.Agent
{
    internal interface IStreamFactory
    {
        void GetStreams(out Stream requestStream, out Stream responseStream);
    }
}
