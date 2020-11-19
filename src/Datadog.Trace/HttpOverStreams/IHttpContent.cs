using System.IO;
using System.Threading.Tasks;

namespace Datadog.Trace.HttpOverStreams
{
    internal interface IHttpContent
    {
        long? Length { get; }

        Task CopyToAsync(Stream destination);
    }
}
