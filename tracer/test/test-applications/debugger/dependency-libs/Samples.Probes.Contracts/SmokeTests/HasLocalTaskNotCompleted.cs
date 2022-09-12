// <copyright file="HasLocalTaskNotCompleted.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Samples.Probes.Contracts.SmokeTests
{
    internal class HasLocalTaskNotCompleted : IRun
    {
#pragma warning disable SA1401 // Fields should be private
        public Task<string> LastNameTask = new Task<string>(new Func<string>(() => throw new Exception()));
#pragma warning restore SA1401 // Fields should be private

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
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

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        [MethodProbeTestData("System.String", new[] { "System.Threading.Tasks.Task`1<System.String>" }, true)]
        public string Method(Task<string> task)
        {
            return task.Status.ToString();
        }
    }
}
