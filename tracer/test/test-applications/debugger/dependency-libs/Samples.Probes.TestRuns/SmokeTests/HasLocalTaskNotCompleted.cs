using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Samples.Probes.TestRuns.SmokeTests
{
    public class HasLocalTaskNotCompleted : IRun
    {
        public Task<string> LastNameTask = new Task<string>(new Func<string>(() => throw new Exception()));

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            try
            {
                LastNameTask.Start();
                LastNameTask.Wait();
            }
            catch (Exception)
            {
            }

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
