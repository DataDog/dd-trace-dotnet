using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Datadog.DynamicDiagnosticSourceBindings;
using DynamicDiagnosticSourceBindings.Demo;

namespace Demo.LoadUnloadPlugin.NetCore31
{
    public class Program
    {
        public static void Main(string[] _)
        {
            (new Program()).Run();
        }

        public void Run()
        {
            const int MaxIterations = 2000;
            const int PhaseOneIterations = 500;

            const int ReceivedEventsVisualWidth = 100;

            ConsoleWrite.LineLine($"Welcome to {this.GetType().FullName} in {Process.GetCurrentProcess().ProcessName}");

            ConsoleWrite.Line($"Starting {nameof(DirectDiagnosticEventsGenerator)}.");

            var directGenerator = new DirectDiagnosticEventsGenerator(MaxIterations, PhaseOneIterations);
            Task directGeneratorTask = Task.Run(directGenerator.Run);

            ConsoleWrite.LineLine($"Setting up {nameof(StubbedDiagnosticEventsCollector)}.");

            var directResultsAccumulator = new ReceivedEventsAccumulator(MaxIterations);
            var stubbedResultsAccumulator = new ReceivedEventsAccumulator(MaxIterations);
            var stubbedCollector = new StubbedDiagnosticEventsCollector(directResultsAccumulator, stubbedResultsAccumulator);

            {
                ConsoleWrite.LineLine($"Kicking off the DS magic.");
                bool prevInit = DiagnosticSourceAssembly.IsInitialized;
                bool nowInit = DiagnosticSourceAssembly.EnsureInitialized();
                ConsoleWrite.Line($"DiagnosticSourceAssembly-magic status: prevInit={prevInit}, nowInit={nowInit}.");
            }

            directGenerator.PhaseOneCompletedEvent.Wait();

            ConsoleWrite.LineLine($"Phase one of {nameof(directGenerator)} completed.");

            ConsoleWrite.LineLine($"Starting {nameof(StubbedDiagnosticEventsGenerator)}.");

            var stubbedGenerator = new StubbedDiagnosticEventsGenerator(MaxIterations, PhaseOneIterations);
            Task stubbedGeneratorTask = Task.Run(stubbedGenerator.Run);

            stubbedGenerator.PhaseOneCompletedEvent.Wait();

            ConsoleWrite.LineLine($"Phase one of {nameof(stubbedGenerator)} completed.");

            ConsoleWrite.LineLine($"Starting Mock Plug-In."
                                + $" directGen.Iteration={directGenerator.CurrentIteration}; stubbedGen.Iteration={stubbedGenerator.CurrentIteration}.");

            Task pulginTask = Task.Run(Plugin);
            pulginTask.Wait();

            ConsoleWrite.LineLine($"Completed Mock Plug-In."
                                + $" directGen.Iteration={directGenerator.CurrentIteration}; stubbedGen.Iteration={stubbedGenerator.CurrentIteration}.");

            ConsoleWrite.LineLine($"Waiting for event generators to finish.");

            Task.WaitAll(stubbedGeneratorTask, directGeneratorTask);

            ConsoleWrite.LineLine($"Both, {nameof(stubbedGenerator)} and {nameof(directGenerator)} finished.");

            ConsoleWrite.LineLine($"Summary of {nameof(stubbedResultsAccumulator)}:"
                                + $" Received events: {stubbedResultsAccumulator.ReceivedCount}; Proportion: {stubbedResultsAccumulator.ReceivedProportion}.");
            ConsoleWrite.Line(Environment.NewLine + stubbedResultsAccumulator.GetReceivedVisual(ReceivedEventsVisualWidth));

            ConsoleWrite.LineLine($"Summary of {nameof(directResultsAccumulator)}:"
                                + $" Received events: {directResultsAccumulator.ReceivedCount}; Proportion: {directResultsAccumulator.ReceivedProportion}.");
            ConsoleWrite.Line(Environment.NewLine + directResultsAccumulator.GetReceivedVisual(ReceivedEventsVisualWidth));

            ConsoleWrite.LineLine("All done. Press enter to exit.");

            Console.ReadLine();
            ConsoleWrite.Line("Good bye.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0059:Unnecessary assignment of a value", Justification = "Need null assignments to work with weak refs.")]
        private async Task Plugin()
        {
            //const string DSAssemblyNameForPlugin = "System.Diagnostics.DiagnosticSource, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51";
            const string DSAssemblyFileForPlugin = "PluginRessource.System.Diagnostics.DiagnosticSource.dll";
            const int RunningTimeMillis = 500;

            string currentDirectory = Directory.GetCurrentDirectory();
            string assemblyPath = Path.Combine(currentDirectory, DSAssemblyFileForPlugin);

            ConsoleWrite.LineLine($"Mock Plug-In: Loading assembly from \"{assemblyPath}\".");

            var asmLoadCtx = new MockPluginAssemblyLoadContext();
            Assembly dsAsm = asmLoadCtx.LoadFromAssemblyPath(assemblyPath);

            ConsoleWrite.Line($"Mock Plug-In: Assembly loaded ({dsAsm.FullName}) from \"{dsAsm.Location}\".");
            ConsoleWrite.Line($"Mock Plug-In: Doing mock work.");

            await Task.Delay(RunningTimeMillis);

            ConsoleWrite.LineLine($"Mock Plug-In: Mock work done. Unloading plugin and assemblies.");

            dsAsm = null;
            asmLoadCtx.Unload();

            var asmLoadCtxWeakRef = new WeakReference(asmLoadCtx);
            asmLoadCtx = null;

            int unloadWaitIterations = 0;
            while (asmLoadCtxWeakRef.IsAlive)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                unloadWaitIterations++;

                if (unloadWaitIterations % 100 == 0)
                {
                    ConsoleWrite.Line($"Plugin => Waiting To Unload => unloadIterations={unloadWaitIterations}.");
                }
                else if (unloadWaitIterations % 10 == 0)
                {
                    await Task.Delay(1);
                }
            }

            ConsoleWrite.LineLine($"Mock Plug-In: Plugin and assemblies unloaded. That's it.");
        }
    }
}
