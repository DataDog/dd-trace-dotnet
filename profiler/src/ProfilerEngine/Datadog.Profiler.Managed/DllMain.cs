// <copyright file="DllMain.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using Datadog.Profiler;

// TODO: Rename namespace to Datadog requires to update ManagedLoader\AssemblyLoader.cs in Tracer repository
namespace Datadog.AutoInstrumentation
{
    public static class DllMain
    {
        public static void Run()
        {
            EnsureProfilerEngineIsRunning();
        }

        public static void EnsureProfilerEngineIsRunning()
        {
            try
            {
                // We cannot properly initialize the logger until we get the configuration inside of TryCreateAndStart(..).
                // In the meantime logs will end up on the console.
                // We must only log critical errors here.

                try
                {
                    bool isEngineRunning = ProfilerEngine.TryCreateAndStart(out ProfilerEngine _);
                }
                catch (Exception ex)
                {
                    Log.Error(Log.WithCallInfo(typeof(DllMain).FullName), ex);
                }
            }
            catch
            {
                // An exception escaped out of the logger. All we can do is swallow it to protect the user app.
            }
        }
    }
}
