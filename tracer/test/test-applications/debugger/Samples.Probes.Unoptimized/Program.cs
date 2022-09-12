using System.Threading.Tasks;
using Samples.Probes.Shared;

namespace Samples.Probes.Unoptimized
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            await SampleHelper.RunTest(args);
        }
    }
}
