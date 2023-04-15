using System;
using System.Threading.Tasks;

namespace Samples.TraceAnnotations
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            try
            {
                await ProgramHelpers.RunTestsAsync();
            }
            catch(Exception e)
            {
                Console.Write(e);
            }
        }
    }
}
