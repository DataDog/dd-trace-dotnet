using System.IO;

namespace Datadog.Trace.Agent
{
    internal interface IStreamFactory
    {
        string Info();

        Stream GetBidirectionalStream();
    }
}
