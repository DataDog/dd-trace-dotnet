using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Samples.Probes.TestRuns.SmokeTests
{
    public class HasLocalTaskCompleted : IRun
    {
        public Task<string> LastNameTask = new Task<string>(new Func<string>(() => "Last"));

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            LastNameTask.Start();
            LastNameTask.Wait();
            Method(LastNameTask);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        // https://datadoghq.atlassian.net/browse/DEBUG-722
        [LogMethodProbeTestData("System.String", new[] { "System.Threading.Tasks.Task`1<System.String>" }, true)]
        public string Method(Task<string> task)
        {
            return task.Status.ToString();
        }
    }
}
