using System;
using Datadog.AutoInstrumentation.ManagedLoader.Demo.Driver.DefAD.Asm1;

namespace Datadog.AutoInstrumentation
{
    public static class DllMain
    {
        public static void Run()
        {
            var dummyWorker = new DummyWorker();
            dummyWorker.PerformDummyWork();
        }
    }
}
