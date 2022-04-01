using System;
using System.Threading.Tasks;

namespace Samples.TraceAnnotations
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            await Task.Delay(1_000);
            await ProgramHelpers.RunTestsAsync();
        }
    }
}
