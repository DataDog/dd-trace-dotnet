using System.Threading.Tasks;
using Datadog.Trace;

namespace Samples.TraceAnnotations.VersionMismatch.BeforeFeature
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            // Access Tracer.Instance.ActiveScope so there's an AssemblyRef to Datadog.Trace
            var scope = Tracer.Instance.ActiveScope;
            await ProgramHelpers.RunTestsAsync();
        }
    }
}
