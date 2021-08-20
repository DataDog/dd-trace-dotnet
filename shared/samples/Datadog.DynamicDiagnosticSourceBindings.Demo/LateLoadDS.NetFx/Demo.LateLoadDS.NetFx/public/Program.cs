using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using DynamicDiagnosticSourceBindings.Demo;

namespace Demo.LateLoadDS.NetFx
{
    public class Program
    {
        // This application uses DiagnosticSource.dll but it uses it AFTER we use the stubbs first.
        // So we expect that the stubbs will look for DS on the standard probing path, WILL find it and use it.
        // This can be validated via logs.
        // In order to validate the correct behaviour where an application did something very custom and
        // used DS where is it NOT on th eprobing path, move the DS.dll into the following directory.
        // This demo uses an assembly resolve event handler to allo looking in this path only phase one is completed.
        // The expected behaviour is then that the stubbs first use the vendored-in version and then dynamically switch to
        // the normal DS once it has been loaded.
        // This can also be validated using the logs.
        private const string DiagnosticSourceAssemblyFilename = "System.Diagnostics.DiagnosticSource.dll";
        private const string DiagnosticSourceAssemblyHiddenPath = "./HiddenAssemblies/";

        // Set to true to automatically move the DS file at the start of the app to automate the validation described above.
        private const bool HideDiagnosticSourceAssembly = true;

        private int _isPhaseOneCompleted = 0;

        public static void Main(string[] _)
        {
            (new Program()).Run();
        }

        public void Run()
        {
            const int MaxIterations = 2000;
            const int PhaseOneIterations = 500;

            const int ReceivedEventsVisualWidth = 100;

            ConsoleWrite.Line();
            ConsoleWrite.Line($"Welcome to {this.GetType().FullName} in {Process.GetCurrentProcess().ProcessName}");

            ConsoleWrite.Line();
            ConsoleWrite.Line($"{nameof(HideDiagnosticSourceAssembly)} = {HideDiagnosticSourceAssembly}");

#pragma warning disable CS0162 // Unreachable code detected: intentional controll via a const bool.
            if (HideDiagnosticSourceAssembly)
            {
                string destination = Path.Combine(DiagnosticSourceAssemblyHiddenPath, DiagnosticSourceAssemblyFilename);

                try
                {
                    Directory.CreateDirectory(DiagnosticSourceAssemblyHiddenPath);

                    if (File.Exists(destination))
                    {
                        File.Delete(destination);
                    }
                }
                catch { }

                File.Move(DiagnosticSourceAssemblyFilename, destination);

                ConsoleWrite.Line($"Moved \"{DiagnosticSourceAssemblyFilename}\" to \"{destination}\".");

                ConsoleWrite.Line();
                ConsoleWrite.Line($"Setting up the AssemblyResolve handler for the current AppDomain.");

                AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolveEventHandler;
            }
            else
            {
                ConsoleWrite.Line("Did not hide the DS assembly.");
            }
#pragma warning restore CS0162 // Unreachable code detected

            ConsoleWrite.LineLine($"Setting up {nameof(StubbedDiagnosticEventsCollector)}.");

            var directResultsAccumulator = new ReceivedEventsAccumulator(MaxIterations);
            var stubbedResultsAccumulator = new ReceivedEventsAccumulator(MaxIterations);
            var stubbedCollector = new StubbedDiagnosticEventsCollector(directResultsAccumulator, stubbedResultsAccumulator);

            SetPhaseOneCompleted(false);

            ConsoleWrite.LineLine($"Starting {nameof(StubbedDiagnosticEventsGenerator)}.");

            var stubbedGenerator = new StubbedDiagnosticEventsGenerator(MaxIterations, PhaseOneIterations);
            Task stubbedGeneratorTask = Task.Run(stubbedGenerator.Run);

            stubbedGenerator.PhaseOneCompletedEvent.Wait();

            ConsoleWrite.LineLine($"Phase one of {nameof(stubbedGenerator)} completed.");
            SetPhaseOneCompleted(true);

            ConsoleWrite.Line($"Starting {nameof(DirectDiagnosticEventsGenerator)}.");

            var directGenerator = new DirectDiagnosticEventsGenerator(MaxIterations, PhaseOneIterations);
            Task directGeneratorTask = Task.Run(directGenerator.Run);

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

        private void SetPhaseOneCompleted(bool isCompleted)
        {
            if (isCompleted)
            {
                Interlocked.Exchange(ref _isPhaseOneCompleted, 1);
            }
            else
            {
                Interlocked.Exchange(ref _isPhaseOneCompleted, 0);
            }
        }

        private bool GetPhaseOneCompleted()
        {
            int isCompleted = Interlocked.Add(ref _isPhaseOneCompleted, 0);
            return (isCompleted != 0);
        }

        private Assembly AssemblyResolveEventHandler(object sender, ResolveEventArgs args)
        {
            if (!GetPhaseOneCompleted())
            {
                ConsoleWrite.Line($"AssemblyResolveEventHandler: Phase One not completed => doing nothing.");
                return null;
            }

            string asmNameRequestedStr = args?.Name;
            if (asmNameRequestedStr == null)
            {
                ConsoleWrite.Line($"AssemblyResolveEventHandler: No good arguments => doing nothing.");
                return null;
            }

            string dsAsmFilePath = Path.Combine(DiagnosticSourceAssemblyHiddenPath, DiagnosticSourceAssemblyFilename);
            AssemblyName asmNameAtPath = GetAssemblyNameFromPath(dsAsmFilePath);
            if (asmNameAtPath == null)
            {
                ConsoleWrite.Line($"AssemblyResolveEventHandler: Cannot extract assembly name from \"{dsAsmFilePath}\". Doing nothing.");
                return null;
            }
            else
            {
                AssemblyName asmNameRequested = new AssemblyName(asmNameRequestedStr);
                if (AreEqual(asmNameAtPath, asmNameRequested))
                {
                    ConsoleWrite.Line($"AssemblyResolveEventHandler: Match. Loading DS from special location.");
                    return Assembly.Load(asmNameAtPath);
                }
                else
                {
                    ConsoleWrite.Line($"AssemblyResolveEventHandler: No Match. Doing nothing.");
                    ConsoleWrite.Line($"    Requested assembly: \"{asmNameRequested.FullName}\";");
                    ConsoleWrite.Line($"    Present assembly:   \"{asmNameAtPath.FullName}\".");
                    return null;
                }
            }
        }

        private static bool AreEqual(AssemblyName asmName1, AssemblyName asmName2)
        {
            if (Object.ReferenceEquals(asmName1, asmName2))
            {
                return true;
            }

            if (asmName1 == null || asmName2 == null)
            {
                return false;
            }

            return AssemblyName.ReferenceMatchesDefinition(asmName1, asmName2) && asmName1.FullName.Equals(asmName2.FullName);
        }

        private static AssemblyName GetAssemblyNameFromPath(string path)
        {
            if (String.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            try
            {
                return AssemblyName.GetAssemblyName(path);
            }
            catch (Exception ex)
            {
                ConsoleWrite.Exception(ex);
                return null;
            }
        }
    }
}
