using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Samples.Probes.SmokeTests
{
    internal class HasLocalTaskCompleted : IRun
    {
        public Task<string> LastNameTask = new Task<string>(new Func<string>(() => "Last"));

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void Run()
        {
            LastNameTask.Start();
            LastNameTask.Wait();
            Method(LastNameTask);
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        // https://datadoghq.atlassian.net/browse/DEBUG-722
        [MethodProbeTestData("System.String", new[] { "System.Threading.Tasks.Task`1<System.String>" }, true)]
        public string Method(Task<string> task)
        {
            return task.Status.ToString();
        }
    }
}
