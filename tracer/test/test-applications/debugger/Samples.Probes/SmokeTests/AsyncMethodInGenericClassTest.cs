using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Samples.Probes.Shared;

namespace Samples.Probes.SmokeTests
{
    public class AsyncMethodInGenericClassTest : IAsyncRun
    {
        public async Task RunAsync()
        {
            await new GenericClass<Person>().Run();
        }
    }

    internal class GenericClass<T>
    {
        public async Task Run()
        {
            var def = default(T);
            await Task.Yield();
            Console.WriteLine(def);
        }
    }
}
