using System;
using System.Threading.Tasks;
using Datadog.Trace;

namespace Samples.TraceAnnotations
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            await ProgramHelpers.RunTestsAsync();
        }
    }
}
